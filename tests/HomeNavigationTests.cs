using System.Xml.Linq;

internal static class HomeNavigationTests
{
    internal static IReadOnlyList<string> Run(string candidateRoot)
    {
        var failures = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition)
            {
                failures.Add("Home navigation: " + message);
            }
        }

        string appRoot = Path.Combine(candidateRoot, "application", "Descartes.CertaintyLab");
        string xamlText = File.ReadAllText(Path.Combine(appRoot, "ExperienceCatalogWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(appRoot, "ExperienceCatalogWindow.xaml.cs"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement root = XDocument.Parse(xamlText).Root!;
        XElement[] tabs = root.Descendants(presentation + "TabControl")
            .Single()
            .Elements(presentation + "TabItem")
            .ToArray();

        string[] expectedHeaders = ["最近学习", "系统学习", "知识库", "思想体验"];
        string[] expectedTabNames = ["RecentLearningTab", "SystemLearningTab", "KnowledgeTab", "ExperienceTab"];
        string[] expectedListNames = ["ContinueLearningList", "LearningRoutesList", "KnowledgeActionsList", "ExperienceActionsList"];
        string[] expectedHandlers = [
            "OnContinueLearningListKeyDown",
            "OnLearningRoutesListKeyDown",
            "OnKnowledgeActionsListKeyDown",
            "OnExperienceActionsListKeyDown"];

        Check(tabs.Select(tab => (string?)tab.Attribute("Header") ?? string.Empty)
                .SequenceEqual(expectedHeaders, StringComparer.Ordinal),
            "four tabs must retain the approved order");
        for (int index = 0; index < tabs.Length && index < expectedHeaders.Length; index++)
        {
            XElement tab = tabs[index];
            XElement[] lists = tab.Descendants(presentation + "ListBox").ToArray();
            Check((string?)tab.Attribute(x + "Name") == expectedTabNames[index],
                $"{expectedHeaders[index]} tab must expose the named focus target {expectedTabNames[index]}");
            Check(lists.Length == 1 && (string?)lists[0].Attribute(x + "Name") == expectedListNames[index],
                $"{expectedHeaders[index]} tab must contain exactly one explicit {expectedListNames[index]} ListBox");
            if (lists.Length == 1)
            {
                Check((string?)lists[0].Attribute("PreviewKeyDown") == expectedHandlers[index],
                    $"{expectedListNames[index]} must wire its own Enter/Escape handler");
            }
        }

        XElement? recentList = root.Descendants(presentation + "ListBox")
            .SingleOrDefault(list => (string?)list.Attribute(x + "Name") == "ContinueLearningList");
        Check(recentList is not null && !recentList.Elements(presentation + "ListBoxItem").Any() &&
              code.Contains("ContinueLearningList.ItemsSource = overview.Items", StringComparison.Ordinal),
            "empty Recent must remain data-backed and contain no phantom static item");

        foreach ((string listName, string tabName, string handler, string openCall) in new[]
        {
            ("KnowledgeActionsList", "KnowledgeTab", "OnKnowledgeActionsListKeyDown", "OpenKnowledgeLibrary()"),
            ("ExperienceActionsList", "ExperienceTab", "OnExperienceActionsListKeyDown", "OpenExperiences()"),
        })
        {
            XElement? list = root.Descendants(presentation + "ListBox")
                .SingleOrDefault(element => (string?)element.Attribute(x + "Name") == listName);
            Check(list?.Elements(presentation + "ListBoxItem").Count() == 1,
                $"{listName} must expose exactly one real action item");
            Check(MethodContains(code, handler, "Key.Enter") &&
                  MethodContains(code, handler, "Key.Escape") &&
                  MethodContains(code, handler, tabName + ".Focus()") &&
                  MethodContains(code, handler, openCall),
                $"{handler} must implement Enter action and Escape focus return");
        }

        Check(!code.Contains("Key.Up", StringComparison.Ordinal) &&
              !code.Contains("Key.Down", StringComparison.Ordinal),
            "Up/Down must remain native and local to the focused ListBox");
        Check(code.Contains("new KnowledgeLibraryWindow", StringComparison.Ordinal),
            "the knowledge action must keep KnowledgeLibraryWindow as the canonical surface");
        return failures;
    }

    private static bool MethodContains(string source, string methodName, string value)
    {
        int start = source.IndexOf("private void " + methodName, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        int next = source.IndexOf("\n    private ", start + methodName.Length, StringComparison.Ordinal);
        ReadOnlySpan<char> method = next < 0 ? source.AsSpan(start) : source.AsSpan(start, next - start);
        return method.Contains(value, StringComparison.Ordinal);
    }
}
