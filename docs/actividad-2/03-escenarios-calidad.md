# 3. Escenarios de calidad

Cada escenario usa las seis partes clásicas: **fuente → estímulo → artefacto → entorno → respuesta → medida**. La **medida** es lo que permite comprobar después si la arquitectura cumple.

## Resumen

| ID | Atributo | Medida clave | Requisitos |
|---|---|---|---|
| EC-01 | Compatibilidad | 100 % de solicitudes v1 sin `deliveryWindow` aceptadas | RF-02, RNF-01 |
| EC-02 | Compatibilidad | La v1 sigue respondiendo ≥ 90 días después de publicar la v2 | RNF-01 |
| EC-03 | Rendimiento | p95 crear pedido < 2 s con 1.000 pedidos/h | RF-01, RNF-02 |
| EC-04 | Escalabilidad | Con rutas ×20, la creación de pedidos no se degrada más de un 10 % | RNF-03 |
| EC-05 | Resiliencia | Proveedor de mapas caído → pedido creado igual, 0 errores 5xx al cliente | RNF-08 |
| EC-06 | Seguridad/privacidad | 100 % de accesos no autorizados a la ubicación rechazados y auditados | RF-09, RNF-05, RNF-06 |
| EC-07 | Operación en contingencia | 400 entregas reasignadas en < 10 min, clientes notificados en < 5 min | RF-07, RF-11 |
| EC-08 | Consistencia | 0 dobles asignaciones con reasignaciones concurrentes | RF-06, RNF-09 |
| EC-09 | Modificabilidad | Cambiar una regla de inventario toca solo el módulo Inventario | RNF-07 |
| EC-10 | Observabilidad | Degradación de ruteo detectada y alertada en < 5 min | RNF-10 |

---

### EC-01 · Cliente antiguo crea un pedido sin `deliveryWindow`

| Parte | Descripción |
|---|---|
| Fuente | App móvil publicada antes del cambio (versión anterior) |
| Estímulo | Envía `POST /api/v1/orders` **sin** el campo `deliveryWindow` |
| Artefacto | API de Pedidos, contrato v1 |
| Entorno | Operación normal, después de desplegar el soporte de ventana de entrega |
| Respuesta | El pedido se crea con la **ventana por defecto** del negocio. La respuesta conserva los mismos campos (`address`, `total`) |
| **Medida** | **100 %** de las pruebas de contrato del cliente antiguo pasan. **0** campos renombrados o eliminados en v1 |

### EC-02 · Cambio incompatible en el contrato

| Parte | Descripción |
|---|---|
| Fuente | Equipo de negocio |
| Estímulo | Pide cambiar el significado de `total` (p. ej., incluir impuestos) o convertir `address` en un objeto |
| Artefacto | API pública de Pedidos |
| Entorno | Hay apps publicadas con la versión anterior |
| Respuesta | Se publica `/api/v2/orders`. La v1 sigue funcionando sin cambios y se documenta su fecha de retiro |
| **Medida** | La v1 responde correctamente durante **≥ 90 días** después de publicar la v2. **0** incidentes de "no puedo crear pedidos" atribuidos al cambio |

### EC-03 · Crear pedido en operación normal

| Parte | Descripción |
|---|---|
| Fuente | Clientes (app móvil y web) |
| Estímulo | 1.000 solicitudes de creación de pedido por hora |
| Artefacto | Módulo Pedidos (incluye la consulta a Inventario) |
| Entorno | Operación normal |
| Respuesta | El pedido se registra y se responde con su identificador y estado |
| **Medida** | **p95 < 2 s** y tasa de error **< 0,5 %** |

### EC-04 · Pico de campaña "entregas en menos de 2 horas"

| Parte | Descripción |
|---|---|
| Fuente | Campaña comercial |
| Estímulo | Las consultas de ruta se multiplican **×20** durante 4 horas; los pedidos siguen en su volumen normal |
| Artefacto | Módulo Ruteo y su interacción con Pedidos |
| Entorno | Pico de carga |
| Respuesta | Las estimaciones se sirven desde caché cuando son vigentes. Las demás pasan por una **cola de trabajo**, sin bloquear la creación de pedidos |
| **Medida** | p95 de **crear pedido < 2,2 s** (≤ 10 % de degradación frente a EC-03). **Aciertos de caché ≥ 70 %**. p95 de **confirmación < 5 s** |

