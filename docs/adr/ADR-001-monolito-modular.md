# ADR-001 · Monolito modular con límites de contexto y API versionada

| Campo | Valor |
|---|---|
| **Estado** | Aceptada |
| **Fecha** | 2026-10-24 |
| **Actividad** | Actividad 2 – Requisitos y decisión estructural |
| **Requisitos que la motivan** | RF-02, RF-03, RF-07, RF-09, RNF-01, RNF-03, RNF-05, RNF-08, RNF-11 |
| **Reemplaza a** | — (primera decisión estructural) |

## 1. Contexto

La plataforma logística recibe pedidos desde una **app móvil** y una **web**. Después reserva inventario, estima rutas y asigna entregas a repartidores. Hay cuatro situaciones que condicionan la estructura:

1. **El contrato no se puede romper.** El negocio quiere agregar una **ventana de entrega**. La app móvil publicada tarda días en actualizarse (revisión de las tiendas). Si la API cambia de forma incompatible, los clientes dejan de crear pedidos aunque el servidor funcione.
2. **Hay picos de campaña concentrados en Ruteo.** En una campaña de entregas en menos de 2 horas, las consultas de ruta se multiplican **×20**. Pedidos e Inventario siguen en su volumen normal, pero el cálculo de rutas consume CPU y retrasa las confirmaciones.
3. **La contingencia choca con la privacidad.** En una tormenta hay que reasignar **400 entregas**. Operaciones pide ver la ubicación GPS de los repartidores **sin restricción** y para **todos** los supervisores.
4. **El equipo es pequeño y el presupuesto, limitado.** Se necesita un MVP pronto.

## 2. Decisión

**Construiremos un monolito modular** con **cuatro contextos de negocio** (Pedidos, Inventario, Ruteo, Entregas) y módulos de soporte (Notificaciones, Auditoría, Identidad). Se aplican estas reglas:

1. **Límites por contexto.** Cada módulo tiene su modelo, su vocabulario y su esquema de base de datos. **Ningún módulo lee las tablas de otro.** Se comunican solo por **contratos internos** (`IInventoryAvailability`, `IRouteEstimator`, `IDeliveryAssignment`).
2. **Puertos y adaptadores** para todo lo externo: mapas/GPS, identidad y notificaciones. Las reglas de negocio no conocen a ningún proveedor concreto.
3. **API pública versionada** (`/api/v1/orders`):
   - Los cambios compatibles **solo agregan campos opcionales**. `deliveryWindow` entra como **opcional**: si no llega, se aplica la ventana por defecto.
   - Los cambios incompatibles (renombrar `total` a `amount`, convertir `address` en objeto) se publican en `/api/v2` y la v1 se mantiene **al menos 90 días**.
   - Los controladores **no contienen reglas de negocio**: reciben HTTP, validan el formato y llaman al caso de uso.
4. **Ruteo preparado para los picos sin separarlo todavía.**
   - **Caché** de estimaciones con vigencia.
   - **Cola de trabajo** para las estimaciones que no están en caché.
   - Timeout de 2 s, 2 reintentos y circuito abierto hacia el proveedor de mapas.
   - Si no hay ruta, el pedido se crea en estado `PENDIENTE_RUTA`.
   - La aplicación se **escala horizontalmente**.
5. **Privacidad de la ubicación por diseño.** La ubicación del repartidor se consulta solo así:
   - con **rol autorizado**;
   - para **entregas a cargo** del supervisor;
   - con acceso **temporal** (máximo 2 horas);
   - con **auditoría** de cada consulta.

   La reasignación masiva usa el mismo módulo Entregas, con procesamiento por lotes y una restricción única para evitar la doble asignación.
6. **No crearemos microservicios todavía.** Los límites de dominio **no obligan** a tener despliegues independientes.

## 3. Opciones consideradas

| Opción | Puntaje (doc. 4) | Resumen |
|---|:-:|---|
| **A. Monolito modular** ✅ | **4,20** | Un despliegue, límites claros, contratos internos, caché y cola para Ruteo |
| B. Microservicios por dominio | 2,45 | Escalado independiente, pero alto costo y complejidad para un equipo pequeño |
| C. Monolito por capas sin módulos | 3,35 | Rápido hoy, pero acoplado (`OrderService` que lo hace todo) |

El detalle de la comparación está en [04-comparacion-alternativas.md](../actividad-2/04-comparacion-alternativas.md).

