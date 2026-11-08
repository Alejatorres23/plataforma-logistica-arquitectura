# 4. Invariantes y escenarios de aceptación

## Invariantes

Una invariante es una regla que **siempre** debe cumplirse. Todas están protegidas **dentro de la entidad `Delivery`**, no en el caso de uso, ni en el controlador, ni en la base de datos. Así, ningún camino puede saltárselas.

| ID | Invariante | Dónde se protege | Error si se viola | Prueba |
|---|---|---|---|---|
| **I1** | Una entrega activa tiene **como máximo un repartidor asignado** | `Delivery.AssignCourier` | `ENTREGA_YA_ASIGNADA` | `AssignCourier_EntregaYaAsignada_SeRechaza` |
| **I2** | Solo se asigna una entrega **pendiente de asignación** | `Delivery.AssignCourier` | `ESTADO_NO_PERMITE_OPERACION` | `AssignCourier_EntregaCancelada_SeRechaza` |
| **I3** | No se asigna a un repartidor **inactivo** ni **saturado** (la "ruta saturada" de la guía) | `Delivery.EnsureCanTakeDelivery` | `REPARTIDOR_NO_DISPONIBLE` / `REPARTIDOR_SATURADO` | `AssignCourier_RepartidorSaturado_SeRechaza`, `AssignCourier_RepartidorInactivo_SeRechaza` |
| **I4** | Reasignar exige **motivo** y un **repartidor distinto**; la asignación anterior queda **cerrada** con ese motivo | `Delivery.Reassign` | `MOTIVO_OBLIGATORIO` / `MISMO_REPARTIDOR` | `Reassign_SinMotivo_SeRechaza`, `Reassign_AlMismoRepartidor_SeRechaza`, `Reassign_PorTormenta_...` |
| **I5** | Una entrega **no se completa sin evidencia** de recepción | `Delivery.Complete` | `EVIDENCIA_OBLIGATORIA` | `Complete_SinEvidencia_SeRechaza` |
| **I6** | Las transiciones siguen el ciclo definido; **Entregada y Cancelada no cambian** | `Delivery.EnsureStatus` | `ESTADO_NO_PERMITE_OPERACION` | `EntregaEntregada_NoPuedeCancelarse` |
| **I7** | La **ventana de entrega** termina después de empezar; la **dirección** no está vacía | Constructores de `DeliveryWindow` / `DeliveryAddress` | `VALOR_INVALIDO` | `DeliveryWindow_FinAntesDelInicio_NoPuedeCrearse` |

> **¿Y la concurrencia?** Si dos supervisores asignan la misma entrega **al mismo tiempo**, I1 se protege en memoria, pero cada uno podría tener su propia copia de la entrega. En la Actividad 4, el adaptador de base de datos agregará **control de concurrencia optimista** (una columna de versión): el segundo en guardar recibirá un conflicto. Esto quedó registrado en el [ADR-002](../adr/ADR-002-dominio-entregas-puertos-adaptadores.md).

## Escenarios de aceptación (Dado-Cuando-Entonces)

Los escenarios completos están en **[`tests/features/asignar-entrega.feature`](../../tests/features/asignar-entrega.feature)**, en Gherkin en español. Cada uno indica la prueba automática que lo comprueba. Resumen:

### Camino feliz

```gherkin
Escenario: Asignar una entrega a una repartidora disponible
  Dado una entrega "E-100" pendiente de asignación
  Y una repartidora "Ana" activa con 2 entregas activas y capacidad máxima de 5
  Cuando el supervisor asigna la entrega "E-100" a "Ana"
  Entonces la entrega queda en estado "Asignada"
  Y "Ana" es la repartidora activa de la entrega
  Y se publica el evento "RepartidorAsignado"
```

### Rechazos (protegen las invariantes)

```gherkin
Escenario: No se puede asignar una entrega que ya tiene repartidor      # I1
  Dado que la entrega "E-100" ya está asignada a "Ana"
  Cuando el supervisor asigna la entrega "E-100" a "Luis"
  Entonces la asignación se rechaza con el código "ENTREGA_YA_ASIGNADA"
  Y "Ana" sigue siendo la repartidora activa

Escenario: No se puede asignar a un repartidor saturado                 # I3
  Dado un repartidor "Carlos" activo con 5 entregas activas y capacidad máxima de 5
  Cuando el supervisor asigna la entrega "E-100" a "Carlos"
  Entonces la asignación se rechaza con el código "REPARTIDOR_SATURADO"
  Y la entrega sigue "PendienteDeAsignacion"

Escenario: Reasignar una entrega por una contingencia                   # I4
  Dado que la entrega "E-100" está asignada a "Ana"
  Cuando el supervisor reasigna la entrega "E-100" a "Luis" con el motivo "Tormenta: vía cerrada"
  Entonces "Luis" es el repartidor activo de la entrega
  Y la asignación de "Ana" queda cerrada con el motivo "Tormenta: vía cerrada"

Escenario: No se puede completar una entrega sin evidencia de recepción # I5
  Dado que la entrega "E-100" está en camino con "Ana"
  Cuando "Ana" intenta completar la entrega sin evidencia
  Entonces la operación se rechaza con el código "EVIDENCIA_OBLIGATORIA"
```

## Cómo se ejecutan las pruebas

```bash
dotnet test LogisticaUltimaMilla.slnx
```

Resultado al cerrar la Actividad 3: **26 pruebas correctas**. Con la Actividad 4 la suite creció a 60 (ver [Actividad 4, doc. 02](../actividad-4/02-pruebas-y-evidencia.md)).

| Grupo | Cantidad | Qué prueba |
|---|:-:|---|
| Dominio | 14 | Invariantes de la entidad `Delivery`, sin ninguna infraestructura |
| Aplicación | 8 | Escenarios del `.feature`, ejecutados con los casos de uso y los adaptadores en memoria |
| Arquitectura | 4 | Que las dependencias apunten hacia adentro y que el dominio no use tipos de infraestructura |
