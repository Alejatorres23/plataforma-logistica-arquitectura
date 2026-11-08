using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Logistica.Entregas.Infrastructure.Observability;

/// <summary>
/// Señales de observabilidad del contexto Entregas (métricas y trazas con las APIs estándar de .NET,
/// compatibles con OpenTelemetry). Solo llevan identificadores y códigos: nunca direcciones,
/// nombres de quien recibe ni ubicaciones.
/// Se pueden ver en vivo con: dotnet-counters monitor --counters Logistica.Entregas -n Logistica.Entregas.Api
/// </summary>
public sealed class EntregasTelemetry : IDisposable
{
    public const string Name = "Logistica.Entregas";

    public static readonly ActivitySource Tracing = new(Name);

    private readonly Meter _meter = new(Name);

    public EntregasTelemetry()
    {
        Assignments = _meter.CreateCounter<long>("entregas.asignaciones", description: "Asignaciones y reasignaciones por resultado y código");
        AssignmentDuration = _meter.CreateHistogram<double>("entregas.asignacion.duracion", unit: "ms", description: "Duración del caso de uso de asignación");
        CourierServiceFailures = _meter.CreateCounter<long>("entregas.servicio_repartidores.fallos", description: "Intentos fallidos contra el servicio de turnos");
        OutboxPublished = _meter.CreateCounter<long>("entregas.outbox.publicados", description: "Eventos publicados desde el outbox");
        OutboxDeadLettered = _meter.CreateCounter<long>("entregas.outbox.dlq", description: "Eventos enviados a la cola de mensajes fallidos");
    }

    public Counter<long> Assignments { get; }
    public Histogram<double> AssignmentDuration { get; }
    public Counter<long> CourierServiceFailures { get; }
    public Counter<long> OutboxPublished { get; }
    public Counter<long> OutboxDeadLettered { get; }

    public void Dispose() => _meter.Dispose();
}
