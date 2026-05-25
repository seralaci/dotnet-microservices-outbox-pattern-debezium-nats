using NATS.Client.Core;
using NATS.Client.JetStream;

namespace OutboxDemo.Notifier.Infrastructure.Nats;

/// <summary>
/// DI registration helpers that build a JetStream context on top of the
/// <c>INatsConnection</c> provided by <c>Aspire.NATS.Net</c>.
/// </summary>
internal static class NatsJetStreamRegistration
{
    /// <summary>
    /// Registers <see cref="INatsJSContext"/> as a singleton that reuses the
    /// shared <see cref="INatsConnection"/> resolved by the NATS client integration.
    /// </summary>
    internal static IServiceCollection AddNatsJetStream(this IServiceCollection services)
    {
        services.AddSingleton<INatsJSContext>(sp =>
        {
            // INatsConnection is registered by AddNatsClient and is safe to share across components.
            var connection = sp.GetRequiredService<INatsConnection>();
            return new NatsJSContext(connection);
        });

        return services;
    }
}
