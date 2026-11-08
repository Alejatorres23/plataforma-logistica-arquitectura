-- V1: tablas del contexto Entregas.
-- Las fechas se guardan en UTC con formato ISO-8601 (ordenables como texto).

CREATE TABLE deliveries (
    id                 TEXT    NOT NULL PRIMARY KEY,
    order_id           TEXT    NOT NULL,
    address            TEXT    NOT NULL,
    window_start       TEXT    NOT NULL,
    window_end         TEXT    NOT NULL,
    priority           TEXT    NOT NULL CHECK (priority IN ('Normal', 'Urgente')),
    status             TEXT    NOT NULL CHECK (status IN ('PendienteDeAsignacion', 'Asignada', 'EnCamino', 'Entregada', 'Fallida', 'Cancelada')),
    proof_received_by  TEXT    NULL,
    proof_received_at  TEXT    NULL,
    version            INTEGER NOT NULL,          -- control de concurrencia optimista
    created_at         TEXT    NOT NULL,
    updated_at         TEXT    NOT NULL,
    CHECK (window_end > window_start)
);

CREATE INDEX ix_deliveries_status ON deliveries (status);

CREATE TABLE delivery_assignments (
    delivery_id     TEXT    NOT NULL REFERENCES deliveries (id) ON DELETE CASCADE,
    sequence        INTEGER NOT NULL,
    courier_id      TEXT    NOT NULL,
    assigned_at     TEXT    NOT NULL,
    released_at     TEXT    NULL,
    release_reason  TEXT    NULL,
    PRIMARY KEY (delivery_id, sequence)
);

-- Segunda línea de defensa de la invariante I1 (la primera es la entidad Delivery):
-- una entrega no puede tener dos asignaciones activas al mismo tiempo.
CREATE UNIQUE INDEX ux_assignments_one_active
    ON delivery_assignments (delivery_id)
    WHERE released_at IS NULL;

CREATE INDEX ix_assignments_courier_active
    ON delivery_assignments (courier_id)
    WHERE released_at IS NULL;
