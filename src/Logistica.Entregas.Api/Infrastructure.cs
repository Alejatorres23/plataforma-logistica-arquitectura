using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Logistica.Entregas.Infrastructure.Messaging;
using Logistica.Entregas.Infrastructure.Persistence;

namespace Logistica.Entregas.Api;

/// <summary>
/// Asigna un identificador de correlación a cada solicitud (o usa el que llega en X-Correlation-Id),
/// lo devuelve en la respuesta y lo agrega a todos los logs, para seguir una operación de punta a punta.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var correlationId = IsSafe(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);
        context.Response.Headers[Header] = correlationId;

        // Plantilla de texto: el formateador de consola la muestra como "CorrelationId:abc" y los
        // proveedores estructurados (OpenTelemetry, JSON) la reciben como propiedad CorrelationId.
        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
            await next(context);
    }

    // Evita inyectar texto arbitrario en logs y cabeceras.
    private static bool IsSafe(string value)
        => value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

/// <summary>
/// Autenticación mínima para los endpoints de operación: cabecera X-Api-Key comparada en tiempo constante.
/// La clave se configura fuera del código (variable de entorno Seguridad__ApiKey); si no hay clave, se rechaza todo.
/// </summary>
public sealed class ApiKeyFilter(IConfiguration config) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = config["Seguridad:ApiKey"];
        var provided = context.HttpContext.Request.Headers["X-Api-Key"].ToString();

        if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected)))
            return Results.Problem(title: "Falta la clave de API o no es válida.", statusCode: StatusCodes.Status401Unauthorized);

        return await next(context);
    }
}

/// <summary>Ejecuta el despachador del outbox cada pocos segundos mientras la API está en marcha.</summary>
public sealed class OutboxBackgroundService(OutboxDispatcher dispatcher, OutboxOptions options, ILogger<OutboxBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.PollIntervalSeconds)));
        do
        {
            try
            {
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Error inesperado en el despachador del outbox");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public static class HealthEndpoint
{
    /// <summary>
    /// Estado de la API para operación: base de datos accesible, eventos pendientes y mensajes fallidos.
    /// Responde 503 si la base de datos no está disponible.
    /// </summary>
    public static void MapHealthEndpoint(this WebApplication app)
    {
        app.MapGet("/health", async (OutboxDispatcher outbox, CancellationToken ct) =>
        {
            try
            {
                var stats = await outbox.GetStatsAsync(ct);
                return Results.Ok(new
                {
                    estado = stats.DeadLettered > 0 ? "degradado" : "ok",
                    baseDeDatos = "ok",
                    outboxPendientes = stats.Pending,
                    mensajesFallidos = stats.DeadLettered,
                });
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return Results.Json(new { estado = "caido", baseDeDatos = "error" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }
}
