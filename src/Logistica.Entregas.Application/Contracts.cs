namespace Logistica.Entregas.Application.Contracts;

// Contratos de aplicación: lo que entra y sale de los casos de uso.
// Son datos simples (sin HTTP ni base de datos) que cualquier adaptador de entrada puede usar.

/// <summary>Solicitud para asignar un repartidor a una entrega.</summary>
public sealed record AssignCourierCommand(Guid DeliveryId, Guid CourierId, string RequestedBy);

/// <summary>Solicitud para cambiar el repartidor de una entrega.</summary>
public sealed record ReassignDeliveryCommand(Guid DeliveryId, Guid NewCourierId, string Reason, string RequestedBy);

/// <summary>
/// Resultado de un caso de uso de asignación. Los rechazos esperados (reglas del negocio)
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
    public static AssignmentResult Ok(Guid deliveryId, Guid courierId, string status)
        => new(true, deliveryId, courierId, status, null, null);

    public static AssignmentResult Fail(Guid deliveryId, string code, string message)
        => new(false, deliveryId, null, null, code, message);
}

/// <summary>Códigos de error propios de la capa de aplicación (los del negocio están en el dominio).</summary>
public static class ApplicationErrors
{
    public const string DeliveryNotFound = "ENTREGA_NO_ENCONTRADA";
    public const string CourierNotFound = "REPARTIDOR_NO_ENCONTRADO";
}
