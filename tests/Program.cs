using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Descartes.CertaintyLab;
using Descartes.CertaintyLab.ThoughtCompanion;
using Descartes.CertaintyLab.ThoughtCompanion.Security;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

bool aiSettingsA1Only = args.Contains("--a1-only", StringComparer.Ordinal);
bool aiSettingsA2Only = args.Contains("--a2-only", StringComparer.Ordinal);
bool aiSettingsA3Only = args.Contains("--a3-only", StringComparer.Ordinal);
bool homeOnly = args.Contains("--home-only", StringComparer.Ordinal);
bool expansion90Only = args.Contains("--expansion-90", StringComparer.Ordinal);
bool expansion94Only = args.Contains("--expansion-94", StringComparer.Ordinal);
bool quizTitleOnly = args.Contains("--quiz-title-only", StringComparer.Ordinal);
bool baconPromptsOnly = args.Contains("--bacon-prompts-only", StringComparer.Ordinal);
bool anyFocusedRun = aiSettingsA1Only || aiSettingsA2Only || aiSettingsA3Only || homeOnly || expansion90Only || expansion94Only || quizTitleOnly || baconPromptsOnly;
string? candidateArgument = args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal));
string candidateRoot = Path.GetFullPath(
    candidateArgument is not null
        ? candidateArgument
        : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
string appRoot = Path.Combine(
    candidateRoot,
    "application",
    "Descartes.CertaintyLab");
string contentRoot = Path.Combine(appRoot, "Content");

var failures = new List<string>();
if (!anyFocusedRun || aiSettingsA1Only)
{
    failures.AddRange(AiSettingsA1Tests.Run());
}
if (!anyFocusedRun || aiSettingsA2Only)
{
    failures.AddRange(await AiSettingsA2Tests.RunAsync());
}
if (!anyFocusedRun || aiSettingsA3Only)
{
    failures.AddRange(await AiSettingsA3Tests.RunAsync(candidateRoot));
}
if (!anyFocusedRun || homeOnly)
{
    failures.AddRange(HomeNavigationTests.Run(candidateRoot));
}
if (!anyFocusedRun || quizTitleOnly)
{
    failures.AddRange(LearningCheckTitleTests.Run(candidateRoot));
}
void Check(bool condition, string message)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

if (aiSettingsA1Only)
{
    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"FAIL ai-settings-a1 failures={failures.Count}");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }
        return 1;
    }

    Console.WriteLine("PASS ai-settings-a1");
    return 0;
}

if (aiSettingsA2Only)
{
    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"FAIL ai-settings-a2 failures={failures.Count}");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }
        return 1;
    }

    Console.WriteLine("PASS ai-settings-a2");
    return 0;
}

if (aiSettingsA3Only)
{
    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"FAIL ai-settings-a3 failures={failures.Count}");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }
        return 1;
    }

    Console.WriteLine("PASS ai-settings-a3");
    return 0;
}

if (homeOnly)
{
    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"FAIL home-navigation failures={failures.Count}");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }
        return 1;
    }

    Console.WriteLine("PASS home-navigation");
    return 0;
}

if (quizTitleOnly)
{
    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"FAIL learning-check-title failures={failures.Count}");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }
        return 1;
    }

    Console.WriteLine("PASS learning-check-title routes=4 fallback=1 accessibility=consistent");
    return 0;
}

if (baconPromptsOnly)
{
    using JsonDocument baconDocument = JsonDocument.Parse(File.ReadAllText(
        Path.Combine(contentRoot, "francis-bacon-learning-route.json")));
    JsonElement[] baconChecks = baconDocument.RootElement
        .GetProperty("checks")
        .EnumerateArray()
        .ToArray();
    Check(baconChecks.Length == 56, "Bacon route must contain exactly 56 learning checks");

    var promptPattern = new Regex(
        "陈述A：“(?<a1>[^”]+)”能够支持“(?<a2>[^”]+)”。\\r?\\n" +
        "陈述B：“(?<b1>[^”]+)”能够支持“(?<b2>[^”]+)”。\\r?\\n" +
        "请选择A、B的真假组合。$",
        RegexOptions.CultureInvariant);
    string[] truthCombinations = ["A真，B真", "A真，B假", "A假，B真", "A假，B假"];

    foreach (JsonElement check in baconChecks)
    {
        string id = check.GetProperty("id").GetString()!;
        string prompt = check.GetProperty("prompt").GetString()!;
        Match promptMatch = promptPattern.Match(prompt);
        Check(promptMatch.Success, $"Bacon prompt must expose two concrete support claims before answering: {id}");

        JsonElement[] options = check.GetProperty("options").EnumerateArray().ToArray();
        Check(options.Length == 4, $"Bacon check must retain four truth-combination options: {id}");
        Check(options.Select(option => option.GetProperty("text").GetString()!)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(truthCombinations),
            $"Bacon check options must be the exact four A/B truth combinations: {id}");

        string correctOptionId = check.GetProperty("correctOptionId").GetString()!;
        JsonElement correctOption = options.Single(option =>
            option.GetProperty("id").GetString() == correctOptionId);
        string correctText = correctOption.GetProperty("text").GetString()!;
        string correctFeedback = correctOption.GetProperty("feedback").GetString()!;
        MatchCollection feedbackSentences = Regex.Matches(
            correctFeedback,
            "[^。]+。",
            RegexOptions.CultureInvariant);
        Check(feedbackSentences.Count == 4,
            $"Bacon correct feedback must retain exactly two sentence pairs: {id}");
        if (!promptMatch.Success || feedbackSentences.Count != 4)
        {
            continue;
        }

        string[] promptClaims =
        [
            promptMatch.Groups["a1"].Value + "。",
            promptMatch.Groups["a2"].Value + "。",
            promptMatch.Groups["b1"].Value + "。",
            promptMatch.Groups["b2"].Value + "。",
        ];
        Check(promptClaims.SequenceEqual(feedbackSentences.Select(sentence => sentence.Value.Trim())),
            $"Bacon prompt claims must match the option-specific feedback sentence pairs: {id}");

        bool correctA = correctText.StartsWith("A真", StringComparison.Ordinal);
        bool correctB = correctText.EndsWith("B真", StringComparison.Ordinal);
        string pairA = promptClaims[0] + " " + promptClaims[1];
        string pairB = promptClaims[2] + " " + promptClaims[3];
        foreach (JsonElement option in options)
        {
            string optionText = option.GetProperty("text").GetString()!;
            string feedback = option.GetProperty("feedback").GetString()!;
            bool selectedA = optionText.StartsWith("A真", StringComparison.Ordinal);
            bool selectedB = optionText.EndsWith("B真", StringComparison.Ordinal);
            string expectedAFeedback = selectedA == correctA
                ? pairA
                : pairA + (correctA
                    ? " 所选关系删去了这条必要联系。"
                    : " 所选关系接受了这一步越界。");
            string expectedBFeedback = selectedB == correctB
                ? pairB
                : pairB + (correctB
                    ? " 所选关系删去了这条必要联系。"
                    : " 所选关系接受了这一步越界。");
            Check(feedback == expectedAFeedback + " " + expectedBFeedback,
                $"Bacon option feedback must agree with its A/B truth combination: {id}/{optionText}");
        }
    }

    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"FAIL bacon-prompts failures={failures.Count}");
        foreach (string failure in failures) Console.Error.WriteLine($"- {failure}");
        return 1;
    }
    Console.WriteLine("PASS bacon-prompts checks=56 concreteClaims=112 optionSemantics=224");
    return 0;
}

