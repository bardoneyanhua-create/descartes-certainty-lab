using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Descartes.CertaintyLab.ThoughtCompanion;
using Descartes.CertaintyLab.ThoughtCompanion.OpenAICompatible;
using Descartes.CertaintyLab.ThoughtCompanion.Security;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

internal static class AiSettingsA2Tests
{
    private static readonly CompanionBudgetOptions BudgetOptions = new(
        20_000, 40_000, 1, "unit", 0, 0, 0);

    internal static async Task<IReadOnlyList<string>> RunAsync()
    {
        var failures = new List<string>();
        await PresetsAndCustomUseSafeEndpointsAsync(failures);
        await CreationAndOfflineDemoNeverSendAsync(failures);
        await RemoteSendConsentIsExplicitAndFailClosedAsync(failures);
        await MissingCredentialFailsBeforeSendAsync(failures);
        await ErrorKindsAreMappedAsync(failures);
        await CancellationAndTimeoutAreDistinctAsync(failures);
        await MalformedEnvelopeAndUsageAreHandledAsync(failures);
        await ResponseBoundaryMatrixIsEnforcedAsync(failures);
        await RetryAfterBoundariesAreMappedAsync(failures);
        await DeepSeekCacheUsageMustBeConsistentAsync(failures);
        await DiagnosticsRedactSecretsAndBodiesAsync(failures);
        return failures;
    }

