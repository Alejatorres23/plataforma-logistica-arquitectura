# Riesgos principales

Un riesgo es un punto donde el sistema **puede fallar o causar pérdidas**. La probabilidad y el impacto se califican **Alta / Media / Baja**.

| ID | Riesgo | Causa | Consecuencia | Prob. | Impacto | Respuesta inicial |
|---|---|---|---|:-:|:-:|---|
| RG-01 | **Duplicación de pedidos** | Cada canal registra el pedido por su lado; el cliente reintenta cuando la app no responde | Cobros dobles, envíos dobles, reclamos | Alta | Alta | Un único punto de registro de pedidos; un identificador único por compra para que los reintentos no dupliquen |
| RG-02 | **Inconsistencias de inventario** | Stock distribuido que se actualiza tarde | Se vende lo que no hay; cancelaciones | Alta | Alta | Reservar el inventario al confirmar; una sola fuente de verdad por bodega |
| RG-03 | **Entregas fallidas o retrasadas** | Rutas con datos incompletos, contingencias (lluvia, vías cerradas) | Se incumple la promesa; el cliente se va | Alta | Alta | Asignación centralizada, reasignación en contingencia y aviso al cliente |
| RG-04 | **Pérdida de trazabilidad** | Cambios de estado sin registro de quién ni por qué | Soporte no puede responder; no hay forma de auditar | Media | Alta | Historial de estados con fecha, responsable y motivo |
| RG-05 | **Mala coordinación entre almacén, rutas y soporte** | Cada área usa su propia herramienta | Errores que se descubren tarde | Alta | Media | Una vista compartida del estado del pedido |
| RG-06 | **Crecimiento operativo sin control** | Cada canal o bodega nueva agrega otra isla de información | Los costos y errores crecen más rápido que el negocio | Media | Media | Separar responsabilidades desde el inicio para poder crecer por partes |
| RG-07 | **Acceso indebido a la ubicación del repartidor** | Para resolver una urgencia se abre el GPS a todos | Riesgo para la seguridad del repartidor, sanción legal, pérdida de confianza | Alta (en contingencias) | Alta | Ubicación mínima, solo en entrega activa, por rol, temporal y auditada |
| RG-08 | **Doble asignación de un repartidor o de una entrega** | Dos supervisores asignan al mismo tiempo | Entregas sin hacer o repartidores sobrecargados | Media | Alta | Una entrega activa = un repartidor, validado por el sistema |
| RG-09 | **Proveedor de mapas/GPS lento o caído** | Dependencia externa que no controlamos | Se frena la asignación y la creación de pedidos | Media | Alta | Aislar al proveedor con un tiempo máximo de espera y una alternativa (ruta pendiente) |
| RG-10 | **Cliente sin actualización** | Los cambios no se notifican | Llamadas a soporte, mala experiencia | Alta | Media | Notificar cada cambio de estado relevante |

## Los cuatro que más preocupan

1. **RG-03 Entregas fallidas o retrasadas.** Es la promesa central del negocio.
2. **RG-01 / RG-02 Duplicados e inventario.** Cuestan dinero en cada pedido.
3. **RG-07 Acceso indebido a la ubicación.** Pone en riesgo a personas, no solo al negocio.
4. **RG-09 Proveedor externo.** No lo controlamos, pero sus fallas nos afectan directamente.
