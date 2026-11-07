# 3. Modelo de dominio

## Entidades vs. objetos de valor

| Tipo | Elemento | Por qué |
|---|---|---|
| **Entidad (raíz del agregado)** | `Delivery` (Entrega) | Tiene identidad propia: la entrega E-100 sigue siendo E-100 aunque cambie de estado o de repartidor |
| **Entidad interna** | `Assignment` (Asignación) | Tiene ciclo de vida (se abre y se cierra), pero **solo existe dentro de una entrega** y solo se modifica a través de ella |
| Objeto de valor | `DeliveryId`, `CourierId`, `OrderReference` | Identificadores tipados: evitan confundir un Guid de entrega con uno de repartidor |
| Objeto de valor | `DeliveryAddress` | Se describe por su texto; no puede estar vacía |
| Objeto de valor | `DeliveryWindow` | Se describe por inicio y fin; el fin siempre es posterior al inicio |
| Objeto de valor | `DeliveryPriority` | Normal / Urgente |
| Objeto de valor | `CourierCapacity` | **Foto** de la capacidad de un repartidor en un momento: activo, carga activa y máximo |
| Objeto de valor | `ProofOfDelivery` | Quién recibió y cuándo |
| Evento de dominio | `CourierAssigned`, `DeliveryReassigned` | Hechos ya ocurridos que otros contextos necesitan conocer |

> **El agregado `Delivery`.** Es la frontera de consistencia. Todo cambio (asignar, reasignar, completar…) pasa por un método de `Delivery`, que es quien protege las invariantes. Nadie puede agregar una `Assignment` desde fuera.

## Diagrama del modelo

```mermaid
classDiagram
    direction LR
    class Delivery {
        <<Aggregate Root>>
        +DeliveryId Id
        +OrderReference Order
        +DeliveryAddress Address
        +DeliveryWindow Window
        +DeliveryPriority Priority
        +DeliveryStatus Status
        +ProofOfDelivery? Proof
        +CourierId? CurrentCourierId
        +Create(order, address, window, priority)$ Delivery
        +AssignCourier(CourierCapacity, at)
        +Reassign(CourierCapacity, reason, at)
        +StartRoute()
        +Complete(ProofOfDelivery)
        +Fail(reason, at)
        +ScheduleRetry()
        +Cancel(reason, at)
        +PullEvents() IDomainEvent[]
    }
    class Assignment {
        <<Entity>>
        +CourierId CourierId
        +DateTimeOffset AssignedAt
        +DateTimeOffset? ReleasedAt
        +string? ReleaseReason
        +bool IsActive
    }
    class CourierCapacity {
        <<Value Object>>
        +CourierId CourierId
        +bool IsActive
        +int ActiveDeliveries
        +int MaxDeliveries
        +bool IsSaturated
    }
    class DeliveryWindow {
        <<Value Object>>
        +Start
        +End
    }
    class DeliveryAddress {
        <<Value Object>>
        +Value
    }
    class ProofOfDelivery {
        <<Value Object>>
        +ReceivedBy
        +ReceivedAt
    }
    class DeliveryStatus {
        <<enumeration>>
        PendienteDeAsignacion
        Asignada
        EnCamino
        Entregada
        Fallida
        Cancelada
    }
    Delivery "1" *-- "0..*" Assignment : historial
    Delivery --> DeliveryWindow
    Delivery --> DeliveryAddress
    Delivery --> ProofOfDelivery
    Delivery --> DeliveryStatus
    Delivery ..> CourierCapacity : decide con
```

## Estados y transiciones

```mermaid
stateDiagram-v2
    [*] --> PendienteDeAsignacion : Create (pedido confirmado)
    PendienteDeAsignacion --> Asignada : AssignCourier [repartidor activo y no saturado]
    Asignada --> Asignada : Reassign [motivo, otro repartidor]
    Asignada --> EnCamino : StartRoute
    EnCamino --> Asignada : Reassign [motivo, otro repartidor]
    EnCamino --> Entregada : Complete [con evidencia]
    EnCamino --> Fallida : Fail [motivo]
    Fallida --> PendienteDeAsignacion : ScheduleRetry
    PendienteDeAsignacion --> Cancelada : Cancel [motivo]
    Asignada --> Cancelada : Cancel [motivo]
    Entregada --> [*]
    Cancelada --> [*]
```

| Desde | Operación | Hacia | Condición |
|---|---|---|---|
| Pendiente de asignación | `AssignCourier` | Asignada | Sin repartidor activo; repartidor en turno y no saturado |
| Asignada / En camino | `Reassign` | Asignada | Motivo obligatorio; repartidor distinto, en turno y no saturado. Se cierra la asignación anterior |
| Asignada | `StartRoute` | En camino | — |
| En camino | `Complete` | Entregada | Evidencia de recepción obligatoria. Libera al repartidor |
| En camino | `Fail` | Fallida | Motivo obligatorio. Libera al repartidor |
| Fallida | `ScheduleRetry` | Pendiente de asignación | — |
| Pendiente / Asignada | `Cancel` | Cancelada | Motivo obligatorio |
| Entregada / Cancelada | *(cualquiera)* | — | **Estados finales: no cambian** |

Cualquier otra transición se rechaza con el código `ESTADO_NO_PERMITE_OPERACION`.

> **¿Hace falta una máquina de estados en el front-end (video 25)?** No por ahora. Los estados de este flujo viven en el **dominio** y ya están protegidos por la entidad. Una máquina de estados de interfaz tendría sentido en la Actividad 4, si la app del repartidor necesita controlar pantallas (por ejemplo, "escaneando evidencia").

Código: [`src/Logistica.Entregas.Domain/Delivery.cs`](../../src/Logistica.Entregas.Domain/Delivery.cs) y [`ValueObjects.cs`](../../src/Logistica.Entregas.Domain/ValueObjects.cs).