    private static async Task PresetsAndCustomUseSafeEndpointsAsync(List<string> failures)
    {
        CompanionProfile[] profiles =
        [
            CompanionSettings.Default.Profiles.Single(p => p.Kind == CompanionProviderKind.DeepSeek),
            CompanionSettings.Default.Profiles.Single(p => p.Kind == CompanionProviderKind.OpenAI),
            RemoteProfile(CompanionProviderKind.CustomOpenAiCompatible, new Uri("https://models.example.test/gateway/v1"), "custom-model")
        ];
        string[] expectedUrls =
        [
            "https://api.deepseek.com/chat/completions",
            "https://api.openai.com/v1/chat/completions",
            "https://models.example.test/gateway/v1/chat/completions"
        ];

        for (int index = 0; index < profiles.Length; index++)
        {
            CompanionProfile profile = profiles[index];
            var credentials = new FakeCredentialStore();
            credentials.Set(profile.CredentialTarget!, "fixture-key");
            var handler = new RecordingHandler(request =>
            {
                string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                Check(request.Method == HttpMethod.Post, "provider must POST", failures);
                Check(request.RequestUri == new Uri(expectedUrls[index]), $"unexpected endpoint for {profile.Kind}: {request.RequestUri}", failures);
                Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "fixture-key",
                    $"bearer credential missing for {profile.Kind}", failures);
                using JsonDocument document = JsonDocument.Parse(body);
                Check(document.RootElement.GetProperty("model").GetString() == profile.Model,
                    $"configured model missing for {profile.Kind}", failures);
                JsonElement messages = document.RootElement.GetProperty("messages");
                Check(messages.GetArrayLength() == 2,
                    $"request must contain exactly system and context messages for {profile.Kind}", failures);
                Check(messages[0].GetProperty("role").GetString() == "system" &&
                      messages[0].GetProperty("content").GetString() == CompanionRequestCoordinator.SystemContract,
                    $"system contract missing for {profile.Kind}", failures);
                Check(messages[1].GetProperty("role").GetString() == "user",
                    $"context message role missing for {profile.Kind}", failures);
                using JsonDocument context = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
                Check(context.RootElement.GetProperty("CurrentTurn").GetString() == "connection-test" &&
                      context.RootElement.GetProperty("Claims").GetArrayLength() == 0 &&
                      context.RootElement.GetProperty("ClaimIds").GetArrayLength() == 0 &&
                      context.RootElement.GetProperty("EvidenceIds").GetArrayLength() == 0,
                    $"structured context contract missing for {profile.Kind}", failures);
                Check(document.RootElement.GetProperty("response_format").GetProperty("type").GetString() == "json_object",
                    $"JSON response contract missing for {profile.Kind}", failures);
                Check(document.RootElement.GetProperty("max_tokens").GetInt32() == 512,
                    $"bounded output token contract missing for {profile.Kind}", failures);
                Check(document.RootElement.GetProperty("stream").ValueKind == JsonValueKind.False,
                    $"non-streaming contract missing for {profile.Kind}", failures);
                return SuccessEnvelope();
            });
            using var client = new HttpClient(handler);
            var factory = CreateFactory(client, credentials);

            CompanionConnectionTestResult result = await factory.TestConnectionAsync(profile, CancellationToken.None);
            Check(result.IsSuccessful && result.Failure == CompanionFailureKind.None,
                $"connection test must succeed for {profile.Kind}", failures);
            Check(handler.SendCount == 1, $"connection test must make exactly one request for {profile.Kind}", failures);
        }
    }

    private static async Task RemoteSendConsentIsExplicitAndFailClosedAsync(List<string> failures)
    {
        var credentials = new FakeCredentialStore();
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("consent gate reached HTTP"));
        using var client = new HttpClient(handler);
        CompanionProfile profile = CompanionSettings.Default.Profiles.Single(p => p.Kind == CompanionProviderKind.OpenAI);
        ICompanionService service = CreateFactory(client, credentials).Create(profile);

        CompanionOperationResult defaultResult = await service.SendAsync(Draft(), CancellationToken.None);
        CompanionOperationResult rejected = await service.SendAsync(
            Draft(), firstSendConsentAccepted: false, CancellationToken.None);
        Check(defaultResult.Failure == CompanionFailureKind.Cancelled &&
              rejected.Failure == CompanionFailureKind.Cancelled,
            "remote sends must fail closed unless this call explicitly carries consent", failures);
        Check(credentials.ReadCount == 0 && handler.SendCount == 0,
            "rejected remote sends must not read credentials or send HTTP", failures);

        CompanionOperationResult accepted = await service.SendAsync(
            Draft(), firstSendConsentAccepted: true, CancellationToken.None);
        Check(accepted.Failure == CompanionFailureKind.MissingCredential,
            "explicit consent must be passed to the coordinator for this call", failures);
        Check(credentials.ReadCount == 1 && handler.SendCount == 0,
            "an explicitly accepted send may read credentials but must still fail before HTTP when missing", failures);
    }

    private static async Task CreationAndOfflineDemoNeverSendAsync(List<string> failures)
    {
        var credentials = new FakeCredentialStore();
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("unexpected HTTP send"));
        using var client = new HttpClient(handler);
        var factory = CreateFactory(client, credentials);
        CompanionProfile remote = CompanionSettings.Default.Profiles.Single(p => p.Kind == CompanionProviderKind.OpenAI);
        ICompanionService remoteService = factory.Create(remote);
        ICompanionService offline = factory.Create(CompanionSettings.Default.Profiles.Single(p => p.Kind == CompanionProviderKind.OfflineDemo));

        Check(remoteService is not FakeCompanionService, "remote profile must create coordinator-backed service", failures);
        Check(offline is FakeCompanionService, "Offline Demo must create FakeCompanionService", failures);
        Check(handler.SendCount == 0 && credentials.ReadCount == 0,
            "creating services must not send HTTP or read credentials", failures);

        CompanionOperationResult demo = await offline.SendAsync(Draft(), CancellationToken.None);
        Check(demo.Failure == CompanionFailureKind.None, "Offline Demo must remain usable", failures);
        Check(handler.SendCount == 0 && credentials.ReadCount == 0,
            "Offline Demo must never send HTTP or read credentials", failures);
    }

    private static async Task MissingCredentialFailsBeforeSendAsync(List<string> failures)
    {
        var credentials = new FakeCredentialStore();
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("missing credential reached HTTP"));
        using var client = new HttpClient(handler);
        var factory = CreateFactory(client, credentials);
        CompanionProfile profile = CompanionSettings.Default.Profiles.Single(p => p.Kind == CompanionProviderKind.OpenAI);

        CompanionConnectionTestResult test = await factory.TestConnectionAsync(profile, CancellationToken.None);
        CompanionOperationResult send = await factory.Create(profile).SendAsync(
            Draft(), firstSendConsentAccepted: true, CancellationToken.None);
        Check(test.Failure == CompanionFailureKind.MissingCredential,
            "connection test must report a missing credential", failures);
        Check(send.Failure == CompanionFailureKind.MissingCredential,
            "coordinator-backed service must report a missing profile credential", failures);
        Check(handler.SendCount == 0, "missing credential must fail before HTTP send", failures);

        foreach (string blank in new[] { string.Empty, " \t\r\n" })
        {
            credentials.Set(profile.CredentialTarget!, blank);
            CompanionConnectionTestResult blankTest = await factory.TestConnectionAsync(profile, CancellationToken.None);
            CompanionOperationResult blankSend = await factory.Create(profile).SendAsync(
                Draft(), firstSendConsentAccepted: true, CancellationToken.None);
            Check(blankTest.Failure == CompanionFailureKind.MissingCredential &&
                  blankSend.Failure == CompanionFailureKind.MissingCredential,
                "empty and whitespace credentials must map to MissingCredential", failures);
            Check(handler.SendCount == 0,
                "empty and whitespace credentials must fail before HTTP send", failures);
        }
    }

    private static async Task ErrorKindsAreMappedAsync(List<string> failures)
    {
        var cases = new (Func<HttpRequestMessage, HttpResponseMessage> Respond, CompanionFailureKind Expected)[]
        {
            (_ => new HttpResponseMessage(HttpStatusCode.Unauthorized), CompanionFailureKind.Unauthorized),
            (_ => new HttpResponseMessage(HttpStatusCode.PaymentRequired), CompanionFailureKind.InsufficientBalance),
            (_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests), CompanionFailureKind.RateLimited),
            (_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable), CompanionFailureKind.ProviderUnavailable),
            (_ => throw new HttpRequestException("host unavailable"), CompanionFailureKind.Offline),
            (_ => ContentFilteredEnvelope(), CompanionFailureKind.ContentFiltered)
        };

        foreach ((Func<HttpRequestMessage, HttpResponseMessage> respond, CompanionFailureKind expected) in cases)
        {
            CompanionProfile profile = RemoteProfile(CompanionProviderKind.CustomOpenAiCompatible, new Uri("https://provider.example.test/v1/"), "model");
            var credentials = new FakeCredentialStore();
            credentials.Set(profile.CredentialTarget!, "fixture-key");
            using var client = new HttpClient(new RecordingHandler(respond));
            CompanionConnectionTestResult result = await CreateFactory(client, credentials).TestConnectionAsync(profile, CancellationToken.None);
            Check(result.Failure == expected && !string.IsNullOrWhiteSpace(result.UserMessage),
                $"expected user-safe {expected} mapping, got {result.Failure}", failures);
        }
    }

    private static async Task CancellationAndTimeoutAreDistinctAsync(List<string> failures)
    {
        CompanionProfile profile = RemoteProfile(CompanionProviderKind.CustomOpenAiCompatible, new Uri("https://provider.example.test/v1/"), "model");
        var credentials = new FakeCredentialStore();
        credentials.Set(profile.CredentialTarget!, "fixture-key");
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return SuccessEnvelope();
        });
        using var client = new HttpClient(handler);
        var timeoutFactory = CreateFactory(client, credentials, TimeSpan.FromMilliseconds(25));
        CompanionConnectionTestResult timeout = await timeoutFactory.TestConnectionAsync(profile, CancellationToken.None);
        Check(timeout.Failure == CompanionFailureKind.Timeout, "internal deadline must map to Timeout", failures);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        CompanionConnectionTestResult cancellation = await timeoutFactory.TestConnectionAsync(profile, cancelled.Token);
        Check(cancellation.Failure == CompanionFailureKind.Cancelled, "caller cancellation must map to Cancelled", failures);

        using var duringSend = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        CompanionConnectionTestResult cancellationDuringSend = await CreateFactory(
            client, credentials, TimeSpan.FromSeconds(1)).TestConnectionAsync(profile, duringSend.Token);
        Check(cancellationDuringSend.Failure == CompanionFailureKind.Cancelled,
            "caller cancellation during HTTP send must map to Cancelled", failures);
    }

    private static async Task ResponseBoundaryMatrixIsEnforcedAsync(List<string> failures)
    {
        CompanionProfile profile = RemoteProfile(CompanionProviderKind.OpenAI, new Uri("https://api.openai.com/v1/"), "gpt-test");
        using SensitiveBuffer key = SensitiveBuffer.CopyFrom("fixture-key".ToCharArray());
        var cases = new (string Name, Func<HttpResponseMessage> Respond, CompanionFailureKind Expected)[]
        {
            ("malformed JSON", () => JsonResponse("{"), CompanionFailureKind.InvalidResponse),
            ("oversized response", () => JsonResponse(new string('x', OpenAICompatibleProvider.MaximumResponseBytes + 1)), CompanionFailureKind.InvalidResponse),
            ("length finish reason", () => EnvelopeWithFinishReason("length"), CompanionFailureKind.InvalidResponse),
            ("unknown finish reason", () => EnvelopeWithFinishReason("future_reason"), CompanionFailureKind.InvalidResponse),
            ("tool calls finish reason", () => EnvelopeWithFinishReason("tool_calls"), CompanionFailureKind.InvalidResponse),
            ("resource finish reason", () => EnvelopeWithFinishReason("insufficient_system_resource"), CompanionFailureKind.ProviderUnavailable)
        };

        foreach ((string name, Func<HttpResponseMessage> respond, CompanionFailureKind expected) in cases)
        {
            using var client = new HttpClient(new RecordingHandler(_ => respond()));
            var provider = new OpenAICompatibleProvider(client, profile, TimeSpan.FromSeconds(1), 512);
            await ExpectProviderFailureAsync(provider, key, expected, failures, name);
        }
    }

    private static async Task RetryAfterBoundariesAreMappedAsync(List<string> failures)
    {
        CompanionProfile profile = RemoteProfile(CompanionProviderKind.OpenAI, new Uri("https://api.openai.com/v1/"), "gpt-test");
        using SensitiveBuffer key = SensitiveBuffer.CopyFrom("fixture-key".ToCharArray());
        var cases = new (string Value, TimeSpan? Expected)[]
        {
            ("120", TimeSpan.FromMinutes(2)),
            ("999999", OpenAICompatibleProvider.MaximumRetryAfter),
            ("not-a-delay", null)
        };

        foreach ((string value, TimeSpan? expected) in cases)
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", value);
            using var client = new HttpClient(new RecordingHandler(_ => response));
            var provider = new OpenAICompatibleProvider(client, profile, TimeSpan.FromSeconds(1), 512);
            CompanionProviderException exception = await ExpectProviderFailureAsync(
                provider, key, CompanionFailureKind.RateLimited, failures, $"Retry-After {value}");
            Check(exception.RetryAfter == expected,
                $"Retry-After {value} expected {expected}, got {exception.RetryAfter}", failures);
        }
    }

    private static async Task DeepSeekCacheUsageMustBeConsistentAsync(List<string> failures)
    {
        CompanionProfile profile = CompanionSettings.Default.Profiles.Single(p => p.Kind == CompanionProviderKind.DeepSeek);
        using SensitiveBuffer key = SensitiveBuffer.CopyFrom("fixture-key".ToCharArray());
        const string consistent = """
            {"choices":[{"finish_reason":"stop","message":{"content":"{}"}}],"usage":{"prompt_tokens":11,"completion_tokens":7,"prompt_cache_hit_tokens":4,"prompt_cache_miss_tokens":7}}
            """;
        using (var client = new HttpClient(new RecordingHandler(_ => JsonResponse(consistent))))
        {
            var provider = new OpenAICompatibleProvider(client, profile, TimeSpan.FromSeconds(1), 512);
            CompanionProviderResult result = await provider.CompleteAsync(Request("private-user-text"), key, CancellationToken.None);
            Check(result.Usage == new CompanionUsage(11, 7, 4, 7),
                $"DeepSeek cache hit/miss usage must be preserved, got {result.Usage}", failures);
        }

        const string inconsistent = """
            {"choices":[{"finish_reason":"stop","message":{"content":"{}"}}],"usage":{"prompt_tokens":11,"completion_tokens":7,"prompt_cache_hit_tokens":4,"prompt_cache_miss_tokens":6}}
            """;
        using var invalidClient = new HttpClient(new RecordingHandler(_ => JsonResponse(inconsistent)));
        var invalidProvider = new OpenAICompatibleProvider(invalidClient, profile, TimeSpan.FromSeconds(1), 512);
        await ExpectProviderFailureAsync(
            invalidProvider, key, CompanionFailureKind.InvalidResponse, failures, "inconsistent DeepSeek cache usage");
    }

    private static async Task MalformedEnvelopeAndUsageAreHandledAsync(List<string> failures)
    {
        CompanionProfile profile = RemoteProfile(CompanionProviderKind.OpenAI, new Uri("https://api.openai.com/v1/"), "gpt-test");
        using SensitiveBuffer key = SensitiveBuffer.CopyFrom("fixture-key".ToCharArray());
        var malformedHandler = new RecordingHandler(_ => JsonResponse("{\"choices\":[]}"));
        using (var malformedClient = new HttpClient(malformedHandler))
        {
            var provider = new OpenAICompatibleProvider(malformedClient, profile, TimeSpan.FromSeconds(1), 512);
            CompanionProviderException exception = await ExpectProviderFailureAsync(provider, key, CompanionFailureKind.InvalidResponse, failures);
            Check(!exception.SafeDiagnostic.Contains("choices", StringComparison.Ordinal),
                "invalid-response diagnostic must not contain the response body", failures);
        }

        const string envelope = """
            {"choices":[{"finish_reason":"stop","message":{"content":"{}"}}],"usage":{"prompt_tokens":11,"completion_tokens":7,"prompt_tokens_details":{"cached_tokens":3}}}
            """;
        using var usageClient = new HttpClient(new RecordingHandler(_ => JsonResponse(envelope)));
        var usageProvider = new OpenAICompatibleProvider(usageClient, profile, TimeSpan.FromSeconds(1), 512);
        CompanionProviderResult result = await usageProvider.CompleteAsync(Request("private-user-text"), key, CancellationToken.None);
        Check(result.Usage == new CompanionUsage(11, 7, 3, 8),
            $"OpenAI usage must map cached and uncached input tokens, got {result.Usage}", failures);
    }

    private static async Task DiagnosticsRedactSecretsAndBodiesAsync(List<string> failures)
    {
        const string keyText = "secret-fixture-key";
        const string userText = "private-user-marker";
        CompanionProfile profile = RemoteProfile(CompanionProviderKind.CustomOpenAiCompatible, new Uri("https://provider.example.test/v1/"), "model");
        var credentials = new FakeCredentialStore();
        credentials.Set(profile.CredentialTarget!, keyText);
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent($"response-body {keyText} {userText}")
        });
        using var client = new HttpClient(handler);
        CompanionConnectionTestResult result = await CreateFactory(client, credentials).TestConnectionAsync(profile, CancellationToken.None);
        string combined = result.UserMessage + " " + result.Diagnostic;
        Check(!combined.Contains(keyText, StringComparison.Ordinal) &&
              !combined.Contains(userText, StringComparison.Ordinal) &&
              !combined.Contains("response-body", StringComparison.Ordinal),
            "diagnostics must redact bearer values, user text, and full response bodies", failures);
    }

    private static CompanionServiceFactory CreateFactory(
        HttpClient client,
        ICredentialStore credentials,
        TimeSpan? timeout = null) => new(
            client,
            credentials,
            BudgetOptions,
            new FakeAuditSink(),
            TimeProvider.System,
            timeout ?? TimeSpan.FromSeconds(1),
            512);

    private static CompanionProfile RemoteProfile(CompanionProviderKind kind, Uri baseUrl, string model)
    {
        Guid id = Guid.NewGuid();
        return new CompanionProfile(id, kind, kind.ToString(), baseUrl, model, CompanionCredentialTargets.ForProfile(id));
    }

    private static CompanionDraft Draft()
    {
        var evidence = new CompanionEvidence("e1", "work", "edition", "p. 1", true);
        var claim = new CompanionClaim("c1", "claim", "explanation", CompanionVoice.SourceSupported, [evidence]);
        return new CompanionDraft(CompanionAction.ReflectMe, "private-user-text", [claim], null);
    }

    private static CompanionRequest Request(string currentTurn) => new(
        CompanionRequestCoordinator.SystemContract,
        new CompanionContext([], currentTurn, null, new HashSet<string>(), new HashSet<string>()),
        false,
        null,
        1_000,
        512);

    private static HttpResponseMessage SuccessEnvelope() => JsonResponse(
        "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"{}\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"prompt_cache_hit_tokens\":0,\"prompt_cache_miss_tokens\":1}}");

    private static HttpResponseMessage ContentFilteredEnvelope() => JsonResponse(
        "{\"choices\":[{\"finish_reason\":\"content_filter\",\"message\":{\"content\":\"{}\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":0}}");

    private static HttpResponseMessage EnvelopeWithFinishReason(string finishReason) => JsonResponse(
        $"{{\"choices\":[{{\"finish_reason\":\"{finishReason}\",\"message\":{{\"content\":\"{{}}\"}}}}],\"usage\":{{\"prompt_tokens\":1,\"completion_tokens\":1}}}}");

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static async Task<CompanionProviderException> ExpectProviderFailureAsync(
        OpenAICompatibleProvider provider,
        SensitiveBuffer credential,
        CompanionFailureKind expected,
        List<string> failures,
        string? caseName = null)
    {
        try
        {
            await provider.CompleteAsync(Request("private-user-text"), credential, CancellationToken.None);
            failures.Add($"AI settings A2: expected provider failure {expected} for {caseName ?? "provider case"}");
            return new CompanionProviderException(expected, "test-missing-failure");
        }
        catch (CompanionProviderException exception)
        {
            Check(exception.Kind == expected, $"expected {expected}, got {exception.Kind}", failures);
            return exception;
        }
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add("AI settings A2: " + message);
        }
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, char[]> values = new(StringComparer.Ordinal);
        internal int ReadCount { get; private set; }

        internal void Set(string target, string value) => values[target] = value.ToCharArray();
        public bool Exists(string targetName) => values.ContainsKey(targetName);
        public SensitiveBuffer? Read(string targetName)
        {
            ReadCount++;
            return values.TryGetValue(targetName, out char[]? value) ? SensitiveBuffer.CopyFrom(value) : null;
        }
        public void Write(string targetName, SensitiveBuffer value) => values[targetName] = value.Span.ToArray();
        public bool Delete(string targetName) => values.Remove(targetName);
    }

    private sealed class FakeAuditSink : ICompanionAuditSink
    {
        public void Write(CompanionAuditEvent auditEvent) { }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
        internal int SendCount { get; private set; }

        internal RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : this((request, _) => Task.FromResult(respond(request))) { }

        internal RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
            this.respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            return respond(request, cancellationToken);
        }
    }
}
