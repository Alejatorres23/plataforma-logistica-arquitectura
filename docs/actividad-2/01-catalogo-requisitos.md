# 1. Catálogo de requisitos

> Actividad 2 · Unidad 1: Comprender el problema y decidir
> Fuente: diagnóstico de la Actividad 1 y caso obligatorio de la Actividad 2.

## 1.1 Contexto que heredamos de la Actividad 1

La plataforma logística recibe pedidos de clientes desde una **aplicación móvil** y una **interfaz web**. Después reserva inventario, calcula rutas y asigna las entregas a repartidores.

En el diagnóstico de la Actividad 1 aparecieron dos situaciones que muestran dónde duele el sistema:

1. **Contingencia operativa.** Una tormenta obligó a **reasignar 400 entregas**. Al mismo tiempo, Operaciones pidió ver la **ubicación GPS de los repartidores sin restricción** y dar ese acceso a **todos los supervisores**.
2. **Riesgos identificados.**
   - Acceso indebido a la ubicación de los repartidores.
   - Doble asignación de un repartidor.
   - Proveedor de GPS o de mapas lento.
   - Cliente sin actualización del estado de su pedido.

## 1.2 Caso obligatorio de la Actividad 2

> La aplicación móvil necesita **crear pedidos sin romperse** cuando se agrega una **ventana de entrega**, y el sistema debe **absorber los picos de campaña**.

- **Ventana de entrega.** El negocio quiere agregar el campo `deliveryWindow`. La app móvil ya publicada **no se actualiza de inmediato**: las tiendas (Play Store / App Store) tardan días en aprobar una versión nueva. Si el contrato de la API cambia de forma incompatible, los clientes que no actualizan dejan de poder crear pedidos, aunque el servidor funcione.
- **Picos de campaña.** En una campaña de "entregas en menos de 2 horas", las **consultas de rutas se multiplican por 20**. La creación de pedidos y la reserva de inventario se mantienen en su volumen normal. El cálculo de rutas consume CPU y **retrasa la confirmación de los pedidos**.

## 1.3 Actores

| Actor | Qué necesita del sistema |
|---|---|
| **Cliente** (app móvil / web) | Crear pedidos, elegir una ventana de entrega, consultar el estado y el historial, recibir notificaciones |
| **Operador logístico / Supervisor** | Ver la operación, reasignar entregas en contingencia, consultar la ubicación de un repartidor **solo cuando lo necesita** |
| **Repartidor** | Recibir asignaciones, reportar el avance y las incidencias; que su ubicación no se use de forma indebida |
| **Soporte** | Atender incidencias y consultar la trazabilidad de un pedido |
| **Equipo técnico** | Desplegar, observar y evolucionar el sistema sin romper a los clientes |
| **Sistemas externos** | Proveedor de mapas/GPS, proveedor de identidad, red móvil, pasarela de notificaciones (correo/SMS/push) |

## 1.4 Alcance

| Dentro del sistema | Fuera del sistema |
|---|---|
| Pedidos, inventario, ruteo (estimación), entregas y asignación | El proveedor de mapas/GPS en sí (se integra por contrato) |
| Reasignación de entregas en contingencia | El proveedor de identidad (se usa, no se construye) |
| Autorización por rol, acceso temporal a la ubicación y auditoría | La red móvil y el dispositivo del repartidor |
| Notificaciones al cliente (se delegan a una pasarela externa) | La pasarela de pagos (no forma parte de esta entrega) |
| API pública versionada para app móvil y web | La publicación en las tiendas de aplicaciones |

## 1.5 Requisitos funcionales (RF)

