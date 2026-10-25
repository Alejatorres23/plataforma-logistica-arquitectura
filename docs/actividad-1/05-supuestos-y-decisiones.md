# D. Supuestos y decisiones iniciales

Como se recomienda en el video 01, separamos **hechos** (algo observable), **supuestos** (una afirmación que aún hay que validar) y **decisiones** (una elección entre alternativas). Si se mezclan, terminamos defendiendo opiniones como si fueran datos.

## Hechos (lo que sabemos por el caso)

- Los pedidos llegan por **varios canales**.
- El inventario está **distribuido** en varias bodegas.
- Hay **diferentes rutas y repartidores**.
- **No existe una vista centralizada** del estado real del pedido.
- Hoy hay duplicación de información, retrasos, errores operativos y poca trazabilidad.
- Existen sistemas externos: pagos, geolocalización, notificaciones y ERP/CRM.
- En una contingencia (la tormenta) hubo que reasignar **400 entregas**.

## Qué asumimos (supuestos)

| ID | Supuesto | Cómo lo validaremos |
|---|---|---|
| S-01 | Se manejan cerca de **10.000 pedidos diarios**, con picos de campaña | Pedir los datos históricos de ventas al negocio |
| S-02 | El proveedor de mapas responde en **menos de 1 s**, pero **no siempre** | Revisar su acuerdo de servicio (SLA) y medirlo en pruebas |
| S-03 | El **equipo técnico es pequeño** (4 a 6 personas) | Confirmar con la dirección de tecnología |
| S-04 | El ERP/CRM tiene una forma de integrarse (API o exportación) | Reunión con el área de sistemas internos |
| S-05 | El repartidor usa una app en su celular con datos móviles intermitentes | Entrevista con operaciones y algunos repartidores |
| S-06 | La ubicación del repartidor **solo se necesita durante una entrega activa** | Validar con operaciones qué decisiones toman con ella |
| S-07 | El presupuesto de infraestructura es **limitado** | Confirmar con finanzas |

## Qué decisiones ya tomamos

| ID | Decisión | Por qué |
|---|---|---|
| DI-01 | **Una plataforma centralizada** que registre pedidos, valide inventario, asigne rutas, rastree entregas y gestione incidencias desde **un único sistema** | Ataca el problema raíz: la falta de una fuente única de verdad |
| DI-02 | **No elegir tecnologías todavía** (lenguaje, base de datos, nube) | Primero hay que entender el problema; la estructura se decide en la Actividad 2 |
| DI-03 | Los sistemas externos (pagos, mapas, notificaciones, ERP) **se integran, no se construyen**, y quedan **aislados** para que su falla no detenga la operación | No son el núcleo del negocio y pueden fallar |
| DI-04 | **Privacidad desde el diseño:** la ubicación del repartidor se usa solo para la entrega activa, con acceso por rol, temporal y auditado | Protege a personas y evita riesgo legal (Ley 1581 de 2012). Ver el caso de la tormenta |
| DI-05 | **Todo cambio de estado queda registrado** (quién, cuándo, por qué) | La trazabilidad es uno de los problemas declarados |

## Qué aún no sabemos

- Cuántos pedidos llegan en el **peor pico** real.
- Qué tan **confiable** es el proveedor de mapas en hora pico.
- Si los canales comparten un **identificador de cliente**, o cada uno usa el suyo.
- Qué **política de retención** de datos exige el área legal (cuánto tiempo guardar ubicaciones y direcciones).
- Qué **promesa de entrega** se le hace a cada tipo de cliente.

## Qué habrá que confirmar luego

| Pendiente | Con quién | Impacta en |
|---|---|---|
| Volúmenes reales y picos | Negocio / comercial | Escalabilidad (Actividad 2) |
| SLA del proveedor de mapas | Proveedor | Tiempo máximo de espera y alternativa |
| Política de retención de ubicación | Legal | Privacidad (DI-04) |
| Integración con ERP/CRM | Sistemas internos | Alcance y esfuerzo |
| Tamaño y experiencia del equipo | Tecnología | Qué estructura se puede operar (Actividad 2) |
