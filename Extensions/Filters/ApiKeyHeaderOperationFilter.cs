using Microsoft.OpenApi.Models;
using Omni_MVC_2.Extensions.Attributes;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Omni_MVC_2.Extensions.Filters
{
    public class AddApiKeyHeaderOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation == null || context == null) return;

            SwaggerApiKeyAttribute? apiKeyAttribute = context.MethodInfo.GetCustomAttributes(typeof(SwaggerApiKeyAttribute), false).Cast<SwaggerApiKeyAttribute>().FirstOrDefault();
            if (apiKeyAttribute == null) return;

            operation.Parameters ??= [];
            if (!operation.Parameters.Any(p => p.Name == apiKeyAttribute.HeaderName))
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = apiKeyAttribute.HeaderName,
                    In = ParameterLocation.Header,
                    Required = apiKeyAttribute.IsRequired,
                    Description = apiKeyAttribute.Description,
                    Schema = new OpenApiSchema { Type = "string" }
                });
            }
        }
    }
}