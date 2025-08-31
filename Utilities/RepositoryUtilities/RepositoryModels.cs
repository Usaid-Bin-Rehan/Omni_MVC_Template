namespace Omni_MVC_2.Utilities.RepositoryUtilities
{
    public class RepositoryModels { }

    public class CommonMessages
    {
        public const string Success = $"{nameof(Success)}";
        public const string NotFound = $"{nameof(NotFound)}";
    }

    public class SetterResult
    {
        public object? Data { get; set; }
        public bool Result { get; set; }
        public bool IsException { get; set; }
        public string? Message { get; set; }
    }

    public class GetterResult<T>
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }
}
