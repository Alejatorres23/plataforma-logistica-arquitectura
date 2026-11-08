using System.Collections.Concurrent;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;

namespace Logistica.Entregas.Infrastructure;

// Adaptadores en memoria de los puertos. Se usan en las pruebas de los casos de uso y se pueden
// activar en la API (Persistencia:Modo = "Memoria") para una demostración sin base de datos.
// Los adaptadores reales están en Persistence/ (SQLite) y Couriers/ (HTTP).

/// <summary>Repositorio de entregas en memoria. Guarda también los eventos, como haría el outbox.</summary>
public sealed class InMemoryDeliveryRepository : IDeliveryRepository
{
    private readonly ConcurrentDictionary<DeliveryId, Delivery> _store = new();
    private readonly ConcurrentQueue<IDomainEvent> _outbox = new();

    /// <summary>Eventos guardados (equivalente a la tabla outbox_messages).</summary>
    public IReadOnlyCollection<IDomainEvent> Outbox => _outbox.ToArray();

    public Task<Delivery?> GetAsync(DeliveryId id, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(id, out var delivery) ? delivery : null);

    public Task AddAsync(Delivery delivery, IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
        => SaveAsync(delivery, events, ct);

    public Task SaveAsync(Delivery delivery, IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
    {
        _store[delivery.Id] = delivery;
        foreach (var e in events)
            _outbox.Enqueue(e);
        return Task.CompletedTask;
    }
}

/// <summary>Disponibilidad de repartidores en memoria (también la usa el simulador del servicio de turnos).</summary>
public sealed class InMemoryCourierAvailability : ICourierAvailability
{
    private readonly ConcurrentDictionary<CourierId, CourierCapacity> _couriers = new();

    public void Register(CourierCapacity capacity) => _couriers[capacity.CourierId] = capacity;

    public IReadOnlyCollection<CourierCapacity> All => _couriers.Values.ToArray();

    public Task<CourierCapacity?> GetCapacityAsync(CourierId courierId, CancellationToken ct = default)
        => Task.FromResult(_couriers.TryGetValue(courierId, out var capacity) ? capacity : null);
}

/// <summary>Reloj del sistema.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
