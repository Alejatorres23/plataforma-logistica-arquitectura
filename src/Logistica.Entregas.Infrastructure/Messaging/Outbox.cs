using System.Text.Json;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure.Observability;
using Logistica.Entregas.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Logistica.Entregas.Infrastructure.Messaging;

/// <summary>Mensaje guardado en la bandeja de salida, listo para publicarse.</summary>
public sealed record OutboxMessage(Guid Id, Guid DeliveryId, string EventType, string Payload, DateTimeOffset OccurredAt, int Attempts);

/// <summary>Destino de los eventos (bus de mensajes / servicio de notificaciones).</summary>
public interface IEventBus
{
    Task PublishAsync(OutboxMessage message, CancellationToken ct = default);
}

/// <summary>Configuración del despachador de la bandeja de salida.</summary>
public sealed class OutboxOptions
{
    /// <summary>Intentos antes de mover el mensaje a la cola de mensajes fallidos (DLQ).</summary>
    public int MaxAttempts { get; set; } = 5;
    public int BatchSize { get; set; } = 50;
    public int PollIntervalSeconds { get; set; } = 2;
}

/// <summary>Traduce los eventos de dominio al contrato público (Actividad 3, doc. 05) y los inserta en el outbox.</summary>
internal static class OutboxWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static (string type, string payload) Serialize(IDomainEvent e) => e switch
    {
        CourierAssigned a => ("RepartidorAsignado", JsonSerializer.Serialize(new
        {
            evento = "RepartidorAsignado", version = 1, deliveryId = a.DeliveryId.Value, courierId = a.CourierId.Value, ocurrioEn = a.OccurredAt,
        }, Json)),
        DeliveryReassigned r => ("EntregaReasignada", JsonSerializer.Serialize(new
        {
            evento = "EntregaReasignada", version = 1, deliveryId = r.DeliveryId.Value, repartidorAnterior = r.PreviousCourierId.Value,
            repartidorNuevo = r.NewCourierId.Value, motivo = r.Reason, ocurrioEn = r.OccurredAt,
        }, Json)),
        _ => throw new NotSupportedException($"Evento sin contrato de publicación: {e.GetType().Name}"),
    };

    public static async Task InsertAsync(SqliteConnection connection, SqliteTransaction tx, DeliveryId deliveryId,
        IReadOnlyList<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var e in events)
        {
            var (type, payload) = Serialize(e);
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO outbox_messages (id, delivery_id, event_type, payload, occurred_at)
                VALUES ($id, $delivery, $type, $payload, $at)
                """;
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$delivery", deliveryId.Value.ToString());
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$payload", payload);
            cmd.Parameters.AddWithValue("$at", SqliteDates.ToDb(e.OccurredAt));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}

/// <summary>Resultado de una pasada del despachador.</summary>
public sealed record DispatchResult(int Published, int Failed, int DeadLettered);

/// <summary>Estado de la bandeja de salida (para el endpoint de salud y la operación).</summary>
public sealed record OutboxStats(int Pending, int DeadLettered);

/// <summary>
/// Publica los mensajes pendientes del outbox. Si la publicación falla, reintenta en la siguiente
/// pasada; al llegar a <see cref="OutboxOptions.MaxAttempts"/> el mensaje pasa a la cola de mensajes
/// fallidos (dead letter) para revisión manual, sin bloquear a los demás.
/// </summary>
public sealed class OutboxDispatcher(
    SqliteConnectionFactory connections,
    IEventBus bus,
    OutboxOptions options,
    EntregasTelemetry telemetry,
    ILogger<OutboxDispatcher> logger)
{
    public async Task<DispatchResult> DispatchPendingAsync(CancellationToken ct = default)
    {
        await using var connection = await connections.OpenAsync(ct);
        var pending = new List<OutboxMessage>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, delivery_id, event_type, payload, occurred_at, attempts
                FROM outbox_messages
                WHERE processed_at IS NULL AND dead_lettered_at IS NULL
                ORDER BY occurred_at LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$limit", options.BatchSize);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                pending.Add(new OutboxMessage(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
                    reader.GetString(2), reader.GetString(3), SqliteDates.FromDb(reader.GetString(4)), reader.GetInt32(5)));
            }
        }

        int published = 0, failed = 0, deadLettered = 0;
        foreach (var message in pending)
        {
            try
            {
                await bus.PublishAsync(message, ct);
                await Update(connection, "UPDATE outbox_messages SET processed_at = $now, attempts = attempts + 1 WHERE id = $id", message.Id, ct);
                telemetry.OutboxPublished.Add(1, new KeyValuePair<string, object?>("evento", message.EventType));
                published++;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                var attempts = message.Attempts + 1;
                var error = $"{e.GetType().Name}: {e.Message}";
                if (error.Length > 500) error = error[..500];

                if (attempts >= options.MaxAttempts)
                {
                    await Update(connection, """
                        UPDATE outbox_messages SET attempts = $attempts, last_error = $error, dead_lettered_at = $now WHERE id = $id
                        """, message.Id, ct, attempts, error);
                    telemetry.OutboxDeadLettered.Add(1, new KeyValuePair<string, object?>("evento", message.EventType));
                    logger.LogError("Mensaje {MessageId} ({EventType}) enviado a la cola de mensajes fallidos tras {Attempts} intentos: {Error}",
                        message.Id, message.EventType, attempts, error);
                    deadLettered++;
                }
                else
                {
                    await Update(connection, "UPDATE outbox_messages SET attempts = $attempts, last_error = $error WHERE id = $id",
                        message.Id, ct, attempts, error);
                    logger.LogWarning("Falló la publicación del mensaje {MessageId} ({EventType}), intento {Attempts} de {MaxAttempts}",
                        message.Id, message.EventType, attempts, options.MaxAttempts);
                    failed++;
                }
            }
        }
        return new DispatchResult(published, failed, deadLettered);
    }

    public async Task<OutboxStats> GetStatsAsync(CancellationToken ct = default)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
              COALESCE(SUM(CASE WHEN processed_at IS NULL AND dead_lettered_at IS NULL THEN 1 ELSE 0 END), 0),
              COALESCE(SUM(CASE WHEN dead_lettered_at IS NOT NULL THEN 1 ELSE 0 END), 0)
            FROM outbox_messages
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new OutboxStats(reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task Update(SqliteConnection connection, string sql, Guid id, CancellationToken ct, int? attempts = null, string? error = null)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$now", SqliteDates.ToDb(DateTimeOffset.UtcNow));
        if (attempts is not null) cmd.Parameters.AddWithValue("$attempts", attempts);
        if (error is not null) cmd.Parameters.AddWithValue("$error", error);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>
/// Bus de notificaciones simulado: registra el evento publicado (sin datos personales).
/// <see cref="SimulateOutage"/> permite demostrar los reintentos y la cola de mensajes fallidos.
/// </summary>
public sealed class SimulatedNotificationBus(ILogger<SimulatedNotificationBus> logger) : IEventBus
{
    public bool SimulateOutage { get; set; }

    public Task PublishAsync(OutboxMessage message, CancellationToken ct = default)
    {
        if (SimulateOutage)
            throw new HttpRequestException("Servicio de notificaciones no disponible (simulado).");

        logger.LogInformation("Evento {EventType} de la entrega {DeliveryId} publicado a Notificaciones y Auditoría",
            message.EventType, message.DeliveryId);
        return Task.CompletedTask;
    }
}
