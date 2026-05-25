using OutboxDemo.Order.Api.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Aspire's Npgsql integration wires the connection string, retries, and health checks for "orderdb".
builder.AddNpgsqlDbContext<OrderDbContext>("orderdb");

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Discovery surfaces (OpenAPI + Scalar UI) are exposed only in Development to avoid leaking
// schema details from a production deployment.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("OutboxDemo Order API")
            .WithTheme(ScalarTheme.Mars)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapOrderEndpoints();

// Lightweight liveness probe / smoke test for the root URL.
app.MapGet("/", () => Results.Ok(new { service = "OutboxDemo.Order.Api" }));

app.Run();
