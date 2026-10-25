# C. Análisis del problema

## ¿Qué ocurre hoy?

Seguimos el recorrido de un pedido para ver dónde se rompe:

| Paso | Qué pasa hoy | Qué puede salir mal |
|---|---|---|
| 1. El cliente compra | El pedido entra por la app, la web o la tienda, y **cada canal lo registra a su manera** | El mismo pedido se registra dos veces; nadie sabe cuál es el "real" |
| 2. Se valida el inventario | Cada bodega lleva su stock y **se actualiza tarde** | Se vende algo que no hay o se reserva en una bodega equivocada |
| 3. Se arma la ruta | Las rutas se planean con información parcial del stock y de los repartidores | Rutas ineficientes, repartidores sobrecargados o sin trabajo |
| 4. Se asigna el repartidor | La asignación se hace por mensajes o planillas | Doble asignación o entregas sin dueño |
| 5. Se entrega | El estado real solo lo conoce el repartidor | El cliente no sabe cuándo llega; soporte no tiene qué responder |
| 6. Ocurre una novedad | Una tormenta, una vía cerrada, un cliente ausente | No hay una forma ordenada de reasignar ni de avisar al cliente |

## ¿Por qué es un problema?

- **Para el cliente.** No sabe cuándo llega su pedido ni por qué se retrasó. Se pierde la confianza y vuelve a comprar menos.
- **Para la operación.** Sin una vista centralizada, cada área toma decisiones con datos distintos. Los errores se descubren tarde, cuando el cliente ya reclamó.
- **Para el negocio.** Los duplicados y las ventas sin stock cuestan dinero: devoluciones, reenvíos, horas de soporte y reputación.
- **Para el repartidor.** Las asignaciones desordenadas son injustas. Además, si para coordinar se comparte su ubicación sin control, queda **expuesto** (video 004).

## ¿Qué consecuencias tiene?

| Consecuencia | A quién afecta | Gravedad |
|---|---|---|
| Entregas retrasadas o fallidas | Cliente, negocio | Alta |
| Pedidos duplicados y cobros dobles | Cliente, negocio, soporte | Alta |
| Inventario inconsistente (vender lo que no hay) | Cliente, almacén | Alta |
| Soporte sin información para responder | Cliente, soporte | Media |
| Pérdida de trazabilidad: no se sabe quién cambió qué | Negocio, auditoría | Media |
| Exposición de datos personales (ubicación del repartidor, dirección del cliente) | Repartidor, cliente, negocio (riesgo legal) | Alta |
| Crecimiento sin control: cada canal nuevo agrega otra "isla" de información | Negocio, equipo técnico | Media |

## ¿Qué variables importan?

| Variable | Por qué importa | Valor estimado (supuesto) |
|---|---|---|
| Volumen de pedidos por día | Define la carga normal | ~10.000 pedidos/día |
| Picos (campañas, fechas especiales) | Ponen a prueba el sistema | Hasta ×20 en consultas de ruta |
| Número de canales de venta | Más canales, más riesgo de duplicados | 3 (app, web, tienda) |
| Número de bodegas | Complejidad del inventario | Varias (inventario distribuido) |
| Repartidores activos | Capacidad de entrega | Cientos |
| Tiempo de respuesta del proveedor de mapas | Si es lento, frena la asignación | Supuesto: < 1 s, **no garantizado** |
| Promesa de entrega | Lo que se le dijo al cliente | Mismo día / menos de 2 h en campañas |
| Sensibilidad de los datos | Ubicación y direcciones son datos personales | Alta (Ley 1581 de 2012) |

## Una situación que lo resume: la tormenta

> Es viernes a las 7:00 p. m. Una tormenta bloquea varias vías y hay que **reasignar 400 entregas**. Operaciones pide la **ubicación GPS en tiempo real de todos los repartidores**, su **historial completo de trayectos** y que **cualquier supervisor** pueda verlo.

Esta situación junta todos los problemas a la vez:
- no hay una vista central del estado de las entregas;
- la reasignación es manual;
- el cliente no se entera;
- la solución "rápida" (abrir todos los datos) **pone en riesgo a los repartidores**.

Su análisis y la decisión inicial están en [06-caso-reasignacion-tormenta.md](06-caso-reasignacion-tormenta.md).
