using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Demo-only credential. In production this should come from a secret store.
var postgresPassword = builder.AddParameter("postgres-password", value: "postgres", secret: true);

// PostgreSQL is configured for logical replication so Debezium can stream the WAL.
// `wal_level=logical` is mandatory; the replication slot/sender limits leave headroom for additional consumers.
var postgres = builder
    .AddPostgres("postgres", password: postgresPassword, port: 5432)
    .WithImage("postgres", "17-alpine")
    .WithEnvironment("POSTGRES_INITDB_ARGS", "--auth-host=scram-sha-256")
    .WithArgs(
        "-c", "wal_level=logical",
        "-c", "max_replication_slots=10",
        "-c", "max_wal_senders=10")
    .WithPgAdmin();

var orderDb = postgres.AddDatabase("orderdb");

// NATS with JetStream enabled is the messaging backbone consumed by the Notifier.
// The HTTP monitoring endpoint exposes server metrics on port 8222.
var nats = builder
    .AddNats("nats", port: 4222)
    .WithImage("nats", "2.10-alpine")
    .WithJetStream()
    .WithArgs("--http_port", "8222")
    .WithHttpEndpoint(name: "monitoring", targetPort: 8222, port: 8222);

// NATS Insight is a side-car UI for inspecting streams and consumers during local development.
var natsInsight = builder
    .AddContainer("nats-insight", "ghcr.io/seralaci/nats-insight", "latest")
    .WithReference(nats)
    .WithHttpEndpoint(name: "http", targetPort: 8080)
    .WithEnvironment("NATS_URL", "nats://nats:4222")
    .WithEnvironment("NATS_MONITOR_URL", "http://nats:8222")
    .WaitFor(nats)
    .WithUrlForEndpoint("http", static url => url.DisplayText = "NATS Insight UI");

// Mailpit captures emails sent by the Notifier so they can be inspected from a web UI.
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.21")
    .WithEndpoint(targetPort: 1025, port: 1025, name: "smtp", scheme: "tcp")
    .WithHttpEndpoint(targetPort: 8025, port: 8025, name: "http");

// Debezium Server tails the Postgres WAL and emits each outbox row to NATS JetStream.
// The configuration file mounted into /debezium/conf describes the source, sink, and SMT chain.
var debezium = builder
    .AddContainer("debezium", "quay.io/debezium/server", "2.7.3.Final")
    .WithBindMount("./Debezium/conf", "/debezium/conf", isReadOnly: true)
    .WithHttpEndpoint(targetPort: 8080, port: 8082, name: "debezium-http")
    .WithReference(nats)
    .WithEnvironment("POSTGRES_HOST", postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("POSTGRES_PORT", postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("POSTGRES_USER", "postgres")
    .WithEnvironment("POSTGRES_PASSWORD", postgresPassword)
    .WithEnvironment("POSTGRES_DBNAME", "orderdb");

// One-shot migration worker: ensures the EF Core schema and the Debezium publication exist.
var migrationService = builder
    .AddProject<OutboxDemo_Order_MigrationService>("order-migrations")
    .WithReference(orderDb)
    .WaitFor(orderDb);

// Debezium must wait for the migration to create the publication; otherwise its first
// snapshot would fail and the container would crash-loop.
debezium
    .WaitFor(postgres)
    .WaitFor(nats)
    .WaitForCompletion(migrationService);

var orderApi = builder
    .AddProject<OutboxDemo_Order_Api>("order-api")
    .WithReference(orderDb)
    .WaitFor(orderDb)
    .WaitForCompletion(migrationService);

var mailpitSmtp = mailpit.GetEndpoint("smtp");

// Notifier consumes Order events from NATS and delivers confirmation emails through Mailpit.
// Smtp options are propagated as environment variables (Smtp__Host, Smtp__Port) so the strongly-typed
// SmtpOptions binder can pick them up without an appsettings override.
var notifier = builder
    .AddProject<OutboxDemo_Notifier>("notifier")
    .WithReference(nats)
    .WithEnvironment("Smtp__Host", mailpitSmtp.Property(EndpointProperty.Host))
    .WithEnvironment("Smtp__Port", mailpitSmtp.Property(EndpointProperty.Port))
    .WaitFor(nats)
    .WaitFor(mailpit)
    .WaitFor(debezium);

builder.Build().Run();
