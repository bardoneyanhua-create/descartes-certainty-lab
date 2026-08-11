using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Descartes.CertaintyLab.ThoughtCompanion.Security;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

namespace Descartes.CertaintyLab.ThoughtCompanion.OpenAICompatible;

public sealed class OpenAICompatibleProvider : IThoughtCompanionProvider
{
    public const int MaximumResponseBytes = 262_144;
    public static readonly TimeSpan MaximumRetryAfter = TimeSpan.FromHours(24);

    private readonly HttpClient client;
    private readonly CompanionProfile profile;
    private readonly TimeSpan timeout;
    private readonly TimeProvider timeProvider;

    public OpenAICompatibleProvider(
        HttpClient client,
        CompanionProfile profile,
        TimeSpan timeout,
        int maximumOutputTokens)
        : this(client, profile, timeout, maximumOutputTokens, TimeProvider.System)
    {
    }

    internal OpenAICompatibleProvider(
        HttpClient client,
        CompanionProfile profile,
        TimeSpan timeout,
        int maximumOutputTokens,
        TimeProvider timeProvider)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (profile.Kind == CompanionProviderKind.OfflineDemo || profile.BaseUrl is null)
        {
            throw new ArgumentException("A remote provider profile is required.", nameof(profile));
        }
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        if (maximumOutputTokens is < 64 or > 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens));
        }

        this.timeout = timeout;
        MaximumOutputTokens = maximumOutputTokens;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public int MaximumOutputTokens { get; }

    public async Task<CompanionProviderResult> CompleteAsync(
        CompanionRequest request,
        SensitiveBuffer credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);

        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using var message = new HttpRequestMessage(HttpMethod.Post, BuildCompletionUri(profile.BaseUrl!));
        string bearer = new(credential.Span);
        try
        {
            string category = SafeRepairCategory(request.InvalidResponseCategory);
            var messages = new List<Dictionary<string, string>>
            {
                new() { ["role"] = "system", ["content"] = request.SystemContract },
                new() { ["role"] = "user", ["content"] = JsonSerializer.Serialize(request.Context) }
            };
            if (request.IsRepair)
            {
                messages.Add(new Dictionary<string, string>
                {
                    ["role"] = "system",
                    ["content"] = $"Repair the prior answer using only the supplied context. Return the required JSON contract. Failure category: {category}. Do not repeat or infer the rejected response body."
                });
            }

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            message.Content = JsonContent.Create(new
            {
                model = profile.Model,
                messages,
                response_format = new { type = "json_object" },
                max_tokens = Math.Min(request.MaximumOutputTokens, MaximumOutputTokens),
                stream = false
            });

            using HttpResponseMessage response = await client.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    linked.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw Map(response, timeProvider.GetUtcNow());
            }
            if (response.Content.Headers.ContentLength is long length && length > MaximumResponseBytes)
            {
                throw InvalidResponse("provider-envelope-too-large");
            }

            CompanionProviderResult result = await ParseSuccessAsync(response.Content, linked.Token).ConfigureAwait(false);
            if (result.Usage.PromptTokens > request.MaximumPromptTokens ||
                result.Usage.CompletionTokens > Math.Min(request.MaximumOutputTokens, MaximumOutputTokens))
            {
                throw InvalidResponse("provider-usage-exceeds-request-bound");
            }
            return result;
        }
        catch (CompanionProviderException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CompanionProviderException(CompanionFailureKind.Timeout, "provider-timeout");
        }
        catch (OperationCanceledException)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new CompanionProviderException(CompanionFailureKind.Offline, "network-unavailable");
        }
        catch (IOException)
        {
            throw new CompanionProviderException(CompanionFailureKind.Offline, "network-unavailable");
        }
        catch (JsonException)
        {
            throw InvalidResponse("provider-envelope-json");
        }
        catch (Exception)
        {
            throw new CompanionProviderException(CompanionFailureKind.ProviderUnavailable, "provider-failure");
        }
        finally
        {
            bearer = string.Empty;
            message.Headers.Authorization = null;
        }
    }

    private static Uri BuildCompletionUri(Uri baseUrl)
    {
        string path = baseUrl.AbsolutePath.TrimEnd('/');
        var builder = new UriBuilder(baseUrl)
        {
            Path = path + "/chat/completions",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }

    private static async Task<CompanionProviderResult> ParseSuccessAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[MaximumResponseBytes + 1];
        try
        {
            using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            int total = 0;
            while (total <= MaximumResponseBytes)
            {
                int read = await stream.ReadAsync(bytes.AsMemory(total, bytes.Length - total), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;
                total += read;
            }
            if (total > MaximumResponseBytes)
            {
                throw InvalidResponse("provider-envelope-too-large");
            }

            using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(0, total));
            return ParseEnvelope(document.RootElement);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static CompanionProviderResult ParseEnvelope(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("choices", out JsonElement choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0 ||
            choices[0].ValueKind != JsonValueKind.Object)
        {
            throw InvalidResponse("provider-envelope-shape");
        }

        JsonElement choice = choices[0];
        if (!choice.TryGetProperty("finish_reason", out JsonElement finishElement) ||
            finishElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(finishElement.GetString()))
        {
            throw InvalidResponse("provider-finish-reason");
        }

        string finishReason = finishElement.GetString()!;
        switch (finishReason)
        {
            case "stop":
                break;
            case "content_filter":
                throw new CompanionProviderException(CompanionFailureKind.ContentFiltered, "content-filtered");
            case "insufficient_system_resource":
                throw new CompanionProviderException(CompanionFailureKind.ProviderUnavailable, "provider-resource");
            case "length":
                throw InvalidResponse("incomplete-length");
            case "tool_calls":
                throw InvalidResponse("unsupported-tool-calls");
            default:
                throw InvalidResponse("provider-finish-reason");
        }

        if (!choice.TryGetProperty("message", out JsonElement providerMessage) ||
            providerMessage.ValueKind != JsonValueKind.Object ||
            !providerMessage.TryGetProperty("content", out JsonElement contentElement) ||
            contentElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(contentElement.GetString()))
        {
            throw InvalidResponse("empty-content");
        }
        string json = contentElement.GetString()!;
        if (json.Length > CompanionResponseValidator.MaxJsonCharacters)
        {
            throw InvalidResponse("provider-content-too-large");
        }

        if (!root.TryGetProperty("usage", out JsonElement usage) ||
            usage.ValueKind != JsonValueKind.Object ||
            !TryNonNegativeInt(usage, "prompt_tokens", out int promptTokens) ||
            !TryNonNegativeInt(usage, "completion_tokens", out int completionTokens))
        {
            throw InvalidResponse("provider-usage");
        }
        int cacheHitTokens = ReadCacheHitTokens(usage);
        int cacheMissTokens = ReadCacheMissTokens(usage, promptTokens, cacheHitTokens);
        if (cacheHitTokens < 0 || cacheMissTokens < 0 || cacheHitTokens > promptTokens ||
            (long)cacheHitTokens + cacheMissTokens != promptTokens)
        {
            throw InvalidResponse("provider-usage");
        }

        return new CompanionProviderResult(
            json,
            new CompanionUsage(promptTokens, completionTokens, cacheHitTokens, cacheMissTokens),
            finishReason);
    }

    private static int ReadCacheHitTokens(JsonElement usage)
    {
        if (TryNonNegativeInt(usage, "prompt_cache_hit_tokens", out int deepSeekHit))
        {
            return deepSeekHit;
        }
        if (usage.TryGetProperty("prompt_tokens_details", out JsonElement details) &&
            details.ValueKind == JsonValueKind.Object &&
            TryNonNegativeInt(details, "cached_tokens", out int openAiHit))
        {
            return openAiHit;
        }
        return 0;
    }

    private static int ReadCacheMissTokens(JsonElement usage, int promptTokens, int cacheHitTokens)
    {
        return TryNonNegativeInt(usage, "prompt_cache_miss_tokens", out int deepSeekMiss)
            ? deepSeekMiss
            : promptTokens - cacheHitTokens;
    }

    private static bool TryNonNegativeInt(JsonElement parent, string name, out int value)
    {
        value = 0;
        return parent.TryGetProperty(name, out JsonElement element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt32(out value) &&
               value >= 0;
    }

    private static string SafeRepairCategory(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? value
            : "invalid-response";

    private static CompanionProviderException InvalidResponse(string diagnostic) =>
        new(CompanionFailureKind.InvalidResponse, diagnostic);

    private static CompanionProviderException Map(HttpResponseMessage response, DateTimeOffset utcNow) =>
        response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new(CompanionFailureKind.Unauthorized, "http-401"),
            HttpStatusCode.PaymentRequired => new(CompanionFailureKind.InsufficientBalance, "http-402"),
            HttpStatusCode.TooManyRequests => new(
                CompanionFailureKind.RateLimited,
                "http-429",
                ParseRetryAfter(response, utcNow)),
            _ => new(CompanionFailureKind.ProviderUnavailable, $"http-{(int)response.StatusCode}")
        };

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response, DateTimeOffset utcNow)
    {
        if (!response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values)) return null;
        string[] entries = values.ToArray();
        if (entries.Length != 1 || !RetryConditionHeaderValue.TryParse(entries[0], out RetryConditionHeaderValue? parsed))
        {
            return null;
        }
        TimeSpan delay = parsed.Delta ?? (parsed.Date is DateTimeOffset date ? date - utcNow : TimeSpan.Zero);
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        return delay > MaximumRetryAfter ? MaximumRetryAfter : delay;
    }
}
