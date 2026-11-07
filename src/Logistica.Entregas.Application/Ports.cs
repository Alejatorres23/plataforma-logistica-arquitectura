using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Application.Ports;

// Puertos de salida: lo que los casos de uso NECESITAN, expresado en el lenguaje del negocio.
// La infraestructura los implementa (adaptadores); la aplicación nunca conoce esas implementaciones.

/// <summary>Guarda y recupera entregas.</summary>
public interface IDeliveryRepository
{
    Task<Delivery?> GetAsync(DeliveryId id, CancellationToken ct = default);
    Task SaveAsync(Delivery delivery, CancellationToken ct = default);
}

/// <summary>
/// Consulta la disponibilidad y la carga actual de un repartidor.
/// Devuelve null si el repartidor no existe.
/// </summary>
public interface ICourierAvailability
{
    Task<CourierCapacity?> GetCapacityAsync(CourierId courierId, CancellationToken ct = default);
}

/// <summary>Publica los hechos del dominio para Notificaciones y Auditoría.</summary>
public interface IDomainEventPublisher
{
    Task PublishAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default);
}

/// <summary>Fecha y hora actuales (permite probar el dominio sin depender del reloj real).</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