## 4. Justificación (técnica y de negocio)

- **Resuelve el problema real con el menor costo.** El cuello de botella está en **un solo contexto** (Ruteo). Separar los cuatro contextos agregaría llamadas remotas, fallas parciales y coordinación **sin atacar ese cuello de botella**. La caché y la cola sí lo atacan.
- **Protege el ingreso del negocio.** Gracias a la API versionada y los cambios aditivos, la app móvil vieja **sigue creando pedidos** mientras la nueva se aprueba en las tiendas.
- **Reduce el riesgo operativo.** Con un despliegue y una base de datos no hay consistencia eventual entre Pedidos e Inventario: confirmar "con stock y ruta" (RF-03) es una transacción local.
- **Deja la puerta abierta.** Como Ruteo ya está detrás de `IRouteEstimator`, extraerlo mañana a un servicio HTTP solo cambia el **adaptador**. El caso de uso de Pedidos no se entera.
- **Responde al riesgo legal y reputacional.** La ubicación del repartidor es un dato personal (Ley 1581 de 2012). Darla "a todos los supervisores sin restricción" incumple el principio de finalidad y de mínimo privilegio. El acceso temporal y auditado atiende la contingencia **sin** ese riesgo.

## 5. Trade-offs (lo que ganamos y lo que aceptamos pagar)

| Ganamos | Aceptamos |
|---|---|
| Un solo despliegue, menos infraestructura y menos fallas de red | Un cambio en cualquier módulo **redespliega toda la aplicación** |
| Transacciones locales y consistencia fuerte | Ruteo **no escala por separado**: escalamos toda la app aunque solo Ruteo esté cargado |
| Velocidad para construir el MVP | **Disciplina obligatoria**: si alguien salta un límite (lee tablas ajenas), el monolito se vuelve una "bola de barro" |
| Contratos internos que permiten extraer módulos después | Mantener **más interfaces y contratos**, y la composición de dependencias |
| API v1 estable para la app móvil | Mantener **dos versiones** de la API durante la ventana de transición, con sus pruebas de contrato |
| Privacidad de la ubicación por diseño | El supervisor tiene **un paso extra** (solicitar el acceso temporal) durante la contingencia |

## 6. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|:-:|:-:|---|
| Se rompen los límites entre módulos con el tiempo | Media | Alto | **Pruebas de arquitectura** automáticas (p. ej., NetArchTest en .NET) que fallan si un módulo referencia el interior de otro, y revisión en los pull requests |
| La caché y la cola no alcanzan en una campaña más grande | Media | Alto | Métricas de Ruteo y la condición de revisión de la sección 7 |
| El proveedor de mapas falla | Alta | Medio | Timeout, reintentos, circuito abierto y estado `PENDIENTE_RUTA` (EC-05) |
| Doble asignación durante la reasignación masiva | Media | Alto | Restricción única y control de concurrencia optimista (EC-08) |
| Operaciones presiona para abrir la ubicación a todos | Alta | Alto | Acceso temporal rápido de pedir y auditoría visible para la dirección (EC-06) |
| Se acumulan versiones viejas de la API | Baja | Medio | Política de retiro: 90 días con aviso en la cabecera `Deprecation` |

## 7. Condición de revisión (cuándo extraer Ruteo como servicio)

Esta decisión **se revisa** y Ruteo se extrae como servicio independiente **solo si se cumplen las tres señales durante dos campañas consecutivas**:

1. **Métrica.** El p95 de estimación de ruta supera el objetivo acordado (3 s) **y** bloquea la confirmación de pedidos, aun con caché y cola.
2. **Ritmo de cambio.** Ruteo necesita desplegarse con una frecuencia distinta a la de Pedidos e Inventario.
3. **Capacidad del equipo.** El equipo ya puede observar trazas, errores, reintentos y despliegues de un servicio **sin intervención manual**, y hay un responsable de Ruteo como producto.

Mientras no se cumplan las tres, se mantiene el monolito modular. No es una decisión conservadora por miedo: es **proporcional al problema actual**.

## 8. Cómo comprobaremos que la decisión funciona

Ver [05-criterios-verificables.md](../actividad-2/05-criterios-verificables.md). En resumen:

- pruebas de contrato de la v1 sin `deliveryWindow`;
- prueba de carga con rutas ×20;
- pruebas de arquitectura entre módulos;
- auditoría de la ubicación;
- prueba de reasignaciones concurrentes.
