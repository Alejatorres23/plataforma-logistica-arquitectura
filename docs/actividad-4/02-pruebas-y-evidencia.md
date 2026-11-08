# 2. Pruebas y evidencia de ejecución

## Cómo ejecutarlas

```bash
dotnet test LogisticaUltimaMilla.slnx
```

**Resultado: 60 pruebas, 60 correctas, 0 con error.** La lista completa de la última ejecución está en [`evidencia/resultado-pruebas.txt`](evidencia/resultado-pruebas.txt). El CI la publica como artefacto `.trx` en cada push.

## Suite de pruebas

| Grupo | Archivo | Cantidad | Tipo | Qué demuestra |
|---|---|:-:|---|---|
| **Dominio** | `Domain/DeliveryTests.cs` | 16 | Unitarias | Las 7 invariantes (I1–I7), las transiciones de estado y la reconstrucción desde la base de datos (`Restore`) |
| **Casos de uso** | `Application/AssignCourierUseCaseTests.cs` | 11 | Unitarias con adaptadores en memoria | Los escenarios del `.feature`, el servicio de turnos caído y la prioridad inválida |
| **Persistencia** | `Infrastructure/SqlitePersistenceTests.cs` | 6 | Integración con SQLite real | Migraciones reproducibles, detección de una migración alterada, ida y vuelta del agregado, **conflicto de concurrencia** y el índice único como segunda defensa |
| **Servicio de turnos** | `Infrastructure/IntegrationAdapterTests.cs` | 7 | Integración con un servidor HTTP falso | Capa anticorrupción (descarta la ubicación), 404 → null, **reintento y recuperación**, **timeout agotado**, 4xx no se reintenta, JSON mal formado, datos inválidos |
| **Outbox** | `Infrastructure/IntegrationAdapterTests.cs` | 3 | Integración con SQLite real | Publicación única, recuperación después de una falla temporal y **paso a la DLQ** |
| **API (vertical slice)** | `Api/DeliveryApiTests.cs` | 11 | Integración HTTP completa (`WebApplicationFactory` + SQLite real) | El flujo crear → asignar → consultar y todos los códigos HTTP (200/400/401/404/409/422/503), la correlación y la salud |
| **Arquitectura** | `Architecture/DependencyRulesTests.cs` | 6 | Fitness functions | La dirección de las dependencias y que el dominio y la aplicación no usen tecnologías de infraestructura |
| | | **60** | | |

## Casos de error y límite comprobados (requisito 6 de la guía)

| Caso | Tipo | Respuesta esperada | Prueba |
|---|---|---|---|
| Asignar una entrega que ya tiene repartidor | Invariante I1 | `409 ENTREGA_YA_ASIGNADA` | `SegundaAsignacion_Devuelve409ConElCodigoDeLaInvariante` |
| Repartidor saturado (5 de 5) | Límite de capacidad (I3) | `422 REPARTIDOR_SATURADO` | `RepartidorSaturado_Devuelve422` |
| Dos supervisores asignan a la vez | Límite de concurrencia | El segundo recibe un conflicto; queda una sola asignación activa y un solo evento | `Repositorio_DosOperacionesSimultaneas_LaSegundaRecibeConflicto` |
| El servicio de turnos no responde | Error de integración | 3 intentos de 200 ms (en la prueba) y después `503`; la entrega no cambia | `ServicioQueNoResponde_...`, `ServicioDeTurnosCaido_Devuelve503...` |
| El servicio de turnos manda JSON roto | Error de integración | Se trata como "no disponible", **no como un 500** | `JsonMalFormado_FallaDeFormaControlada_NoComoError500` |
| El cliente manda JSON roto | Entrada inválida | `400`, **no 500** | `JsonMalFormado_Devuelve400YNo500` |
| Notificaciones caído | Error de integración asíncrona | Reintentos y después la DLQ; no bloquea | `NotificacionesCaido_SeReintentaYLuegoVaALaColaDeFallidos` |
| Solicitud sin clave | Seguridad | `401` | `SinClaveDeApi_Devuelve401` |
| Campos inválidos | Validación de entrada | `400` con el campo que falla | `SolicitudInvalida_Devuelve400ConElCampoQueFalla` |

## Defectos reales que encontraron las pruebas durante la construcción

Las pruebas y la demostración no se hicieron solo para pasar. Durante la Actividad 4 detectaron **cuatro defectos reales**, que se corrigieron antes de entregar:

1. **JSON mal formado del servicio de turnos → error 500.** La prueba del adaptador falló (`JsonException` sin capturar). Se corrigió convirtiéndolo en `CourierAvailabilityUnavailableException` (→ `503`), y se quitó del DTO un campo que la capa anticorrupción no necesitaba.
2. **JSON mal formado del cliente → error 500.** Se descubrió ejecutando la demostración real contra la API: Windows PowerShell 5.1 envía las fechas en un formato no estándar. Se corrigió el manejador de excepciones para responder `400` y se agregó la prueba `JsonMalFormado_Devuelve400YNo500`.
3. **Vulnerabilidad alta en una dependencia** (GHSA-2m69-gcr7-jv3q en `SQLitePCLRaw 2.1.11`). La detectó el análisis de dependencias al compilar y se corrigió fijando la versión 2.1.14.
4. **El id de correlación no se veía en los logs.** Al revisar la evidencia de la demostración aparecía `Dictionary'2[...]` en lugar del valor, porque el formateador de consola no sabe mostrar ese tipo de *scope*. Se cambió a un *scope* con plantilla (`"CorrelationId:{CorrelationId}"`), y ahora cada línea muestra `CorrelationId:demo-1561`.

## Evidencia de la demostración de punta a punta

Salida real de `scripts/demo.ps1` contra la API en ejecución: [`evidencia/demo-api.txt`](evidencia/demo-api.txt).
