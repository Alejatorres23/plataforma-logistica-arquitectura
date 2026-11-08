using Logistica.Entregas.Application.Contracts;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Application.UseCases;

/// <summary>
/// Consulta: estado de una entrega y su historial de asignaciones.
/// No expone la dirección ni la evidencia (datos personales que esta vista no necesita).
/// </summary>
public sealed class GetDeliveryQuery(IDeliveryRepository deliveries)
{
    public async Task<DeliveryView?> ExecuteAsync(Guid deliveryId, CancellationToken ct = default)
    {
        var delivery = await deliveries.GetAsync(new DeliveryId(deliveryId), ct);
        if (delivery is null)
            return null;

        return new DeliveryView(
            delivery.Id.Value,
            delivery.Order.Value,
            delivery.Status.ToString(),
            delivery.Priority.ToString(),
            delivery.Window.Start,
            delivery.Window.End,
            delivery.CurrentCourierId?.Value,
            delivery.Assignments
                .Select(a => new AssignmentView(a.CourierId.Value, a.AssignedAt, a.ReleasedAt, a.ReleaseReason))
                .ToList());
    }
}
