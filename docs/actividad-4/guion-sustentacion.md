# Guion de la demostración y defensa final – Actividad 4 (12–15 min)

**Antes de grabar:**
- Abre dos terminales en la carpeta del repositorio.
- En la primera, ejecuta `dotnet run --project src/Logistica.Entregas.Api` y espera el mensaje "Now listening on http://localhost:5080".
- En la segunda, ten listos `./scripts/demo.ps1` y `dotnet test LogisticaUltimaMilla.slnx`.

| Min. | Qué decir | Qué mostrar |
|---|---|---|
| 0:00–1:30 | **Problema y decisión** (recorrido rápido por el expediente): falta de una vista centralizada (Act. 1), monolito modular (Act. 2), dominio Entregas con la invariante "un solo repartidor" (Act. 3) | README (tabla de actividades), ARCHITECTURE.md (C4) |
| 1:30–3:00 | **Modelo de dominio:** `Delivery` protege I1–I7; los estados | `Delivery.cs` (`AssignCourier`), diagrama de estados |
| 3:00–6:00 | **Demostración en vivo:** ejecutar `./scripts/demo.ps1` y comentar cada paso: crear (201), saturado (422), asignar (200), segunda asignación (409), tormenta (200), consulta sin dirección, servicio de turnos caído (503), sin clave (401). Mostrar los logs en la otra terminal con el `CorrelationId` | Las dos terminales |
| 6:00–7:30 | **Recorrido por las capas** de una solicitud: endpoint → caso de uso → `Delivery` → repositorio SQLite → outbox. El endpoint no tiene reglas; el caso de uso coordina; la entidad decide | `DeliveryEndpoints.cs`, `AssignCourierUseCase.cs`, `SqliteDeliveryRepository.cs` |
| 7:30–9:00 | **Persistencia e integraciones, y por qué:** SQLite + migraciones estilo Flyway (reproducible); versión optimista; outbox + DLQ; adaptador HTTP con timeout, reintentos y capa anticorrupción (descarta la ubicación) | `V1__crear_entregas.sql`, ADR-003 |
| 9:00–10:30 | **Pruebas:** ejecutar `dotnet test` en vivo y mostrar el total en verde. Señalar un caso de error (servicio caído → 503) y un límite (conflicto de concurrencia → la segunda operación falla) | Terminal, doc. 02 |
| 10:30–12:00 | **Riesgos y observabilidad:** pre-mortem → tabla riesgo / mitigación / evidencia; logs con correlación, métricas, `/health`; qué datos se excluyen. Contar el hallazgo real del SCA (vulnerabilidad alta corregida) | docs 03 y 04, `/health` en el navegador |
| 12:00–13:30 | **Comprobación automática:** el CI en GitHub Actions y las 6 fitness functions | `.github/workflows/ci.yml`, pestaña Actions |
| 13:30–15:00 | **Defensa:** trade-offs (SQLite vs. producción, "al menos una vez", clave de API), riesgos residuales y evolución futura (PostgreSQL, JWT, OpenTelemetry, extraer Ruteo si se cumplen las 3 señales) | doc. 05, ADR-003 |

## Preguntas probables y respuesta corta

- **¿Por qué SQLite?** Porque cualquiera puede ejecutar la entrega sin instalar nada. Como está detrás de un puerto, pasar a PostgreSQL es cambiar un adaptador.
- **¿Qué pasa si el servicio de turnos se cae?** Tres intentos de 2 s; después, `503` con un código claro. La entrega no cambia.
- **¿Y si se cae Notificaciones?** La asignación se guarda igual. El evento queda en el outbox y se reintenta; después de 5 fallas va a la DLQ y `/health` lo muestra.
- **¿Cómo evitas la doble asignación?** Con tres capas de protección: la entidad (I1), la versión optimista y el índice único en la base de datos.
- **¿Dónde está la regla crítica?** En `Delivery.AssignCourier`, nunca en el endpoint ni en la base de datos.
