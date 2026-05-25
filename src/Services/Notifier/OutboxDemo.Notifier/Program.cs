using OutboxDemo.Notifier.Events;
using OutboxDemo.Notifier.Infrastructure.Nats;
using OutboxDemo.Notifier.Infrastructure.Smtp;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Aspire's NATS integration registers INatsConnection; AddNatsJetStream layers the JS context on top.
builder.AddNatsClient("nats");
builder.Services.AddNatsJetStream();

// Bind Smtp:* settings, run DataAnnotations validation, and fail fast at startup
// when the configuration is invalid (instead of throwing on the first email send).
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IEmailSender, EmailSender>();

builder.Services.AddHostedService<OrderCreatedConsumer>();

// Surface the consumer's ActivitySource through the shared OpenTelemetry tracing pipeline.
builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(OrderCreatedConsumer.ActivitySourceName));

builder.Build().Run();
