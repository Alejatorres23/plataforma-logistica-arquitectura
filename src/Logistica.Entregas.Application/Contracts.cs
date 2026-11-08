namespace Logistica.Entregas.Application.Contracts;

// Contratos de aplicación: lo que entra y sale de los casos de uso.
// Son datos simples (sin HTTP ni base de datos) que cualquier adaptador de entrada puede usar.

/// <summary>Datos del pedido confirmado con los que se crea una entrega (evento PedidoConfirmado).</summary>
public sealed record CreateDeliveryCommand(
    Guid OrderId,
    string Address,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string Priority);

/// <summary>Solicitud para asignar un repartidor a una entrega.</summary>
public sealed record AssignCourierCommand(Guid DeliveryId, Guid CourierId, string RequestedBy);

/// <summary>Solicitud para cambiar el repartidor de una entrega.</summary>
public sealed record ReassignDeliveryCommand(Guid DeliveryId, Guid NewCourierId, string Reason, string RequestedBy);

/// <summary>
/// Resultado de un caso de uso. Los rechazos esperados (reglas del negocio, servicios caídos)
/// se devuelven como resultado con un código, no como excepción.
/// </summary>
public sealed record AssignmentResult(
    bool Succeeded,
    Guid DeliveryId,
    Guid? CourierId,
    string? Status,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AssignmentResult Ok(Guid deliveryId, Guid? courierId, string status)
        => new(true, deliveryId, courierId, status, null, null);

    public static AssignmentResult Fail(Guid deliveryId, string code, string message)
        => new(false, deliveryId, null, null, code, message);
}

/// <summary>Vista de lectura de una entrega (sin datos que no necesita quien consulta).</summary>
public sealed record DeliveryView(
    Guid DeliveryId,
    Guid OrderId,
    string Status,
    string Priority,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    Guid? CurrentCourierId,
    IReadOnlyList<AssignmentView> Assignments);

public sealed record AssignmentView(Guid CourierId, DateTimeOffset AssignedAt, DateTimeOffset? ReleasedAt, string? ReleaseReason);

/// <summary>Códigos de error propios de la capa de aplicación (los del negocio están en el dominio).</summary>
public static class ApplicationErrors
{
    public const string DeliveryNotFound = "ENTREGA_NO_ENCONTRADA";
    public const string CourierNotFound = "REPARTIDOR_NO_ENCONTRADO";
    public const string CourierServiceUnavailable = "SERVICIO_REPARTIDORES_NO_DISPONIBLE";
    public const string ConcurrencyConflict = "CONFLICTO_CONCURRENCIA";
}
