# B. Diagrama de contexto

El diagrama muestra **quién usa el sistema**, **con qué sistemas externos se comunica**, **qué información fluye** y **dónde está el límite** del sistema. Todavía **no muestra tecnologías ni la estructura interna**: eso se decide en la Actividad 2.

```mermaid
flowchart LR
    %% Actores
    C["👤 Cliente"]
    OT["🏪 Operador de comercio / tienda"]
    AA["📦 Administrador de almacén"]
    R["🛵 Repartidor"]
    S["🎧 Soporte al cliente"]
    SV["👷 Supervisor de operaciones"]

    %% Sistema
    subgraph LIM["Límite del sistema"]
        P[["Plataforma Logística<br/>de Última Milla"]]
    end

    %% Sistemas externos
    PAG[("💳 Sistema de pagos")]
    GEO[("🗺️ Servicio de geolocalización<br/>(mapas / GPS)")]
    NOT[("✉️ Sistema de notificaciones<br/>(correo / SMS / push)")]
    ERP[("🏢 ERP / CRM interno")]

    C -- "pedido, consulta de estado" --> P
    P -- "estado y hora estimada de entrega" --> C
    OT -- "pedidos de tienda / canal" --> P
    P -- "estado de pedidos de su canal" --> OT
    AA -- "stock, pedido preparado" --> P
    P -- "pedidos por preparar" --> AA
    R -- "avance, entrega, incidencia, ubicación (solo en entrega activa)" --> P
    P -- "entregas asignadas y ruta" --> R
    S -- "consulta de trazabilidad, incidencias" --> P
    P -- "historial del pedido" --> S
    SV -- "reasignaciones, asignación" --> P
    P -- "tablero de operación, alertas" --> SV

    P -- "solicitud de cobro" --> PAG
    PAG -- "pago aprobado / rechazado" --> P
    P -- "origen, destino" --> GEO
    GEO -- "ruta, distancia, tiempo estimado" --> P
    P -- "mensaje y destinatario" --> NOT
    P -- "pedidos confirmados, entregas" --> ERP
    ERP -- "datos de cliente y productos" --> P
```

## Elementos del diagrama

### Actores (personas)

| Actor | Información que **entrega** al sistema | Información que **recibe** |
|---|---|---|
| Cliente | Pedido, dirección, consulta de estado | Confirmación, estado, hora estimada, avisos de cambio |
| Operador de comercio / tienda | Pedidos de su canal | Estado de esos pedidos |
| Administrador de almacén | Stock disponible, pedido preparado | Pedidos por preparar |
| Repartidor | Avance, entrega realizada, incidencias, **ubicación solo durante una entrega activa** | Entregas asignadas y ruta |
| Soporte al cliente | Consultas, registro de incidencias | Historial y trazabilidad del pedido |
| Supervisor de operaciones | Asignaciones y reasignaciones | Tablero de operación y alertas |

### Sistemas externos

| Sistema | Para qué se usa | Por qué queda **fuera** del límite |
|---|---|---|
| Sistema de pagos | Cobrar el pedido | Es un servicio regulado y especializado; lo contratamos, no lo construimos |
| Servicio de geolocalización | Calcular rutas, distancias y tiempos | Es un proveedor externo que **puede ser lento o caerse**: hay que aislarlo |
| Sistema de notificaciones | Enviar correos, SMS y push | Es infraestructura de terceros |
| ERP / CRM interno | Datos maestros de clientes y productos; facturación y contabilidad | Ya existe en la empresa; la plataforma se integra con él |

## Límites: dentro vs. fuera

| ✅ Dentro (lo construimos) | ❌ Fuera (nos integramos) |
|---|---|
| Registro de pedidos de todos los canales en un solo lugar | Cobro y conciliación de pagos |
| Validación y reserva de inventario | Cálculo de mapas y GPS en sí |
| Asignación de rutas y repartidores | Envío físico de mensajes |
| Seguimiento del estado de la entrega | Facturación, contabilidad, maestro de clientes |
| Incidencias y reasignación en contingencia | Red móvil y dispositivo del repartidor |
| Control de acceso, auditoría y retención de la ubicación | Proveedor de identidad (inicio de sesión) |
