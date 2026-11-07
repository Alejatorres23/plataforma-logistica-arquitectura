namespace Logistica.Entregas.Domain;

/// <summary>Estados del ciclo de vida de una entrega.</summary>
public enum DeliveryStatus
{
    PendienteDeAsignacion,
    Asignada,
    EnCamino,
    Entregada,
    Fallida,
    Cancelada,
}

/// <summary>
/// Asignación de un repartidor a la entrega. Es una entidad interna del agregado:
/// solo se crea y se cierra a través de <see cref="Delivery"/>.
/// </summary>
public sealed class Assignment
{
    internal Assignment(CourierId courierId, DateTimeOffset assignedAt)
    {
        CourierId = courierId;
        AssignedAt = assignedAt;
    }

    public CourierId CourierId { get; }
    public DateTimeOffset AssignedAt { get; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public string? ReleaseReason { get; private set; }
    public bool IsActive => ReleasedAt is null;

    internal void Release(string reason, DateTimeOffset at)
    {
        ReleasedAt = at;
        ReleaseReason = reason;
    }
}

/// <summary>
/// Entrega: raíz del agregado del contexto Entregas. Toda modificación pasa por sus métodos,
/// que son los que protegen las invariantes:
///   I1. Una entrega activa tiene como máximo un repartidor asignado.
///   I2. Solo se asigna una entrega pendiente de asignación.
///   I3. No se asigna a un repartidor inactivo o saturado.
///   I4. Reasignar exige un motivo y un repartidor distinto; la asignación anterior queda cerrada.
///   I5. Una entrega no se completa sin evidencia de recepción.
///   I6. Las transiciones de estado siguen el ciclo definido; los estados finales no cambian.
/// </summary>
public sealed class Delivery
{
    private readonly List<Assignment> _assignments = [];
    private readonly List<IDomainEvent> _events = [];

    private Delivery(DeliveryId id, OrderReference order, DeliveryAddress address, DeliveryWindow window, DeliveryPriority priority)
    {
        Id = id;
        Order = order;
        Address = address;
        Window = window;
        Priority = priority;
        Status = DeliveryStatus.PendienteDeAsignacion;
    }

    public DeliveryId Id { get; }
    public OrderReference Order { get; }
    public DeliveryAddress Address { get; }
    public DeliveryWindow Window { get; }
    public DeliveryPriority Priority { get; }
    public DeliveryStatus Status { get; private set; }
    public ProofOfDelivery? Proof { get; private set; }

    /// <summary>Historial de asignaciones (las cerradas y, como máximo, una activa).</summary>
    public IReadOnlyList<Assignment> Assignments => _assignments;

    /// <summary>Repartidor responsable en este momento, si lo hay.</summary>
    public CourierId? CurrentCourierId => _assignments.SingleOrDefault(a => a.IsActive)?.CourierId;

    /// <summary>Crea la entrega de un pedido confirmado. Nace pendiente de asignación.</summary>
    public static Delivery Create(OrderReference order, DeliveryAddress address, DeliveryWindow window, DeliveryPriority priority)
        => new(DeliveryId.New(), order, address, window, priority);

    /// <summary>Asigna el primer repartidor a la entrega.</summary>
    public void AssignCourier(CourierCapacity courier, DateTimeOffset at)
    {
        if (CurrentCourierId is not null)
            throw new DomainException(DomainErrors.AlreadyAssigned, "La entrega ya tiene un repartidor activo.");                     // I1
        EnsureStatus(DeliveryStatus.PendienteDeAsignacion);                                                                          // I2
        EnsureCanTakeDelivery(courier);                                                                                              // I3

        _assignments.Add(new Assignment(courier.CourierId, at));
        Status = DeliveryStatus.Asignada;
        _events.Add(new CourierAssigned(Id, courier.CourierId, at));
    }

    /// <summary>Cambia el repartidor de una entrega asignada o en camino (p. ej., por una tormenta).</summary>
    public void Reassign(CourierCapacity newCourier, string reason, DateTimeOffset at)
    {
        EnsureStatus(DeliveryStatus.Asignada, DeliveryStatus.EnCamino);                                                              // I6
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException(DomainErrors.ReasonRequired, "Toda reasignación debe registrar un motivo.");                    // I4
        var current = _assignments.Single(a => a.IsActive);
        if (current.CourierId == newCourier.CourierId)
            throw new DomainException(DomainErrors.SameCourier, "El nuevo repartidor debe ser distinto del actual.");                 // I4
        EnsureCanTakeDelivery(newCourier);                                                                                           // I3

        current.Release(reason.Trim(), at);
        _assignments.Add(new Assignment(newCourier.CourierId, at));
        Status = DeliveryStatus.Asignada;
        _events.Add(new DeliveryReassigned(Id, current.CourierId, newCourier.CourierId, reason.Trim(), at));
    }

    /// <summary>El repartidor recogió el paquete y sale a ruta.</summary>
    public void StartRoute()
    {
        EnsureStatus(DeliveryStatus.Asignada);
        Status = DeliveryStatus.EnCamino;
    }

    /// <summary>Marca la entrega como completada. Exige evidencia de recepción.</summary>
    public void Complete(ProofOfDelivery? proof)
    {
        EnsureStatus(DeliveryStatus.EnCamino);
        Proof = proof ?? throw new DomainException(DomainErrors.ProofRequired, "No se puede completar una entrega sin evidencia de recepción."); // I5
        _assignments.Single(a => a.IsActive).Release("Entregada", proof.ReceivedAt);
        Status = DeliveryStatus.Entregada;
    }

    /// <summary>La entrega no se pudo realizar (cliente ausente, vía cerrada...). Libera al repartidor.</summary>
    public void Fail(string reason, DateTimeOffset at)
    {
        EnsureStatus(DeliveryStatus.EnCamino);
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException(DomainErrors.ReasonRequired, "Debe registrarse el motivo de la entrega fallida.");
        _assignments.Single(a => a.IsActive).Release(reason.Trim(), at);
        Status = DeliveryStatus.Fallida;
    }

    /// <summary>Vuelve a dejar pendiente una entrega fallida para intentarla de nuevo.</summary>
    public void ScheduleRetry()
    {
        EnsureStatus(DeliveryStatus.Fallida);
        Status = DeliveryStatus.PendienteDeAsignacion;
    }

    /// <summary>Cancela la entrega antes de que salga a ruta.</summary>
    public void Cancel(string reason, DateTimeOffset at)
    {
        EnsureStatus(DeliveryStatus.PendienteDeAsignacion, DeliveryStatus.Asignada);
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException(DomainErrors.ReasonRequired, "Debe registrarse el motivo de la cancelación.");
        _assignments.SingleOrDefault(a => a.IsActive)?.Release(reason.Trim(), at);
        Status = DeliveryStatus.Cancelada;
    }

    /// <summary>Entrega y vacía los eventos ocurridos, para que la aplicación los publique.</summary>
    public IReadOnlyList<IDomainEvent> PullEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }

    private static void EnsureCanTakeDelivery(CourierCapacity courier)
    {
        if (!courier.IsActive)
            throw new DomainException(DomainErrors.CourierUnavailable, "El repartidor no está activo.");
        if (courier.IsSaturated)
            throw new DomainException(DomainErrors.CourierSaturated, "El repartidor alcanzó su máximo de entregas activas.");
    }

    private void EnsureStatus(params DeliveryStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainException(DomainErrors.InvalidState, $"La operación no está permitida en estado {Status}.");
    }
}
