# 5. Criterios verificables

Estos criterios permiten revisar si la decisión del [ADR-001](../adr/ADR-001-monolito-modular.md) responde a las necesidades del sistema. Cada uno indica **qué se verifica, cómo y cuándo se da por cumplido**.

| # | Criterio | Cómo se verifica | Se cumple si… | Escenario |
|---|---|---|---|---|
| CV-01 | La app antigua sigue creando pedidos | Prueba de contrato: se envía a `/api/v1/orders` la misma solicitud de la app vieja (sin `deliveryWindow`) y la de la nueva (con `deliveryWindow`) | Ambas responden `201 Created` y la respuesta conserva `address` y `total` | EC-01 |
| CV-02 | Los cambios incompatibles no tocan la v1 | Revisión del pull request y pruebas de contrato de la v1 en el pipeline | Las pruebas de la v1 pasan sin modificar ninguna | EC-02 |
| CV-03 | Rendimiento al crear pedido | Prueba de carga (p. ej., k6 o JMeter) con 1.000 pedidos/hora | p95 < 2 s y errores < 0,5 % | EC-03 |
| CV-04 | El pico de Ruteo no frena a Pedidos | Prueba de carga combinada: pedidos normales + consultas de ruta ×20 | p95 de crear pedido < 2,2 s y aciertos de caché ≥ 70 % | EC-04 |
| CV-05 | Tolerancia a la caída del proveedor de mapas | Se reemplaza el adaptador de mapas por uno simulado que tarda 5 s o falla | 0 respuestas 5xx; los pedidos quedan en `PENDIENTE_RUTA` | EC-05 |
| CV-06 | Ubicación protegida | Pruebas de autorización: supervisor sin entregas a cargo, acceso vencido, rol incorrecto | Todas responden `403` y aparecen en la tabla de auditoría | EC-06 |
| CV-07 | Reasignación masiva | Simulación de 400 reasignaciones en un ambiente de pruebas | Terminan en < 10 min y el 95 % de las notificaciones sale en < 5 min | EC-07 |
| CV-08 | Sin doble asignación | 50 reasignaciones concurrentes sobre las mismas entregas y una consulta de verificación | 0 entregas activas con más de un repartidor | EC-08 |
| CV-09 | Límites entre módulos respetados | Pruebas de arquitectura (p. ej., NetArchTest) en cada build | Ningún módulo depende del interior de otro; solo de sus contratos públicos | EC-09 |
| CV-10 | Un cambio de regla queda dentro del módulo | Se cambia la "cantidad máxima reservable" y se revisa el diff | 0 archivos modificados fuera de `Modules/Inventory` | EC-09 |
| CV-11 | Se detecta la degradación | Se simula una latencia de 4 s en Ruteo | Llega una alerta en < 5 min con el módulo y la traza | EC-10 |
| CV-12 | La condición de extracción se puede medir | Tablero con el p95 de Ruteo por campaña, la frecuencia de despliegue y los incidentes | Después de cada campaña se puede responder "sí/no" a las 3 señales del ADR-001 | ADR §7 |

## Preguntas de revisión rápida

Estas preguntas vienen del video 06. Si un compañero las puede responder mirando solo el diagrama y los contratos, **sin abrir una tabla compartida ni una clase gigante**, los límites están bien definidos:

1. ¿Qué significa **"disponible"** en Inventario, en Ruteo y en Entregas?
2. ¿Qué regla protege cada módulo?
3. ¿Qué contratos usa Pedidos para confirmar un pedido?
4. Si mañana Ruteo pasa a ser un servicio aparte, ¿qué archivos cambian? *(Respuesta esperada: solo el adaptador de `IRouteEstimator`.)*