LearningRouteRegistry registry = LearningRouteRegistry.Load(contentRoot);
if (expansion90Only)
{
    string[] addedRouteIds =
    [
        "cicero-republic-skepticism-duty",
        "sextus-empiricus-suspension-appearance-practice",
        "william-of-ockham-signs-cognition-power",
        "quine-web-of-belief-reference-ontology",
        "emilie-du-chatelet-hypotheses-force-happiness",
        "judith-butler-performativity-recognition-precarity",
        "enrique-dussel-exteriority-liberation-transmodernity",
        "kwasi-wiredu-conceptual-decolonization-consensus",
    ];
    KnowledgeCatalog expansionCatalog = KnowledgeCatalog.Load(
        Path.Combine(contentRoot, "knowledge-reader-catalog.json"));
    foreach (string routeId in addedRouteIds)
    {
        Check(registry.Routes.Count(route => route.RouteId == routeId) == 1,
            $"expansion route must occur once: {routeId}");
        Check(expansionCatalog.AllEntries.Count(entry => entry.LearningRouteId == routeId) == 1,
            $"expansion knowledge card must occur once: {routeId}");
    }
    Check(registry.Routes.Count == 90, "expansion registry count must be 82");
    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"FAIL expansion-90 failures={failures.Count}");
        foreach (string failure in failures) Console.Error.WriteLine($"- {failure}");
        return 1;
    }
    Console.WriteLine("PASS expansion-90 routes=90 catalogMappings=90 added=4 duplicate=0");
    return 0;
}
if (expansion94Only)
{
    (string RouteId, string FileName)[] addedRoutes =
    [
        ("mary-astell-reason-education-freedom", "mary-astell-learning-route.json"),
        ("watsuji-tetsuro-betweenness-ethics-climate", "watsuji-tetsuro-learning-route.json"),
        ("maria-lugones-world-travelling-coloniality-resistance", "maria-lugones-learning-route.json"),
        ("anton-wilhelm-amo-mind-body-knowledge-method", "anton-wilhelm-amo-learning-route.json"),
    ];
    KnowledgeCatalog expansionCatalog = KnowledgeCatalog.Load(
        Path.Combine(contentRoot, "knowledge-reader-catalog.json"));
    int catalogMappings = expansionCatalog.AllEntries.Count(
        entry => !string.IsNullOrWhiteSpace(entry.LearningRouteId));
    foreach ((string routeId, string fileName) in addedRoutes)
    {
        LearningRouteCatalogItem[] matches = registry.Routes
            .Where(route => route.RouteId == routeId)
            .ToArray();
        Check(matches.Length == 1, $"expansion route must occur once: {routeId}");
        Check(expansionCatalog.AllEntries.Count(entry => entry.LearningRouteId == routeId) == 1,
            $"expansion knowledge card must occur once: {routeId}");
        if (matches.Length != 1) continue;
        Check(matches[0].FileName == fileName,
            $"expansion route must use canonical filename: {routeId}");
        string routePath = Path.Combine(contentRoot, fileName);
        Check(File.Exists(routePath), $"expansion route file must exist: {fileName}");
        if (!File.Exists(routePath)) continue;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(routePath));
        JsonElement root = document.RootElement;
        Check(root.GetProperty("schemaVersion").GetString() == "1.0",
            $"expansion route schema must be canonical 1.0: {routeId}");
        Check(root.GetProperty("routes").GetArrayLength() == 1 &&
              root.GetProperty("routes")[0].GetProperty("id").GetString() == routeId,
            $"expansion pack must contain exactly its registered route: {routeId}");
        Check(root.GetProperty("lessons").GetArrayLength() == 16,
            $"expansion pack must contain 16 lessons: {routeId}");
        Check(root.GetProperty("nodes").GetArrayLength() == 32,
            $"expansion pack must contain 32 claim nodes: {routeId}");
        Check(root.GetProperty("checks").GetArrayLength() == 64,
            $"expansion pack must contain 64 checks: {routeId}");
        int paragraphCount = root.GetProperty("lessons").EnumerateArray()
            .Sum(lesson => lesson.GetProperty("sections").EnumerateArray()
                .Sum(section => section.GetProperty("paragraphs").GetArrayLength()));
        Check(paragraphCount == 32,
            $"expansion pack must contain 32 projected paragraphs: {routeId}");
    }
    Check(registry.Routes.Count == 94, "expansion-94 registry count must be 94");
    Check(catalogMappings == 94, "expansion-94 catalog mapping count must be 94");
    Check(!File.Exists(Path.Combine(contentRoot,
            "mary-astell-reason-education-freedom-learning-route.json")),
        "Mary source-only long filename must not exist");
    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"FAIL expansion-94 failures={failures.Count}");
        foreach (string failure in failures) Console.Error.WriteLine($"- {failure}");
        return 1;
    }
    Console.WriteLine(
        $"PASS expansion-94 routes={registry.Routes.Count} catalogMappings={catalogMappings} added=4 duplicate=0 shapes=4x16/32/32/64");
    return 0;
}

