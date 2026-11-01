using System.Net;

namespace YazioTool.Core.Yazio;

public sealed class YazioApiException(HttpStatusCode? statusCode, string message) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;

    internal static async Task ThrowIfFailedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var request = response.RequestMessage;
        throw new YazioApiException(
            response.StatusCode,
            $"YAZIO {request?.Method} {request?.RequestUri?.AbsolutePath} failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}".Trim());
    }
}
