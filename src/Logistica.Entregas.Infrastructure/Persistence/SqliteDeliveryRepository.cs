using System.Runtime.CompilerServices;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure.Messaging;
using Microsoft.Data.Sqlite;

namespace Logistica.Entregas.Infrastructure.Persistence;

/// <summary>
/// Repositorio de entregas sobre SQLite.
/// - Concurrencia optimista: cada fila tiene una versión; si cambió desde la lectura, el guardado falla.
/// - Outbox: los eventos se insertan en la misma transacción que la entrega.
/// La versión leída se recuerda fuera de la entidad para no meter detalles de persistencia en el dominio.
/// </summary>
public sealed class SqliteDeliveryRepository(SqliteConnectionFactory connections) : IDeliveryRepository
{
    private const int SqliteConstraintError = 19;
    private readonly ConditionalWeakTable<Delivery, StrongBox<long>> _versions = new();

    public async Task<Delivery?> GetAsync(DeliveryId id, CancellationToken ct = default)
    {
        await using var connection = await connections.OpenAsync(ct);

        string orderId, address, priority, status;
        string windowStart, windowEnd;
        string? proofBy, proofAt;
        long version;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT order_id, address, window_start, window_end, priority, status,
                       proof_received_by, proof_received_at, version
                FROM deliveries WHERE id = $id
                """;
            cmd.Parameters.AddWithValue("$id", id.Value.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;
            orderId = reader.GetString(0);
            address = reader.GetString(1);
            windowStart = reader.GetString(2);
            windowEnd = reader.GetString(3);
            priority = reader.GetString(4);
            status = reader.GetString(5);
            proofBy = reader.IsDBNull(6) ? null : reader.GetString(6);
            proofAt = reader.IsDBNull(7) ? null : reader.GetString(7);
            version = reader.GetInt64(8);
        }

        var assignments = new List<AssignmentSnapshot>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT courier_id, assigned_at, released_at, release_reason
                FROM delivery_assignments WHERE delivery_id = $id ORDER BY sequence
                """;
            cmd.Parameters.AddWithValue("$id", id.Value.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                assignments.Add(new AssignmentSnapshot(
                    new CourierId(Guid.Parse(reader.GetString(0))),
                    SqliteDates.FromDb(reader.GetString(1)),
                    reader.IsDBNull(2) ? null : SqliteDates.FromDb(reader.GetString(2)),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
        }

        var delivery = Delivery.Restore(
            id,
            new OrderReference(Guid.Parse(orderId)),
            new DeliveryAddress(address),
            new DeliveryWindow(SqliteDates.FromDb(windowStart), SqliteDates.FromDb(windowEnd)),
            Enum.Parse<DeliveryPriority>(priority),
            Enum.Parse<DeliveryStatus>(status),
            proofBy is null ? null : new ProofOfDelivery(proofBy, SqliteDates.FromDb(proofAt!)),
            assignments);

        _versions.AddOrUpdate(delivery, new StrongBox<long>(version));
        return delivery;
    }

    public async Task AddAsync(Delivery delivery, IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        var now = SqliteDates.ToDb(DateTimeOffset.UtcNow);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO deliveries (id, order_id, address, window_start, window_end, priority, status,
                                        proof_received_by, proof_received_at, version, created_at, updated_at)
                VALUES ($id, $order, $address, $ws, $we, $priority, $status, $proofBy, $proofAt, 1, $now, $now)
                """;
            AddDeliveryParameters(cmd, delivery);
            cmd.Parameters.AddWithValue("$order", delivery.Order.Value.ToString());
            cmd.Parameters.AddWithValue("$address", delivery.Address.Value);
            cmd.Parameters.AddWithValue("$ws", SqliteDates.ToDb(delivery.Window.Start));
            cmd.Parameters.AddWithValue("$we", SqliteDates.ToDb(delivery.Window.End));
            cmd.Parameters.AddWithValue("$priority", delivery.Priority.ToString());
            cmd.Parameters.AddWithValue("$now", now);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await WriteAssignments(connection, tx, delivery, ct);
        await OutboxWriter.InsertAsync(connection, tx, delivery.Id, events, ct);
        await tx.CommitAsync(ct);
        _versions.AddOrUpdate(delivery, new StrongBox<long>(1));
    }

    public async Task SaveAsync(Delivery delivery, IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
    {
        if (!_versions.TryGetValue(delivery, out var loaded))
            throw new InvalidOperationException("Solo se pueden guardar entregas cargadas con este repositorio.");

        await using var connection = await connections.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE deliveries
                    SET status = $status, proof_received_by = $proofBy, proof_received_at = $proofAt,
                        version = version + 1, updated_at = $now
                    WHERE id = $id AND version = $version
                    """;
                AddDeliveryParameters(cmd, delivery);
                cmd.Parameters.AddWithValue("$version", loaded.Value);
                cmd.Parameters.AddWithValue("$now", SqliteDates.ToDb(DateTimeOffset.UtcNow));
                if (await cmd.ExecuteNonQueryAsync(ct) == 0)
                    throw new ConcurrencyConflictException(delivery.Id);
            }

            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = tx;
                delete.CommandText = "DELETE FROM delivery_assignments WHERE delivery_id = $id";
                delete.Parameters.AddWithValue("$id", delivery.Id.Value.ToString());
                await delete.ExecuteNonQueryAsync(ct);
            }

            await WriteAssignments(connection, tx, delivery, ct);
            await OutboxWriter.InsertAsync(connection, tx, delivery.Id, events, ct);
            await tx.CommitAsync(ct);
        }
        catch (SqliteException e) when (e.SqliteErrorCode == SqliteConstraintError)
        {
            // El índice único de asignación activa también protege I1 si dos procesos compiten.
            throw new ConcurrencyConflictException(delivery.Id);
        }

        loaded.Value++;
    }

    private static void AddDeliveryParameters(SqliteCommand cmd, Delivery delivery)
    {
        cmd.Parameters.AddWithValue("$id", delivery.Id.Value.ToString());
        cmd.Parameters.AddWithValue("$status", delivery.Status.ToString());
        cmd.Parameters.AddWithValue("$proofBy", (object?)delivery.Proof?.ReceivedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$proofAt", delivery.Proof is null ? DBNull.Value : SqliteDates.ToDb(delivery.Proof.ReceivedAt));
    }

    private static async Task WriteAssignments(SqliteConnection connection, SqliteTransaction tx, Delivery delivery, CancellationToken ct)
    {
        for (var i = 0; i < delivery.Assignments.Count; i++)
        {
            var a = delivery.Assignments[i];
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO delivery_assignments (delivery_id, sequence, courier_id, assigned_at, released_at, release_reason)
                VALUES ($id, $seq, $courier, $assignedAt, $releasedAt, $reason)
                """;
            cmd.Parameters.AddWithValue("$id", delivery.Id.Value.ToString());
            cmd.Parameters.AddWithValue("$seq", i + 1);
            cmd.Parameters.AddWithValue("$courier", a.CourierId.Value.ToString());
            cmd.Parameters.AddWithValue("$assignedAt", SqliteDates.ToDb(a.AssignedAt));
            cmd.Parameters.AddWithValue("$releasedAt", a.ReleasedAt is null ? DBNull.Value : SqliteDates.ToDb(a.ReleasedAt.Value));
            cmd.Parameters.AddWithValue("$reason", (object?)a.ReleaseReason ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
