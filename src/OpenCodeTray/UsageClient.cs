using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OpenCodeTray;

/// <summary>Error with a message that is safe to show to the user.</summary>
public sealed class UsageException : Exception
{
    public UsageException(string message) : base(message)
    {
    }
}

/// <summary>Client for the OpenCode Go usage endpoint.</summary>
public static class UsageClient
{
    // Documented in the OpenCode console source: GET /zen/go/v1/usage
    public const string Endpoint = "https://opencode.ai/zen/go/v1/usage";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    public static async Task<UsageSnapshot> FetchAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new UsageException("Nie ustawiono klucza API OpenCode Go.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Headers.UserAgent.ParseAdd("opencode-tray/1.0");

        HttpResponseMessage response;
        try
        {
            response = await Http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UsageException("Przekroczono czas połączenia z opencode.ai.");
        }
        catch (HttpRequestException ex)
        {
            throw new UsageException($"Błąd połączenia: {ex.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var message = TryReadErrorMessage(body);
                throw response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => new UsageException(message ?? "Nieprawidłowy klucz API."),
                    HttpStatusCode.Forbidden => new UsageException(message ?? "Wymagana aktywna subskrypcja OpenCode Go."),
                    _ => new UsageException(message ?? $"Serwer zwrócił {(int)response.StatusCode} {response.ReasonPhrase}."),
                };
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                var usage = document.RootElement.GetProperty("usage");
                return new UsageSnapshot
                {
                    Rolling = ParseWindow(usage.GetProperty("rolling")),
                    Weekly = ParseWindow(usage.GetProperty("weekly")),
                    Monthly = ParseWindow(usage.GetProperty("monthly")),
                    FetchedAt = DateTimeOffset.Now,
                };
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new UsageException("Nieoczekiwany format odpowiedzi serwera.");
            }
        }
    }

    private static UsageWindow ParseWindow(JsonElement element)
    {
        var status = element.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? "ok"
            : "ok";

        double percent = 0;
        if (element.TryGetProperty("percent", out var percentElement) &&
            percentElement.ValueKind == JsonValueKind.Number)
        {
            percent = percentElement.GetDouble();
        }

        var resetsAt = DateTimeOffset.Now;
        if (element.TryGetProperty("resetsAt", out var resetElement) &&
            resetElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(resetElement.GetString(), out var parsed))
        {
            resetsAt = parsed.ToLocalTime();
        }

        return new UsageWindow
        {
            Status = status,
            Percent = Math.Clamp(percent, 0, 100),
            ResetsAt = resetsAt,
        };
    }

    private static string? TryReadErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch
        {
            // Ignore malformed error bodies.
        }

        return null;
    }
}
