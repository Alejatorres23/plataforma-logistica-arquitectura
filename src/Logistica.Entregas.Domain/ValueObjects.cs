namespace Logistica.Entregas.Domain;

// Objetos de valor: no tienen identidad propia, se comparan por sus atributos
// y validan sus propias reglas al crearse (un objeto de valor inválido no puede existir).

/// <summary>Identificador de una entrega.</summary>
public sealed record DeliveryId(Guid Value)
{
    public static DeliveryId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>Identificador de un repartidor (el repartidor como persona vive en otro contexto).</summary>
public sealed record CourierId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Referencia al pedido que originó la entrega. Entregas no conoce el modelo de Pedidos:
/// solo guarda su identificador (límite de contexto).
/// </summary>
public sealed record OrderReference(Guid Value);

/// <summary>Dirección de entrega.</summary>
public sealed record DeliveryAddress
{
    public string Value { get; }

    public DeliveryAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException(DomainErrors.InvalidValue, "La dirección de entrega es obligatoria.");
        Value = value.Trim();
    }
}

/// <summary>Franja horaria prometida al cliente. El fin siempre es posterior al inicio.</summary>
public sealed record DeliveryWindow
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public DeliveryWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new DomainException(DomainErrors.InvalidValue, "La ventana de entrega debe terminar después de comenzar.");
        Start = start;
        End = end;
    }
}

/// <summary>Prioridad de la entrega.</summary>
public enum DeliveryPriority
{
    Normal,
    Urgente,
}

/// <summary>
/// Foto de la capacidad de un repartidor en un momento dado. La entrega la usa para decidir
/// si puede asignarse, sin saber de dónde salió el dato (lo trae un puerto).
/// </summary>
public sealed record CourierCapacity
{
    public CourierId CourierId { get; }
    public bool IsActive { get; }
    public int ActiveDeliveries { get; }
    public int MaxDeliveries { get; }

    public CourierCapacity(CourierId courierId, bool isActive, int activeDeliveries, int maxDeliveries)
    {
        if (activeDeliveries < 0 || maxDeliveries <= 0)
            throw new DomainException(DomainErrors.InvalidValue, "La capacidad del repartidor no es válida.");
        CourierId = courierId;
        IsActive = isActive;
        ActiveDeliveries = activeDeliveries;
        MaxDeliveries = maxDeliveries;
    }

    /// <summary>Un repartidor está saturado cuando su carga activa alcanzó su máximo.</summary>
    public bool IsSaturated => ActiveDeliveries >= MaxDeliveries;
}

/// <summary>Evidencia de que el cliente recibió el paquete.</summary>
public sealed record ProofOfDelivery
{
    public string ReceivedBy { get; }
    public DateTimeOffset ReceivedAt { get; }

    public ProofOfDelivery(string receivedBy, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(receivedBy))
            throw new DomainException(DomainErrors.ProofRequired, "La evidencia debe indicar quién recibió.");
        ReceivedBy = receivedBy.Trim();
        ReceivedAt = receivedAt;
    }
}
