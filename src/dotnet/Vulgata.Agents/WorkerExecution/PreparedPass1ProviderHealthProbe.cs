using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Sends exactly one fixed, bounded Chat Completions health request and discards
/// its response content.
/// </summary>
public sealed class PreparedPass1ProviderHealthProbe : IPreparedPass1ProviderHealthProbe
{
    private readonly HttpClient _client;
    private readonly TimeProvider _timeProvider;

    public PreparedPass1ProviderHealthProbe(HttpClient client, TimeProvider timeProvider)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public PreparedPass1ProviderHealthProbe(HttpMessageHandler transport, TimeProvider timeProvider)
        : this(new HttpClient(
            transport ?? throw new ArgumentNullException(nameof(transport)),
            disposeHandler: false), timeProvider)
    {
    }

    public async Task<PreparedPass1ProviderHealthResult> ProbeAsync(
        Uri endpoint,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(apiKey) ||
            Encoding.UTF8.GetByteCount(model) > 4_096 ||
            Encoding.UTF8.GetByteCount(apiKey) > 8_192)
        {
            return new(PreparedPass1ProviderHealthOutcome.ProviderConfiguration, null);
        }

        string body = "{\"model\":\"" + EscapeJson(model) +
            "\",\"messages\":[{\"role\":\"user\",\"content\":\"Reply OK.\"}]," +
            "\"max_tokens\":1,\"temperature\":0}";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            using HttpResponseMessage response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            DateTimeOffset observedAt = _timeProvider.GetUtcNow();
            DateTimeOffset? retryAfterAt = ReadRetryAfter(response.Headers.RetryAfter, observedAt);
            if (response.IsSuccessStatusCode)
            {
                return new(PreparedPass1ProviderHealthOutcome.Available, null);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new(PreparedPass1ProviderHealthOutcome.ProviderConfiguration, null);
            }

            if (response.StatusCode is HttpStatusCode.RequestTimeout or
                HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                return new(PreparedPass1ProviderHealthOutcome.ProviderUnavailable, retryAfterAt);
            }

            return new(PreparedPass1ProviderHealthOutcome.UnclassifiedSystemic, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or OperationCanceledException)
        {
            return new(PreparedPass1ProviderHealthOutcome.ProviderUnavailable, null);
        }
    }

    private static DateTimeOffset? ReadRetryAfter(
        RetryConditionHeaderValue? retryAfter,
        DateTimeOffset observedAt)
    {
        if (retryAfter?.Date is DateTimeOffset date)
        {
            return date.ToUniversalTime();
        }

        return retryAfter?.Delta is TimeSpan delay && delay > TimeSpan.Zero
            ? observedAt.Add(delay)
            : null;
    }

    private static string EscapeJson(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);
}
