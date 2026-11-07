# ADR-002 · Dominio rico en Entregas, tres capas y puertos y adaptadores

| Campo | Valor |
|---|---|
| **Estado** | Aceptada |
| **Fecha** | 2026-11-07 |
| **Actividad** | Actividad 3 – Diseño y dominio |
| **Relacionada con** | [ADR-001](ADR-001-monolito-modular.md) (monolito modular con contextos separados) |

## 1. Contexto

ADR-001 definió un monolito modular con los contextos Pedidos, Inventario, Ruteo y Entregas. Ahora hay que diseñar **por dentro** el contexto **Entregas** y su flujo principal: **asignar un repartidor a una entrega**. Las condiciones son:

- **Hay reglas críticas que no pueden saltarse:**
  - una entrega activa tiene un solo repartidor;
  - no se asigna a un repartidor saturado;
  - reasignar exige un motivo;
  - no se completa una entrega sin evidencia.

  Si se violan, aparecen dobles asignaciones, entregas sin dueño y pérdida de trazabilidad (riesgos RG-03, RG-04 y RG-08).
- **La persistencia y las integraciones reales llegan en la Actividad 4.** El diseño debe permitir cambiarlas **sin tocar las reglas**.
- **La disponibilidad del repartidor viene de otro sistema** (turnos / app del repartidor), que puede cambiar o fallar.
- **La ubicación del repartidor no debe entrar** a este contexto (privacidad, Actividad 1).

## 2. Decisiones

**D1. Dominio rico.** Las invariantes viven **dentro de la entidad `Delivery`** (raíz del agregado), en métodos con intención: `AssignCourier`, `Reassign`, `Complete`… No hay setters públicos, y `Assignment` solo se modifica a través de `Delivery`.

**D2. Tres capas con dependencias hacia adentro.** Dominio ← Aplicación ← Infraestructura, en **tres proyectos separados**. El compilador impide que el dominio use la infraestructura, y las **pruebas de arquitectura** lo verifican.

**D3. Puertos y adaptadores.** La aplicación define las interfaces que necesita, en el lenguaje del negocio:
- `IDeliveryRepository`
- `ICourierAvailability`
- `IDomainEventPublisher`
- `IClock`

La infraestructura las implementa. En esta actividad los adaptadores son **en memoria**.

**D4. La capacidad del repartidor entra como objeto de valor.** El caso de uso obtiene una **foto** (`CourierCapacity`: activo, carga y máximo) a través del puerto, y **la entidad decide** con ella. La entidad no consulta servicios.

**D5. Rechazos esperados como resultado, no como excepción hacia afuera.**
- El dominio lanza `DomainException` con un **código**.
- El caso de uso la convierte en `AssignmentResult.Fail(código)`.
- El adaptador de entrada (HTTP en la Actividad 4) traduce ese código a `409` / `422`, sin leer mensajes.

**D6. Los resultados se comunican con eventos de dominio.** `CourierAssigned` y `DeliveryReassigned` se publican después de guardar. Entregas no sabe quién los consume (Notificaciones, Auditoría).

## 3. Alternativas consideradas

| # | Alternativa | Por qué se descartó |
|---|---|---|
| A | **Modelo anémico + servicio:** `Delivery` con propiedades públicas y las reglas en un `DeliveryService` o en el controlador | Las reglas quedan dispersas: cualquier código puede hacer `delivery.CourierId = x` y saltarse la invariante I1. Es el `OrderService` "que lo sabe todo" del video 05 |
| B | **Reglas en la base de datos** (triggers / restricciones como única protección) | Ata el negocio a una tecnología. No se puede probar sin una BD real y no explica el *porqué* del rechazo. Una restricción única **sí** se agregará en la Actividad 4, pero como **segunda línea de defensa**, no como la regla principal |
| C | **Entidad `Order` compartida** entre Pedidos, Ruteo y Entregas | Mezcla vocabularios ("disponible" significa tres cosas distintas). Un cambio de Inventario rompería Entregas (videos 06 y 12.1) |
| D | **Llamar al servicio de turnos desde la entidad** (`delivery.AssignCourier(courierId)` consultando por dentro) | El dominio dependería de infraestructura y las pruebas necesitarían el servicio real. Por eso la firma es `AssignCourier(CourierCapacity, at)` y no solo `courierId`: es un cambio deliberado respecto al ejemplo de la guía |
| E | **Dos capas** (dominio + todo lo demás junto) | Mezcla los casos de uso con los detalles técnicos. Cambiar la base de datos obligaría a tocar el flujo |
| F | **Errores esperados como excepciones** hasta el controlador | Mezcla flujo normal ("repartidor saturado" es un resultado de negocio esperable) con fallas reales. Complica las pruebas y los códigos HTTP |

## 4. Trade-offs

| Ganamos | Aceptamos |
|---|---|
| Las reglas están en **un solo lugar**, se pueden leer y se prueban sin infraestructura (14 pruebas de dominio, en milisegundos) | **Más archivos y tipos**: objetos de valor, puertos, contratos, resultados |
| Se puede cambiar la base de datos o el proveedor de turnos **sin tocar** el dominio ni los casos de uso | **Mapeo extra** en la Actividad 4: de la entidad a tablas, y de HTTP a comandos |
| Las capas separadas en proyectos hacen que **el compilador impida** las dependencias incorrectas | Una curva de aprendizaje para el equipo (DDD, puertos) |
| La foto de capacidad mantiene el dominio puro | **La foto puede quedar desactualizada** entre la consulta y el guardado. Se mitiga con concurrencia optimista en la Actividad 4 y con revalidación |
| Los resultados con código facilitan la API y el soporte | Hay que mantener un **catálogo de códigos** estable (`DomainErrors`, `ApplicationErrors`) |

## 5. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Dos asignaciones concurrentes sobre la misma entrega (cada una con su copia) | Actividad 4: columna de versión en el repositorio SQL + restricción única "una asignación activa por entrega" |
| La capacidad cambia entre la consulta y el guardado (dos entregas al mismo repartidor a la vez) | Actividad 4: contador de carga con concurrencia optimista en el servicio de disponibilidad. Se acepta un margen mínimo mientras tanto |
| Se publica un evento pero falla el guardado (o al revés) | Actividad 4: patrón outbox (evento y entrega en la misma transacción) |
| Alguien agrega lógica de negocio en el caso de uso o en el adaptador | Revisión de código + este ADR + pruebas de arquitectura |

## 6. Cómo verificamos la decisión

- `dotnet test`: **26 pruebas correctas**.
  - Las **invariantes I1–I7** tienen al menos una prueba de dominio cada una.
  - Los **escenarios Dado-Cuando-Entonces** del `.feature` tienen su prueba con los casos de uso y los adaptadores en memoria.
  - Las **pruebas de arquitectura** fallan si el dominio referencia otra capa o tipos de BD/HTTP/archivos.
- **Prueba de cambio** (video 06): cambiar la regla de saturación (p. ej., por peso en vez de cantidad) debe tocar **solo** `CourierCapacity` y su adaptador, no el caso de uso.

## 7. Condición de revisión

Se revisa esta decisión si:
- el flujo de asignación pasa a ser **automático**, con optimización de rutas (puede necesitar un servicio de dominio de "selección de repartidor"); o
- Entregas se **extrae como servicio independiente** según las señales del ADR-001. Los puertos ya están listos para eso.
