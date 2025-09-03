namespace Omni_MVC_2.Utilities.NatsClient118Utilities
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddNotificationProvider(this IServiceCollection services)
        {
            var natsUrl = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
            ArgumentException.ThrowIfNullOrEmpty(nameof(services));
            services.AddScoped<INatsClientUtility, OmniNatsClientUtility>();
            return services;
        }
    }
}