Check(registry.Routes.Count == 94, "registry runtime route count must be 94");
Check(registry.Routes.Select(route => route.RouteId).Distinct(StringComparer.Ordinal).Count() == 94,
    "registry runtime route IDs must be an exact unique 94-set");
Check(registry.Routes.Select(route => route.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 94,
    "registry runtime file names must be an exact unique 94-set");

foreach (LearningRouteCatalogItem item in registry.Routes)
{
    string path = Path.Combine(contentRoot, item.FileName);
    Check(File.Exists(path), $"registered content is missing: {item.FileName}");
    using FileStream stream = File.OpenRead(path);
    LearningPack pack = LearningPack.Load(stream);
    LearningRouteDefinition route = pack.GetRoute(item.RouteId);
    Check(route.Title == item.Title, $"registry/pack title mismatch: {item.RouteId}");
    Check(item.AutomationName.Contains(item.Title, StringComparison.Ordinal),
        $"route accessibility name omits title: {item.RouteId}");
}

void CheckPackMutationRejected(string fileName, string before, string after, string message)
{
    string json = File.ReadAllText(Path.Combine(contentRoot, fileName));
    Check(json.Contains(before, StringComparison.Ordinal), $"mutation fixture missing: {fileName}");
    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(
        json.Replace(before, after, StringComparison.Ordinal));
    try
    {
        using var stream = new MemoryStream(bytes);
        _ = LearningPack.Load(stream);
        failures.Add(message);
    }
    catch (Exception exception) when (exception is JsonException or InvalidDataException)
    {
    }
}

CheckPackMutationRejected(
    "deleuze-learning-route.json",
    "\"status\": \"authoringCandidate\"",
    "\"status\": \"arbitraryStatus\"",
    "unknown fixed-route node status must fail closed");
CheckPackMutationRejected(
    "deleuze-learning-route.json",
    "\"id\": \"deleuze-difference-immanence-becoming\"",
    "\"id\": \"unregistered-alias-route\"",
    "formal schema alias must remain bound to its registered route identity");
CheckPackMutationRejected(
    "al-farabi-learning-route.json",
    "\"status\": \"teachingReady\"",
    "\"status\": \"authoringCandidate\"",
    "authoringCandidate must not be accepted by an unrelated 1.0 route");
CheckPackMutationRejected(
    "austin-learning-route.json",
    "\"schemaVersion\": \"strict-six/v1\"",
    "\"schemaVersion\": \"strict-six/v999\"",
    "unknown strict-six schema must fail closed");
CheckPackMutationRejected(
    "averroes-learning-route.json",
    "\"id\": \"ibn-rushd-demonstration-commentary-intellect\"",
    "\"id\": \"unregistered-strict-six-route\"",
    "unregistered strict-six route must fail closed");

string csproj = File.ReadAllText(Path.Combine(appRoot, "Descartes.CertaintyLab.csproj"));
HashSet<string> projectRouteFiles = Regex.Matches(
        csproj,
        "Content\\\\(?<file>[^\"<>]+-learning-route\\.json)",
        RegexOptions.CultureInvariant)
    .Select(match => match.Groups["file"].Value)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
Check(projectRouteFiles.SetEquals(registry.Routes.Select(route => route.FileName)),
    "csproj route content must equal the registry file set");

Check(!registry.TryResolve("not-a-route", out _), "invalid route must fail closed");
try
{
    registry.Resolve("not-a-route");
    failures.Add("invalid route Resolve must throw");
}
catch (KeyNotFoundException)
{
}

KnowledgeCatalog catalog = KnowledgeCatalog.Load(
    Path.Combine(contentRoot, "knowledge-reader-catalog.json"));
KnowledgeEntry[] mapped = catalog.AllEntries
    .Where(entry => entry.LearningRouteId is not null)
    .ToArray();
Check(mapped.Length == 94, "catalog must expose exactly one knowledge entry for each of 94 routes");
Check(mapped.Select(entry => entry.LearningRouteId!).Distinct(StringComparer.Ordinal).Count() == mapped.Length,
    "catalog learningRouteId values must be unique");
Check(mapped.All(entry => registry.TryResolve(entry.LearningRouteId!, out _)),
    "every catalog learningRouteId must resolve through the registry");
Check(typeof(KnowledgeEntry).GetProperty(nameof(KnowledgeEntry.InclusionStatus)) is not null &&
      typeof(KnowledgeEntry).GetProperty(nameof(KnowledgeEntry.SourceConfidence)) is not null &&
      typeof(KnowledgeEntry).GetProperty(nameof(KnowledgeEntry.ReviewNote)) is not null,
    "catalog schema must preserve cautious inclusion/review metadata without asserting verification");

KnowledgeEntry comparison = catalog.GetById("entry-006");
Check(comparison.Category == KnowledgeCategory.Comparison,
    "entry-006 must remain a comparison card");
Check(comparison.LearningRouteId is null,
    "entry-006 must not bind either Plato or Socrates");

KnowledgeEntry[] derivedCards = mapped
    .Where(entry => entry.Id.StartsWith("route-card:", StringComparison.Ordinal))
    .ToArray();
Check(derivedCards.Length == 53, "exactly 53 independent reader cards must be route-derived");
Check(derivedCards.All(entry => entry.Category == KnowledgeCategory.Learning),
    "route-derived reader cards must remain learning cards, not second-person identities");

foreach (LearningRouteCatalogItem routeItem in registry.Routes)
{
    KnowledgeEntry[] routeEntries = mapped
        .Where(entry => entry.LearningRouteId == routeItem.RouteId)
        .ToArray();
    Check(routeEntries.Length == 1, $"route mapping must be globally unique: {routeItem.RouteId}");
    if (routeEntries.Length != 1)
    {
        continue;
    }

    var routeViewModel = new KnowledgeLibraryViewModel(catalog)
    {
        Query = routeEntries[0].Title,
    };
    routeViewModel.SearchCommand.Execute(null);
    routeViewModel.SelectById(routeEntries[0].Id);
    Check(!string.IsNullOrWhiteSpace(routeViewModel.Detail.ReadingText),
        $"search -> detail must expose reader content: {routeItem.RouteId}");
    Check(routeViewModel.Detail.LearningRouteId == routeItem.RouteId,
        $"search -> detail -> route must preserve the route ID: {routeItem.RouteId}");
}

string[] reviewOnlyEntryIds =
[
    "entry-032", "entry-033", "entry-041", "entry-055", "entry-057", "entry-067",
    "entry-068", "entry-076", "entry-094", "entry-098", "entry-127",
];
KnowledgeEntry[] reviewOnlyEntries = reviewOnlyEntryIds
    .Select(catalog.GetById)
    .ToArray();
Check(reviewOnlyEntries.All(entry =>
        entry.InclusionStatus == "UNVERIFIED_REVIEW_ONLY_NON_LOAD_BEARING" &&
        !string.IsNullOrWhiteSpace(entry.SourceConfidence) &&
        !string.IsNullOrWhiteSpace(entry.ReviewNote)),
    "all 11 tier-B groups must carry explicit unverified, confidence, and review metadata");
Check(reviewOnlyEntries.All(entry =>
        KnowledgeDetailViewModel.From(entry).ReadingText.Contains(
            "UNVERIFIED_REVIEW_ONLY_NON_LOAD_BEARING",
            StringComparison.Ordinal)),
    "tier-B status must be visibly different in reader detail");

using JsonDocument catalogDocument = JsonDocument.Parse(
    File.ReadAllText(Path.Combine(contentRoot, "knowledge-reader-catalog.json")));
JsonElement[] reviewOnlyJsonEntries = catalogDocument.RootElement
    .GetProperty("entries")
    .EnumerateArray()
    .Where(entry => reviewOnlyEntryIds.Contains(entry.GetProperty("id").GetString()))
    .ToArray();
int reviewOnlyFragmentCount = reviewOnlyJsonEntries.Sum(entry =>
    entry.TryGetProperty("reviewOnlyFragments", out JsonElement fragments)
        ? fragments.GetArrayLength()
        : 0);
Check(reviewOnlyFragmentCount == 12, "tier B must expose exactly 12 review-only fragments");

string isolatedReviewCatalogPath = Path.Combine(
    Path.GetTempPath(),
    $"knowledge-reader-review-only-{Guid.NewGuid():N}.json");
try
{
    JsonObject isolatedEntry = JsonNode.Parse(reviewOnlyJsonEntries[0].GetRawText())!.AsObject();
    isolatedEntry["order"] = 1;
    isolatedEntry["title"] = "隔离测试卡";
    isolatedEntry["originalName"] = "";
    isolatedEntry["summary"] = "隔离测试摘要";
    isolatedEntry["keywords"] = new JsonArray();
    isolatedEntry["profile"] = null;
    isolatedEntry["experienceId"] = null;
    isolatedEntry["learningRouteId"] = null;
    var isolatedRoot = new JsonObject
    {
        ["schemaVersion"] = "1.0",
        ["catalogNotice"] = new JsonObject { ["readerMessage"] = "隔离测试" },
        ["entries"] = new JsonArray(isolatedEntry),
    };
    File.WriteAllText(isolatedReviewCatalogPath, isolatedRoot.ToJsonString());
    KnowledgeCatalog isolatedCatalog = KnowledgeCatalog.Load(isolatedReviewCatalogPath);
    string isolatedFragment = isolatedEntry["reviewOnlyFragments"]![0]!["text"]!.GetValue<string>();
    Check(isolatedCatalog.Search(isolatedFragment).Count == 0,
        "tier-B review-only fragments must not enter load-bearing search fields");
}
finally
{
    if (File.Exists(isolatedReviewCatalogPath))
    {
        File.Delete(isolatedReviewCatalogPath);
    }
}

string[] quarantinedFragments =
[
    "al-Isharat", "Al-Najat", "Anti-Oedipus", "Dharma as nature and value",
];
string runtimeCatalogText = File.ReadAllText(
    Path.Combine(contentRoot, "knowledge-reader-catalog.json"));
foreach (string fragment in quarantinedFragments)
{
    Check(!runtimeCatalogText.Contains(fragment, StringComparison.Ordinal),
        $"tier-C fragment must remain unreachable from runtime content: {fragment}");
    Check(catalog.Search(fragment).Count == 0,
        $"tier-C fragment must not be searchable: {fragment}");
}

string invalidCatalogPath = Path.Combine(
    Path.GetTempPath(),
    $"knowledge-reader-invalid-{Guid.NewGuid():N}.json");
try
{
    JsonObject invalidRoot = JsonNode.Parse(runtimeCatalogText)!.AsObject();
    JsonArray invalidEntries = invalidRoot["entries"]!.AsArray();
    JsonObject firstMapped = invalidEntries
        .Select(node => node!.AsObject())
        .First(entry => entry["learningRouteId"] is not null);
    JsonObject secondMapped = invalidEntries
        .Select(node => node!.AsObject())
        .SkipWhile(entry => !ReferenceEquals(entry, firstMapped))
        .Skip(1)
        .First(entry => entry["learningRouteId"] is not null);
    secondMapped["learningRouteId"] = firstMapped["learningRouteId"]!.GetValue<string>();
    File.WriteAllText(invalidCatalogPath, invalidRoot.ToJsonString());
    try
    {
        _ = KnowledgeCatalog.Load(invalidCatalogPath);
        failures.Add("duplicate learningRouteId catalog must fail closed");
    }
    catch (InvalidDataException)
    {
    }
}
finally
{
    if (File.Exists(invalidCatalogPath))
    {
        File.Delete(invalidCatalogPath);
    }
}

string[] expectedCanonicalIds =
[
    "habermas-communication-public-law",
    "ramanuja-qualified-nonduality",
    "ibn-arabi-disclosure-imagination-reception",
    "merleau-ponty-perception-body-flesh",
    "dogen-practice-time-expression",
    "gadamer-truth-understanding-language",
    "heraclitus-logos-strife-measure",
    "parmenides-being-thinking-appearance",
    "bertrand-russell-analysis-facts-revision",
    "henri-bergson-duration-memory-creation",
    "al-ghazali-philosophy-critique-practice",
    "schelling-nature-freedom-actuality",
    "william-james-experience-belief-truth",
    "anscombe-intention-action-justice",
    "seneca-judgment-time-practice",
    "popper-criticism-knowledge-open-society",
    "simone-weil-attention-force-rootedness",
    "nishida-experience-place-historical-world",
    "cicero-republic-skepticism-duty",
    "sextus-empiricus-suspension-appearance-practice",
    "william-of-ockham-signs-cognition-power",
    "quine-web-of-belief-reference-ontology",
];
foreach (string routeId in expectedCanonicalIds)
{
    KnowledgeEntry[] canonical = mapped
        .Where(entry => entry.LearningRouteId == routeId)
        .ToArray();
    Check(canonical.Length == 1, $"canonical catalog mapping must be exactly one: {routeId}");
    if (canonical.Length == 1)
    {
        var viewModel = new KnowledgeLibraryViewModel(catalog)
        {
            Query = canonical[0].Title,
        };
        viewModel.SearchCommand.Execute(null);
        viewModel.SelectById(canonical[0].Id);
        Check(viewModel.Detail.LearningRouteId == routeId,
            $"search -> detail must retain learning route: {routeId}");
        Check(viewModel.Detail.SystemLearningActionText.StartsWith("系统学习", StringComparison.Ordinal),
            $"detail action needs an accessible system-learning label: {routeId}");
    }
}

LearningRouteCatalogItem gadamerRoute = registry.Resolve(
    "gadamer-truth-understanding-language");
Check(gadamerRoute.FileName == "gadamer-learning-route.json",
    "Gadamer must use the one canonical route file name");
Check(gadamerRoute.Sha256 ==
      "539B5B7765526AE32DBC9428C84DB5AF65BA83794A0545A795C8229ED2E8C8CD",
    "Gadamer route must be the exact fixed-ZIP route bytes");
KnowledgeEntry gadamerCard = mapped.Single(entry =>
    entry.LearningRouteId == "gadamer-truth-understanding-language");
Check(gadamerCard.Id == "route-card:gadamer-truth-understanding-language" &&
      gadamerCard.Category == KnowledgeCategory.Learning,
    "Gadamer knowledge mapping must be one route-derived reader card");
Check(gadamerCard.InclusionStatus == "ROUTE_DERIVED_FIXED_ZIP" &&
      gadamerCard.SourceConfidence == "ROUTE_DERIVED_NOT_LOCATOR_VERIFIED" &&
      gadamerCard.ReviewNote?.Contains(
          "CLAIM_SPECIFIC_VERIFICATION_PENDING",
          StringComparison.Ordinal) == true,
    "Gadamer reader card must keep pending locators visibly unverified");

string progressDirectory = Path.Combine(
    Path.GetTempPath(),
    $"single-app-progress-{Guid.NewGuid():N}");
try
{
    LearningRouteDefinition gadamerDefinition;
    LearningPack gadamerPack;
    using (FileStream stream = File.OpenRead(
        Path.Combine(contentRoot, gadamerRoute.FileName)))
    {
        gadamerPack = LearningPack.Load(stream);
    }
    gadamerDefinition = gadamerPack.GetRoute(gadamerRoute.RouteId);
    LessonDefinition targetLesson = gadamerPack.GetLesson("g09");
    string targetNodeId = targetLesson.NodeIds[0];
    DateTimeOffset updatedAt = DateTimeOffset.Parse(
        "2026-08-06T12:34:56+08:00",
        System.Globalization.CultureInfo.InvariantCulture);
    var store = new LearningProgressStore(progressDirectory);
    store.Save(new LearningProgress(
        gadamerDefinition.Id,
        "older-route-version",
        new Dictionary<string, NodeMastery>(StringComparer.Ordinal)
        {
            [targetNodeId] = new NodeMastery(
                targetNodeId,
                MasteryState.Read,
                [],
                0,
                updatedAt),
        }));

    LearningProgressOverview overview = LearningProgressOverview.Load(
        contentRoot,
        progressDirectory);
    LearningProgressRouteItem recent = overview.Items.Single();
    Check(recent.RouteId == gadamerDefinition.Id &&
          recent.LessonId == targetLesson.Id &&
          recent.LessonTitle == targetLesson.Title,
        "cross-route progress must deep-link to the exact most-recent lesson");
    Check(recent.AutomationName.Contains(targetLesson.Title, StringComparison.Ordinal) &&
          recent.AutomationName.Contains("按回车继续", StringComparison.Ordinal),
        "continue-learning item needs an accessible exact-lesson action name");
    Check(overview.Diagnostic is null,
        "old route versions must migrate fail-safe without hiding valid progress");

    string progressPath = Path.Combine(
        progressDirectory,
        gadamerDefinition.Id + ".json");
    void CheckMalformedProgressFailsSafe(
        string json,
        string scenario)
    {
        File.WriteAllText(progressPath, json);
        try
        {
            ProgressLoadResult load = store.Load(
                gadamerDefinition.Id,
                gadamerDefinition.Version);
            Check(load.Progress.Nodes.Count == 0 &&
                  !string.IsNullOrWhiteSpace(load.Diagnostic),
                $"{scenario} must fail safe to empty progress with a diagnostic");
        }
        catch (Exception exception)
        {
            failures.Add(
                $"{scenario} must not escape Load: {exception.GetType().Name}");
        }
    }

    CheckMalformedProgressFailsSafe(
        $"{{\"routeId\":\"{gadamerDefinition.Id}\",\"routeVersion\":\"{gadamerDefinition.Version}\",\"nodes\":null}}",
        "nodes:null progress");
    CheckMalformedProgressFailsSafe(
        $"{{\"routeId\":\"{gadamerDefinition.Id}\",\"routeVersion\":\"{gadamerDefinition.Version}\"}}",
        "missing nodes progress");
    CheckMalformedProgressFailsSafe(
        $"{{\"routeId\":\"{gadamerDefinition.Id}\",\"routeVersion\":\"{gadamerDefinition.Version}\",\"nodes\":[]}}",
        "wrong-type nodes progress");
    CheckMalformedProgressFailsSafe(
        $"{{\"routeId\":\"{gadamerDefinition.Id}\",\"routeVersion\":\"{gadamerDefinition.Version}\",\"nodes\":{{\"{targetNodeId}\":{{\"nodeId\":\"{targetNodeId}\",\"state\":\"read\",\"targetMisconceptionCount\":0,\"updatedAt\":\"2026-08-06T12:34:56+08:00\"}}}}}}",
        "missing node-record fields progress");
    CheckMalformedProgressFailsSafe(
        $"{{\"routeId\":\"{gadamerDefinition.Id}\",\"routeVersion\":\"{gadamerDefinition.Version}\",\"nodes\":{{\"{targetNodeId}\":null}}}}",
        "null node-record progress");

    var committedAnswer = new CompanionAnswer(
        "测试听见",
        "测试问题",
        "测试关系",
        CompanionBasisLabel.AI提问,
        []);
    var successfulCompanion = new CompanionViewModel(
        new ImmediateCompanionService(new CompanionOperationResult(
            committedAnswer,
            CompanionFailureKind.None,
            string.Empty,
            null)));
    successfulCompanion.SetLesson(gadamerPack, targetLesson);
    successfulCompanion.UserText = "成功提交测试";
    int successfulCommits = 0;
    successfulCompanion.ResponseCommitted += (_, _) => successfulCommits++;
    await successfulCompanion.SendAsync();
    Check(successfulCommits == 1 && successfulCompanion.HasResponse,
        "successful companion response must commit exactly once");

    var failedCompanion = new CompanionViewModel(
        new ImmediateCompanionService(new CompanionOperationResult(
            null,
            CompanionFailureKind.ProviderUnavailable,
            "测试失败",
            null)));
    failedCompanion.SetLesson(gadamerPack, targetLesson);
    failedCompanion.UserText = "失败提交测试";
    int failedCommits = 0;
    failedCompanion.ResponseCommitted += (_, _) => failedCommits++;
    await failedCompanion.SendAsync();
    Check(failedCommits == 0 && !failedCompanion.HasResponse,
        "failed companion response must not commit focus");

    var throwingCompanion = new CompanionViewModel(
        new ThrowingCompanionService());
    throwingCompanion.SetLesson(gadamerPack, targetLesson);
    throwingCompanion.UserText = "异常提交测试";
    int throwingCommits = 0;
    throwingCompanion.ResponseCommitted += (_, _) => throwingCommits++;
    await throwingCompanion.SendAsync();
    Check(throwingCommits == 0 && !throwingCompanion.HasResponse,
        "exceptional companion response must not commit focus");

    var deferredService = new DeferredCompanionService();
    var cancelledCompanion = new CompanionViewModel(deferredService);
    cancelledCompanion.SetLesson(gadamerPack, targetLesson);
    cancelledCompanion.UserText = "取消迟到测试";
    int cancelledCommits = 0;
    cancelledCompanion.ResponseCommitted += (_, _) => cancelledCommits++;
    Task cancelledSend = cancelledCompanion.SendAsync();
    cancelledCompanion.Cancel();
    deferredService.Complete(new CompanionOperationResult(
        committedAnswer,
        CompanionFailureKind.None,
        string.Empty,
        null));
    await cancelledSend;
    Check(cancelledCommits == 0 && !cancelledCompanion.HasResponse,
        "cancelled late companion response must not commit focus");

    File.WriteAllText(
        progressPath,
        "{broken-json");
    LearningProgressOverview broken = LearningProgressOverview.Load(
        contentRoot,
        progressDirectory);
    Check(broken.Items.Count == 0 &&
          !string.IsNullOrWhiteSpace(broken.Diagnostic),
        "damaged progress must fail safe to an empty overview with a diagnostic");

    Directory.Delete(progressDirectory, recursive: true);
    LearningProgressOverview empty = LearningProgressOverview.Load(
        contentRoot,
        progressDirectory);
    Check(empty.Items.Count == 0,
        "missing progress directory must fail safe to an empty overview");
}
finally
{
    if (Directory.Exists(progressDirectory))
    {
        Directory.Delete(progressDirectory, recursive: true);
    }
}

string catalogXaml = File.ReadAllText(Path.Combine(appRoot, "ExperienceCatalogWindow.xaml"));
string catalogCode = File.ReadAllText(Path.Combine(appRoot, "ExperienceCatalogWindow.xaml.cs"));
Check(catalogXaml.Contains("ItemsSource=\"{Binding LearningRoutes}\"", StringComparison.Ordinal),
    "home route UI must bind to registry-backed LearningRoutes");
Check(catalogXaml.Contains("AutomationProperties.Name=\"{Binding AutomationName}\"", StringComparison.Ordinal),
    "home route UI must bind accessible route names");
Check(!registry.Routes.Any(route => catalogCode.Contains($"\"{route.RouteId}\"", StringComparison.Ordinal)),
    "home code-behind must not duplicate registry route IDs");

string knowledgeXaml = File.ReadAllText(Path.Combine(appRoot, "KnowledgeLibraryWindow.xaml"));
string knowledgeCode = File.ReadAllText(Path.Combine(appRoot, "KnowledgeLibraryWindow.xaml.cs"));
Check(knowledgeXaml.Contains("x:Name=\"SystemLearningButton\"", StringComparison.Ordinal),
    "knowledge detail needs one generic system-learning button");
Check(!Regex.IsMatch(knowledgeXaml, "(Kant|Plato|Descartes|Hume|Arendt)CourseButton"),
    "knowledge detail must remove five hard-coded course buttons");
Check(!expectedCanonicalIds.Any(id => knowledgeCode.Contains($"\"{id}\"", StringComparison.Ordinal)),
    "knowledge dispatch must not special-case the five canonical routes");
Check(knowledgeCode.Contains("SystemLearningButton.Focus()", StringComparison.Ordinal),
    "route return must restore focus to the generic action");

string routeCode = File.ReadAllText(Path.Combine(appRoot, "LearningRouteWindow.xaml.cs"));
string routeXaml = File.ReadAllText(Path.Combine(appRoot, "LearningRouteWindow.xaml"));
Check(routeCode.Contains("LearningRouteRegistry.Load", StringComparison.Ordinal),
    "runtime route mapping must use the registry");
Check(!routeCode.Contains("ResolveRouteConfiguration", StringComparison.Ordinal),
    "runtime route mapping must not retain the legacy hard-coded route table");
Check(routeCode.Contains("Companion.SetLesson(pack, lesson)", StringComparison.Ordinal),
    "AI context must remain tied to the current lesson");
Check(routeCode.Contains("Key.Escape", StringComparison.Ordinal), "Escape behavior must remain");
Check(routeCode.Contains("OnNavigationStarting", StringComparison.Ordinal) &&
      routeCode.Contains("e.Cancel = true", StringComparison.Ordinal),
    "external-link fail-closed behavior must remain");
Check(routeCode.Contains("用上下方向键选择章节", StringComparison.Ordinal) &&
      routeXaml.Contains("OnLessonsListKeyDown", StringComparison.Ordinal),
    "direction-key list behavior must remain");
Check(Regex.Matches(routeXaml, "<wv2:WebView2\\b").Count == 1,
    "the application must retain exactly one WebView2 in LearningRouteWindow");
Check(routeCode.Contains("initialLessonId", StringComparison.Ordinal) &&
      routeCode.Contains("LoadLessonAsync", StringComparison.Ordinal),
    "progress deep links must enter the exact lesson through the single route window");
Check(routeCode.Contains("Companion.ResponseCommitted += OnCompanionResponseCommitted", StringComparison.Ordinal),
    "route window must subscribe to successful companion response commits");
Check(routeCode.Contains("Companion.ResponseCommitted -= OnCompanionResponseCommitted", StringComparison.Ordinal) &&
      routeCode.Contains("Unloaded", StringComparison.Ordinal),
    "route window unload must unsubscribe companion response focus handling");
Check(routeCode.Contains("Dispatcher.CheckAccess()", StringComparison.Ordinal) &&
      routeCode.Contains("Keyboard.Focus(CompanionAnswer)", StringComparison.Ordinal) &&
      routeCode.Contains("AutomationEvents.AutomationFocusChanged", StringComparison.Ordinal),
    "successful companion commits must move keyboard and assistive-technology focus on the UI thread");

string homeXaml = File.ReadAllText(Path.Combine(appRoot, "ExperienceCatalogWindow.xaml"));
string homeCode = File.ReadAllText(Path.Combine(appRoot, "ExperienceCatalogWindow.xaml.cs"));
Check(homeXaml.Contains("x:Name=\"ContinueLearningList\"", StringComparison.Ordinal) &&
      homeXaml.Contains("AutomationProperties.LiveSetting=\"Polite\"", StringComparison.Ordinal),
    "home needs a named, live continue-learning region");
Check(homeCode.Contains("LearningProgressOverview.Load", StringComparison.Ordinal) &&
      homeCode.Contains("item.LessonId", StringComparison.Ordinal),
    "home continue-learning must read the existing progress store and preserve lesson deep links");

XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
XElement homeDocument = XDocument.Parse(homeXaml).Root!;
XElement[] homeTabControls = homeDocument
    .Descendants(presentation + "TabControl")
    .ToArray();
Check(homeTabControls.Length == 1,
    "home must contain exactly one native TabControl");
string[] homeTabHeaders = homeTabControls
    .SelectMany(control => control.Elements(presentation + "TabItem"))
    .Select(tab => (string?)tab.Attribute("Header") ?? string.Empty)
    .ToArray();
Check(homeTabHeaders.SequenceEqual(
        new[] { "最近学习", "系统学习", "知识库", "思想体验" },
        StringComparer.Ordinal),
    "home tabs must appear in the approved functional order");
Check(homeXaml.Contains("x:Name=\"RecentLearningTab\"", StringComparison.Ordinal) &&
      homeXaml.Contains("x:Name=\"SystemLearningTab\"", StringComparison.Ordinal) &&
      homeXaml.Contains("x:Name=\"KnowledgeTab\"", StringComparison.Ordinal) &&
      homeXaml.Contains("x:Name=\"ExperienceTab\"", StringComparison.Ordinal),
    "all home list tabs need named headers for Escape focus restoration");
Check(homeXaml.Contains("<ListBox x:Name=\"ContinueLearningList\"", StringComparison.Ordinal) &&
      homeXaml.Contains("<ListBox x:Name=\"LearningRoutesList\"", StringComparison.Ordinal) &&
      homeXaml.Contains("<ListBox x:Name=\"KnowledgeActionsList\"", StringComparison.Ordinal) &&
      homeXaml.Contains("<ListBox x:Name=\"ExperienceActionsList\"", StringComparison.Ordinal),
    "all four home tabs must use native ListBox selection");
Check(homeXaml.Contains("PreviewKeyDown=\"OnContinueLearningListKeyDown\"", StringComparison.Ordinal) &&
      homeXaml.Contains("PreviewKeyDown=\"OnLearningRoutesListKeyDown\"", StringComparison.Ordinal) &&
      homeXaml.Contains("PreviewKeyDown=\"OnKnowledgeActionsListKeyDown\"", StringComparison.Ordinal) &&
      homeXaml.Contains("PreviewKeyDown=\"OnExperienceActionsListKeyDown\"", StringComparison.Ordinal),
    "home lists must scope Enter and Escape handling to the current list");
Check(homeCode.Contains("Key.Enter", StringComparison.Ordinal) &&
      homeCode.Contains("Key.Escape", StringComparison.Ordinal) &&
      homeCode.Contains("RecentLearningTab.Focus()", StringComparison.Ordinal) &&
      homeCode.Contains("SystemLearningTab.Focus()", StringComparison.Ordinal) &&
      homeCode.Contains("KnowledgeTab.Focus()", StringComparison.Ordinal) &&
      homeCode.Contains("ExperienceTab.Focus()", StringComparison.Ordinal),
    "home lists must open selected items with Enter and return Escape to their tab headers");
Check(!homeCode.Contains("Key.Up", StringComparison.Ordinal) &&
      !homeCode.Contains("Key.Down", StringComparison.Ordinal) &&
      !homeCode.Contains("Key.Left", StringComparison.Ordinal) &&
      !homeCode.Contains("Key.Right", StringComparison.Ordinal),
    "home must leave arrow navigation to native TabControl and ListBox semantics");
Check(homeXaml.Contains("x:Name=\"KnowledgeActionsList\"", StringComparison.Ordinal) &&
      homeXaml.Contains("x:Name=\"ExperienceActionsList\"", StringComparison.Ordinal) &&
      !homeXaml.Contains("x:Name=\"OpenKnowledgeLibrary\"", StringComparison.Ordinal) &&
      !homeXaml.Contains("x:Name=\"OpenExperiences\"", StringComparison.Ordinal),
    "knowledge and experience actions must be real lists rather than single-button substitutes");
Check(homeCode.Contains("new KnowledgeLibraryWindow", StringComparison.Ordinal),
    "home knowledge action must keep the canonical KnowledgeLibraryWindow");
Check(homeCode.Contains("ContinueLearningList.ItemsSource = overview.Items", StringComparison.Ordinal) &&
      homeCode.Contains("还没有进行中的路线", StringComparison.Ordinal),
    "recent empty state must remain polite without creating a placeholder item");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"FAIL single-app-wiring failures={failures.Count}");
    foreach (string failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }
    return 1;
}

Console.WriteLine(
    $"PASS single-app-wiring routes={registry.Routes.Count} catalogMappings={mapped.Length} canonicalMappings={expectedCanonicalIds.Length}");
return 0;

sealed class ImmediateCompanionService(
    CompanionOperationResult result) : ICompanionService
{
    public Task<CompanionOperationResult> SendAsync(
        CompanionDraft draft,
        CancellationToken cancellationToken) =>
        Task.FromResult(result);
}

sealed class ThrowingCompanionService : ICompanionService
{
    public Task<CompanionOperationResult> SendAsync(
        CompanionDraft draft,
        CancellationToken cancellationToken) =>
        Task.FromException<CompanionOperationResult>(
            new InvalidOperationException("test exception"));
}

sealed class DeferredCompanionService : ICompanionService
{
    private readonly TaskCompletionSource<CompanionOperationResult>
        completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<CompanionOperationResult> SendAsync(
        CompanionDraft draft,
        CancellationToken cancellationToken) =>
        completion.Task;

    public void Complete(CompanionOperationResult result) =>
        completion.SetResult(result);
}
