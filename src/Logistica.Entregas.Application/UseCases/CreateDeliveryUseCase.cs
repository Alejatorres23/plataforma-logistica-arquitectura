using Logistica.Entregas.Application.Contracts;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Application.UseCases;

/// <summary>
/// Caso de uso: crear la entrega de un pedido confirmado (traduce el evento PedidoConfirmado
/// del contexto Pedidos al modelo de Entregas).
/// </summary>
public sealed class CreateDeliveryUseCase(IDeliveryRepository deliveries)
{
    public async Task<AssignmentResult> ExecuteAsync(CreateDeliveryCommand command, CancellationToken ct = default)
    {
        Delivery delivery;
        try
        {
            if (!Enum.TryParse<DeliveryPriority>(command.Priority, ignoreCase: true, out var priority)
                || !Enum.IsDefined(priority) || int.TryParse(command.Priority, out _))
                throw new DomainException(DomainErrors.InvalidValue, "La prioridad debe ser Normal o Urgente.");

            delivery = Delivery.Create(
                new OrderReference(command.OrderId),
                new DeliveryAddress(command.Address),
                new DeliveryWindow(command.WindowStart, command.WindowEnd),
                priority);
        }
        catch (DomainException e)
        {
            return AssignmentResult.Fail(Guid.Empty, e.Code, e.Message);
        }

        await deliveries.AddAsync(delivery, delivery.PullEvents(), ct);
        return AssignmentResult.Ok(delivery.Id.Value, null, delivery.Status.ToString());
    }
}
