# 5. Resultados, riesgos residuales y próximos pasos

## Comprobación automatizada de calidad y arquitectura

El flujo [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) se ejecuta en cada *push* y *pull request* a `main`:

| Paso | Qué comprueba | Falla si… |
|---|---|---|
| Compilar (Release) | Que todo compile | Hay errores de compilación |
| **Revisar dependencias vulnerables (SCA)** | `dotnet list package --vulnerable --include-transitive` | Hay alguna vulnerabilidad **alta o crítica** |
| **Pruebas** | Unitarias, de integración y **de arquitectura** (fitness functions, video 27) | Falla cualquier prueba, por ejemplo si el dominio llega a referenciar SQLite o ASP.NET |
| Publicar resultados | Archivo `.trx` con el resultado de cada prueba | — (se publica siempre, como evidencia) |

### Fitness functions (pruebas de arquitectura)

| Regla | Prueba |
|---|---|
| El dominio no depende de ninguna otra capa | `Dominio_NoDependeDeNingunaOtraCapa` |
| La aplicación solo depende del dominio | `Aplicacion_SoloDependeDelDominio` |
| La infraestructura no depende de la entrada (API) | `Infraestructura_NoDependeDeLaCapaDeEntrada` |
| Cada puerto tiene al menos un adaptador | `Infraestructura_ImplementaLosPuertosDeLaAplicacion` |
| Dominio y aplicación no usan BD, HTTP, ASP.NET ni archivos | `DominioYAplicacion_NoUsanTecnologiasDeInfraestructura` |
| Los endpoints no reciben conexiones SQLite (usan casos de uso) | `Endpoints_NoAccedenALaBaseDeDatosDirectamente` |

### Métricas de la arquitectura (video 13)

| Métrica | Valor | Lectura |
|---|---|---|
| Dependencias de proyecto del **dominio** | **0** | Núcleo totalmente aislado |
| Paquetes externos en dominio + aplicación | **0** | El negocio no depende de ninguna tecnología |
| Puertos / adaptadores reales / adaptadores de prueba | 3 / 3 / 2 | Cada dependencia externa se puede cambiar |
| Invariantes con prueba automática | **7 de 7** | Todas las reglas críticas están comprobadas |
| Pruebas en total | ver [doc. 02](02-pruebas-y-evidencia.md) | Unitarias + integración + arquitectura |

### Resultado que dejó el análisis de dependencias

Al compilar se detectó **GHSA-2m69-gcr7-jv3q** (severidad alta) en `SQLitePCLRaw.lib.e_sqlite3 2.1.11`, que llega por defecto con `Microsoft.Data.Sqlite`. Se corrigió fijando `SQLitePCLRaw.bundle_e_sqlite3 2.1.14`, y la compilación quedó con **0 advertencias**. Es el ejemplo de por qué el paso SCA está en el CI.

## Patrones de los videos: cuáles se aplicaron y cuáles no

| Patrón / técnica | ¿Se aplicó? | Justificación |
|---|:-:|---|
| Puertos y adaptadores, DDD | ✅ | Base del diseño (ADR-002) |
| Migraciones versionadas (Flyway, video 15) | ✅ | `V1__crear_entregas.sql`, `V2__crear_outbox.sql`, `schema_version` |
| Mensajes vs. eventos (video 19) | ✅ | `RepartidorAsignado` y `EntregaReasignada` son **eventos** (hechos), no comandos |
| Productor-consumidor + **Dead Letter Queue** (videos 20 y 21) | ✅ | Outbox → despachador → bus, con DLQ después de 5 intentos |
| Durable State (video 24) | ✅ | Estado actual + historial de asignaciones; ver ADR-003 sobre Event Sourcing |
| Pre-mortem → pruebas (videos 11 y 12) | ✅ | [doc. 03](03-riesgos-y-mitigaciones.md): cada riesgo tiene su prueba |
| Fitness functions (video 27) | ✅ | 6 pruebas de arquitectura en el CI |
| Observabilidad + caos controlado (video 28) | ✅ (básico) | Métricas, trazas, logs con correlación, simulador de fallas |
| SAST/DAST (video 26) | ⚠️ Parcial | SCA automático + revisión manual; sin DAST ni pentest |
| Trunk-based development (video 4) | ⚠️ Parcial | Commits pequeños a `main` con CI. Faltan los *rulesets* de protección de rama en GitHub (lo configura el dueño del repo) |
| Monorepo (video 3) | ✅ (simple) | Todo el código, la documentación y la infraestructura (CI) en un solo repositorio, sin Pantsbuild (un solo lenguaje) |
| Infraestructura como código (video 18) | ⚠️ Parcial | El CI está como código. No hay despliegue a la nube en este alcance |
| Revisión con IA en GitHub (video 8) | ➖ | Opcional; no sustituye las pruebas |
| API Gateway, Strangler Fig, Process Manager, Competing Consumers | ❌ | No aplican al alcance actual (ver ADR-003, sección 3) |
| Máquina de estados en el front-end (video 25) | ❌ | No hay front-end en esta entrega. Los estados viven en el dominio |

## Riesgos residuales

| Riesgo | Por qué sigue abierto | Mitigación futura |
|---|---|---|
| Autenticación con una sola clave compartida, sin roles | La clave de API es el mínimo viable | OAuth2/JWT con roles (supervisor, operador) y auditoría de `X-Usuario` |
| SQLite: un solo escritor y sin alta disponibilidad | Se eligió por reproducibilidad (ADR-003) | Adaptador para SQL Server/PostgreSQL + migraciones equivalentes |
| Entrega de eventos "al menos una vez" (posibles duplicados) | Propio del patrón outbox | Consumidores idempotentes, usando el `id` del mensaje |
| Mensajes en la DLQ que nadie atiende | No hay una herramienta para reprocesarlos | Endpoint o tarea de reproceso y alerta sobre `entregas.outbox.dlq > 0` |
| La capacidad del repartidor puede cambiar entre la consulta y el guardado | Viene de otro sistema (foto, ADR-002) | Reserva de capacidad en el servicio de turnos o revalidación al confirmar |
| Sin DAST ni pentest | Fuera del alcance de tiempo | OWASP ZAP contra la API en el CI |
| Sin exportador de telemetría configurado | Las señales existen pero no se envían a un backend | Agregar el exportador OTLP de OpenTelemetry (no cambia el código del flujo) |

## Próximos pasos (en orden)

1. Proteger la rama `main` en GitHub con un *ruleset*: el CI debe pasar antes de integrar (trunk-based, video 4).
2. Autenticación con roles (JWT) y registro del usuario real en cada asignación.
3. Adaptador PostgreSQL y prueba de integración con un contenedor (Testcontainers).
4. Exportar las métricas y trazas a un backend OpenTelemetry y crear una alerta de DLQ.
5. Escaneo DAST (OWASP ZAP) en el CI.
6. Implementar los casos de uso restantes del contexto Entregas: iniciar ruta, completar con evidencia y entrega fallida (las reglas ya existen en el dominio).
