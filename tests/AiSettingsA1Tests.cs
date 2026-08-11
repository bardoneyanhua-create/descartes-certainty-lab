using Descartes.CertaintyLab.ThoughtCompanion.Security;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;
using System.Text.Json.Nodes;

internal static class AiSettingsA1Tests
{
    internal static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition)
            {
                failures.Add("AI settings A1: " + message);
            }
        }

        CompanionSettings defaults = CompanionSettings.Default;
        Check(defaults.Profiles.Count == 3, "defaults must contain Offline Demo, DeepSeek, and OpenAI");
        CompanionProfile offline = defaults.Profiles.Single(profile => profile.Kind == CompanionProviderKind.OfflineDemo);
        CompanionProfile deepSeek = defaults.Profiles.Single(profile => profile.Kind == CompanionProviderKind.DeepSeek);
        CompanionProfile openAi = defaults.Profiles.Single(profile => profile.Kind == CompanionProviderKind.OpenAI);
        Check(defaults.ActiveProfileId == offline.Id, "Offline Demo must be active by default");
        Check(deepSeek.BaseUrl == new Uri("https://api.deepseek.com/") && !string.IsNullOrWhiteSpace(deepSeek.Model),
            "DeepSeek preset must have a validated HTTPS URL and model");
        Check(openAi.BaseUrl == new Uri("https://api.openai.com/v1/") && !string.IsNullOrWhiteSpace(openAi.Model),
            "OpenAI preset must have a validated HTTPS URL and model");

        void ExpectPresetEndpointRejected(CompanionProfile preset, Uri tamperedUrl)
        {
            try
            {
                _ = new CompanionProfile(
                    preset.Id,
                    preset.Kind,
                    preset.DisplayName,
                    tamperedUrl,
                    preset.Model,
                    preset.CredentialTarget);
                failures.Add($"AI settings A1: {preset.Kind} must reject a non-canonical endpoint");
            }
            catch (ArgumentException)
            {
            }
        }

        ExpectPresetEndpointRejected(deepSeek, new Uri("https://attacker.example/v1/"));
        ExpectPresetEndpointRejected(openAi, new Uri("https://attacker.example/v1/"));

        Guid customId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        string customTarget = CompanionCredentialTargets.ForProfile(customId);
        var custom = new CompanionProfile(
            customId,
            CompanionProviderKind.CustomOpenAiCompatible,
            "Local name",
            new Uri("https://models.example.test/v1/"),
            "editable-model",
            customTarget);
        Check(custom.Model == "editable-model" && custom.CredentialTarget == customTarget,
            "custom profile must preserve editable model and its profile credential target");

        void ExpectProfileRejected(Uri? url, string displayName = "Custom", string model = "model", string? target = null)
        {
            try
            {
                _ = new CompanionProfile(
                    customId,
                    CompanionProviderKind.CustomOpenAiCompatible,
                    displayName,
                    url,
                    model,
                    target ?? customTarget);
                failures.Add($"AI settings A1: invalid profile must be rejected: {url}");
            }
            catch (ArgumentException)
            {
            }
        }

        ExpectProfileRejected(new Uri("http://models.example.test/v1/"));
        ExpectProfileRejected(new Uri("https://user:pass@models.example.test/v1/"));
        ExpectProfileRejected(new Uri("https://models.example.test/v1/?x=1"));
        ExpectProfileRejected(new Uri("https://models.example.test/v1/#fragment"));
        ExpectProfileRejected(new Uri("https://127.0.0.1/v1/"));
        ExpectProfileRejected(new Uri("https://127.0.0.2/v1/"));
        ExpectProfileRejected(new Uri("https://127.255.255.254/v1/"));
        ExpectProfileRejected(new Uri("https://127.1/v1/"));
        ExpectProfileRejected(new Uri("https://2130706433/v1/"));
        ExpectProfileRejected(new Uri("https://0177.0.0.1/v1/"));
        ExpectProfileRejected(new Uri("https://0x7f000001/v1/"));
        ExpectProfileRejected(new Uri("https://10.0.0.1/v1/"));
        ExpectProfileRejected(new Uri("https://172.16.0.1/v1/"));
        ExpectProfileRejected(new Uri("https://192.168.0.1/v1/"));
        ExpectProfileRejected(new Uri("https://[::1]/v1/"));
        ExpectProfileRejected(new Uri("https://[0:0:0:0:0:0:0:1]/v1/"));
        ExpectProfileRejected(new Uri("https://[::ffff:127.0.0.1]/v1/"));
        ExpectProfileRejected(new Uri("https://[::ffff:7f00:1]/v1/"));
        ExpectProfileRejected(new Uri("https://[fd00::1]/v1/"));
        ExpectProfileRejected(new Uri("https://localhost/v1/"));
        ExpectProfileRejected(new Uri("https://LOCALHOST/v1/"));
        ExpectProfileRejected(new Uri("https://localhost./v1/"));
        ExpectProfileRejected(new Uri("https://api.localhost/v1/"));
        ExpectProfileRejected(new Uri("https://api.localhost./v1/"));
        ExpectProfileRejected(new Uri("https://models.example.test/v1/"), " ");
        ExpectProfileRejected(new Uri("https://models.example.test/v1/"), model: " ");
        ExpectProfileRejected(new Uri("https://models.example.test/v1/"), target: CompanionCredentialTargets.ForProfile(Guid.NewGuid()));

        void ExpectSettingsRejected(Guid activeProfileId, CompanionProfile[] profiles, string scenario)
        {
            try
            {
                _ = new CompanionSettings(activeProfileId, profiles);
                failures.Add($"AI settings A1: semantically invalid settings must be rejected: {scenario}");
            }
            catch (ArgumentException)
            {
            }
        }

        var alternateOffline = new CompanionProfile(
            Guid.NewGuid(),
            CompanionProviderKind.OfflineDemo,
            "Offline Demo",
            null,
            string.Empty,
            null);
        var damagedOffline = new CompanionProfile(
            offline.Id,
            CompanionProviderKind.OfflineDemo,
            "Offline Demo tampered",
            null,
            string.Empty,
            null);
        Guid duplicateDeepSeekId = Guid.NewGuid();
        var duplicateDeepSeek = new CompanionProfile(
            duplicateDeepSeekId,
            CompanionProviderKind.DeepSeek,
            "DeepSeek duplicate",
            CompanionProfile.DeepSeekBaseUrl,
            "deepseek-chat",
            CompanionCredentialTargets.ForProfile(duplicateDeepSeekId));
        ExpectSettingsRejected(deepSeek.Id, [deepSeek, openAi], "missing Offline Demo");
        ExpectSettingsRejected(offline.Id, [offline, alternateOffline, deepSeek, openAi], "duplicate Offline Demo");
        ExpectSettingsRejected(damagedOffline.Id, [damagedOffline, deepSeek, openAi], "damaged canonical Offline Demo");
        ExpectSettingsRejected(offline.Id, [offline, deepSeek, duplicateDeepSeek, openAi], "duplicate DeepSeek preset");
        ExpectSettingsRejected(Guid.NewGuid(), [offline, deepSeek, openAi], "missing active profile");

        string settingsDirectory = Path.Combine(Path.GetTempPath(), "certainty-lab-a1-" + Guid.NewGuid().ToString("N"));
        string settingsPath = Path.Combine(settingsDirectory, "companion-settings.json");
        try
        {
            var store = new JsonCompanionSettingsStore(settingsPath);
            CompanionSettingsLoadResult missing = store.Load();
            Check(missing.Settings.ActiveProfileId == CompanionSettings.Default.ActiveProfileId &&
                  !string.IsNullOrWhiteSpace(missing.Diagnostic),
                "missing settings must fail safely to Offline Demo with a diagnostic");

            Directory.CreateDirectory(settingsDirectory);
            File.WriteAllText(settingsPath, "{broken-json");
            CompanionSettingsLoadResult malformed = store.Load();
            Check(malformed.Settings.ActiveProfileId == CompanionSettings.Default.ActiveProfileId &&
                  !string.IsNullOrWhiteSpace(malformed.Diagnostic) &&
                  !malformed.Diagnostic.Contains("broken-json", StringComparison.Ordinal),
                "malformed settings must fail safely with a non-secret diagnostic");

            void CheckTamperedPresetFailsSafe(
                CompanionProfile preset,
                string tamperedBaseUrl,
                string scenario)
            {
                string json = $$"""
                    {
                      "activeProfileId": "{{preset.Id}}",
                      "profiles": [{
                        "id": "47f93aa7-160b-45eb-b76a-0982870a3da8",
                        "kind": "offlineDemo",
                        "displayName": "Offline Demo",
                        "baseUrl": null,
                        "model": "",
                        "credentialTarget": null
                      }, {
                        "id": "{{preset.Id}}",
                        "kind": "{{JsonKind(preset.Kind)}}",
                        "displayName": "{{preset.DisplayName}}",
                        "baseUrl": "{{tamperedBaseUrl}}",
                        "model": "{{preset.Model}}",
                        "credentialTarget": "{{preset.CredentialTarget}}"
                      }]
                    }
                    """;
                File.WriteAllText(settingsPath, json);
                CompanionSettingsLoadResult result = store.Load();
                Check(result.Settings.ActiveProfileId == CompanionSettings.Default.ActiveProfileId &&
                      !string.IsNullOrWhiteSpace(result.Diagnostic) &&
                      !result.Diagnostic.Contains("attacker.example", StringComparison.Ordinal),
                    $"{scenario} must fail safely to canonical Offline Demo with a non-secret diagnostic");
            }

            CheckTamperedPresetFailsSafe(deepSeek, "https://attacker.example/v1/", "tampered DeepSeek endpoint");
            CheckTamperedPresetFailsSafe(openAi, "https://attacker.example/v1/", "tampered OpenAI endpoint");

            void CheckSemanticMutationFailsSafe(Action<JsonObject> mutate, string scenario)
            {
                store.Save(CompanionSettings.Default);
                JsonObject root = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
                mutate(root);
                root["untrusted"] = "semantic-diagnostic-marker";
                File.WriteAllText(settingsPath, root.ToJsonString());
                CompanionSettingsLoadResult result = store.Load();
                Check(result.Settings.ActiveProfileId == CompanionSettings.Default.ActiveProfileId &&
                      result.Settings.Profiles.Count(profile => profile.Kind == CompanionProviderKind.OfflineDemo) == 1 &&
                      !string.IsNullOrWhiteSpace(result.Diagnostic) &&
                      !result.Diagnostic.Contains("semantic-diagnostic-marker", StringComparison.Ordinal),
                    $"{scenario} must make the entire Load fail safely to canonical Offline Demo");
            }

            CheckSemanticMutationFailsSafe(root =>
            {
                JsonArray profiles = root["profiles"]!.AsArray();
                JsonNode offlineNode = profiles.Single(node =>
                    node!["kind"]!.GetValue<string>() == "offlineDemo")!;
                profiles.Remove(offlineNode);
                root["activeProfileId"] = deepSeek.Id;
            }, "missing Offline Demo");
            CheckSemanticMutationFailsSafe(root =>
            {
                JsonArray profiles = root["profiles"]!.AsArray();
                JsonObject duplicate = profiles.Single(node =>
                    node!["kind"]!.GetValue<string>() == "offlineDemo")!.DeepClone().AsObject();
                duplicate["id"] = Guid.NewGuid();
                profiles.Add(duplicate);
            }, "duplicate Offline Demo");
            CheckSemanticMutationFailsSafe(root =>
            {
                JsonObject offlineNode = root["profiles"]!.AsArray().Single(node =>
                    node!["kind"]!.GetValue<string>() == "offlineDemo")!.AsObject();
                offlineNode["displayName"] = "Offline Demo tampered";
            }, "damaged Offline Demo");
            CheckSemanticMutationFailsSafe(root =>
            {
                JsonArray profiles = root["profiles"]!.AsArray();
                JsonObject duplicate = profiles.Single(node =>
                    node!["kind"]!.GetValue<string>() == "deepSeek")!.DeepClone().AsObject();
                Guid duplicateId = Guid.NewGuid();
                duplicate["id"] = duplicateId;
                duplicate["credentialTarget"] = CompanionCredentialTargets.ForProfile(duplicateId);
                profiles.Add(duplicate);
            }, "duplicate built-in preset kind");
            CheckSemanticMutationFailsSafe(root =>
            {
                root["activeProfileId"] = Guid.NewGuid();
            }, "missing active profile");

            void CheckNullBearingJsonFailsSafe(string json, string scenario)
            {
                File.WriteAllText(settingsPath, json);
                try
                {
                    CompanionSettingsLoadResult result = store.Load();
                    Check(result.Settings.ActiveProfileId == CompanionSettings.Default.ActiveProfileId &&
                          !string.IsNullOrWhiteSpace(result.Diagnostic) &&
                          !result.Diagnostic.Contains("diagnostic-marker", StringComparison.Ordinal),
                        $"{scenario} must fail safely to Offline Demo with a non-secret diagnostic");
                }
                catch (Exception exception)
                {
                    failures.Add($"AI settings A1: {scenario} escaped Load: {exception.GetType().Name}");
                }
            }

            CheckNullBearingJsonFailsSafe(
                """
                {
                  "activeProfileId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                  "profiles": [null],
                  "untrusted": "diagnostic-marker"
                }
                """,
                "null profile element");
            CheckNullBearingJsonFailsSafe(
                """
                {
                  "activeProfileId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                  "profiles": [{
                    "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                    "kind": "offlineDemo",
                    "displayName": "Offline Demo",
                    "baseUrl": null,
                    "model": null,
                    "credentialTarget": null
                  }],
                  "untrusted": "diagnostic-marker"
                }
                """,
                "Offline Demo null model");
            CheckNullBearingJsonFailsSafe(
                """
                {
                  "activeProfileId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                  "profiles": [{
                    "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                    "kind": "customOpenAiCompatible",
                    "displayName": null,
                    "baseUrl": null,
                    "model": null,
                    "credentialTarget": null
                  }],
                  "untrusted": "diagnostic-marker"
                }
                """,
                "remote null-bearing profile");

            File.WriteAllText(
                settingsPath,
                """
                {
                  "activeProfileId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                  "profiles": [{
                    "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                    "kind": "customOpenAiCompatible",
                    "displayName": "Malformed URL",
                    "baseUrl": "not-an-absolute-url",
                    "model": "model",
                    "credentialTarget": "PhilosophyVault/Descartes.CertaintyLab/Profiles/aaaaaaaabbbbccccddddeeeeeeeeeeee"
                  }]
                }
                """);
            try
            {
                CompanionSettingsLoadResult invalidUrl = store.Load();
                Check(invalidUrl.Settings.ActiveProfileId == CompanionSettings.Default.ActiveProfileId &&
                      !string.IsNullOrWhiteSpace(invalidUrl.Diagnostic),
                    "semantically malformed settings must fail safely to Offline Demo");
            }
            catch (Exception exception)
            {
                failures.Add($"AI settings A1: semantically malformed settings escaped Load: {exception.GetType().Name}");
            }

            var settings = new CompanionSettings(custom.Id, [offline, deepSeek, openAi, custom]);
            store.Save(settings);
            string firstJson = File.ReadAllText(settingsPath);
            Check(!firstJson.Contains("bearer", StringComparison.OrdinalIgnoreCase) &&
                  !firstJson.Contains("apiKey", StringComparison.OrdinalIgnoreCase),
                "settings JSON must not contain key, bearer token, or credential values");
            Check(firstJson.Contains(customTarget, StringComparison.Ordinal),
                "settings JSON may persist the profile credential target reference");

            var replacement = new CompanionSettings(deepSeek.Id, [offline, deepSeek, openAi]);
            store.Save(replacement);
            CompanionSettingsLoadResult reloaded = store.Load();
            Check(reloaded.Diagnostic is null && reloaded.Settings.ActiveProfileId == deepSeek.Id,
                "atomic replacement must load the complete replacement settings");
            Check(Directory.GetFiles(settingsDirectory, "*.tmp").Length == 0,
                "atomic save must leave no same-directory temporary file");

            var failingOperations = new FailingSaveFileOperations();
            var failingStore = new JsonCompanionSettingsStore(settingsPath, failingOperations);
            try
            {
                failingStore.Save(replacement);
                failures.Add("AI settings A1: simulated atomic replacement failure must escape Save");
            }
            catch (InvalidOperationException exception)
            {
                Check(exception.Message == FailingSaveFileOperations.OriginalFailureMessage,
                    "temporary-file cleanup failure must not mask the original save failure");
            }
            catch (Exception exception)
            {
                failures.Add($"AI settings A1: cleanup masked original save failure with {exception.GetType().Name}");
            }
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, recursive: true);
            }
        }

        Check(customTarget == CompanionCredentialTargets.ForProfile(customId),
            "profile credential target derivation must be stable");
        Check(customTarget != CompanionCredentialTargets.ForProfile(Guid.NewGuid()),
            "different profiles must have isolated credential targets");

        var native = new MemoryCredentialNativeApi();
        var credentials = new WindowsCredentialStore(native);
        string firstTarget = CompanionCredentialTargets.ForProfile(Guid.NewGuid());
        string secondTarget = CompanionCredentialTargets.ForProfile(Guid.NewGuid());
        using (SensitiveBuffer first = SensitiveBuffer.CopyFrom("alpha-value"))
        using (SensitiveBuffer second = SensitiveBuffer.CopyFrom("beta-value"))
        {
            credentials.Write(firstTarget, first);
            credentials.Write(secondTarget, second);
        }
        using (SensitiveBuffer? firstRead = credentials.Read(firstTarget))
        using (SensitiveBuffer? secondRead = credentials.Read(secondTarget))
        {
            Check(firstRead is not null && firstRead.Span.SequenceEqual("alpha-value"),
                "first profile credential must remain isolated");
            Check(secondRead is not null && secondRead.Span.SequenceEqual("beta-value"),
                "second profile credential must remain isolated");
        }

        using (SensitiveBuffer replacement = SensitiveBuffer.CopyFrom("replacement"))
        {
            credentials.Write(firstTarget, replacement);
        }
        using (SensitiveBuffer? replacedRead = credentials.Read(firstTarget))
        {
            Check(replacedRead is not null && replacedRead.Span.SequenceEqual("replacement"),
                "credential write must replace the same profile target atomically");
        }
        Check(credentials.Delete(firstTarget) && !credentials.Exists(firstTarget),
            "credential deletion must be idempotent and remove the profile value");
        Check(credentials.Delete(firstTarget), "deleting a missing profile credential must succeed");

        foreach (string forbiddenTarget in new[]
                 {
                     "OtherApplication/Profile/" + Guid.NewGuid().ToString("N"),
                     "PhilosophyVault/Descartes.CertaintyLab/Profiles/not-a-guid",
                     CompanionCredentialTargets.ForProfile(Guid.NewGuid()) + "/suffix",
                 })
        {
            try
            {
                _ = credentials.Exists(forbiddenTarget);
                failures.Add("AI settings A1: arbitrary credential target must be rejected");
            }
            catch (ArgumentException)
            {
            }
        }

        using (SensitiveBuffer legacy = SensitiveBuffer.CopyFrom("legacy-value"))
        {
            credentials.Write(WindowsCredentialStore.TargetName, legacy);
        }
        Check(credentials.Exists(WindowsCredentialStore.TargetName),
            "existing legacy DeepSeek credential target must remain accepted");
        Check(native.LastWriteBuffer is not null && native.LastWriteBuffer.All(value => value == 0),
            "managed native-write buffer must be zeroized after use");
        Check(native.LastReadBuffer is not null && native.LastReadBuffer.All(value => value == 0),
            "managed native-read buffer must be zeroized after use");

        byte[]? failedCopy = null;
        try
        {
            _ = WindowsCredentialNativeApi.CopyCredentialBlob(12, bytes =>
            {
                failedCopy = bytes;
                Array.Fill(bytes, (byte)0x5a);
                throw new InvalidOperationException("simulated native copy failure");
            });
            failures.Add("AI settings A1: failed credential copy must throw");
        }
        catch (InvalidOperationException)
        {
        }
        Check(failedCopy is not null && failedCopy.All(value => value == 0),
            "failed native credential copy must zeroize its managed buffer");

        return failures;
    }

    private static string JsonKind(CompanionProviderKind kind) => kind switch
    {
        CompanionProviderKind.DeepSeek => "deepSeek",
        CompanionProviderKind.OpenAI => "openAI",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private sealed class MemoryCredentialNativeApi : ICredentialNativeApi
    {
        private readonly Dictionary<string, byte[]> values = new(StringComparer.Ordinal);

        internal byte[]? LastWriteBuffer { get; private set; }
        internal byte[]? LastReadBuffer { get; private set; }

        public bool Write(string targetName, byte[] credentialBlob)
        {
            LastWriteBuffer = credentialBlob;
            values[targetName] = credentialBlob.ToArray();
            return true;
        }

        public byte[]? Read(string targetName)
        {
            if (!values.TryGetValue(targetName, out byte[]? value))
            {
                return null;
            }

            LastReadBuffer = value.ToArray();
            return LastReadBuffer;
        }

        public bool Delete(string targetName)
        {
            values.Remove(targetName);
            return true;
        }
    }

    private sealed class FailingSaveFileOperations : ICompanionSettingsFileOperations
    {
        internal const string OriginalFailureMessage = "simulated original save failure";

        public void MoveReplace(string sourcePath, string destinationPath) =>
            throw new InvalidOperationException(OriginalFailureMessage);

        public bool Exists(string path) => true;

        public void Delete(string path) =>
            throw new UnauthorizedAccessException("simulated cleanup failure");
    }
}
