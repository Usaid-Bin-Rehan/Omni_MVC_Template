using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.AIChatService;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.DelegatingHandlers;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.Options;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.SkPlugins;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.Summarizer;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.ThreadStore;

namespace Omni_MVC_2.Utilities.SemanticKernelUtilities
{
    public static class SkDI
    {
        public static IServiceCollection AddSemanticKernel(this IServiceCollection services, Action<SemanticKernelOptions>? configure = null)
        {
            services.AddHttpContextAccessor();

            /// Delegating Handler and Named-HttpClient
            services.AddTransient<GroqDelegatingHandler>();
            services.AddHttpClient("GroqClient").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(120)).AddHttpMessageHandler<GroqDelegatingHandler>();

            /// Options
            if (configure != null) services.Configure(configure);
            services.AddOptions<SemanticKernelOptions>().Configure(o => { /* defaults if needed */ });

            /// Kernel singleton and Named-HttpClient
            services.AddSingleton(provider =>
            {
                var config = provider.GetRequiredService<IConfiguration>();
                var apiKey = (config["OpenAI:ApiKey"] ?? config["OPENAI__APIKEY"] ?? ApiConstants.groqApiKey)?.Trim();
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("OpenAI API key not configured. Set OpenAI:ApiKey in configuration (user-secrets / env var).");

                var model = config["OpenAI:Model"] ?? ApiConstants.groqModelName;
                var httpFactory = provider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpFactory.CreateClient("GroqClient");

                var builder = Kernel.CreateBuilder();
                builder.AddOpenAIChatCompletion(modelId: model, apiKey: apiKey, httpClient: httpClient);
                builder.Plugins.AddFromType<SkPlugin>("MyPlugin");
                return builder.Build();
            });

            /// expose IChatCompletionService from Kernel for other services
            services.AddSingleton(provider => provider.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

            /// Thread store constructed with a concrete root path
            var storeRoot = Path.Combine(AppContext.BaseDirectory, "chat_store");
            services.AddSingleton<IThreadStore>(provider => new FileThreadStore(storeRoot));

            /// Summarizer and AI-Chat
            services.AddScoped<ISummarizer, SkSummarizer>();
            services.AddScoped<IAIChatService, SemanticKernelChatService>();
            
            return services;
        }
    }
}