# A. Resumen ejecutivo

> Actividad 1 · Diagnóstico y contexto arquitectónico
> *"La arquitectura comienza con el contexto, no con el código."*

## Producto

**Plataforma Logística de Última Milla.** Es una solución de gestión logística para una empresa de **retail digital**. Centraliza el ciclo de vida de un pedido: desde que el cliente compra hasta que el repartidor lo entrega en su puerta.

## Problema principal

La empresa recibe pedidos por **varios canales** (app móvil, web, tiendas), maneja **inventarios en varias bodegas** y coordina entregas con **diferentes rutas y repartidores**. Pero **no tiene una vista centralizada del estado real de cada pedido**.

Cada área ve un pedazo de la información, en sistemas o en hojas distintas. El resultado:

- información duplicada;
- retrasos;
- errores operativos;
- poca trazabilidad.

El cliente no sabe cuándo llega su pedido, y el operador no puede gestionar bien las entregas.

## Usuarios

| Usuario | Para qué usa la plataforma |
|---|---|
| **Cliente** | Comprar, saber cuándo llega su pedido y enterarse si algo cambia |
| **Operador de comercio o tienda** | Registrar y vigilar los pedidos de su canal |
| **Administrador de almacén** | Mantener el inventario real y preparar los pedidos |
| **Repartidor** | Recibir sus entregas, seguir la ruta y reportar novedades |
| **Soporte al cliente** | Responder "¿dónde está mi pedido?" y gestionar incidencias |
| **Supervisor de operaciones** | Coordinar la operación del día y reaccionar ante contingencias |

## Objetivos

| # | Objetivo de negocio | Cómo sabremos que se cumple (indicador inicial) |
|---|---|---|
| O1 | **Una sola fuente de verdad** para el estado de cada pedido | 0 pedidos sin estado conocido; las áreas consultan el mismo dato |
| O2 | **Reducir los pedidos duplicados** y las ventas sin stock | Menos del 0,5 % de pedidos duplicados o sin inventario |
| O3 | **Cumplir la promesa de entrega** | ≥ 95 % de entregas dentro de la franja prometida |
| O4 | **Trazabilidad completa** del pedido y de quién hizo qué | 100 % de los cambios de estado con fecha, responsable y motivo |
| O5 | **Responder rápido a contingencias** sin exponer datos personales | Reasignación masiva en minutos, con acceso a la ubicación limitado y auditado |

## Alcance

**Dentro del sistema:**
- registro de pedidos desde todos los canales;
- validación y reserva de inventario;
- planeación y asignación de rutas y repartidores;
- seguimiento del estado de la entrega;
- gestión de incidencias;
- reasignación en contingencia;
- notificaciones al cliente (a través de un proveedor);
- auditoría.

**Fuera del sistema** (se integran, no se construyen):
- el cobro (**sistema de pagos**);
- los mapas y el GPS (**servicio de geolocalización**);
- el envío físico de correos/SMS/push (**sistema de notificaciones**);
- la contabilidad, la facturación y los datos maestros de clientes (**ERP/CRM interno**).

El detalle y el diagrama están en [02-diagrama-contexto.md](02-diagrama-contexto.md).
