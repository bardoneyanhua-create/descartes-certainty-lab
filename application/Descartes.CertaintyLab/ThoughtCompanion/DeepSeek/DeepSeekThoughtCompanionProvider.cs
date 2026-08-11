using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Descartes.CertaintyLab.ThoughtCompanion.Security;

namespace Descartes.CertaintyLab.ThoughtCompanion.DeepSeek;

public sealed class DeepSeekThoughtCompanionProvider : IThoughtCompanionProvider
{
    public const int MaximumResponseBytes = 262_144;
    public static readonly TimeSpan MaximumRetryAfter = TimeSpan.FromHours(24);

    private readonly HttpClient client;
    private readonly DeepSeekOptions options;
    private readonly TimeProvider timeProvider;
    public int MaximumOutputTokens => options.MaximumOutputTokens;

    public DeepSeekThoughtCompanionProvider(HttpClient client, DeepSeekOptions options)
        : this(client, options, TimeProvider.System)
    {
    }

    internal DeepSeekThoughtCompanionProvider(
        HttpClient client,
        DeepSeekOptions options,
        TimeProvider timeProvider)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<CompanionProviderResult> CompleteAsync(
        CompanionRequest request,
        SensitiveBuffer credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);

        using var timeout = new CancellationTokenSource(options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUrl, "chat/completions"));
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
                model = options.Model,
                messages,
                response_format = new { type = "json_object" },
                thinking = new { type = "disabled" },
                max_tokens = Math.Min(request.MaximumOutputTokens, options.MaximumOutputTokens),
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

            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength > MaximumResponseBytes)
            {
                throw InvalidResponse("provider-envelope-too-large");
            }

            CompanionProviderResult result = await ParseSuccessAsync(response.Content, linked.Token).ConfigureAwait(false);
            if (result.Usage.PromptTokens > request.MaximumPromptTokens ||
                result.Usage.CompletionTokens > Math.Min(request.MaximumOutputTokens, options.MaximumOutputTokens))
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
                if (read == 0)
                {
                    break;
                }

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
            case "length":
                throw InvalidResponse("incomplete-length");
            case "tool_calls":
                throw InvalidResponse("unsupported-tool-calls");
            case "insufficient_system_resource":
                throw new CompanionProviderException(CompanionFailureKind.ProviderUnavailable, "provider-resource");
            default:
                throw InvalidResponse("provider-finish-reason");
        }

        if (!choice.TryGetProperty("message", out JsonElement message) ||
            message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("content", out JsonElement contentElement) ||
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

        if (!root.TryGetProperty("usage", out JsonElement usageElement) ||
            usageElement.ValueKind != JsonValueKind.Object ||
            !TryNonNegativeInt(usageElement, "prompt_tokens", out int promptTokens) ||
            !TryNonNegativeInt(usageElement, "completion_tokens", out int completionTokens) ||
            !TryNonNegativeInt(usageElement, "prompt_cache_hit_tokens", out int cacheHitTokens) ||
            !TryNonNegativeInt(usageElement, "prompt_cache_miss_tokens", out int cacheMissTokens))
        {
            throw InvalidResponse("provider-usage");
        }

        return new CompanionProviderResult(
            json,
            new CompanionUsage(promptTokens, completionTokens, cacheHitTokens, cacheMissTokens),
            finishReason);
    }

    private static bool TryNonNegativeInt(JsonElement parent, string name, out int value)
    {
        value = 0;
        return parent.TryGetProperty(name, out JsonElement element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt32(out value) &&
               value >= 0;
    }

    private static CompanionProviderException InvalidResponse(string diagnostic) =>
        new(CompanionFailureKind.InvalidResponse, diagnostic);

    private static string SafeRepairCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64 ||
            value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '-')))
        {
            return "invalid-response";
        }
        return value;
    }

    private static CompanionProviderException Map(
        HttpResponseMessage response,
        DateTimeOffset utcNow) => response.StatusCode switch
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
        if (!response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values))
        {
            return null;
        }

        string[] entries = values.ToArray();
        if (entries.Length != 1 ||
            !RetryConditionHeaderValue.TryParse(entries[0], out RetryConditionHeaderValue? parsed))
        {
            return null;
        }

        TimeSpan delay;
        if (parsed.Delta is TimeSpan delta)
        {
            delay = delta;
        }
        else if (parsed.Date is DateTimeOffset date)
        {
            delay = date - utcNow;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }
        }
        else
        {
            return null;
        }

        return delay > MaximumRetryAfter ? MaximumRetryAfter : delay;
    }
}
