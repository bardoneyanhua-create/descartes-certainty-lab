using Descartes.CertaintyLab;

internal static class LearningCheckTitleTests
{
    public static IReadOnlyList<string> Run(string candidateRoot)
    {
        var failures = new List<string>();
        string appRoot = Path.Combine(
            candidateRoot,
            "application",
            "Descartes.CertaintyLab");
        string contentRoot = Path.Combine(appRoot, "Content");
        LearningRouteRegistry registry = LearningRouteRegistry.Load(contentRoot);
        (string RouteId, string Cohort)[] representatives =
        [
            ("kant-foundations", "Kant"),
            ("aristotle-foundations", "legacy"),
            ("habermas-communication-public-law", "mature-six"),
            ("seneca-judgment-time-practice", "third-wave"),
        ];

        foreach ((string routeId, string cohort) in representatives)
        {
            LearningRouteCatalogItem registration = registry.Resolve(routeId);
            string packPath = Path.Combine(contentRoot, registration.FileName);
            using FileStream stream = File.OpenRead(packPath);
            LearningPack pack = LearningPack.Load(stream);
            LearningRouteDefinition route = pack.GetRoute(routeId);
            string actual = LearningCheckWindow.CreateTitle(route.Title);
            string expected = $"{route.Title}课程理解检查";
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                failures.Add(
                    $"{cohort} route title must derive from display title: " +
                    $"expected '{expected}', actual '{actual}'");
            }

            if (routeId != "kant-foundations" &&
                actual.Contains("康德", StringComparison.Ordinal))
            {
                failures.Add(
                    $"{cohort} route title must not contain hard-coded 康德: '{actual}'");
            }
        }

        string fallback = LearningCheckWindow.CreateTitle(null);
        if (!string.Equals(fallback, "课程理解检查", StringComparison.Ordinal))
        {
            failures.Add(
                $"missing route must use safe title fallback: '{fallback}'");
        }

        string xaml = File.ReadAllText(
            Path.Combine(appRoot, "LearningCheckWindow.xaml"));
        string code = File.ReadAllText(
            Path.Combine(appRoot, "LearningCheckWindow.xaml.cs"));
        if (xaml.Contains("康德课程理解检查", StringComparison.Ordinal))
        {
            failures.Add("learning-check XAML must not hard-code the Kant title");
        }
        if (!code.Contains(
                "AutomationProperties.SetName(this, windowTitle)",
                StringComparison.Ordinal))
        {
            failures.Add(
                "window AutomationName must use the same derived value as Title");
        }

        return failures;
    }
}
