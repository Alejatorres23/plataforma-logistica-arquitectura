# Caso: reasignar 400 entregas durante una tormenta

Este es el caso que plantea el docente en el video 04.1. Se resuelve **sin empezar por una tecnología**.

## 1. El problema

Es viernes a las 7:00 p. m. Una tormenta bloquea varias vías y hay que **reasignar 400 entregas**. Operaciones pide:
- la **ubicación GPS en tiempo real** de todos los repartidores;
- su **historial completo de trayectos**;
- que **cualquier supervisor** tenga acceso.

El objetivo (reducir retrasos) es válido. Pero la propuesta expone **más información de la necesaria** y puede afectar la **seguridad personal** de los repartidores.

## 2. Actores y necesidades

| Actor | Qué necesita |
|---|---|
| **Cliente** | Saber si su pedido llegará y recibir una explicación confiable si cambia la promesa |
| **Repartidor** | Una ruta y una asignación justas, **sin** que su ubicación ni su historial queden a la vista de quien no lo necesita |
| **Supervisor / operador logístico** | Reasignar rápido, con la información **suficiente** para decidir |
| **Soporte** | Explicarle al cliente qué pasó y poder auditar |
| **Equipo técnico** | Que el sistema aguante el pico de reasignaciones sin abrir los datos |

## 3. Restricciones

- La ubicación de una persona es un **dato personal** (Ley 1581 de 2012): requiere una finalidad y un acceso controlado.
- El proveedor de GPS **puede responder lento** justo en la emergencia.
- La decisión debe tomarse **en minutos**, no en días.

## 4. Alcance del caso

| Dentro | Fuera |
|---|---|
| Reasignación de entregas | Proveedor de GPS y mapas |
| Autorización por rol | Red móvil |
| Auditoría de consultas | Proveedor de identidad externo |
| Notificaciones al cliente | |
| Consulta mínima de la ubicación | |

## 5. Alternativas

| Criterio | A · Compartir toda la información | B · Datos mínimos, temporales y auditados |
|---|---|---|
| Qué hace | GPS en tiempo real + historial completo, visible para todos los supervisores | Ubicación **solo de repartidores con entrega activa o candidatos**, con **precisión reducida** si no se necesita exacta, **solo para supervisores a cargo**, por **tiempo limitado**, y cada consulta **queda registrada**. El historial detallado se **elimina** al terminar la ventana operativa |
| Costo inicial | Bajo: es abrir un reporte | Medio: roles, auditoría y regla de retención |
| Calidad / rapidez para reasignar | Alta | Alta: el supervisor tiene lo que necesita para decidir |
| Riesgo | **Alto:** exceso de datos personales, mayor superficie de ataque, posible uso indebido (acoso), no se sabe quién consultó | **Bajo:** acceso limitado y trazable |
| Facilidad de cambio | Difícil "cerrar" después lo que ya se abrió | Las reglas se ajustan sin rediseñar |

## 6. Decisión

**Elegimos la alternativa B.** La reasignación se hace con un **módulo de reasignación** dentro de la plataforma central, con estas reglas:

- **autorización por rol** (supervisor a cargo de la zona o de las entregas);
- **ubicación temporal** (solo durante la entrega activa y la ventana de contingencia, máximo 2 horas);
- **auditoría de cada consulta** (quién, para qué pedido, a qué hora y con qué resultado).

**No creamos microservicios todavía:** el problema es de reglas y de datos, no de escala independiente.

### Trade-off declarado

> **Aceptamos un costo inicial mayor** (construir roles, auditoría y retención) **y un paso extra** para el supervisor (solicitar el acceso a la zona), **a cambio de** proteger la seguridad y la privacidad del repartidor y de poder demostrar quién vio qué.

## 7. Justificación

- La decisión **reduce la complejidad operativa**: un solo sistema, sin nuevos servicios que operar.
- **Permite validar el problema** antes de distribuir la solución.
- Le da a operaciones lo que necesita para reasignar, **sin** convertir al repartidor en una fuente de vigilancia permanente.
- Se revisará si el volumen, el tamaño del equipo o la necesidad de escalar de forma independiente justifican otra estructura.

## 8. Cómo comprobaremos que la decisión funciona

| Verificación | Criterio de éxito |
|---|---|
| Prueba de acceso **permitido**: un supervisor a cargo consulta la ubicación de un repartidor con entrega activa | Ve la ubicación actual (no el historial) y la consulta queda auditada |
| Prueba de acceso **denegado**: un usuario sin rol, o un supervisor de otra zona | Recibe un rechazo y el intento también queda auditado |
| Métrica: **% de consultas de ubicación auditadas** | 100 % |
| Métrica: **% de ubicaciones detalladas eliminadas** al terminar la ventana | 100 % |
| Simulacro de reasignación de 400 entregas | Termina en menos de 10 minutos y los clientes reciben el aviso |

Si se pueden demostrar estas tres cosas, la decisión cumple:
1. el cliente recibe una actualización útil;
2. el operador puede reasignar;
3. un usuario no autorizado **no puede** consultar ni reconstruir el historial del repartidor.

### Condición que obliga a revisar la decisión

Si en una emergencia real **más del 10 % de las reasignaciones** se retrasan por no tener acceso a tiempo a la ubicación, se revisan las reglas de acceso (por ejemplo, aprobación automática por zona afectada). **No se vuelve a abrir el historial completo.**
