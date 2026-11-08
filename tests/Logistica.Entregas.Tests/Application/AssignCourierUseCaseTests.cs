using Logistica.Entregas.Application.Contracts;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Application.UseCases;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure;

namespace Logistica.Entregas.Tests.Application;

/// <summary>
/// Pruebas de los casos de uso con los adaptadores en memoria.
/// Siguen los escenarios de tests/features/asignar-entrega.feature.
/// </summary>
public class AssignCourierUseCaseTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; } = now;
    }

    private sealed class DownCourierService : ICourierAvailability
    {
        public Task<CourierCapacity?> GetCapacityAsync(CourierId courierId, CancellationToken ct = default)
            => throw new CourierAvailabilityUnavailableException("Servicio caído (prueba).");
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 7, 15, 0, 0, TimeSpan.FromHours(-5));

    private readonly InMemoryDeliveryRepository _deliveries = new();
    private readonly InMemoryCourierAvailability _couriers = new();
    private readonly FixedClock _clock = new(Now);
    private readonly AssignCourierUseCase _assign;
    private readonly ReassignDeliveryUseCase _reassign;

    private readonly Delivery _delivery;                    // "E-100"
    private readonly CourierId _ana = new(Guid.NewGuid());  // activa, 2 de 5

    public AssignCourierUseCaseTests()
    {
        _assign = new AssignCourierUseCase(_deliveries, _couriers, _clock);
        _reassign = new ReassignDeliveryUseCase(_deliveries, _couriers, _clock);

        // Antecedentes
        _delivery = Delivery.Create(
            new OrderReference(Guid.NewGuid()),
            new DeliveryAddress("Carrera 50 # 10-30, Medellín"),
            new DeliveryWindow(Now.AddHours(1), Now.AddHours(3)),
            DeliveryPriority.Normal);
        _deliveries.AddAsync(_delivery, []).Wait();
        _couriers.Register(new CourierCapacity(_ana, isActive: true, activeDeliveries: 2, maxDeliveries: 5));
    }

    private CourierId RegisterCourier(int active, int max = 5, bool isActive = true)
    {
        var id = new CourierId(Guid.NewGuid());
        _couriers.Register(new CourierCapacity(id, isActive, active, max));
        return id;
    }

    private Task<AssignmentResult> Assign(CourierId courier, Guid? deliveryId = null)
        => _assign.ExecuteAsync(new AssignCourierCommand(deliveryId ?? _delivery.Id.Value, courier.Value, "supervisor-1"));

    [Fact]
    public async Task AssignCourier_RepartidorDisponible_QuedaAsignadaYSeGuardaElEvento()
    {
        var result = await Assign(_ana);

        Assert.True(result.Succeeded);
        var saved = await _deliveries.GetAsync(_delivery.Id);
        Assert.Equal(DeliveryStatus.Asignada, saved!.Status);
        Assert.Equal(_ana, saved.CurrentCourierId);
        var evento = Assert.IsType<CourierAssigned>(Assert.Single(_deliveries.Outbox));
        Assert.Equal(_ana, evento.CourierId);
    }

    [Fact]
    public async Task AssignCourier_EntregaYaAsignada_SeRechaza()
    {
        await Assign(_ana);
        var luis = RegisterCourier(active: 0);

        var result = await Assign(luis);

        Assert.False(result.Succeeded);
        Assert.Equal(DomainErrors.AlreadyAssigned, result.ErrorCode);
        Assert.Equal(_ana, (await _deliveries.GetAsync(_delivery.Id))!.CurrentCourierId);
        Assert.Single(_deliveries.Outbox); // solo el evento de la primera asignación
    }

    [Fact]
    public async Task AssignCourier_RepartidorSaturado_SeRechaza()
    {
        var carlos = RegisterCourier(active: 5, max: 5);

        var result = await Assign(carlos);

        Assert.False(result.Succeeded);
        Assert.Equal(DomainErrors.CourierSaturated, result.ErrorCode);
        Assert.Equal(DeliveryStatus.PendienteDeAsignacion, (await _deliveries.GetAsync(_delivery.Id))!.Status);
        Assert.Empty(_deliveries.Outbox);
    }

    [Fact]
    public async Task AssignCourier_RepartidorInactivo_SeRechaza()
    {
        var pedro = RegisterCourier(active: 0, isActive: false);

        var result = await Assign(pedro);

        Assert.Equal(DomainErrors.CourierUnavailable, result.ErrorCode);
    }

    [Fact]
    public async Task AssignCourier_EntregaInexistente_SeRechaza()
    {
        var result = await Assign(_ana, deliveryId: Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrors.DeliveryNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task AssignCourier_RepartidorInexistente_SeRechaza()
    {
        var result = await Assign(new CourierId(Guid.NewGuid()));

        Assert.Equal(ApplicationErrors.CourierNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task AssignCourier_ServicioDeTurnosCaido_FallaControladaSinCambiarLaEntrega()
    {
        var useCase = new AssignCourierUseCase(_deliveries, new DownCourierService(), _clock);

        var result = await useCase.ExecuteAsync(new AssignCourierCommand(_delivery.Id.Value, _ana.Value, "supervisor-1"));

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrors.CourierServiceUnavailable, result.ErrorCode);
        Assert.Equal(DeliveryStatus.PendienteDeAsignacion, (await _deliveries.GetAsync(_delivery.Id))!.Status);
    }

    [Fact]
    public async Task Reassign_PorTormenta_CierraAsignacionAnteriorYRegistraMotivo()
    {
        await Assign(_ana);
        var luis = RegisterCourier(active: 1);

        var result = await _reassign.ExecuteAsync(
            new ReassignDeliveryCommand(_delivery.Id.Value, luis.Value, "Tormenta: vía cerrada", "supervisor-1"));

        Assert.True(result.Succeeded);
        var saved = (await _deliveries.GetAsync(_delivery.Id))!;
        Assert.Equal(luis, saved.CurrentCourierId);
        Assert.Equal("Tormenta: vía cerrada", saved.Assignments.Single(a => a.CourierId == _ana).ReleaseReason);
        Assert.IsType<DeliveryReassigned>(_deliveries.Outbox.Last());
    }

    [Fact]
    public async Task Reassign_SinMotivo_SeRechaza()
    {
        await Assign(_ana);
        var luis = RegisterCourier(active: 1);

        var result = await _reassign.ExecuteAsync(
            new ReassignDeliveryCommand(_delivery.Id.Value, luis.Value, "", "supervisor-1"));

        Assert.Equal(DomainErrors.ReasonRequired, result.ErrorCode);
    }

    [Theory]
    [InlineData("Expresa")]
    [InlineData("7")]
    public async Task CreateDelivery_PrioridadInvalida_SeRechaza(string priority)
    {
        var useCase = new CreateDeliveryUseCase(_deliveries);

        var result = await useCase.ExecuteAsync(new CreateDeliveryCommand(
            Guid.NewGuid(), "Calle 1", Now, Now.AddHours(2), priority));

        Assert.Equal(DomainErrors.InvalidValue, result.ErrorCode);
    }
}
