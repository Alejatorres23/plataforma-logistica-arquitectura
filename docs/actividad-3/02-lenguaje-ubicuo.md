# 2. Lenguaje ubicuo

El lenguaje ubicuo es el conjunto de términos que **el negocio y el equipo técnico usan con el mismo significado**. El código usa los mismos conceptos, para que una regla se pueda leer igual en una conversación y en una clase.

## Glosario del contexto Entregas

| Término | Significado acordado | En el código |
|---|---|---|
| **Entrega** | Traslado de un pedido confirmado hasta la dirección del cliente. Tiene identidad propia y un ciclo de vida | `Delivery` (raíz del agregado) |
| **Pedido (referencia)** | Para Entregas, el pedido es **solo un identificador**; su contenido pertenece al contexto Pedidos | `OrderReference` |
| **Repartidor** | Persona que ejecuta la entrega. Entregas solo guarda su identificador | `CourierId` |
| **Asignación** | Vínculo entre una entrega y un repartidor, con fecha de inicio y, si termina, fecha y motivo de cierre | `Assignment` |
| **Repartidor activo (de una entrega)** | El repartidor de la asignación que **no está cerrada** | `Delivery.CurrentCourierId` |
| **Repartidor disponible** | Repartidor que **está en turno** (activo) y **no está saturado** | `CourierCapacity` (`IsActive`, `IsSaturated`) |
| **Saturado** | Repartidor cuya carga activa **alcanzó su máximo** de entregas simultáneas | `CourierCapacity.IsSaturated` |
| **Carga activa** | Número de entregas asignadas o en camino que tiene un repartidor ahora | `CourierCapacity.ActiveDeliveries` |
| **Reasignación** | Cambio de repartidor de una entrega asignada o en camino. Siempre tiene **motivo** | `Delivery.Reassign(...)` |
| **Ventana de entrega** | Franja horaria prometida al cliente (inicio < fin) | `DeliveryWindow` |
| **Prioridad** | Urgencia de la entrega (Normal / Urgente) | `DeliveryPriority` |
| **Evidencia de recepción** | Prueba de que el cliente recibió el paquete (quién y cuándo) | `ProofOfDelivery` |
| **Entrega fallida** | Se intentó y no se pudo realizar (cliente ausente, vía cerrada...). Puede reprogramarse | `DeliveryStatus.Fallida` |
| **Reprogramar** | Volver a dejar pendiente una entrega fallida para un nuevo intento | `Delivery.ScheduleRetry()` |

### Estados de la entrega

| Estado | Significado |
|---|---|
| **Pendiente de asignación** | Existe la entrega, pero nadie la lleva todavía |
| **Asignada** | Tiene un repartidor responsable que aún no sale a ruta |
| **En camino** | El repartidor recogió el paquete y va hacia el cliente |
| **Entregada** | El cliente la recibió y hay evidencia (estado final) |
| **Fallida** | Se intentó sin éxito; se puede reprogramar |
| **Cancelada** | No se hará (estado final) |

## Términos ambiguos que hay que confirmar con el negocio

| Término | Por qué es ambiguo | Decisión provisional | Confirmar con |
|---|---|---|---|
| **"Disponible"** | En Inventario = hay stock; en Ruteo = hay ruta viable; en Entregas = repartidor en turno y no saturado (videos 06 y 12.1) | En este contexto, "disponible" **solo** se refiere al repartidor. Se nombra distinto en cada contexto | Operaciones |
| **"Asignada"** | ¿Basta con que el supervisor la asigne o el repartidor debe **aceptarla** en su app? | Se considera asignada al registrarla; la aceptación del repartidor queda como un posible estado futuro | Operaciones / repartidores |
| **"Saturado"** | ¿Se mide por **número de entregas**, por **peso/volumen** o por **tiempo de ruta**? | Por número de entregas activas (máximo por repartidor) | Operaciones |
| **"Entregada"** | ¿Qué cuenta como evidencia: firma, foto, código del cliente? | Nombre de quien recibe + fecha y hora | Legal / Soporte |
| **"Pedido confirmado"** vs. **"entrega completada"** | Son hechos distintos: el pedido se confirma mucho antes de que llegue (video 19) | Pertenecen a contextos distintos y nunca se mezclan | Comercial |
| **"Repartidor activo"** | Puede significar "en turno" o "el que lleva esta entrega" | "Activo" (`IsActive`) = en turno; "repartidor actual" = el que lleva la entrega | Equipo técnico |
