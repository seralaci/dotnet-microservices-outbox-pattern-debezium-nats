using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Shared bootstrapping extensions used by every service in the solution
/// (OpenTelemetry, health checks, service discovery, resilient HTTP clients).
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        /// Wires the common cross-cutting concerns onto the host builder:
        /// OpenTelemetry pipelines, health checks, service discovery, and standard
        /// resilience for outbound HTTP clients.
        /// </summary>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public TBuilder AddServiceDefaults()
        {
            builder.ConfigureOpenTelemetry();
            builder.AddDefaultHealthChecks();

            builder.Services.AddServiceDiscovery();

            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                // Retry + circuit breaker on every outbound HTTP call.
                http.AddStandardResilienceHandler();

                // Resolve "http://service-name" style URLs through Aspire's service discovery.
                http.AddServiceDiscovery();
            });

            return builder;
        }

        /// <summary>
        /// Configures the OpenTelemetry logging, metric, and tracing pipelines
        /// shared by every service.
        /// </summary>
        private TBuilder ConfigureOpenTelemetry()
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services
                .AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();
                })
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource(builder.Environment.ApplicationName)
                        // Health probes would otherwise dominate the trace volume — filter them out.
                        .AddAspNetCoreInstrumentation(t =>
                            t.Filter = context => !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                                                  && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                        .AddHttpClientInstrumentation();
                });

            builder.AddOpenTelemetryExporters();

            return builder;
        }

        /// <summary>
        /// Enables the OTLP exporter when an endpoint is configured via the
        /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable (set by Aspire).
        /// </summary>
        private TBuilder AddOpenTelemetryExporters()
        {
            var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            if (useOtlpExporter)
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }

            return builder;
        }

        /// <summary>
        /// Registers a baseline <c>"self"</c> liveness check tagged with <c>live</c>
        /// so that the <c>/alive</c> endpoint can be exposed separately from <c>/health</c>.
        /// </summary>
        private TBuilder AddDefaultHealthChecks()
        {
            builder.Services
                .AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

            return builder;
        }
    }

    /// <summary>
    /// Maps the default <c>/health</c> and <c>/alive</c> endpoints.
    /// </summary>
    /// <remarks>
    /// Endpoints are exposed only in the <c>Development</c> environment because they
    /// would otherwise be reachable without authentication in production.
    /// </remarks>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        // Aggregated readiness probe: returns Healthy only when every registered check passes.
        app.MapHealthChecks(HealthEndpointPath);

        // Liveness probe: succeeds whenever the process is responsive (uses checks tagged "live").
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
