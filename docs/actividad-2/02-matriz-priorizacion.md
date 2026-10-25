# 2. Matriz de priorización

## 2.1 Cómo se calificó

Cada requisito se califica de **1 (bajo)** a **5 (alto)** en tres ejes:

- **Impacto en el negocio.** Qué pasa si no está: se pierden pedidos o dinero, hay riesgo legal, se pierde la confianza del cliente.
- **Impacto en la arquitectura.** Cuánto condiciona la estructura: obliga a separar módulos, versionar contratos, aislar fallas, etc.
- **Complejidad.** Cuánto esfuerzo y riesgo técnico implica construirlo.

**Puntaje = Impacto en negocio × Impacto en arquitectura** (de 1 a 25). La complejidad **no resta puntos**: sirve para ordenar la construcción cuando dos requisitos empatan y para saber dónde hay más riesgo.

**Prioridad (MoSCoW)** según el puntaje:

| Prioridad | Puntaje |
|---|---|
| **Must** (debe estar) | 16 o más |
| **Should** (debería estar) | 9 a 15 |
| **Could** (podría estar) | 8 o menos |

## 2.2 Requisitos funcionales

| ID | Requisito | Negocio | Arquitectura | Complejidad | Puntaje | Prioridad |
|---|---|:-:|:-:|:-:|:-:|:-:|
| RF-02 | Ventana de entrega opcional sin romper clientes | 5 | 5 | 3 | **25** | Must |
| RF-03 | Confirmar solo con stock **y** ruta viable | 5 | 5 | 3 | **25** | Must |
| RF-01 | Crear pedido (móvil/web) | 5 | 4 | 2 | **20** | Must |
| RF-07 | Reasignación masiva en contingencia | 5 | 4 | 4 | **20** | Must |
| RF-09 | Ubicación del repartidor por rol, temporal y acotada | 4 | 5 | 3 | **20** | Must |
| RF-05 | Estimar ruta con proveedor de mapas | 4 | 5 | 3 | **20** | Must |
| RF-06 | Una entrega activa = un repartidor | 5 | 4 | 3 | **20** | Must |
| RF-04 | Reservar inventario sin exceder lo disponible | 5 | 4 | 2 | **20** | Must |
| RF-10 | Auditoría de ubicación y reasignaciones | 4 | 4 | 2 | **16** | Must |
| RF-11 | Notificar cambios de estado al cliente | 4 | 3 | 2 | 12 | Should |
| RF-08 | Consultar estado y seguimiento del pedido | 4 | 3 | 2 | 12 | Should |
| RF-13 | Autenticación y roles con proveedor externo | 3 | 4 | 2 | 12 | Should |
| RF-12 | Registro de incidencias | 3 | 2 | 2 | 6 | Could |
| RF-14 | Historial de pedidos del cliente | 3 | 2 | 1 | 6 | Could |
| RF-15 | Reportes operativos | 2 | 2 | 2 | 4 | Could |

## 2.3 Atributos de calidad

| ID | Atributo | Negocio | Arquitectura | Complejidad | Puntaje | Prioridad |
|---|---|:-:|:-:|:-:|:-:|:-:|
| RNF-01 | Compatibilidad / versionado de contratos | 5 | 5 | 3 | **25** | Must |
| RNF-03 | Escalabilidad en picos de campaña | 5 | 5 | 4 | **25** | Must |
| RNF-05 | Seguridad y privacidad de la ubicación | 5 | 4 | 3 | **20** | Must |
| RNF-02 | Rendimiento (p95 < 2 s al crear pedido) | 5 | 4 | 3 | **20** | Must |
| RNF-07 | Mantenibilidad / facilidad de cambio | 4 | 5 | 3 | **20** | Must |
| RNF-08 | Resiliencia ante proveedor de mapas | 4 | 5 | 3 | **20** | Must |
| RNF-09 | Consistencia (sin doble asignación) | 5 | 4 | 3 | **20** | Must |
| RNF-06 | Auditabilidad / trazabilidad | 4 | 4 | 2 | **16** | Must |
| RNF-11 | Costo proporcional al equipo | 4 | 4 | 1 | **16** | Must |
| RNF-10 | Observabilidad | 3 | 4 | 3 | 12 | Should |
| RNF-04 | Disponibilidad 99,5 % | 4 | 3 | 3 | 12 | Should |

## 2.4 Requisitos que más pesan en la decisión (drivers arquitectónicos)

Son los requisitos con **puntaje 25**, más los que **más restringen** la elección de estructura:

1. **RF-02 + RNF-01. Contratos que no se rompen.** La app móvil no se puede actualizar a voluntad, así que la API pública necesita versionado y cambios aditivos.
2. **RF-03. Confirmar con stock y ruta.** Cruza tres contextos: Pedidos, Inventario y Ruteo. Hacen falta límites claros y contratos internos.
3. **RNF-03 + RNF-08. Picos de campaña en ruteo.** La carga de ruteo no debe frenar la creación de pedidos. Por eso ruteo es el candidato natural a aislarse.
4. **RF-07 + RF-09 + RNF-05. Contingencia con privacidad.** Hay que reasignar rápido **sin** abrir la ubicación a todos.
5. **RNF-11 + R-02. Costo y equipo pequeño.** Limitan cuánta infraestructura distribuida se puede operar con calidad.

Estos cinco puntos son los criterios con los que se comparan las alternativas en el documento 4.
