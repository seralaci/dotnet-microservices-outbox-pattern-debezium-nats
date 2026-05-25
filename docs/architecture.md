# Architecture details

## Component diagram

```
                                        +-----------------------+
                                        |  Aspire AppHost       |
                                        |  (DCP orchestrator)   |
                                        +----------+------------+
                                                   |
            +-------+-------+-------+--------+-----+-----+-----+-----+
            |       |       |       |        |           |     |     |
            v       v       v       v        v           v     v     v
        +-----+ +------+ +-----+ +-----+ +---------+ +------+ +----+ +---------+
        | PG  | |pgAdm.| |NATS | |Mail-| | Debezium| |Order | |Mig.| | Notifier|
        | 17  | |      | | JS  | | pit | | Server  | | Api  | |Svc | |         |
        +-----+ +------+ +-----+ +-----+ +---------+ +------+ +----+ +---------+
            |                                |          |       |         |
            +----------(logical repl.)-------+          |       |         |
                                                        v       v         |
                                                    +-----------+         |
                                                    | orderdb   |<--------+
                                                    | (Postgres)|
                                                    +-----------+
                                                                          
                       +------> NATS JetStream subject: order.events <----+
```

## Data models

### `orders` table

| Column | Type | Notes |
|--------|------|-------|
| `id` | `uuid` | PK |
| `first_name` | `varchar(200)` | required |
| `last_name` | `varchar(200)` | required |
| `email` | `varchar(320)` | required |
| `order_date` | `timestamptz` | UTC |

### `outbox_messages` table

| Column | Type | Notes |
|--------|------|-------|
| `id` | `uuid` | PK, Debezium event id |
| `aggregate_type` | `varchar(100)` | route key (`order`) |
| `aggregate_id` | `uuid` | aggregate primary key |
| `type` | `varchar(200)` | event type (`OrderCreated`) – becomes a header |
| `payload` | `jsonb` | event payload (expanded) |
| `date_occurred` | `timestamptz` | event timestamp |

The `REPLICA IDENTITY FULL` setting is required for logical replication so the
full row is available to Debezium on DELETE events.

## Debezium Event Router

The `io.debezium.transforms.outbox.EventRouter` transform converts table
columns into NATS message metadata:

| Outbox column | Debezium config | Purpose |
|---------------|-----------------|---------|
| `id` | `table.field.event.id` | message id (for deduplication) |
| `aggregate_id` | `table.field.event.key` | NATS message key |
| `payload` | `table.field.event.payload` | NATS message value |
| `aggregate_type` | `route.by.field` | NATS subject prefix |
| `type` | `fields.additional.placement=type:header:eventType` | `eventType` header |

Subject template: `${routedByValue}.events` → dynamic based on the
`aggregate_type` value.

## NATS JetStream configuration

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Stream name | `outbox-events` | One stream for every outbox-derived subject |
| Subject filter | `*.events` | Wildcard: `order.events`, later `customer.events`, etc. |
| Storage | `File` | Durable storage (survives restarts) |
| Retention | `Limits` + 7-day MaxAge | Time-based expiration |
| DuplicateWindow | 2 minutes | Idempotent redelivery based on Message-Id |

The consumer:

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Name | `notifier-order-events` | Durable, survives restarts |
| Filter | `order.events` | Receives only Order events |
| AckPolicy | `Explicit` | At-least-once guarantee |
| MaxDeliver | 5 | Poison-message limit after NAKs |
| AckWait | 30 sec | Timeout for auto-redelivery |

## Aspire orchestration

| Resource | Start order | Waits for (`WaitFor`) |
|----------|-------------|-----------------------|
| `postgres` | 1. | – |
| `nats` | 1. | – |
| `mailpit` | 1. | – |
| `debezium` | 2. | postgres, nats |
| `order-migrations` | 2. | postgres (orderdb), nats |
| `order-api` | 3. | orderdb + `WaitForCompletion(order-migrations)` |
| `notifier` | 3. | nats + mailpit + `WaitForCompletion(order-migrations)` |

`WaitForCompletion(order-migrations)` guarantees that the API and the Notifier
only start after the publication and the JetStream stream exist.

## Observability (OpenTelemetry)

Every .NET service exports:

- **Traces**: ASP.NET Core, HttpClient, custom ActivitySources
  (`OutboxDemo.Order.MigrationService`, `OutboxDemo.Notifier`)
- **Metrics**: Runtime, ASP.NET Core, HttpClient
- **Logs**: structured JSON, OTLP exporter

The Aspire dashboard receives and displays these automatically
(via the `OTEL_EXPORTER_OTLP_ENDPOINT` env var).
