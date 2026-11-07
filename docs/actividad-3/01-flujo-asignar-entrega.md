# 1. Flujo de negocio: asignar una entrega

> Actividad 3 · Diseño y dominio
> Continúa la [Actividad 1](../actividad-1/01-resumen-ejecutivo.md) (diagnóstico) y la [Actividad 2](../actividad-2/01-catalogo-requisitos.md) (monolito modular con contextos Pedidos, Inventario, Ruteo y Entregas).

## Por qué este flujo

De los requisitos de la Actividad 2 se toma **RF-06** ("una entrega activa = un repartidor") y **RF-07** (reasignación en contingencia). Este flujo:

- **es el corazón del contexto Entregas;**
- **concentra los riesgos más graves del diagnóstico:**
  - doble asignación (RG-08);
  - entregas retrasadas (RG-03);
  - la tormenta de las 400 entregas (caso de la Actividad 1);
- **es acotado:** se puede modelar completo sin construir todavía la persistencia ni las integraciones, que corresponden a la Actividad 4.

## Actores

| Actor | Rol en el flujo |
|---|---|
| **Supervisor de operaciones** | Inicia la asignación o la reasignación |
| **Repartidor** | Recibe la entrega; su disponibilidad y su carga condicionan la decisión |
| **Sistema (contexto Entregas)** | Aplica las reglas y registra el resultado |
| **Contexto Pedidos** | Origina la entrega cuando se confirma un pedido |
| **Notificaciones / Auditoría** | Se enteran del resultado (evento) |

## Propósito

Que **cada entrega pendiente tenga un único repartidor responsable**, elegido entre los que **están activos y no están saturados**, para cumplir la ventana de entrega prometida al cliente.

## Resultado esperado

- **Si se puede asignar:**
  - la entrega pasa a **Asignada**;
  - queda registrado **quién la lleva y desde cuándo**;
  - se publica el evento **RepartidorAsignado**.
- **Si no se puede asignar:** la entrega **no cambia** y el supervisor recibe un **motivo claro con un código**, por ejemplo `REPARTIDOR_SATURADO`.

## Pasos (camino feliz)

1. El supervisor elige una entrega pendiente y un repartidor.
2. El sistema carga la entrega y verifica que exista y que no tenga repartidor activo.
3. El sistema consulta la disponibilidad y la carga actual del repartidor.
4. La entrega decide si acepta la asignación (aquí se protegen las invariantes).
5. El sistema guarda la entrega.
6. El sistema publica el resultado para Notificaciones y Auditoría.

## Variante: reasignación por contingencia

Una entrega **asignada o en camino** puede cambiar de repartidor (por ejemplo, durante la tormenta) **solo con un motivo**. La asignación anterior queda **cerrada con ese motivo**, de modo que se conserva la trazabilidad.

## Fuera del alcance de esta actividad

- **La persistencia real** (base de datos) y la **integración real** con el servicio de repartidores. Se usan **adaptadores en memoria**.
- **La API HTTP**, que es el adaptador de entrada de la Actividad 4.
- **La elección automática del "mejor" repartidor** (optimización de rutas). Aquí el supervisor elige y el sistema valida.