| ID | Requisito | Contexto (módulo) | Actor |
|---|---|---|---|
| RF-01 | Crear un pedido desde la app móvil o la web, validando cliente, artículos y dirección | Pedidos | Cliente |
| RF-02 | Permitir indicar una **ventana de entrega opcional** (`deliveryWindow`) sin romper a los clientes que no la envían | Pedidos / API | Cliente |
| RF-03 | Confirmar el pedido **solo** si hay stock reservable **y** una ruta viable | Pedidos | Sistema |
| RF-04 | Reservar inventario sin superar la cantidad disponible | Inventario | Sistema |
| RF-05 | Estimar la ruta (origen, destino, tiempo y vigencia de la estimación) usando el proveedor de mapas | Ruteo | Sistema |
| RF-06 | Asignar cada entrega activa a **un solo** repartidor | Entregas | Operador |
| RF-07 | **Reasignar entregas de forma masiva** ante una contingencia (p. ej., 400 por una tormenta) | Entregas | Supervisor |
| RF-08 | Consultar el estado y el seguimiento de un pedido | Pedidos | Cliente, Soporte |
| RF-09 | Consultar la ubicación de un repartidor **solo con un rol autorizado, por tiempo limitado y para entregas que el supervisor tiene a cargo** | Entregas / Seguridad | Supervisor |
| RF-10 | Registrar la auditoría de cada consulta de ubicación y de cada reasignación (quién, cuándo, por qué) | Auditoría | Sistema |
| RF-11 | Notificar al cliente los cambios de estado (confirmado, reasignado, en camino, entregado) | Notificaciones | Cliente |
| RF-12 | Registrar y gestionar las incidencias de una entrega | Incidencias | Repartidor, Soporte |
| RF-13 | Autenticar a los usuarios con el proveedor de identidad y gestionar sus roles | Identidad | Todos |
| RF-14 | Consultar el historial de pedidos del cliente | Pedidos | Cliente |
| RF-15 | Generar reportes operativos (entregas por estado, tiempos, incidencias) | Reportes | Operador |

## 1.6 Atributos de calidad (requisitos no funcionales, RNF)

| ID | Atributo | Requisito |
|---|---|---|
| RNF-01 | **Compatibilidad / evolución de contratos** | Un cambio compatible solo **agrega** campos opcionales. Un cambio incompatible se publica como una **versión nueva** (`/v2`) y la versión anterior se mantiene durante una ventana acordada |
| RNF-02 | **Rendimiento** | El 95 % de las solicitudes de crear pedido responde en **menos de 2 s** |
| RNF-03 | **Escalabilidad** | Soportar los picos de campaña (rutas ×20) sin degradar la creación de pedidos |
| RNF-04 | **Disponibilidad** | 99,5 % mensual en horario operativo |
| RNF-05 | **Seguridad y privacidad** | Ubicación del repartidor con **mínimo privilegio**: acceso por rol, temporal y auditado. Datos sensibles cifrados en tránsito (TLS) y en reposo. Cumplimiento de la Ley 1581 de 2012 (protección de datos personales, Colombia) |
| RNF-06 | **Auditabilidad / trazabilidad** | El 100 % de las consultas de ubicación y de las reasignaciones queda registrado |
| RNF-07 | **Mantenibilidad / facilidad de cambio** | Cambiar una regla de un contexto (p. ej., la cantidad máxima reservable) no obliga a modificar otros módulos |
| RNF-08 | **Resiliencia** | Si el proveedor de mapas se demora o cae, el pedido **se crea igual** y queda en "pendiente de ruta". Se usan timeouts, reintentos y caché |
| RNF-09 | **Consistencia** | Nunca dos repartidores asignados a la misma entrega activa, ni una entrega asignada dos veces |
| RNF-10 | **Observabilidad** | Métricas de latencia y errores por módulo, trazas por solicitud y alertas |
| RNF-11 | **Costo** | La infraestructura y la operación deben ser proporcionales a un equipo pequeño y a un crecimiento moderado |

## 1.7 Restricciones

| ID | Restricción | Por qué condiciona la arquitectura |
|---|---|---|
| R-01 | La app móvil publicada **tarda días** en actualizarse (revisión de las tiendas) | Obliga a mantener contratos compatibles y versionados |
| R-02 | **Equipo de desarrollo pequeño** (supuesto: 4–6 personas) | Limita cuántos servicios se pueden operar con calidad |
| R-03 | **Presupuesto de infraestructura limitado** | Penaliza las opciones con muchos despliegues, bases de datos y monitoreo distribuido |
| R-04 | Dependencia de proveedores externos (mapas/GPS, identidad, notificaciones) | Exige adaptadores y aislamiento de fallas |
| R-05 | Normativa de protección de datos (Ley 1581 de 2012) | La ubicación del repartidor es un dato personal: requiere finalidad, autorización y registro de acceso |
| R-06 | Se necesita salir rápido con un MVP | Favorece las opciones con menor complejidad inicial |

> **Supuestos.** El tamaño del equipo, el presupuesto y los volúmenes (1.000 pedidos/hora en operación normal) son supuestos razonables para el caso. Si el negocio da cifras reales, se reemplazan y se revisan los escenarios del documento 3.
