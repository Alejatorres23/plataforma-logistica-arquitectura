using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure;
using Logistica.Entregas.Infrastructure.Messaging;

namespace Logistica.Entregas.Api;

/// <summary>
/// Estado del simulador local del servicio de turnos de repartidores. Reemplaza al sistema externo
/// real en desarrollo y en la demostración; se apaga con Simulador:Habilitado = false.
/// </summary>
public sealed class SimulatorState
{
    public InMemoryCourierAvailability Couriers { get; } = new();
    public bool CourierServiceDown { get; set; }

    // Identificadores fijos para que la demostración sea repetible.
    public static readonly Guid Ana = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Luis = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Carlos = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Pedro = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static SimulatorState CreateSeeded()
    {
        var state = new SimulatorState();
        state.Couriers.Register(new CourierCapacity(new CourierId(Ana), isActive: true, activeDeliveries: 2, maxDeliveries: 5));
        state.Couriers.Register(new CourierCapacity(new CourierId(Luis), isActive: true, activeDeliveries: 1, maxDeliveries: 5));
        state.Couriers.Register(new CourierCapacity(new CourierId(Carlos), isActive: true, activeDeliveries: 5, maxDeliveries: 5)); // saturado
        state.Couriers.Register(new CourierCapacity(new CourierId(Pedro), isActive: false, activeDeliveries: 0, maxDeliveries: 5)); // fuera de turno
        return state;
    }
}

public sealed record SimulatedFailuresRequest(bool? ServicioTurnos, bool? Notificaciones);

public static class SimulatorEndpoints
{
    public static void MapSimulatorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/simulador");

        // Contrato del servicio externo de turnos (lo que consume HttpCourierAvailability).
        group.MapGet("/turnos/{courierId:guid}", async (Guid courierId, SimulatorState state) =>
        {
            if (state.CourierServiceDown)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var capacity = await state.Couriers.GetCapacityAsync(new CourierId(courierId));
            return capacity is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    courierId,
                    activo = capacity.IsActive,
                    entregasActivas = capacity.ActiveDeliveries,
                    maximoEntregas = capacity.MaxDeliveries,
                    // El sistema externo también expone la ubicación; la capa anticorrupción la ignora.
                    ubicacion = new { lat = 6.2442, lon = -75.5812 },
                });
        });

        group.MapGet("/repartidores", (SimulatorState state) => Results.Ok(new
        {
            ana = SimulatorState.Ana,
            luis = SimulatorState.Luis,
            carlosSaturado = SimulatorState.Carlos,
            pedroFueraDeTurno = SimulatorState.Pedro,
        }));

        // Activa o desactiva fallas para demostrar el manejo de errores.
        group.MapPost("/fallas", (SimulatedFailuresRequest body, SimulatorState state, SimulatedNotificationBus bus) =>
        {
            if (body.ServicioTurnos is { } turnos) state.CourierServiceDown = turnos;
            if (body.Notificaciones is { } notificaciones) bus.SimulateOutage = notificaciones;
            return Results.Ok(new { servicioTurnosCaido = state.CourierServiceDown, notificacionesCaidas = bus.SimulateOutage });
        });
    }
}
