# Debezium Server configuration

This directory holds the Debezium Server `application.properties` file, which
the Aspire AppHost bind-mounts into the container (`/debezium/conf`).

## Components

| Setting | Value |
|---------|-------|
| Source connector | `io.debezium.connector.postgresql.PostgresConnector` |
| Replication plugin | `pgoutput` (PostgreSQL built-in) |
| Publication name | `dbz_outbox_publication` (created by the MigrationService) |
| Replication slot name | `dbz_outbox_slot` (auto-created by Debezium) |
| Transform | `io.debezium.transforms.outbox.EventRouter` |
| Sink | NATS JetStream (`io.debezium.server.nats.jetstream.NatsJetStreamChangeConsumer`) |
| NATS subject | `<aggregate_type>.events` (e.g. `order.events`) |
| Event-type header | `eventType` (`OrderCreated`) |

## Message flow

1. `Order.Api` writes the `orders` and `outbox_messages` records into the
   PostgreSQL `orderdb` database in a single transaction.
2. Debezium Server reads the WAL via the `pgoutput` plugin and extracts the
   `outbox_messages` table through the `dbz_outbox_publication` publication.
3. The `EventRouter` transform unpacks the `payload` JSON value, attaches the
   `type` field as an `eventType` header, and routes the message to the
   `<aggregate_type>.events` NATS subject based on `aggregate_type`.
4. The NATS JetStream sink places the message into the `outbox-events` stream.
5. The `Notifier` service consumes messages with the `notifier-order-events`
   durable consumer and sends emails to Mailpit.

## Offsets

Debezium Server uses file-backed offset storage (`/debezium/data/offsets.dat`).
The `data` folder is bind-mounted to the host, so processing resumes after a restart.
