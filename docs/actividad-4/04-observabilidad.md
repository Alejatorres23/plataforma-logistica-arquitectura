# 4. Observabilidad

**Objetivo:** que alguien de operación pueda responder **"¿por qué falló esta asignación?"** sin depurar el código y **sin ver datos personales**.

## Señales implementadas

### 1. Logs estructurados con correlación

- Cada solicitud recibe un **`X-Correlation-Id`**. Se usa el del cliente si llega, y si no se genera uno. Viaja en:
  - el *scope* de todos los logs de esa solicitud;
  - la cabecera de la respuesta;
  - el cuerpo de los errores (`correlationId`).
- **Por cada asignación o reasignación** hay un log con: operación, id de entrega, resultado, código y duración. Ejemplo real de la demostración ([evidencia completa](evidencia/demo-api.txt)):

  ```
  19:45:54 warn: Logistica.Entregas.Infrastructure.Couriers.HttpCourierAvailability[0]
        => /api/v1/deliveries/43e9ec29-…/assignment => CorrelationId:demo-1561
        Falla transitoria consultando el servicio de turnos (intento 1 de 3): El servicio de turnos no respondió en 2000 ms.
  19:45:56 info: Logistica.Entregas.Application.UseCases.AssignCourierUseCase[0]
        => /api/v1/deliveries/43e9ec29-…/assignment => CorrelationId:demo-1561
        Operación asignar de la entrega 43e9ec29-…: rechazada (REPARTIDOR_SATURADO) en 5307 ms
  ```

  **Lo que dijo este log.** En la primera llamada de la demostración, el servicio de turnos tardó más de 2 s: es el arranque en frío del simulador, que vive en el mismo proceso. El adaptador reintentó solo y la operación terminó bien en el segundo intento, en un total de 5,3 s. Las siguientes asignaciones tardaron entre 200 y 800 ms. Es justo el tipo de diagnóstico que estas señales permiten hacer sin abrir el código.

- **Fallas de integración:**
  - `warn` por cada intento fallido contra el servicio de turnos (con el número de intento);
  - `warn` por cada publicación fallida del outbox;
  - `error` cuando un mensaje va a la cola de mensajes fallidos.

### 2. Métricas

Las métricas usan `System.Diagnostics.Metrics`, con el *meter* `Logistica.Entregas` (compatible con OpenTelemetry).

| Métrica | Tipo | Etiquetas | Para qué sirve |
|---|---|---|---|
| `entregas.asignaciones` | Contador | `operacion`, `resultado`, `codigo` | Ver cuántas asignaciones se rechazan y por qué (p. ej., un pico de `REPARTIDOR_SATURADO` en una campaña) |
| `entregas.asignacion.duracion` | Histograma (ms) | `operacion`, `resultado`, `codigo` | Comprobar el escenario de rendimiento (p95 < 2 s) |
| `entregas.servicio_repartidores.fallos` | Contador | — | Detectar que el servicio de turnos está degradado |
| `entregas.outbox.publicados` | Contador | `evento` | Confirmar que los avisos salen |
| `entregas.outbox.dlq` | Contador | `evento` | **Alerta**: hay mensajes que necesitan revisión manual |

Verlas en vivo, con la API corriendo:

```bash
dotnet tool install -g dotnet-counters
dotnet-counters monitor -n Logistica.Entregas.Api --counters Logistica.Entregas
```

### 3. Trazas

Cada asignación crea una actividad (`ActivitySource` `Logistica.Entregas`) llamada `entregas.asignar` o `entregas.reasignar`, con las etiquetas `entrega.id`, `resultado`, `codigo` y `correlation.id`. El estado es `Error` si la operación fue rechazada. Cualquier exportador de OpenTelemetry (Jaeger, Azure Monitor, etc.) las recoge **sin cambiar el código del flujo**.

### 4. Salud

`GET /health` devuelve:
- `baseDeDatos: ok|error`;
- `outboxPendientes`;
- `mensajesFallidos`;
- `estado`: `ok`, o `degradado` si hay mensajes en la DLQ, o `503 caido` si no hay base de datos.

## Qué información se excluye a propósito

| Dato | ¿Aparece en logs, métricas, trazas o eventos? | Por qué |
|---|---|---|
| Dirección de entrega | ❌ No | Dato personal del cliente; no ayuda a diagnosticar |
| Nombre de quien recibe (evidencia) | ❌ No | Dato personal |
| **Ubicación GPS del repartidor** | ❌ No: ni siquiera entra al sistema | Privacidad (Actividad 1, caso de la tormenta) |
| Clave de API | ❌ No | Secreto |
| Ids de entrega, repartidor y pedido | ✅ Sí | Son opacos (GUID) y permiten seguir el caso |
| Códigos de error y duración | ✅ Sí | Son la información que diagnostica |

## Cómo se investiga una falla (ejemplo)

1. Soporte reporta que el supervisor no pudo asignar la entrega X. El supervisor tiene el `correlationId` del error que vio en pantalla.
2. Se buscan los logs con ese `CorrelationId`. Aparece `rechazada (SERVICIO_REPARTIDORES_NO_DISPONIBLE)` y, antes, tres `warn` de "Falla transitoria consultando el servicio de turnos (intento n de 3)".
3. La métrica `entregas.servicio_repartidores.fallos` muestra un pico a esa hora.
4. **Conclusión:** falló el sistema externo y no el flujo. La entrega quedó sin cambios, como se esperaba, y se puede reintentar.

> **Hipótesis de resiliencia comprobada de forma controlada (video 28, ingeniería del caos a pequeña escala).** Con `POST /simulador/fallas` se apaga el servicio de turnos o el de notificaciones mientras la API está en marcha. Las pruebas automáticas y la demostración (`scripts/demo.ps1`, paso 7) verifican el comportamiento esperado: `503` sin cambios en la entrega, y reintentos más DLQ en el outbox.
