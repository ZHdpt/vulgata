using System.Net;
using System.Net.Http.Headers;
using Vulgata.Core.Entities;

namespace Vulgata.Web.Data;

public interface ILlmProviderConnectionTestService
{
    Task<LlmProviderConnectionAttemptResult> TestAsync(
        LlmProvider provider,
        string apiKey,
        CancellationToken cancellationToken = default);
}

public sealed record LlmProviderConnectionAttemptResult(bool Success, string Message);

public sealed class LlmProviderConnectionTestService(
    IHttpClientFactory httpClientFactory,
    ILogger<LlmProviderConnectionTestService> logger)
    : ILlmProviderConnectionTestService
{
    public async Task<LlmProviderConnectionAttemptResult> TestAsync(
        LlmProvider provider,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        HttpClient client = httpClientFactory.CreateClient("LlmProviderConnectionTest");
        Uri requestUri = BuildModelsUri(provider.BaseEndpointUrl);

        using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("LLM provider connection test succeeded for provider {ProviderId}.", provider.Id);
                return new LlmProviderConnectionAttemptResult(true, "连接测试成功。");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogWarning(
                    "LLM provider connection test was rejected for provider {ProviderId} with status code {StatusCode}.",
                    provider.Id,
                    (int)response.StatusCode);
                return new LlmProviderConnectionAttemptResult(false, "凭据无效或没有访问权限。请检查 API 密钥。");
            }

            logger.LogWarning(
                "LLM provider connection test failed for provider {ProviderId} with status code {StatusCode}.",
                provider.Id,
                (int)response.StatusCode);
            return new LlmProviderConnectionAttemptResult(false, $"服务返回状态码 {(int)response.StatusCode}。");
        }
        catch (UriFormatException ex)
        {
            logger.LogWarning(ex, "LLM provider connection test has invalid endpoint URL for provider {ProviderId}.", provider.Id);
            return new LlmProviderConnectionAttemptResult(false, "提供商基础地址格式无效。");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("LLM provider connection test timed out for provider {ProviderId}.", provider.Id);
            return new LlmProviderConnectionAttemptResult(false, "连接测试超时。请检查基础地址或稍后重试。");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "LLM provider connection test could not reach provider {ProviderId}.", provider.Id);
            return new LlmProviderConnectionAttemptResult(false, "无法连接到提供商端点。请检查基础地址和网络连通性。");
        }
    }

    private static Uri BuildModelsUri(string baseEndpointUrl)
    {
        string normalizedBaseUrl = baseEndpointUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), "models");
    }
}
