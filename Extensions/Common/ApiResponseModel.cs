using System.Diagnostics;
using System.Text.Json;

namespace Omni_MVC_2.Extensions.Common
{
    #region HTTP Status Codes
    public static class HTTPStatusCode200
    {
        public const int Ok = 200;
        public const int Created = 201;
        public const int Accepted = 202;
        public const int NoContent = 204;
        public const int ResetContent = 205;
        public const int AlreadyReported = 208;
        public const int IAmUsed = 226;
    }

    public class HTTPStatusCode400
    {
        public const int BadRequest = 400;
        public const int Unauthorized = 401;
        public const int PaymentRequiredClient = 402;
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int NotAcceptable = 406;
        public const int Conflict = 409;
        public const int UnprocessableEntity = 422;
        public const int UpgradeRequired = 426;
    }

    public class HTTPStatusCode500
    {
        public const int InternalServerError = 500;
        public const int BadGateway = 502;
        public const int ServiceUnavailable = 503;
        public const int GatewayTimeout = 504;
    }
    #endregion HTTP Status Codes

    public class ApiResponseModel : IResult
    {
        public bool IsApiHandled { get; set; }
        public bool IsRequestSuccess { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public object Data { get; set; } = new object();
        public object Exception { get; set; } = new List<string>();
        public string? TraceId { get; set; }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            HttpResponse? response = httpContext.Response;
            response.StatusCode = StatusCode;
            response.ContentType = "application/json";
            JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
            string? json = JsonSerializer.Serialize(this, options); await response.WriteAsync(json);
        }
    }

    public static class ApiResponseHelper
    {
        /// <summary>
        /// Standard API Response with Trace ID
        /// </summary>
        public static ApiResponseModel Convert(bool IsRequestHandled, bool status, string message, int statusCode, object data)
        {
            ApiResponseModel model = new()
            {
                IsApiHandled = IsRequestHandled,
                IsRequestSuccess = status,
                StatusCode = statusCode,
                Message = message,
                Data = data,
                TraceId = Activity.Current?.TraceId.ToString() ?? "NoTraceId"
            };
            return model;
        }

        /// <summary>
        /// Standard API Response with Exception
        /// </summary>
        public static ApiResponseModel Convert(bool IsRequestHandled, bool status, string message, int statusCode, object data, object exception)
        {
            ApiResponseModel model = new()
            {
                IsApiHandled = IsRequestHandled,
                IsRequestSuccess = status,
                StatusCode = statusCode,
                Message = message,
                Data = data,
                Exception = exception
            };
            return model;
        }
    }
}