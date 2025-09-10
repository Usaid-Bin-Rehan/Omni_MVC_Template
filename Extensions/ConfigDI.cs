using FluentValidation;
using Microsoft.OpenApi.Models;
using Omni_MVC_2.Extensions.Filters;
using Omni_MVC_2.Services;
using Omni_MVC_2.Utilities.ValidatorUtilities;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Omni_MVC_2.Extensions
{
    public static class ConfigDI
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            #region Session Management

            //services.AddDistributedMemoryCache();

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = "localhost:6379";
                options.InstanceName = "MyApp:";
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            #endregion Session Management

            #region Open-Telemetry
            services.AddOpenTelemetry()
                    .WithTracing(builder =>
                    {
                        builder
                            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Omni_MVC_2"))
                            .AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddSource("Omni_MVC_2")
                            //.AddOtlpExporter(options =>
                            //{
                            //    // For HTTP/JSON OTLP, use port 4318 with OtlpExportProtocol = HttpProtobuf. If using gRPC, default is port 4317.
                            //    options.Endpoint = new Uri(Environment.GetEnvironmentVariable("JAEGER") ?? "http://localhost:4318");
                            //})
                            //.AddConsoleExporter()
                            ;
                    })
                    .WithMetrics(builder =>
                    {
                        builder
                            .AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            //.AddConsoleExporter()
                            ;
                    });

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddOpenTelemetry(options =>
                {
                    options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Omni_MVC_2"));
                    options.IncludeScopes = true;
                    options.IncludeFormattedMessage = true;
                    options.ParseStateValues = true;
                    //options.AddConsoleExporter();
                });
            });
            #endregion Open-Telemetry

            // Add API
            services.AddControllers();

            // Add Endpoints
            services.AddEndpointsApiExplorer();

            #region Swagger
            services.AddSwaggerGen(options =>
            {
                OpenApiSecurityScheme jwtScheme = new()
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter JWT as: Bearer {token}",
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = JwtBearerDefaults.AuthenticationScheme }
                };
                options.SwaggerDoc("v1", new OpenApiInfo() { Title = "Omni_MVC_2", Version = "v1", });
                options.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
                options.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
                options.OperationFilter<AddApiKeyHeaderOperationFilter>();
                options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));
            });
            #endregion Swagger

            // Add MVC with Global Filters
            services.AddControllersWithViews(options =>
            {
                options.Filters.Add<LogExecutionTimeFilter>();

            });

            // Add FluentValidations
            services.AddValidatorsFromAssembly(typeof(ModelValidator<>).Assembly);

            // Add Business Layer
            services.AddBusinessLayer(configuration);

            Console.WriteLine($"[Info]----->{nameof(RegisterServices)} service added");
            return services;
        }
    }
}