### EC-05 · El proveedor de mapas se demora o cae

| Parte | Descripción |
|---|---|
| Fuente | Proveedor externo de mapas/GPS |
| Estímulo | Responde en más de 3 s o devuelve errores |
| Artefacto | Adaptador de mapas del módulo Ruteo |
| Entorno | Operación normal o pico |
| Respuesta | Timeout de 2 s, **2 reintentos** con espera creciente y luego **circuito abierto**. El pedido se crea en estado `PENDIENTE_RUTA` y se confirma cuando se obtiene la ruta |
| **Medida** | **0** errores 5xx al cliente por culpa del proveedor. **100 %** de pedidos en `PENDIENTE_RUTA` resueltos en < 15 min después de que el proveedor se recupera |

### EC-06 · Supervisor intenta ver la ubicación de un repartidor

| Parte | Descripción |
|---|---|
| Fuente | Usuario con rol Supervisor |
| Estímulo | Consulta la ubicación de un repartidor que **no** tiene entregas a su cargo, o fuera de la ventana de acceso |
| Artefacto | Módulo Entregas, política de autorización y módulo Auditoría |
| Entorno | Contingencia (p. ej., la tormenta) |
| Respuesta | Se rechaza con `403`. Si el acceso sí es válido, se concede por un **máximo de 2 horas**, solo para sus entregas, y se registra quién, cuándo, a quién y el motivo |
| **Medida** | **100 %** de accesos no autorizados rechazados. **100 %** de consultas (aceptadas y rechazadas) presentes en la auditoría. **0** accesos vigentes después de 2 h |

### EC-07 · Reasignar 400 entregas por una tormenta

| Parte | Descripción |
|---|---|
| Fuente | Supervisor de operaciones |
| Estímulo | Ordena reasignar 400 entregas de una zona afectada |
| Artefacto | Módulo Entregas y Notificaciones |
| Entorno | Contingencia, con carga normal de pedidos |
| Respuesta | Se procesan por lotes, se registra cada reasignación y se notifica a los clientes afectados |
| **Medida** | **400 entregas reasignadas en < 10 min**. **95 %** de los clientes notificados en **< 5 min** desde su reasignación |

### EC-08 · Reasignaciones concurrentes

| Parte | Descripción |
|---|---|
| Fuente | Dos supervisores al mismo tiempo |
| Estímulo | Ambos asignan la misma entrega a repartidores distintos |
| Artefacto | Módulo Entregas (persistencia) |
| Entorno | Contingencia |
| Respuesta | Se acepta la primera operación. La segunda recibe un `409 Conflicto` con el estado actual (control de concurrencia optimista y una restricción única en la base de datos) |
| **Medida** | **0** entregas activas con más de un repartidor, comprobado con una consulta de verificación después de una prueba de 50 reasignaciones simultáneas |

### EC-09 · Cambio de una regla de negocio

| Parte | Descripción |
|---|---|
| Fuente | Equipo de Inventario |
| Estímulo | Cambia la regla de la "cantidad máxima reservable por pedido" |
| Artefacto | Código del módulo Inventario |
| Entorno | Desarrollo |
| Respuesta | Se modifica solo la implementación de Inventario. Pedidos sigue usando el mismo contrato (`IInventoryAvailability`) |
| **Medida** | **0** archivos modificados fuera de `Modules/Inventory`. Las pruebas de dependencias entre módulos pasan. El cambio se hace en **≤ 1 día** |

### EC-10 · Degradación del ruteo

| Parte | Descripción |
|---|---|
| Fuente | Monitoreo |
| Estímulo | El p95 de estimación de ruta supera 3 s durante 5 minutos |
| Artefacto | Métricas y alertas del módulo Ruteo |
| Entorno | Pico de campaña |
| Respuesta | Se dispara una alerta al equipo con el módulo afectado y la traza de ejemplo |
| **Medida** | Alerta en **< 5 min**. Las métricas quedan guardadas para evaluar si se debe extraer Ruteo (ver ADR-001) |
