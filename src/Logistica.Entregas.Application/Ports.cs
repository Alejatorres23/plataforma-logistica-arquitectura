using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Application.Ports;

// Puertos de salida: lo que los casos de uso NECESITAN, expresado en el lenguaje del negocio.
// La infraestructura los implementa (adaptadores); la aplicación nunca conoce esas implementaciones.

/// <summary>
/// Guarda y recupera entregas. Al guardar recibe también los eventos ocurridos, para que el
/// adaptador los persista en la misma transacción que la entrega (patrón outbox, ADR-003).
/// </summary>
public interface IDeliveryRepository
{
    Task<Delivery?> GetAsync(DeliveryId id, CancellationToken ct = default);

    /// <summary>Guarda una entrega nueva.</summary>
    Task AddAsync(Delivery delivery, IReadOnlyList<IDomainEvent> events, CancellationToken ct = default);

    /// <summary>
    /// Guarda los cambios de una entrega cargada antes con <see cref="GetAsync"/>.
    /// Lanza <see cref="ConcurrencyConflictException"/> si otra operación la modificó mientras tanto.
    /// </summary>
    Task SaveAsync(Delivery delivery, IReadOnlyList<IDomainEvent> events, CancellationToken ct = default);
}

/// <summary>
/// Consulta la disponibilidad y la carga actual de un repartidor.
/// Devuelve null si el repartidor no existe; lanza <see cref="CourierAvailabilityUnavailableException"/>
/// si el servicio que lo sabe no responde.
/// </summary>
public interface ICourierAvailability
{
    Task<CourierCapacity?> GetCapacityAsync(CourierId courierId, CancellationToken ct = default);
}

/// <summary>Fecha y hora actuales (permite probar el dominio sin depender del reloj real).</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

/// <summary>Otra operación modificó la entrega entre la lectura y el guardado.</summary>
public sealed class ConcurrencyConflictException(DeliveryId id)
    : Exception($"La entrega {id} fue modificada por otra operación.");

/// <summary>El servicio de disponibilidad de repartidores no respondió a tiempo o falló.</summary>
public sealed class CourierAvailabilityUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
