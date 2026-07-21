using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LiveTranscribe.Api.Services.Transcription;

public sealed class GroqWhisperProvider(
    HttpClient httpClient,
    IOptions<GroqOptions> options,
    ILogger<GroqWhisperProvider> logger) : ITranscriptionProvider
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);

    private readonly GroqOptions _options = options.Value;

    public async Task<TranscriptionResult> TranscribeAsync(byte[] wavBytes, string languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new TranscriptionUnavailableException(
                "Groq:ApiKey não configurada. Rode 'dotnet user-secrets set Groq:ApiKey <sua-chave>' dentro de LiveTranscribe.Api.");
        }

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using HttpRequestMessage request = BuildRequest(wavBytes, languageCode);
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await ParseResultAsync(response, cancellationToken);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new TranscriptionUnavailableException($"Chave da Groq rejeitada (HTTP {(int)response.StatusCode}) — verifique se ainda é válida.");
            }

            bool isRetryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!isRetryable || attempt == MaxAttempts)
            {
                logger.LogError("Groq retornou {StatusCode} (tentativa {Attempt}/{Max}): {Body}", response.StatusCode, attempt, MaxAttempts, body);
                throw new InvalidOperationException($"Falha ao transcrever via Groq (HTTP {(int)response.StatusCode}).");
            }

            TimeSpan delay = GetRetryDelay(response, attempt);
            logger.LogWarning(
                "Groq retornou {StatusCode} (tentativa {Attempt}/{Max}) — tentando de novo em {DelaySeconds}s.",
                response.StatusCode, attempt, MaxAttempts, delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException("Falha ao transcrever via Groq: número máximo de tentativas excedido.");
    }

    private HttpRequestMessage BuildRequest(byte[] wavBytes, string languageCode)
    {
        var content = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "chunk.wav");
        content.Add(new StringContent(_options.Model), "model");
        content.Add(new StringContent("json"), "response_format");
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            content.Add(new StringContent(languageCode), "language");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/audio/transcriptions")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return request;
    }

    private static async Task<TranscriptionResult> ParseResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        string text = doc.RootElement.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
        string? detectedLanguage = doc.RootElement.TryGetProperty("language", out var langProp) ? langProp.GetString() : null;

        return new TranscriptionResult(text.Trim(), detectedLanguage);
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        TimeSpan? fromHeader = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);

        TimeSpan delay = fromHeader is { } value && value > TimeSpan.Zero
            ? value
            : TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s

        return delay > MaxRetryDelay ? MaxRetryDelay : delay;
    }
}
