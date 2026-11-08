using Logistica.Entregas.Application.Contracts;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Application.UseCases;

/// <summary>
/// Caso de uso: cambiar el repartidor de una entrega (contingencias como la tormenta de la Actividad 1).
/// Mismo patrón que <see cref="AssignCourierUseCase"/>: coordina, y la entidad decide.
/// </summary>
public sealed class ReassignDeliveryUseCase(
    IDeliveryRepository deliveries,
    ICourierAvailability couriers,
    IClock clock)
{
    public async Task<AssignmentResult> ExecuteAsync(ReassignDeliveryCommand command, CancellationToken ct = default)
    {
        var delivery = await deliveries.GetAsync(new DeliveryId(command.DeliveryId), ct);
        if (delivery is null)
            return AssignmentResult.Fail(command.DeliveryId, ApplicationErrors.DeliveryNotFound, "La entrega no existe.");

        CourierCapacity? capacity;
        try
        {
            capacity = await couriers.GetCapacityAsync(new CourierId(command.NewCourierId), ct);
        }
        catch (CourierAvailabilityUnavailableException e)
        {
            return AssignmentResult.Fail(command.DeliveryId, ApplicationErrors.CourierServiceUnavailable, e.Message);
        }
        if (capacity is null)
            return AssignmentResult.Fail(command.DeliveryId, ApplicationErrors.CourierNotFound, "El repartidor no existe.");

        try
        {
            delivery.Reassign(capacity, command.Reason, clock.Now);
        }
        catch (DomainException e)
        {
            return AssignmentResult.Fail(command.DeliveryId, e.Code, e.Message);
        }

        try
        {
            await deliveries.SaveAsync(delivery, delivery.PullEvents(), ct);
        }
        catch (ConcurrencyConflictException e)
        {
            return AssignmentResult.Fail(command.DeliveryId, ApplicationErrors.ConcurrencyConflict, e.Message);
        }

        return AssignmentResult.Ok(command.DeliveryId, command.NewCourierId, delivery.Status.ToString());
    }
}
