# 3. Riesgos operativos y de seguridad

Se partió de un **pre-mortem** (videos 11 y 12): *"Es la noche de la tormenta y el flujo de asignación falló. ¿Qué pasó?"*. Cada causa probable se convirtió en un riesgo, cada riesgo en una mitigación, y **cada mitigación tiene una evidencia que se puede verificar**, casi siempre una prueba automática.

## Riesgos operativos

| ID | Riesgo (pre-mortem) | Impacto | Mitigación implementada | Evidencia de verificación |
|---|---|:-:|---|---|
| RO-1 | Dos supervisores asignan la misma entrega al mismo tiempo y queda con dos repartidores | Alto | Invariante I1 en `Delivery` + columna `version` (concurrencia optimista) + índice único `ux_assignments_one_active` | `Repositorio_DosOperacionesSimultaneas_LaSegundaRecibeConflicto`, `BaseDeDatos_RechazaDosAsignacionesActivas_...`, `SegundaAsignacion_Devuelve409...` |
| RO-2 | El servicio de turnos está lento o caído y las solicitudes se quedan colgadas | Alto | Timeout de 2 s por intento, 2 reintentos con espera creciente solo ante fallas transitorias, y respuesta `503` clara. La entrega no cambia | `ServicioQueNoResponde_AgotaReintentos...`, `FallaTransitoria_SeReintentaYSeRecupera`, `ServicioDeTurnosCaido_Devuelve503...` |
| RO-3 | Se asigna la entrega pero el aviso al cliente se pierde (Notificaciones caído) | Medio | **Outbox**: el evento se guarda en la misma transacción. El despachador reintenta en cada pasada | `FallaTemporal_SeRecuperaEnLaSiguientePasada`, `Repositorio_GuardaYReconstruye...` (evento en el outbox) |
| RO-4 | Un mensaje que siempre falla bloquea la cola o se reintenta para siempre | Medio | Después de 5 intentos va a la **cola de mensajes fallidos (DLQ)**. No se reprocesa solo, `/health` muestra `degradado` y se cuenta en `entregas.outbox.dlq` | `NotificacionesCaido_SeReintentaYLuegoVaALaColaDeFallidos` |
| RO-5 | Un cambio de esquema se aplica distinto en cada máquina, o alguien edita una migración ya aplicada | Medio | Migraciones versionadas `V{n}__*.sql` (convención de Flyway), tabla `schema_version` y suma de verificación | `Migraciones_SonReproducibles...`, `Migraciones_DetectanUnaMigracionAplicadaQueFueModificada` |
| RO-6 | Datos corruptos en la base de datos violan una regla al cargarlos | Medio | `Delivery.Restore` revalida I1, y hay restricciones `CHECK` en las tablas (estados, prioridad, ventana) | `Restore_ConDosAsignacionesActivas_SeRechaza` |
| RO-7 | Una falla en producción no se puede investigar | Medio | Id de correlación en logs y respuestas, métricas y trazas (ver doc. 04) | `Errores_IncluyenElIdentificadorDeCorrelacion` |
| RO-8 | Alguien rompe las capas (por ejemplo, el endpoint consulta SQL directamente) | Medio | Pruebas de arquitectura (fitness functions) en el CI | `DependencyRulesTests` (6 reglas) |

## Riesgos de seguridad y privacidad

| ID | Riesgo | Mitigación implementada | Evidencia |
|---|---|---|---|
| RS-1 | Cualquiera puede asignar o reasignar entregas | Clave de API obligatoria (`X-Api-Key`), comparada en tiempo constante. Sin clave configurada se rechaza todo | `SinClaveDeApi_Devuelve401` |
| RS-2 | Secretos en el repositorio | La clave real va por la variable de entorno `Seguridad__ApiKey`. En el repo solo está la clave de **desarrollo local**, marcada como tal | Revisión de `appsettings.json` (clave vacía) |
| RS-3 | **Exposición de la ubicación del repartidor** (caso de la tormenta, Act. 1) | La capa anticorrupción descarta la `ubicacion` que manda el servicio de turnos. Ninguna tabla, log, evento ni respuesta la contiene | `RespuestaValida_SeTraduceACapacidad_IgnorandoLaUbicacion` |
| RS-4 | Datos personales en logs o eventos (dirección, quién recibió) | Los logs, métricas y eventos llevan **solo identificadores y códigos**. La consulta `GET` no devuelve la dirección | `EventoPendiente_SePublicaUnaSolaVez` (sin dirección), `FlujoCompleto_...` (sin `address`) |
| RS-5 | Inyección SQL | Todas las consultas usan parámetros (`$id`, `$status`…), nunca concatenación con datos del usuario | Revisión de código en `SqliteDeliveryRepository` |
| RS-6 | Entradas maliciosas o enormes | Validación de formato en los DTOs (Guid no vacío, dirección ≤ 300, motivo ≤ 200). El id de correlación solo acepta `[A-Za-z0-9-_]`, máx. 64 caracteres | `SolicitudInvalida_Devuelve400...` |
| RS-7 | Filtración de detalles internos en los errores | `UseExceptionHandler` + `ProblemDetails` sin traza | Respuestas de error en las pruebas de la API |
| RS-8 | **Dependencias con vulnerabilidades conocidas** | Revisión SCA en el CI (`dotnet list package --vulnerable`). Al construir se detectó **GHSA-2m69-gcr7-jv3q (alta)** en `SQLitePCLRaw.lib.e_sqlite3 2.1.11`, y se corrigió fijando `SQLitePCLRaw.bundle_e_sqlite3 2.1.14` | Compilación con **0 advertencias** y el paso "Revisar dependencias vulnerables" del CI |

> **Sobre SAST, DAST y pentesting (video 26).** En esta entrega hay **SCA** (análisis de dependencias) automático y una **revisión manual** de seguridad del código (tabla anterior). No se hizo DAST ni pentesting; quedan como próximos pasos en el doc. 05.
