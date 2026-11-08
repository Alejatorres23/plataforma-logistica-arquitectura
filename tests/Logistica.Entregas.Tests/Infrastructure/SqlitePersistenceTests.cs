using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Logistica.Entregas.Tests.Infrastructure;

/// <summary>Pruebas de integración del repositorio SQLite y de las migraciones.</summary>
public class SqlitePersistenceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 15, 0, 0, TimeSpan.FromHours(-5));

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private static Delivery NewDelivery() => Delivery.Create(
        new OrderReference(Guid.NewGuid()),
        new DeliveryAddress("Calle 10 # 43-20, Medellín"),
        new DeliveryWindow(Now.AddHours(1), Now.AddHours(3)),
        DeliveryPriority.Urgente);

    private static CourierCapacity Courier() => new(new CourierId(Guid.NewGuid()), true, 0, 5);

    [Fact]
    public async Task Migraciones_SonReproducibles_YNoSeAplicanDosVeces()
    {
        var again = await new SqliteMigrator(_db.Connections).MigrateAsync();

        Assert.Equal(0, again);
        Assert.Equal(SqliteMigrator.Discover().Count, await _db.ScalarAsync("SELECT COUNT(*) FROM schema_version"));
    }

    [Fact]
    public async Task Migraciones_DetectanUnaMigracionAplicadaQueFueModificada()
    {
        await _db.ExecuteAsync("UPDATE schema_version SET checksum = 'alterado' WHERE version = 1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new SqliteMigrator(_db.Connections).MigrateAsync());
    }

    [Fact]
    public async Task Repositorio_GuardaYReconstruyeLaEntregaConSusAsignaciones()
    {
        var repo = new SqliteDeliveryRepository(_db.Connections);
        var delivery = NewDelivery();
        await repo.AddAsync(delivery, []);

        var loaded = (await repo.GetAsync(delivery.Id))!;
        var ana = Courier();
        loaded.AssignCourier(ana, Now);
        await repo.SaveAsync(loaded, loaded.PullEvents());

        var reloaded = (await new SqliteDeliveryRepository(_db.Connections).GetAsync(delivery.Id))!;
        Assert.Equal(DeliveryStatus.Asignada, reloaded.Status);
        Assert.Equal(ana.CourierId, reloaded.CurrentCourierId);
        Assert.Equal(DeliveryPriority.Urgente, reloaded.Priority);
        Assert.Equal(delivery.Window, reloaded.Window);
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM outbox_messages WHERE event_type = 'RepartidorAsignado'"));
    }

    [Fact]
    public async Task Repositorio_DosOperacionesSimultaneas_LaSegundaRecibeConflicto()
    {
        var delivery = NewDelivery();
        await new SqliteDeliveryRepository(_db.Connections).AddAsync(delivery, []);

        // Dos supervisores cargan la misma entrega al mismo tiempo.
        var repoA = new SqliteDeliveryRepository(_db.Connections);
        var repoB = new SqliteDeliveryRepository(_db.Connections);
        var copyA = (await repoA.GetAsync(delivery.Id))!;
        var copyB = (await repoB.GetAsync(delivery.Id))!;
        copyA.AssignCourier(Courier(), Now);
        copyB.AssignCourier(Courier(), Now);

        await repoA.SaveAsync(copyA, copyA.PullEvents());
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repoB.SaveAsync(copyB, copyB.PullEvents()));

        Assert.Equal(1, await _db.ScalarAsync(
            $"SELECT COUNT(*) FROM delivery_assignments WHERE delivery_id = '{delivery.Id.Value}' AND released_at IS NULL"));
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM outbox_messages"));
    }

    [Fact]
    public async Task BaseDeDatos_RechazaDosAsignacionesActivas_ComoSegundaLineaDeDefensa()
    {
        var delivery = NewDelivery();
        await new SqliteDeliveryRepository(_db.Connections).AddAsync(delivery, []);
        var id = delivery.Id.Value;
        await _db.ExecuteAsync($"INSERT INTO delivery_assignments VALUES ('{id}', 1, '{Guid.NewGuid()}', '2026-11-07T20:00:00.0000000+00:00', NULL, NULL)");

        var error = await Assert.ThrowsAsync<SqliteException>(() => _db.ExecuteAsync(
            $"INSERT INTO delivery_assignments VALUES ('{id}', 2, '{Guid.NewGuid()}', '2026-11-07T20:05:00.0000000+00:00', NULL, NULL)"));

        Assert.Equal(19, error.SqliteErrorCode); // SQLITE_CONSTRAINT
    }

    [Fact]
    public async Task Repositorio_EntregaInexistente_DevuelveNull()
        => Assert.Null(await new SqliteDeliveryRepository(_db.Connections).GetAsync(DeliveryId.New()));
}
