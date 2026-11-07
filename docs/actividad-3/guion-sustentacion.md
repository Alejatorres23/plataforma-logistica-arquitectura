# Guion para el video de sustentación – Actividad 3 (8–10 min)

Cubre los puntos que pide la guía: qué es el dominio, las capas, dónde están las reglas, cómo se evita el acoplamiento, las decisiones clave, los puertos y adaptadores, y los escenarios.

| Min. | Qué decir | Qué mostrar |
|---|---|---|
| 0:00–1:00 | Flujo elegido (asignar una entrega), actores, propósito y resultado esperado. Por qué este flujo (RF-06, RF-07, la tormenta) | `01-flujo-asignar-entrega.md` |
| 1:00–2:00 | **Qué es el dominio** y el lenguaje ubicuo. "Disponible" significa algo distinto en cada contexto. Términos por confirmar | `02-lenguaje-ubicuo.md` |
| 2:00–3:15 | Entidad `Delivery` (agregado) vs. objetos de valor. Diagrama de estados | `03-modelo-dominio.md` |
| 3:15–4:30 | **Dónde están las reglas críticas:** las invariantes I1–I7 dentro de `Delivery`. Mostrar el código de `AssignCourier` | `04-invariantes-y-escenarios.md`, `Delivery.cs` |
| 4:30–5:30 | Contextos y contratos: mapa de contextos, evento `PedidoConfirmado`, capa anticorrupción hacia la disponibilidad de repartidores (sin ubicación) | `05-contextos-y-contratos.md` |
| 5:30–7:00 | **Capas, puertos y adaptadores:** tres capas y por qué; diagrama de secuencia; el caso de uso coordina y la entidad decide; adaptadores en memoria hoy y reales en la Actividad 4 | `06-casos-uso-puertos-adaptadores.md`, `AssignCourierUseCase.cs` |
| 7:00–8:00 | **Decisiones clave y trade-offs** (ADR-002): dominio rico, la foto de capacidad, resultados con código, alternativas descartadas | `ADR-002` |
| 8:00–9:30 | **Escenarios que comprueban las invariantes:** el `.feature` y **ejecutar `dotnet test` en vivo** (26 correctas). Las pruebas de arquitectura comprueban la dirección de las dependencias | `asignar-entrega.feature`, terminal |
| 9:30–10:00 | Architecture.md y C4: dónde encaja Entregas en el sistema; qué queda para la Actividad 4 | `ARCHITECTURE.md` |
