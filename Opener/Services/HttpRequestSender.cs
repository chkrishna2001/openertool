using System.Net.Http;
using System.Threading.Tasks;

namespace Opener.Services;

public record HttpCallResult(int StatusCode, bool IsSuccess, string Body);

/// <summary>
/// Thin abstraction over sending an HTTP request, so REST-key execution can be unit
/// tested without making real network calls (mirrors IProcessRunner's role for CLI calls).
/// </summary>
public interface IHttpRequestSender
{
    Task<HttpCallResult> SendAsync(HttpRequestMessage request);
}

public class SystemHttpRequestSender : IHttpRequestSender
{
    // Shared instance: avoids the socket-exhaustion issue that comes from creating a new
    // HttpClient per call (which is what this replaced).
    private static readonly HttpClient _client = new();

    public async Task<HttpCallResult> SendAsync(HttpRequestMessage request)
    {
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return new HttpCallResult((int)response.StatusCode, response.IsSuccessStatusCode, body);
    }
}
