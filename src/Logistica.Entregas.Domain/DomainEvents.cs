namespace Logistica.Entregas.Domain;

/// <summary>Hecho del negocio que ya ocurrió y que otros contextos pueden necesitar conocer.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Se asignó un repartidor a una entrega que no tenía.</summary>
public sealed record CourierAssigned(DeliveryId DeliveryId, CourierId CourierId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Una entrega cambió de repartidor (por ejemplo, durante una contingencia).</summary>
public sealed record DeliveryReassigned(
    DeliveryId DeliveryId,
    CourierId PreviousCourierId,
    CourierId NewCourierId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
