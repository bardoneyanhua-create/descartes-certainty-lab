using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Descartes.CertaintyLab.ThoughtCompanion.Security;

namespace Descartes.CertaintyLab.ThoughtCompanion.DeepSeek;

public sealed class DeepSeekCredentialProbe : ICredentialProbe
{
    private const int MaximumProbeResponseBytes = 16_384;

    private readonly HttpClient client;
    private readonly DeepSeekOptions options;

    public DeepSeekCredentialProbe(HttpClient client, DeepSeekOptions options)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<bool> IsAcceptedAsync(SensitiveBuffer value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var timeout = new CancellationTokenSource(options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(options.BaseUrl, "user/balance"));
        string bearer = new(value.Span);
        try
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            using HttpResponseMessage response = await client.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    linked.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength is long length && length > MaximumProbeResponseBytes)
            {
                return false;
            }

            return await ReadAvailabilityAsync(response.Content, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            bearer = string.Empty;
            message.Headers.Authorization = null;
        }
    }

    private static async Task<bool> ReadAvailabilityAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[MaximumProbeResponseBytes + 1];
        try
        {
            using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            int total = 0;
            while (total <= MaximumProbeResponseBytes)
            {
                int read = await stream.ReadAsync(bytes.AsMemory(total, bytes.Length - total), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            if (total > MaximumProbeResponseBytes)
            {
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(0, total));
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("is_available", out JsonElement available) &&
                   available.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                   available.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
