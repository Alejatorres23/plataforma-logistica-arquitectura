using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Tests.Domain;

/// <summary>Pruebas unitarias de las invariantes de la entidad Delivery (sin infraestructura).</summary>
public class DeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 15, 0, 0, TimeSpan.FromHours(-5));

    private static Delivery NewDelivery() => Delivery.Create(
        new OrderReference(Guid.NewGuid()),
        new DeliveryAddress("Calle 10 # 43-20, Medellín"),
        new DeliveryWindow(Now.AddHours(1), Now.AddHours(3)),
        DeliveryPriority.Normal);

    private static CourierCapacity Courier(int active = 0, int max = 5, bool isActive = true)
        => new(new CourierId(Guid.NewGuid()), isActive, active, max);

    [Fact]
    public void NuevaEntrega_NacePendienteYSinRepartidor()
    {
        var delivery = NewDelivery();

        Assert.Equal(DeliveryStatus.PendienteDeAsignacion, delivery.Status);
        Assert.Null(delivery.CurrentCourierId);
    }

    [Fact]
    public void AssignCourier_RepartidorDisponible_QuedaAsignada()
    {
        var delivery = NewDelivery();
        var ana = Courier(active: 2);

        delivery.AssignCourier(ana, Now);

        Assert.Equal(DeliveryStatus.Asignada, delivery.Status);
        Assert.Equal(ana.CourierId, delivery.CurrentCourierId);
        Assert.IsType<CourierAssigned>(Assert.Single(delivery.PullEvents()));
    }

    [Fact] // I1
    public void AssignCourier_EntregaYaAsignada_SeRechaza()
    {
        var delivery = NewDelivery();
        var ana = Courier();
        delivery.AssignCourier(ana, Now);

        var error = Assert.Throws<DomainException>(() => delivery.AssignCourier(Courier(), Now));

        Assert.Equal(DomainErrors.AlreadyAssigned, error.Code);
        Assert.Equal(ana.CourierId, delivery.CurrentCourierId);
        Assert.Single(delivery.Assignments, a => a.IsActive);
    }

    [Fact] // I3
    public void AssignCourier_RepartidorSaturado_SeRechaza()
    {
        var delivery = NewDelivery();

        var error = Assert.Throws<DomainException>(() => delivery.AssignCourier(Courier(active: 5, max: 5), Now));

        Assert.Equal(DomainErrors.CourierSaturated, error.Code);
        Assert.Equal(DeliveryStatus.PendienteDeAsignacion, delivery.Status);
    }

    [Fact] // I3
    public void AssignCourier_RepartidorInactivo_SeRechaza()
    {
        var delivery = NewDelivery();

        var error = Assert.Throws<DomainException>(() => delivery.AssignCourier(Courier(isActive: false), Now));

        Assert.Equal(DomainErrors.CourierUnavailable, error.Code);
    }

    [Fact] // I2 / I6
    public void AssignCourier_EntregaCancelada_SeRechaza()
    {
        var delivery = NewDelivery();
        delivery.Cancel("Cliente canceló la compra", Now);

        var error = Assert.Throws<DomainException>(() => delivery.AssignCourier(Courier(), Now));

        Assert.Equal(DomainErrors.InvalidState, error.Code);
    }

    [Fact] // I4
    public void Reassign_PorTormenta_CierraAsignacionAnteriorYRegistraMotivo()
    {
        var delivery = NewDelivery();
        var ana = Courier();
        var luis = Courier(active: 1);
        delivery.AssignCourier(ana, Now);
        delivery.PullEvents();

        delivery.Reassign(luis, "Tormenta: vía cerrada", Now.AddMinutes(30));

        Assert.Equal(luis.CourierId, delivery.CurrentCourierId);
        var anterior = delivery.Assignments.Single(a => a.CourierId == ana.CourierId);
        Assert.False(anterior.IsActive);
        Assert.Equal("Tormenta: vía cerrada", anterior.ReleaseReason);
        Assert.Single(delivery.Assignments, a => a.IsActive);
        Assert.IsType<DeliveryReassigned>(Assert.Single(delivery.PullEvents()));
    }

    [Fact] // I4
    public void Reassign_SinMotivo_SeRechaza()
    {
        var delivery = NewDelivery();
        delivery.AssignCourier(Courier(), Now);

        var error = Assert.Throws<DomainException>(() => delivery.Reassign(Courier(), "  ", Now));

        Assert.Equal(DomainErrors.ReasonRequired, error.Code);
    }

    [Fact] // I4
    public void Reassign_AlMismoRepartidor_SeRechaza()
    {
        var delivery = NewDelivery();
        var ana = Courier();
        delivery.AssignCourier(ana, Now);

        var error = Assert.Throws<DomainException>(() => delivery.Reassign(ana, "Prueba", Now));

        Assert.Equal(DomainErrors.SameCourier, error.Code);
    }

    [Fact] // I5
    public void Complete_SinEvidencia_SeRechaza()
    {
        var delivery = NewDelivery();
        delivery.AssignCourier(Courier(), Now);
        delivery.StartRoute();

        var error = Assert.Throws<DomainException>(() => delivery.Complete(null));

        Assert.Equal(DomainErrors.ProofRequired, error.Code);
        Assert.Equal(DeliveryStatus.EnCamino, delivery.Status);
    }

    [Fact]
    public void Complete_ConEvidencia_QuedaEntregadaYLiberaAlRepartidor()
    {
        var delivery = NewDelivery();
        delivery.AssignCourier(Courier(), Now);
        delivery.StartRoute();

        delivery.Complete(new ProofOfDelivery("María Gómez", Now.AddHours(2)));

        Assert.Equal(DeliveryStatus.Entregada, delivery.Status);
        Assert.Null(delivery.CurrentCourierId);
    }

    [Fact] // I6
    public void EntregaEntregada_NoPuedeCancelarse()
    {
        var delivery = NewDelivery();
        delivery.AssignCourier(Courier(), Now);
        delivery.StartRoute();
        delivery.Complete(new ProofOfDelivery("María Gómez", Now.AddHours(2)));

        var error = Assert.Throws<DomainException>(() => delivery.Cancel("Error", Now));

        Assert.Equal(DomainErrors.InvalidState, error.Code);
    }

    [Fact]
    public void EntregaFallida_PuedeReprogramarseYAsignarseDeNuevo()
    {
        var delivery = NewDelivery();
        delivery.AssignCourier(Courier(), Now);
        delivery.StartRoute();
        delivery.Fail("Cliente ausente", Now.AddHours(2));

        delivery.ScheduleRetry();
        delivery.AssignCourier(Courier(), Now.AddDays(1));

        Assert.Equal(DeliveryStatus.Asignada, delivery.Status);
        Assert.Single(delivery.Assignments, a => a.IsActive);
    }

    [Fact]
    public void DeliveryWindow_FinAntesDelInicio_NoPuedeCrearse()
    {
        var error = Assert.Throws<DomainException>(() => new DeliveryWindow(Now, Now.AddMinutes(-1)));

        Assert.Equal(DomainErrors.InvalidValue, error.Code);
    }
}
