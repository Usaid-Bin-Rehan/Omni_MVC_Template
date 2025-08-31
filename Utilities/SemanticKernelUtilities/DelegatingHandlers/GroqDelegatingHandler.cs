namespace Omni_MVC_2.Utilities.SemanticKernelUtilities.DelegatingHandlers
{
    public class GroqDelegatingHandler : DelegatingHandler
    {
        public GroqDelegatingHandler() { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri != null)
            {
                string? original = request.RequestUri.ToString();
                string? updated = original.Replace("https://api.openai.com/v1", "https://api.groq.com/openai/v1");
                if (!string.Equals(original, updated, StringComparison.Ordinal))
                {
                    request.RequestUri = new Uri(updated);
                    Console.WriteLine($"🔁 Redirected request: {original} -> {updated}");
                }
            }
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}