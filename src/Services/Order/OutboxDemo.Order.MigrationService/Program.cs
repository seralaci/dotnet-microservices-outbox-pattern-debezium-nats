using OutboxDemo.Order.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Reuse the same DbContext type as the Order API so that EF Core sees a single model.
builder.AddNpgsqlDbContext<OrderDbContext>("orderdb");

builder.Services.AddHostedService<MigrationWorker>();

// Register the worker's ActivitySource so its spans are exported through the shared OTel pipeline.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationWorker.ActivitySourceName));

builder.Build().Run();
