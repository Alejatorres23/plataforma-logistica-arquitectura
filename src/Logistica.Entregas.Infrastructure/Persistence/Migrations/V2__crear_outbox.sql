-- V2: bandeja de salida (outbox) de eventos de dominio.
-- Los eventos se guardan en la misma transacción que la entrega y un proceso aparte los publica.
-- Si un mensaje falla demasiadas veces queda marcado como "dead letter" (cola de mensajes fallidos).

CREATE TABLE outbox_messages (
    id                TEXT    NOT NULL PRIMARY KEY,
    delivery_id       TEXT    NOT NULL,
    event_type        TEXT    NOT NULL,
    payload           TEXT    NOT NULL,
    occurred_at       TEXT    NOT NULL,
    attempts          INTEGER NOT NULL DEFAULT 0,
    last_error        TEXT    NULL,
    processed_at      TEXT    NULL,
    dead_lettered_at  TEXT    NULL
);

CREATE INDEX ix_outbox_pending
    ON outbox_messages (occurred_at)
    WHERE processed_at IS NULL AND dead_lettered_at IS NULL;
