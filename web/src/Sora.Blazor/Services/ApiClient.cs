using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sora.Blazor.Services;

namespace Sora.Blazor.Services;

public sealed record ApiCall<T>(T? Value, HttpStatusCode Status, string? Error)
{
    public bool IsSuccess => (int)Status is >= 200 and < 300;
}

public sealed class ApiClient(HttpClient http, ApiSession session)
{
    public async Task<ApiCall<TResponse>> SendAsync<TResponse>(HttpMethod method, string path,
        object? body = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (session.AccessToken is { Length: > 0 } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = content;
            try { message = System.Text.Json.JsonDocument.Parse(content).RootElement.GetProperty("code").GetString() ?? content; }
            catch { }
            return new(default, response.StatusCode, message);
        }
        if (response.StatusCode == HttpStatusCode.NoContent) return new(default, response.StatusCode, null);
        var value = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
        return new(value, response.StatusCode, null);
    }
}
