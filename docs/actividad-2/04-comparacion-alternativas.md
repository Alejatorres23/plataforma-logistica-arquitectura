# 4. Comparación de alternativas estructurales

Se comparan **tres** alternativas (la guía pide al menos dos). Se incluye la C porque es lo que suele salir "por defecto" cuando no se toma una decisión explícita.

## 4.1 Las alternativas

### A · Monolito modular con límites de contexto claros

Una sola aplicación desplegable, organizada en **módulos por contexto de negocio**: Pedidos, Inventario, Ruteo, Entregas, más los de soporte (Notificaciones, Auditoría, Identidad).

- Cada módulo tiene **su propio modelo y vocabulario**. "Disponible" significa algo distinto en Inventario (stock reservable), en Ruteo (ruta viable) y en Entregas (repartidor libre).
- Los módulos se comunican **solo por contratos internos** (interfaces como `IInventoryAvailability` o `IRouteEstimator`), **nunca leyendo tablas ajenas**.
- Los proveedores externos quedan detrás de **puertos y adaptadores**: mapas, identidad y notificaciones.
- La API pública está **versionada** (`/api/v1`). Los cambios compatibles son aditivos y los incompatibles van a `/v2`.
- Para los picos, Ruteo usa **caché de estimaciones** y una **cola de trabajo** dentro del mismo proceso, y la aplicación se escala horizontalmente.

### B · Microservicios por dominio

Cada contexto es un **servicio independiente**, con su propia base de datos, su despliegue y su pipeline. La comunicación es por HTTP y eventos, a través de un API Gateway.

- Ruteo escala solo, sin afectar a Pedidos.
- A cambio, aparecen llamadas por red, consistencia eventual, monitoreo distribuido, gobierno de contratos y varias bases de datos que operar.

### C · Monolito tradicional por capas (sin módulos de dominio)

Una aplicación con capas técnicas: controladores → servicios → repositorios. Un `OrderService` concentra la validación, el inventario, las rutas, el guardado y los correos.

- Es lo más rápido para arrancar.
- Pero termina en lo que mostró el video 05: **baja cohesión y alto acoplamiento**. Un cambio en rutas puede romper las notificaciones o el guardado del pedido.

## 4.2 Matriz de evaluación ponderada

Escala de **1 (peor)** a **5 (mejor)** para el caso. En complejidad, costo y riesgo operativo, **5 = menos complejo, más barato o menos riesgoso**.

| Criterio | Peso | A · Monolito modular | B · Microservicios | C · Capas sin módulos |
|---|:-:|:-:|:-:|:-:|
| Complejidad (5 = baja) | 15 % | 4 | 1 | 5 |
| Costo de infraestructura y operación (5 = bajo) | 15 % | 5 | 1 | 5 |
| Rapidez de implementación del MVP | 15 % | 4 | 2 | 5 |
| Facilidad de evolución / cambio | 15 % | 4 | 4 | 1 |
| Riesgo operativo (5 = bajo) | 15 % | 4 | 2 | 3 |
| Alineación con el negocio (drivers del doc. 2) | 15 % | 5 | 3 | 2 |
| Escalabilidad en picos de campaña | 10 % | 3 | 5 | 2 |
| **Total ponderado** | **100 %** | **4,20** | **2,45** | **3,35** |

**Cálculo de A:** 0,15 × (4 + 5 + 4 + 4 + 4 + 5) + 0,10 × 3 = 3,90 + 0,30 = **4,20**.

## 4.3 Justificación de cada calificación

| Criterio | A · Monolito modular | B · Microservicios | C · Capas sin módulos |
|---|---|---|---|
| Complejidad | Un despliegue y una base de datos, pero exige disciplina de módulos | Red, gateway, varias bases de datos, consistencia eventual | Mínima al inicio |
| Costo | Una app escalada horizontalmente y una base de datos | Varios servicios, bases de datos y monitoreo distribuido (el video 07 cuenta un caso real: la factura pasó de unos 200 USD a miles al mes) | Igual que A |
| Rapidez | Rápido; los límites cuestan algo de diseño al inicio | Lento: el video 07 menciona un trabajo de 3 meses que tomó 7 | El más rápido |
| Evolución | Contratos internos: se puede extraer un módulo cuando haya evidencia | Muy buena por servicio, pero cambiar un contrato entre servicios es caro | Mala: todo está acoplado en `OrderService` |
| Riesgo operativo | Pocas piezas móviles; el riesgo es que se pierdan los límites (se mitiga con pruebas de arquitectura) | Fallas parciales, timeouts entre servicios y despliegues coordinados con un equipo pequeño | Un cambio en rutas puede tumbar la creación de pedidos |
| Alineación con negocio | Responde a los 5 drivers sin sobrecosto | Responde muy bien al pico de ruteo, pero sobrediseña Pedidos e Inventario, que **no** están saturados | No protege la privacidad ni los contratos de forma estructural |
| Escalabilidad en picos | Se escala toda la app; Ruteo se alivia con caché y cola | Ruteo escala de forma independiente | Se escala todo, sin aislar el cuello de botella |

## 4.4 Cómo responde cada alternativa a los drivers

| Driver (doc. 2) | A | B | C |
|---|---|---|---|
| 1. Contratos que no se rompen (RF-02, RNF-01) | ✅ API versionada; el controlador no tiene reglas de negocio | ✅ Igual, pero con más contratos que gobernar | ⚠️ Posible, pero el contrato termina pegado a la entidad interna |
| 2. Confirmar con stock y ruta (RF-03) | ✅ Llamada en proceso, transacción local | ⚠️ Dos llamadas remotas, consistencia eventual | ⚠️ Funciona, pero todo vive en una sola clase |
| 3. Picos de ruteo (RNF-03, RNF-08) | ⚠️ Caché + cola + escalado horizontal; **plan de extracción si hay evidencia** | ✅ Escalado independiente | ❌ Ruteo frena la creación de pedidos |
| 4. Contingencia con privacidad (RF-07, RF-09) | ✅ Política de autorización y auditoría centralizadas | ✅ Posible, pero repartida en varios servicios | ⚠️ Se puede, sin límites que la protejan |
| 5. Costo y equipo pequeño (RNF-11, R-02) | ✅ | ❌ | ✅ |

## 4.5 Resultado

Gana la **alternativa A (Monolito modular)**, con **4,20** puntos.

- **B** solo gana en "escalabilidad en picos", y el problema de picos está concentrado en **un único contexto: Ruteo**. Separar los cuatro contextos agregaría llamadas remotas y coordinación **sin resolver el cuello de botella**.
- **C** es rápida hoy, pero cada cambio futuro sale caro y riesgoso.

La decisión, sus trade-offs y la condición para extraer Ruteo están en el **[ADR-001](../adr/ADR-001-monolito-modular.md)**.
