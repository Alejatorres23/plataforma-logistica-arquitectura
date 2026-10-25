# Guion para el video de sustentación (8–10 min)

Basado en el guion que propone el docente (videos 04.1 y 08.1) y en el punto **"Qué debe explicar tu video"** de la guía. Sugerencia: muestra el README y los documentos en pantalla mientras hablas. **Dilo con tus palabras**, no lo leas.

| Min. | Qué decir | Qué mostrar |
|---|---|---|
| 0:00–1:00 | **El problema y por qué importa.** La app móvil necesita crear pedidos aunque agreguemos una ventana de entrega; las campañas multiplican ×20 las rutas; en la tormenta hubo que reasignar 400 entregas y pidieron el GPS de todos. | README, sección 1 |
| 1:00–2:00 | **Actores y alcance.** Cliente, supervisor, repartidor, soporte y equipo técnico. Qué está dentro y qué fuera (proveedor de mapas, identidad, red móvil). | `01-catalogo-requisitos.md` §1.3–1.4 |
| 2:00–3:30 | **Cómo prioricé.** Califiqué negocio × arquitectura de 1 a 5, con la complejidad aparte. Los 25 puntos: ventana de entrega sin romper clientes, confirmar con stock y ruta, picos de ruteo. | `02-matriz-priorizacion.md` |
| 3:30–4:30 | **Escenarios de calidad.** Explica 2 o 3 con su medida: EC-01 (100 % de solicitudes v1 aceptadas), EC-04 (p95 < 2,2 s en el pico), EC-06 (100 % de accesos auditados). | `03-escenarios-calidad.md` |
| 4:30–6:30 | **Alternativas comparadas.** Monolito modular, microservicios y monolito por capas. Criterios decisivos: costo, riesgo operativo y alineación con el negocio. Microservicios solo gana en escalabilidad, y el pico está en **un solo** módulo. | `04-comparacion-alternativas.md` (tabla 4.2) |
| 6:30–8:00 | **Decisión y costo aceptado (trade-off).** Monolito modular con contratos internos, API versionada (`deliveryWindow` opcional, v2 para lo incompatible), caché y cola en Ruteo, ubicación temporal y auditada. Aceptamos un solo despliegue y escalar todo junto. | ADR-001 §2 y §5, diagrama del README |
| 8:00–9:00 | **Por qué reduce riesgo y costo.** Una base de datos, sin consistencia eventual y sin factura de 30 servicios. Ruteo se puede extraer después cambiando solo un adaptador. | ADR-001 §4 |
| 9:00–10:00 | **Evidencia y condición de revisión.** Cómo lo compruebo (pruebas de contrato, de carga y de arquitectura) y cuándo extraería Ruteo (3 señales durante 2 campañas). | `05-criterios-verificables.md`, ADR-001 §7 |

## Frases clave para recordar

- "La decisión no es la mejor arquitectura; es la más apropiada para este caso."
- "Un límite de dominio no obliga a un despliegue independiente."
- "Agregar un campo opcional no rompe a nadie; cambiar el significado de uno exige una versión nueva."
- "No vamos a matar un mosquito con una bazuca: extraemos Ruteo cuando la métrica lo pida, no por moda."
