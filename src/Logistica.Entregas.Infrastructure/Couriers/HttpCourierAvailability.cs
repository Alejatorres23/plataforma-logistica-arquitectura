using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Logistica.Entregas.Application.Ports;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace Logistica.Entregas.Infrastructure.Couriers;

/// <summary>Configuración del cliente del servicio de turnos.</summary>
public sealed class CourierServiceOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5080/simulador/";
    /// <summary>Tiempo máximo de espera por intento (escenario EC-05: 2 s).</summary>
    public int TimeoutMilliseconds { get; set; } = 2000;
    /// <summary>Reintentos ante fallas transitorias (5xx, 408, 429, red, timeout).</summary>
    public int MaxRetries { get; set; } = 2;
    public int BaseDelayMilliseconds { get; set; } = 200;
}

/// <summary>
/// Adaptador HTTP del puerto <see cref="ICourierAvailability"/> hacia el servicio de turnos de repartidores.
/// Es la capa anticorrupción de la Actividad 3: del modelo externo solo toma activo, carga y máximo
/// (ignora cualquier otro dato, como la ubicación). Aplica timeout por intento y reintentos con espera
/// creciente; si el servicio no responde, lo informa como <see cref="CourierAvailabilityUnavailableException"/>.
/// </summary>
public sealed class HttpCourierAvailability(
    HttpClient http,
    CourierServiceOptions options,
    EntregasTelemetry telemetry,
    ILogger<HttpCourierAvailability> logger) : ICourierAvailability
{
    // Solo los campos que la regla necesita: el resto del modelo externo (ubicación, nombre...) se ignora.
    private sealed record CourierShiftDto(bool Activo, int EntregasActivas, int MaximoEntregas);

    public async Task<CourierCapacity?> GetCapacityAsync(CourierId courierId, CancellationToken ct = default)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(options.TimeoutMilliseconds);
            try
            {
                using var response = await http.GetAsync($"turnos/{courierId.Value}", timeout.Token);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var dto = await response.Content.ReadFromJsonAsync<CourierShiftDto>(timeout.Token)
                                  ?? throw new CourierAvailabilityUnavailableException("El servicio de turnos devolvió una respuesta vacía.");
                        return new CourierCapacity(courierId, dto.Activo, dto.EntregasActivas, dto.MaximoEntregas);
                    }
                    catch (Exception e) when (e is JsonException or NotSupportedException or DomainException)
                    {
                        // Una respuesta mal formada no debe convertirse en un error 500 de nuestra API.
                        throw new CourierAvailabilityUnavailableException("El servicio de turnos devolvió datos inválidos.", e);
                    }
                }

                if (!IsTransient(response.StatusCode))
                    throw new CourierAvailabilityUnavailableException(
                        $"El servicio de turnos rechazó la consulta ({(int)response.StatusCode}).");

                lastError = new HttpRequestException($"Respuesta {(int)response.StatusCode} del servicio de turnos.");
            }
            catch (HttpRequestException e)
            {
                lastError = e;
            }
            catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
            {
                lastError = new TimeoutException($"El servicio de turnos no respondió en {options.TimeoutMilliseconds} ms.", e);
            }

            telemetry.CourierServiceFailures.Add(1);
            logger.LogWarning("Falla transitoria consultando el servicio de turnos (intento {Attempt} de {Total}): {Error}",
                attempt + 1, options.MaxRetries + 1, lastError.Message);

            if (attempt < options.MaxRetries)
                await Task.Delay(options.BaseDelayMilliseconds * (1 << attempt), ct);
        }

        throw new CourierAvailabilityUnavailableException(
            "El servicio de turnos de repartidores no está disponible. Intente de nuevo en unos minutos.", lastError);
    }

    private static bool IsTransient(HttpStatusCode code)
        => (int)code >= 500 || code is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
