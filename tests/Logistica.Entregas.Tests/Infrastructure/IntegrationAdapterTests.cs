using System.Net;
using System.Text;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure.Couriers;
using Logistica.Entregas.Infrastructure.Messaging;
using Logistica.Entregas.Infrastructure.Observability;
using Logistica.Entregas.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Logistica.Entregas.Tests.Infrastructure;

/// <summary>Pruebas del adaptador HTTP del servicio de turnos (timeout, reintentos, capa anticorrupción).</summary>
public class HttpCourierAvailabilityTests
{
    /// <summary>Servidor HTTP falso: responde según una lista de respuestas, una por llamada.</summary>
    private sealed class ScriptedHandler(params Func<Task<HttpResponseMessage>>[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var respond = responses[Math.Min(Calls, responses.Length - 1)];
            Calls++;
            return respond().WaitAsync(ct);
        }
    }

    private static readonly CourierId Ana = new(Guid.NewGuid());

    private static Func<Task<HttpResponseMessage>> Json(string body)
        => () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    private static Func<Task<HttpResponseMessage>> Status(HttpStatusCode code)
        => () => Task.FromResult(new HttpResponseMessage(code));

    private static Func<Task<HttpResponseMessage>> Hang()
        => () => Task.Delay(Timeout.Infinite).ContinueWith(_ => new HttpResponseMessage(HttpStatusCode.OK));

    private static HttpCourierAvailability Adapter(ScriptedHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://turnos.test/") },
        new CourierServiceOptions { TimeoutMilliseconds = 200, MaxRetries = 2, BaseDelayMilliseconds = 10 },
        new EntregasTelemetry(),
        NullLogger<HttpCourierAvailability>.Instance);

    [Fact]
    public async Task RespuestaValida_SeTraduceACapacidad_IgnorandoLaUbicacion()
    {
        var handler = new ScriptedHandler(Json("""{"courierId":"x","activo":true,"entregasActivas":2,"maximoEntregas":5,"ubicacion":{"lat":6.2}}"""));

        var capacity = await Adapter(handler).GetCapacityAsync(Ana);

        Assert.NotNull(capacity);
        Assert.True(capacity.IsActive);
        Assert.Equal(2, capacity.ActiveDeliveries);
        Assert.Equal(5, capacity.MaxDeliveries);
    }

    [Fact]
    public async Task RepartidorQueNoExiste_DevuelveNull()
        => Assert.Null(await Adapter(new ScriptedHandler(Status(HttpStatusCode.NotFound))).GetCapacityAsync(Ana));

    [Fact]
    public async Task FallaTransitoria_SeReintentaYSeRecupera()
    {
        var handler = new ScriptedHandler(
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.InternalServerError),
            Json("""{"activo":true,"entregasActivas":0,"maximoEntregas":3}"""));

        var capacity = await Adapter(handler).GetCapacityAsync(Ana);

        Assert.NotNull(capacity);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task ServicioQueNoResponde_AgotaReintentosYFallaDeFormaControlada()
    {
        var handler = new ScriptedHandler(Hang());

        await Assert.ThrowsAsync<CourierAvailabilityUnavailableException>(() => Adapter(handler).GetCapacityAsync(Ana));
        Assert.Equal(3, handler.Calls); // 1 intento + 2 reintentos, cada uno con timeout de 200 ms
    }

    [Fact]
    public async Task ErrorNoTransitorio_NoSeReintenta()
    {
        var handler = new ScriptedHandler(Status(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<CourierAvailabilityUnavailableException>(() => Adapter(handler).GetCapacityAsync(Ana));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task JsonMalFormado_FallaDeFormaControlada_NoComoError500()
    {
        var handler = new ScriptedHandler(Json("""{"activo":"quizas","entregasActivas":"""));

        await Assert.ThrowsAsync<CourierAvailabilityUnavailableException>(() => Adapter(handler).GetCapacityAsync(Ana));
    }

    [Fact]
    public async Task DatosInvalidos_SeRechazanEnLaCapaAnticorrupcion()
    {
        var handler = new ScriptedHandler(Json("""{"activo":true,"entregasActivas":-1,"maximoEntregas":0}"""));

        await Assert.ThrowsAsync<CourierAvailabilityUnavailableException>(() => Adapter(handler).GetCapacityAsync(Ana));
    }
}

/// <summary>Pruebas del despachador del outbox: publicación, reintentos y cola de mensajes fallidos.</summary>
public class OutboxDispatcherTests : IAsyncLifetime
{
    private sealed class FlakyBus : IEventBus
    {
        public bool Down { get; set; }
        public List<OutboxMessage> Published { get; } = [];

        public Task PublishAsync(OutboxMessage message, CancellationToken ct = default)
        {
            if (Down) throw new HttpRequestException("Notificaciones caído (prueba).");
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private readonly TestDatabase _db = new();
    private readonly FlakyBus _bus = new();
    private OutboxDispatcher _dispatcher = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _dispatcher = new OutboxDispatcher(_db.Connections, _bus, new OutboxOptions { MaxAttempts = 3 },
            new EntregasTelemetry(), NullLogger<OutboxDispatcher>.Instance);

        // Una entrega asignada deja un evento en el outbox.
        var repo = new SqliteDeliveryRepository(_db.Connections);
        var now = DateTimeOffset.UtcNow;
        var delivery = Delivery.Create(new OrderReference(Guid.NewGuid()), new DeliveryAddress("Calle 1"),
            new DeliveryWindow(now.AddHours(1), now.AddHours(2)), DeliveryPriority.Normal);
        delivery.AssignCourier(new CourierCapacity(new CourierId(Guid.NewGuid()), true, 0, 5), now);
        await repo.AddAsync(delivery, delivery.PullEvents());
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task EventoPendiente_SePublicaUnaSolaVez()
    {
        var first = await _dispatcher.DispatchPendingAsync();
        var second = await _dispatcher.DispatchPendingAsync();

        Assert.Equal(1, first.Published);
        Assert.Equal(0, second.Published);
        var message = Assert.Single(_bus.Published);
        Assert.Equal("RepartidorAsignado", message.EventType);
        Assert.Contains("\"evento\":\"RepartidorAsignado\"", message.Payload);
        Assert.DoesNotContain("Calle 1", message.Payload); // el evento no lleva la dirección
    }

    [Fact]
    public async Task NotificacionesCaido_SeReintentaYLuegoVaALaColaDeFallidos()
    {
        _bus.Down = true;

        await _dispatcher.DispatchPendingAsync();
        await _dispatcher.DispatchPendingAsync();
        var third = await _dispatcher.DispatchPendingAsync();

        Assert.Equal(1, third.DeadLettered);
        var stats = await _dispatcher.GetStatsAsync();
        Assert.Equal(0, stats.Pending);
        Assert.Equal(1, stats.DeadLettered);

        // Cuando el servicio vuelve, el mensaje fallido NO se reprocesa solo: queda para revisión.
        _bus.Down = false;
        Assert.Equal(0, (await _dispatcher.DispatchPendingAsync()).Published);
    }

    [Fact]
    public async Task FallaTemporal_SeRecuperaEnLaSiguientePasada()
    {
        _bus.Down = true;
        var failed = await _dispatcher.DispatchPendingAsync();
        _bus.Down = false;
        var recovered = await _dispatcher.DispatchPendingAsync();

        Assert.Equal(1, failed.Failed);
        Assert.Equal(1, recovered.Published);
    }
}
