using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace Omni_MVC_2.Extensions.Filters
{
    public class LogExecutionTimeFilter : IActionFilter
    {
        private readonly ILogger<LogExecutionTimeFilter> _logger;
        private Stopwatch _stopwatch;
        public LogExecutionTimeFilter(ILogger<LogExecutionTimeFilter> logger)
        {
            _logger = logger;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            _stopwatch = Stopwatch.StartNew();
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            _stopwatch.Stop();
            _logger.LogInformation($"Action {context.ActionDescriptor.DisplayName} took {_stopwatch.ElapsedMilliseconds} ms");
        }
    }
}