# language: es
# Escenarios de aceptación del flujo "Asignar una entrega".
# Cada escenario tiene su prueba automática en tests/Logistica.Entregas.Tests (se indica el nombre).

Característica: Asignar un repartidor a una entrega
  Como supervisor de operaciones
  Quiero asignar cada entrega pendiente a un repartidor disponible
  Para cumplir la ventana de entrega prometida al cliente sin sobrecargar a nadie

  Antecedentes:
    Dado una entrega "E-100" del pedido "P-245" pendiente de asignación
    Y una repartidora "Ana" activa con 2 entregas activas y capacidad máxima de 5

  # Prueba: AssignCourier_RepartidorDisponible_QuedaAsignadaYSePublicaEvento
  Escenario: Asignar una entrega a una repartidora disponible
    Cuando el supervisor asigna la entrega "E-100" a "Ana"
    Entonces la entrega queda en estado "Asignada"
    Y "Ana" es la repartidora activa de la entrega
    Y se publica el evento "RepartidorAsignado"

  # Prueba: AssignCourier_EntregaYaAsignada_SeRechaza  (invariante I1)
  Escenario: No se puede asignar una entrega que ya tiene repartidor
    Dado que la entrega "E-100" ya está asignada a "Ana"
    Y un repartidor "Luis" activo con 0 entregas activas
    Cuando el supervisor asigna la entrega "E-100" a "Luis"
    Entonces la asignación se rechaza con el código "ENTREGA_YA_ASIGNADA"
    Y "Ana" sigue siendo la repartidora activa
    Y no se publica ningún evento nuevo

  # Prueba: AssignCourier_RepartidorSaturado_SeRechaza  (invariante I3)
  Escenario: No se puede asignar a un repartidor saturado
    Dado un repartidor "Carlos" activo con 5 entregas activas y capacidad máxima de 5
    Cuando el supervisor asigna la entrega "E-100" a "Carlos"
    Entonces la asignación se rechaza con el código "REPARTIDOR_SATURADO"
    Y la entrega sigue "PendienteDeAsignacion"

  # Prueba: AssignCourier_RepartidorInactivo_SeRechaza  (invariante I3)
  Escenario: No se puede asignar a un repartidor que no está activo
    Dado un repartidor "Pedro" inactivo
    Cuando el supervisor asigna la entrega "E-100" a "Pedro"
    Entonces la asignación se rechaza con el código "REPARTIDOR_NO_DISPONIBLE"

  # Prueba: AssignCourier_EntregaInexistente_SeRechaza
  Escenario: La entrega no existe
    Cuando el supervisor asigna la entrega "E-999" a "Ana"
    Entonces la asignación se rechaza con el código "ENTREGA_NO_ENCONTRADA"

  # Prueba: Reassign_PorTormenta_CierraAsignacionAnteriorYRegistraMotivo  (invariante I4)
  Escenario: Reasignar una entrega por una contingencia
    Dado que la entrega "E-100" está asignada a "Ana"
    Y un repartidor "Luis" activo con 1 entrega activa y capacidad máxima de 5
    Cuando el supervisor reasigna la entrega "E-100" a "Luis" con el motivo "Tormenta: vía cerrada"
    Entonces "Luis" es el repartidor activo de la entrega
    Y la asignación de "Ana" queda cerrada con el motivo "Tormenta: vía cerrada"
    Y se publica el evento "EntregaReasignada"

  # Prueba: Reassign_SinMotivo_SeRechaza  (invariante I4)
  Escenario: No se puede reasignar sin motivo
    Dado que la entrega "E-100" está asignada a "Ana"
    Cuando el supervisor reasigna la entrega "E-100" a "Luis" sin motivo
    Entonces la reasignación se rechaza con el código "MOTIVO_OBLIGATORIO"

  # Prueba: Complete_SinEvidencia_SeRechaza  (invariante I5)
  Escenario: No se puede completar una entrega sin evidencia de recepción
    Dado que la entrega "E-100" está en camino con "Ana"
    Cuando "Ana" intenta completar la entrega sin evidencia
    Entonces la operación se rechaza con el código "EVIDENCIA_OBLIGATORIA"
    Y la entrega sigue "EnCamino"
