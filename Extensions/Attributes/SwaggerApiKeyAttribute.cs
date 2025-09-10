namespace Omni_MVC_2.Extensions.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SwaggerApiKeyAttribute : Attribute
    {
        public string HeaderName { get; }
        public bool IsRequired { get; }
        public string Description { get; }
        public string ExpectedApiKey { get; }

        public SwaggerApiKeyAttribute(string headerName = "API_Key", bool isRequired = false, string description = "", string? expectedApiKey = null)
        {
            HeaderName = headerName;
            IsRequired = isRequired;
            Description = description;
            ExpectedApiKey = expectedApiKey ?? (Environment.GetEnvironmentVariable("DEFAULT_API_KEY") ?? "myDummyKey");
        }
    }
}
