using Logistica.Entregas.Api;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Application.UseCases;
using Logistica.Entregas.Infrastructure;
using Logistica.Entregas.Infrastructure.Couriers;
using Logistica.Entregas.Infrastructure.Messaging;
using Logistica.Entregas.Infrastructure.Observability;
using Logistica.Entregas.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------- Composición de dependencias (el único lugar que conoce todas las capas) ----------
var config = builder.Configuration;

builder.Services.AddSingleton<EntregasTelemetry>();
builder.Services.AddSingleton<IClock, SystemClock>();

// Persistencia: SQLite con migraciones versionadas (o memoria para una demo sin archivo).
var connectionString = config.GetConnectionString("Entregas") ?? "Data Source=entregas.db";
builder.Services.AddSingleton(new SqliteConnectionFactory(connectionString));
builder.Services.AddSingleton<SqliteMigrator>();
builder.Services.AddScoped<IDeliveryRepository, SqliteDeliveryRepository>();

// Integración con el servicio de turnos de repartidores (HTTP con timeout y reintentos).
var courierOptions = config.GetSection("ServicioTurnos").Get<CourierServiceOptions>() ?? new CourierServiceOptions();
builder.Services.AddSingleton(courierOptions);
builder.Services.AddHttpClient<ICourierAvailability, HttpCourierAvailability>(client =>
{
    client.BaseAddress = new Uri(courierOptions.BaseUrl);
    client.Timeout = Timeout.InfiniteTimeSpan; // el timeout lo controla el adaptador por intento
});

// Integración asíncrona: outbox + despachador con reintentos y cola de mensajes fallidos.
builder.Services.AddSingleton(config.GetSection("Outbox").Get<OutboxOptions>() ?? new OutboxOptions());
builder.Services.AddSingleton<SimulatedNotificationBus>();
builder.Services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<SimulatedNotificationBus>());
builder.Services.AddSingleton<OutboxDispatcher>();
if (config.GetValue("Outbox:Habilitado", true))
    builder.Services.AddHostedService<OutboxBackgroundService>();

// Casos de uso.
builder.Services.AddScoped<CreateDeliveryUseCase>();
builder.Services.AddScoped<AssignCourierUseCase>();
builder.Services.AddScoped<ReassignDeliveryUseCase>();
builder.Services.AddScoped<GetDeliveryQuery>();

// Simulador local del servicio de turnos (solo para desarrollo y la demostración).
builder.Services.AddSingleton(SimulatorState.CreateSeeded());

// Todas las respuestas de error (ProblemDetails) llevan el identificador de correlación.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.Items[CorrelationIdMiddleware.ItemKey]);

var app = builder.Build();

// ---------- Migraciones reproducibles al iniciar ----------
if (config.GetValue("Persistencia:MigrarAlIniciar", true))
{
    var applied = await app.Services.GetRequiredService<SqliteMigrator>().MigrateAsync();
    app.Logger.LogInformation("Migraciones aplicadas al iniciar: {Count}", applied);
}

// ---------- Tubería HTTP ----------
// Errores inesperados -> ProblemDetails sin detalles internos. Un cuerpo JSON mal formado es culpa
// de quien llama: se responde 400, no 500.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = e => e is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthEndpoint();
app.MapDeliveryEndpoints();
if (config.GetValue("Simulador:Habilitado", false))
    app.MapSimulatorEndpoints();

app.Run();

/// <summary>Visible para las pruebas de integración (WebApplicationFactory).</summary>
public partial class Program;
