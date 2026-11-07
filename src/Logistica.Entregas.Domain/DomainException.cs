namespace Logistica.Entregas.Domain;

/// <summary>
/// Se lanza cuando una operación violaría una regla (invariante) del negocio.
/// El <see cref="Code"/> permite a la capa de aplicación traducir el error sin leer el mensaje.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Códigos de las reglas de negocio del contexto Entregas.</summary>
public static class DomainErrors
{
    public const string AlreadyAssigned = "ENTREGA_YA_ASIGNADA";
    public const string InvalidState = "ESTADO_NO_PERMITE_OPERACION";
    public const string CourierUnavailable = "REPARTIDOR_NO_DISPONIBLE";
    public const string CourierSaturated = "REPARTIDOR_SATURADO";
    public const string ReasonRequired = "MOTIVO_OBLIGATORIO";
    public const string SameCourier = "MISMO_REPARTIDOR";
    public const string ProofRequired = "EVIDENCIA_OBLIGATORIA";
    public const string InvalidValue = "VALOR_INVALIDO";
}
