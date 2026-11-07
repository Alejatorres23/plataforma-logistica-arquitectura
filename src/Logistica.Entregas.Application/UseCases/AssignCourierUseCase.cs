using Logistica.Entregas.Application.Contracts;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Application.UseCases;

/// <summary>
/// Caso de uso: asignar un repartidor a una entrega.
/// Coordina el flujo; las reglas las decide la entidad <see cref="Delivery"/>.
/// Recibe sus dependencias por constructor y solo depende de interfaces (puertos).
/// </summary>
public sealed class AssignCourierUseCase(
    IDeliveryRepository deliveries,
    ICourierAvailability couriers,
    IDomainEventPublisher events,
    IClock clock)
{
    public async Task<AssignmentResult> ExecuteAsync(AssignCourierCommand command, CancellationToken ct = default)
    {
        // 1-2. Cargar la entrega y verificar que exista.
        var delivery = await deliveries.GetAsync(new DeliveryId(command.DeliveryId), ct);
        if (delivery is null)
            return AssignmentResult.Fail(command.DeliveryId, ApplicationErrors.DeliveryNotFound, "La entrega no existe.");

        // 3. Consultar la disponibilidad del repartidor mediante un puerto, no una API concreta.
        var capacity = await couriers.GetCapacityAsync(new CourierId(command.CourierId), ct);
        if (capacity is null)
            return AssignmentResult.Fail(command.DeliveryId, ApplicationErrors.CourierNotFound, "El repartidor no existe.");

        // 4. La entidad protege las invariantes (sin repartidor activo, estado válido, capacidad).
        try
        {
            delivery.AssignCourier(capacity, clock.Now);
        }
        catch (DomainException e)
        {
            return AssignmentResult.Fail(command.DeliveryId, e.Code, e.Message);
        }

        // 5. Guardar mediante el repositorio.
        await deliveries.SaveAsync(delivery, ct);

        // 6. Publicar el resultado mediante un adaptador (Notificaciones / Auditoría).
        await events.PublishAsync(delivery.PullEvents(), ct);

        return AssignmentResult.Ok(command.DeliveryId, command.CourierId, delivery.Status.ToString());
    }
}
