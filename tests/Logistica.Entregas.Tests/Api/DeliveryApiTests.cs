using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure;
using Logistica.Entregas.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Logistica.Entregas.Tests.Api;

/// <summary>
/// Pruebas de integración del vertical slice completo: HTTP → endpoint → caso de uso → dominio → SQLite.
/// Solo se reemplaza el servicio externo de turnos; la base de datos es real (archivo temporal).
/// </summary>
public class DeliveryApiTests : IAsyncLifetime
{
    private const string ApiKey = "clave-de-prueba";

    private sealed class SwitchableCourierService(InMemoryCourierAvailability inner) : ICourierAvailability
    {
        public bool Down { get; set; }

        public Task<CourierCapacity?> GetCapacityAsync(CourierId courierId, CancellationToken ct = default)
            => Down ? throw new CourierAvailabilityUnavailableException("Servicio de turnos caído (prueba).") : inner.GetCapacityAsync(courierId, ct);
    }

    private readonly TestDatabase _db = new();
    private readonly InMemoryCourierAvailability _couriers = new();
    private SwitchableCourierService _courierService = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _ana = Guid.NewGuid();
    private readonly Guid _luis = Guid.NewGuid();
    private readonly Guid _carlosSaturado = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _couriers.Register(new CourierCapacity(new CourierId(_ana), true, 2, 5));
        _couriers.Register(new CourierCapacity(new CourierId(_luis), true, 1, 5));
        _couriers.Register(new CourierCapacity(new CourierId(_carlosSaturado), true, 5, 5));
        _courierService = new SwitchableCourierService(_couriers);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Testing");
            web.UseSetting("ConnectionStrings:Entregas", _db.ConnectionString);
            web.UseSetting("Seguridad:ApiKey", ApiKey);
            web.UseSetting("Outbox:Habilitado", "false");
            web.UseSetting("Simulador:Habilitado", "false");
            web.ConfigureServices(services =>
            {
                services.RemoveAll<ICourierAvailability>();
                services.AddSingleton<ICourierAvailability>(_courierService);
            });
        });
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    private async Task<Guid> CreateDelivery()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries", new
        {
            orderId = Guid.NewGuid(),
            address = "Carrera 50 # 10-30, Medellín",
            windowStart = DateTimeOffset.UtcNow.AddHours(1),
            windowEnd = DateTimeOffset.UtcNow.AddHours(3),
            priority = "Normal",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("deliveryId").GetGuid();
    }

    private Task<HttpResponseMessage> Assign(Guid deliveryId, Guid courierId)
        => _client.PostAsJsonAsync($"/api/v1/deliveries/{deliveryId}/assignment", new { courierId });

    private static async Task<string?> Code(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    [Fact]
    public async Task FlujoCompleto_CrearAsignarYConsultar()
    {
        var id = await CreateDelivery();

        var assign = await Assign(id, _ana);
        var view = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/deliveries/{id}");

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        Assert.Equal("Asignada", view.GetProperty("status").GetString());
        Assert.Equal(_ana, view.GetProperty("currentCourierId").GetGuid());
        Assert.False(view.TryGetProperty("address", out _)); // la vista no expone la dirección
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM outbox_messages WHERE event_type = 'RepartidorAsignado'"));
    }

    [Fact]
    public async Task SegundaAsignacion_Devuelve409ConElCodigoDeLaInvariante()
    {
        var id = await CreateDelivery();
        await Assign(id, _ana);

        var second = await Assign(id, _luis);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(DomainErrors.AlreadyAssigned, await Code(second));
    }

    [Fact]
    public async Task RepartidorSaturado_Devuelve422()
    {
        var id = await CreateDelivery();

        var response = await Assign(id, _carlosSaturado);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(DomainErrors.CourierSaturated, await Code(response));
    }

    [Fact]
    public async Task ServicioDeTurnosCaido_Devuelve503YLaEntregaSigueSinAsignar()
    {
        var id = await CreateDelivery();
        _courierService.Down = true;

        var response = await Assign(id, _ana);
        var view = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/deliveries/{id}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("SERVICIO_REPARTIDORES_NO_DISPONIBLE", await Code(response));
        Assert.Equal("PendienteDeAsignacion", view.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reasignacion_PorTormenta_QuedaEnElHistorial()
    {
        var id = await CreateDelivery();
        await Assign(id, _ana);

        var response = await _client.PostAsJsonAsync($"/api/v1/deliveries/{id}/reassignment",
            new { courierId = _luis, motivo = "Tormenta: vía cerrada" });
        var view = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/deliveries/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_luis, view.GetProperty("currentCourierId").GetGuid());
        Assert.Equal("Tormenta: vía cerrada", view.GetProperty("assignments")[0].GetProperty("releaseReason").GetString());
    }

    [Fact]
    public async Task EntregaInexistente_Devuelve404()
    {
        var response = await Assign(Guid.NewGuid(), _ana);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SolicitudInvalida_Devuelve400ConElCampoQueFalla()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries", new { orderId = Guid.Empty, address = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("orderId", body);
        Assert.Contains("address", body);
    }

    [Fact]
    public async Task JsonMalFormado_Devuelve400YNo500()
    {
        var content = new StringContent("""{"orderId":"8c1e7f1a-0000-0000-0000-000000000001","windowStart":"\/Date(1762560000000)\/"}""",
            System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/deliveries", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SinClaveDeApi_Devuelve401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/v1/deliveries/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Errores_IncluyenElIdentificadorDeCorrelacion()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/deliveries/{Guid.NewGuid()}/assignment")
        {
            Content = JsonContent.Create(new { courierId = _ana }),
        };
        request.Headers.Add("X-Correlation-Id", "demo-123");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("demo-123", response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Equal("demo-123", body.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Salud_InformaBaseDeDatosYOutbox()
    {
        var health = await _client.GetFromJsonAsync<JsonElement>("/health");

        Assert.Equal("ok", health.GetProperty("baseDeDatos").GetString());
        Assert.Equal(0, health.GetProperty("mensajesFallidos").GetInt32());
    }
}
