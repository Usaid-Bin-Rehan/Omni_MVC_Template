using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Omni_MVC_2.Extensions.Attributes
{
    public class ApiKeyAuthAttribute : ActionFilterAttribute
    {
        private readonly string _defaultApiKey = Environment.GetEnvironmentVariable("DEFAULT_API_KEY") ?? "REMOVE_ON_PRODUCTION";
        private const string DefaultHeaderName = "X-API-KEY";
        public ApiKeyAuthAttribute() { }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
            {
                base.OnActionExecuting(context);
                return;
            }
            var methodInfo = controllerActionDescriptor.MethodInfo; var apiKeyAttr = methodInfo.GetCustomAttributes(typeof(SwaggerApiKeyAttribute), false).Cast<SwaggerApiKeyAttribute>().FirstOrDefault();
            var headerName = apiKeyAttr?.HeaderName ?? DefaultHeaderName;
            var expectedApiKey = apiKeyAttr?.ExpectedApiKey ?? _defaultApiKey;
            if (!context.HttpContext.Request.Headers.TryGetValue(headerName, out var actualApiKey))
            {
                context.Result = new UnauthorizedObjectResult($"Missing API key header '{headerName}'.");
                return;
            }
            if (actualApiKey != expectedApiKey)
            {
                context.Result = new UnauthorizedObjectResult("Invalid API key.");
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}