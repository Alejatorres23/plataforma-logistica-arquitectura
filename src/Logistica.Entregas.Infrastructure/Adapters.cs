using System.Collections.Concurrent;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Infrastructure;

// Adaptadores de esta actividad: implementaciones en memoria de los puertos.
// Permiten ejecutar y probar el flujo completo sin base de datos ni APIs externas.
// En la Actividad 4 se reemplazan por adaptadores reales (SQL, HTTP, cola) sin tocar
// el dominio ni los casos de uso, porque estos solo conocen las interfaces.

/// <summary>Repositorio de entregas en memoria.</summary>
public sealed class InMemoryDeliveryRepository : IDeliveryRepository
{
    private readonly ConcurrentDictionary<DeliveryId, Delivery> _store = new();

    public Task<Delivery?> GetAsync(DeliveryId id, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(id, out var delivery) ? delivery : null);

    public Task SaveAsync(Delivery delivery, CancellationToken ct = default)
    {
        _store[delivery.Id] = delivery;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Disponibilidad de repartidores en memoria. Representa al servicio que, en producción,
/// conocerá los turnos y la carga activa de cada repartidor.
/// </summary>
public sealed class InMemoryCourierAvailability : ICourierAvailability
{
    private readonly ConcurrentDictionary<CourierId, CourierCapacity> _couriers = new();

    public void Register(CourierCapacity capacity) => _couriers[capacity.CourierId] = capacity;

    public Task<CourierCapacity?> GetCapacityAsync(CourierId courierId, CancellationToken ct = default)
        => Task.FromResult(_couriers.TryGetValue(courierId, out var capacity) ? capacity : null);
}

/// <summary>
/// Publicador de eventos que los guarda en memoria. Sirve como registro de auditoría
/// en esta actividad y permite verificar en las pruebas qué se publicó.
/// </summary>
public sealed class InMemoryDomainEventPublisher : IDomainEventPublisher
{
    private readonly ConcurrentQueue<IDomainEvent> _published = new();

    public IReadOnlyCollection<IDomainEvent> Published => _published.ToArray();

    public Task PublishAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var e in events)
            _published.Enqueue(e);
        return Task.CompletedTask;
    }
}

/// <summary>Reloj del sistema.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
