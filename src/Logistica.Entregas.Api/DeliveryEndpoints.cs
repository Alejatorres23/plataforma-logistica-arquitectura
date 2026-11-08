using System.Diagnostics;
using Logistica.Entregas.Application.Contracts;
using Logistica.Entregas.Application.UseCases;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure.Observability;

namespace Logistica.Entregas.Api;

// DTOs de entrada de la API (versión 1). Los cambios compatibles solo agregan campos opcionales (ADR-001).
public sealed record CreateDeliveryRequest(Guid OrderId, string? Address, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string? Priority);
public sealed record AssignCourierRequest(Guid CourierId);
public sealed record ReassignDeliveryRequest(Guid CourierId, string? Motivo);

/// <summary>
/// Endpoints del contexto Entregas. Solo traducen HTTP a contratos de aplicación y de vuelta:
/// validan el formato, llaman al caso de uso, registran señales y convierten el código de error
/// en un estado HTTP. No contienen reglas de negocio.
/// </summary>
public static class DeliveryEndpoints
{
    public static void MapDeliveryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/deliveries").AddEndpointFilter<ApiKeyFilter>();

        group.MapPost("/", async (CreateDeliveryRequest body, CreateDeliveryUseCase useCase, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (body.OrderId == Guid.Empty) errors["orderId"] = ["Es obligatorio."];
            if (string.IsNullOrWhiteSpace(body.Address) || body.Address.Length > 300) errors["address"] = ["Es obligatoria (máx. 300 caracteres)."];
            if (body.WindowStart is null) errors["windowStart"] = ["Es obligatorio."];
            if (body.WindowEnd is null) errors["windowEnd"] = ["Es obligatorio."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var result = await useCase.ExecuteAsync(new CreateDeliveryCommand(
                body.OrderId, body.Address!, body.WindowStart!.Value, body.WindowEnd!.Value, body.Priority ?? "Normal"), ct);

            return result.Succeeded
                ? Results.Created($"/api/v1/deliveries/{result.DeliveryId}", result)
                : Problem(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, GetDeliveryQuery query, CancellationToken ct) =>
            await query.ExecuteAsync(id, ct) is { } view
                ? Results.Ok(view)
                : Problem(AssignmentResult.Fail(id, ApplicationErrors.DeliveryNotFound, "La entrega no existe.")));

        group.MapPost("/{id:guid}/assignment", async (Guid id, AssignCourierRequest body, HttpContext http,
            AssignCourierUseCase useCase, EntregasTelemetry telemetry, ILogger<AssignCourierUseCase> logger, CancellationToken ct) =>
        {
            if (body.CourierId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["courierId"] = ["Es obligatorio."] });

            return await Observe("asignar", id, telemetry, logger, () =>
                useCase.ExecuteAsync(new AssignCourierCommand(id, body.CourierId, RequestedBy(http)), ct));
        });

        group.MapPost("/{id:guid}/reassignment", async (Guid id, ReassignDeliveryRequest body, HttpContext http,
            ReassignDeliveryUseCase useCase, EntregasTelemetry telemetry, ILogger<ReassignDeliveryUseCase> logger, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (body.CourierId == Guid.Empty) errors["courierId"] = ["Es obligatorio."];
            if (body.Motivo is { Length: > 200 }) errors["motivo"] = ["Máximo 200 caracteres."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            return await Observe("reasignar", id, telemetry, logger, () =>
                useCase.ExecuteAsync(new ReassignDeliveryCommand(id, body.CourierId, body.Motivo ?? string.Empty, RequestedBy(http)), ct));
        });
    }

    /// <summary>Ejecuta el caso de uso dentro de una traza y registra métricas y un log estructurado sin datos personales.</summary>
    private static async Task<IResult> Observe(string operation, Guid deliveryId, EntregasTelemetry telemetry, ILogger logger,
        Func<Task<AssignmentResult>> execute)
    {
        using var activity = EntregasTelemetry.Tracing.StartActivity($"entregas.{operation}");
        activity?.SetTag("entrega.id", deliveryId);
        var watch = Stopwatch.StartNew();

        var result = await execute();

        watch.Stop();
        var outcome = result.Succeeded ? "ok" : "rechazada";
        var code = result.ErrorCode ?? "OK";
        var tags = new TagList { { "operacion", operation }, { "resultado", outcome }, { "codigo", code } };
        telemetry.Assignments.Add(1, tags);
        telemetry.AssignmentDuration.Record(watch.Elapsed.TotalMilliseconds, tags);
        activity?.SetTag("resultado", outcome);
        activity?.SetTag("codigo", code);
        if (!result.Succeeded) activity?.SetStatus(ActivityStatusCode.Error, code);

        if (result.ErrorCode == ApplicationErrors.CourierServiceUnavailable)
            logger.LogWarning("Operación {Operation} de la entrega {DeliveryId}: {Outcome} ({Code}) en {ElapsedMs} ms",
                operation, deliveryId, outcome, code, watch.ElapsedMilliseconds);
        else
            logger.LogInformation("Operación {Operation} de la entrega {DeliveryId}: {Outcome} ({Code}) en {ElapsedMs} ms",
                operation, deliveryId, outcome, code, watch.ElapsedMilliseconds);

        return result.Succeeded ? Results.Ok(result) : Problem(result);
    }

    /// <summary>Traduce el código de error del caso de uso a un estado HTTP con un cuerpo ProblemDetails.</summary>
    public static IResult Problem(AssignmentResult result)
    {
        var status = result.ErrorCode switch
        {
            ApplicationErrors.DeliveryNotFound or ApplicationErrors.CourierNotFound => StatusCodes.Status404NotFound,
            DomainErrors.AlreadyAssigned or DomainErrors.InvalidState or DomainErrors.SameCourier
                or ApplicationErrors.ConcurrencyConflict => StatusCodes.Status409Conflict,
            ApplicationErrors.CourierServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        return Results.Problem(
            title: result.ErrorMessage,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.ErrorCode,
                ["deliveryId"] = result.DeliveryId,
            });
    }

    private static string RequestedBy(HttpContext http)
        => http.Request.Headers.TryGetValue("X-Usuario", out var user) && !string.IsNullOrWhiteSpace(user) ? user.ToString() : "supervisor";
}
