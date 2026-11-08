# ADR-003 · Implementación: SQLite con migraciones, outbox con DLQ y adaptador HTTP resiliente

| Campo | Valor |
|---|---|
| **Estado** | Aceptada |
| **Fecha** | 2026-11-07 |
| **Actividad** | Actividad 4 – Construcción, validación y defensa final |
| **Relacionada con** | [ADR-001](ADR-001-monolito-modular.md), [ADR-002](ADR-002-dominio-entregas-puertos-adaptadores.md) |

## 1. Contexto

Hay que convertir el diseño de la Actividad 3 en un **vertical slice ejecutable**: entrada, caso de uso, dominio, persistencia e integración. Las condiciones son:
- debe correr en cualquier computador **sin instalar un servidor de base de datos**;
- el esquema debe ser **reproducible**;
- el ADR-002 dejó tres riesgos abiertos: **concurrencia**, **eventos perdidos** y **servicio de turnos que falla**.

## 2. Decisiones

**D1. Persistencia en SQLite, con SQL escrito a mano y detrás del puerto `IDeliveryRepository`.**
SQLite es un archivo y no hay que instalar nada, así que cualquiera ejecuta `dotnet run` y funciona. Como el repositorio es un adaptador, **pasar a SQL Server o PostgreSQL en producción** solo exige otro adaptador y migraciones equivalentes. El dominio y los casos de uso no cambian.

**D2. Migraciones versionadas con la convención de Flyway** (video 15): archivos `V{n}__{descripcion}.sql` embebidos, tabla `schema_version` y suma de verificación. Se aplican al iniciar la API. No se usa Flyway, porque requeriría Java, ni EF Core Migrations, porque no hay ORM. Se replica su disciplina con unas 80 líneas de código (`SqliteMigrator`).

**D3. Concurrencia optimista** con una columna `version`. La versión leída se guarda **en el repositorio** (`ConditionalWeakTable`), no en la entidad. Además, un índice único parcial (`ux_assignments_one_active`) actúa como segunda línea de defensa de I1.

**D4. Patrón outbox para los eventos.**
- **Cambia la firma del puerto:** `SaveAsync(delivery, events)` reemplaza a `IDomainEventPublisher`. La entrega y sus eventos se guardan **en la misma transacción**.
- **Un despachador en segundo plano** publica los pendientes.
- **Si un mensaje falla**, se reintenta en la siguiente pasada.
- **Después de 5 intentos** se marca como **dead letter** (DLQ, video 21). Queda para revisión manual y no bloquea a los demás.

**D5. Adaptador HTTP resiliente** para el servicio de turnos:
- timeout de 2 s **por intento**;
- 2 reintentos con espera creciente (200 ms, 400 ms), **solo ante fallas transitorias** (5xx, 408, 429, red, timeout);
- un 4xx o una respuesta mal formada no se reintentan;
- si se agotan los intentos, se devuelve `503 SERVICIO_REPARTIDORES_NO_DISPONIBLE` y la entrega no cambia.

Se implementó a mano en lugar de usar Polly para que la política sea visible y probable en un solo archivo.

**D6. Entrada con Minimal API**, una clave de API por cabecera e identificador de correlación. Los endpoints solo traducen HTTP ↔ contratos.

**D7. Observabilidad con las APIs estándar de .NET** (`Meter`, `ActivitySource`, `ILogger` con *scopes*). Son compatibles con OpenTelemetry sin agregar paquetes. Las señales no llevan datos personales.

## 3. Alternativas consideradas

| Tema | Alternativa descartada | Por qué |
|---|---|---|
| Persistencia | SQL Server o PostgreSQL en Docker | Más realista, pero obliga a instalar Docker o un servidor para revisar la entrega. Queda como próximo paso (cambiar solo el adaptador) |
| Persistencia | EF Core | Agrega un ORM y un mapeo para un agregado pequeño. El SQL explícito muestra mejor la versión optimista y el outbox |
| Estado | **Event Sourcing** (video 24) | Da historial completo, pero agrega mucha complejidad (proyecciones, versiones de eventos). **Durable State** + historial de asignaciones + outbox cubre la trazabilidad que pide el negocio |
| Eventos | Publicar directo al bus después de guardar | Si el bus falla después del guardado, el evento se pierde (riesgo RO-3) |
| Eventos | Broker real (RabbitMQ, Azure Service Bus) | Infraestructura extra para un solo consumidor. El puerto `IEventBus` permite conectarlo después |
| Entrada | API Gateway (video 16) | Con una sola API no aporta. Se reconsidera al extraer servicios (ADR-001) |
| Coordinación | Process manager / saga (video 23) | El flujo de asignar es una sola transacción local; no hay pasos distribuidos que coordinar |
| Migración | Strangler Fig (video 14) | No hay un sistema legado que reemplazar en este alcance |
| Consumo | Competing consumers / fan-out (videos 20 y 22) | Un solo despachador basta con el volumen actual. Si crece, se escalan los consumidores del bus |
| Resiliencia | Polly / `Microsoft.Extensions.Http.Resilience` | Válido. Se prefirió código propio, corto y probado, para que se vea en la defensa. Es candidato a migrar |
| Seguridad | OAuth2 / JWT con roles | Es lo correcto en producción (supervisor vs. operador). La clave de API es el mínimo para esta entrega (riesgo residual) |

## 4. Trade-offs

| Ganamos | Aceptamos |
|---|---|
| Ejecutable en cualquier PC con un comando | SQLite no es la base de datos de producción: un solo escritor a la vez y sin alta disponibilidad |
| Eventos que no se pierden (outbox) | **Entrega "al menos una vez"**: si el despachador cae entre publicar y marcar, el evento puede llegar duplicado. Los consumidores deben ser idempotentes (usar el `id` del mensaje) |
| La API responde rápido aunque Notificaciones esté caído | El aviso al cliente puede llegar **con unos segundos de retraso** (intervalo del despachador) |
| Una falla del servicio de turnos no bloquea la API (timeout y `503`) | En el peor caso la petición tarda unos 6,6 s (3 intentos de 2 s más las esperas) antes del `503` |
| Política de reintentos visible y probada | Código propio que mantener (en lugar de una librería estándar) |
| Dominio intacto (la versión vive en el repositorio) | `ConditionalWeakTable`: el repositorio debe ser *scoped* por solicitud |

## 5. Verificación

Ver [02-pruebas-y-evidencia.md](../actividad-4/02-pruebas-y-evidencia.md):
- **Integración con SQLite real:** migraciones reproducibles, conflicto de concurrencia, índice único.
- **Adaptador HTTP con un servidor falso:** timeout, reintentos, ACL y JSON mal formado.
- **Outbox:** publicación única, reintento y DLQ.
- **API completa** con `WebApplicationFactory`.

## 6. Condición de revisión

Se revisa esta decisión si:
- hay **más de una instancia** de la API (SQLite deja de servir y hay que pasar a SQL Server o PostgreSQL); o
- aparece un **segundo consumidor** de eventos o el volumen supera lo que procesa un solo despachador (pasar a un broker); o
- el negocio exige **auditoría completa de cada cambio** (reconsiderar Event Sourcing para el agregado `Delivery`).
