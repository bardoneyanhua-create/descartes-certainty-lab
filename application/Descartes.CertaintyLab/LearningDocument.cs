using System.Net;
using System.Text;

namespace Descartes.CertaintyLab;

public static class LearningDocument
{
    public static string CreateLesson(
        LessonDefinition lesson,
        LearningPack pack)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(pack);
        StringBuilder html = StartDocument();
        html.Append("<article aria-labelledby=\"lesson-title\">");
        AppendElement(
            html,
            "h1",
            lesson.Title,
            " id=\"lesson-title\" tabindex=\"-1\"");
        bool renderAuxiliary = lesson.RenderAuxiliary != false;
        if (renderAuxiliary)
        {
            AppendSection(
                html,
                "要追问的问题",
                [lesson.GuidingQuestion]);
        }
        var formalParagraphs = lesson.Sections
            .SelectMany(section => section.Paragraphs)
            .Select(NormalizeReaderText)
            .ToHashSet(StringComparer.Ordinal);
        foreach (LessonSectionDefinition section in lesson.Sections)
        {
            AppendSection(html, section.Heading, section.Paragraphs);
        }

        if (renderAuxiliary)
        {
            AppendSection(
                html,
                "把线索合在一起",
                WithoutFormalDuplicates([lesson.CoreExplanation], formalParagraphs));
            AppendSection(
                html,
                "一个具体处境",
                WithoutFormalDuplicates([lesson.CaseText], formalParagraphs));
            AppendSection(
                html,
                "由此可以分辨",
                WithoutFormalDuplicates(lesson.AbilitySummary, formalParagraphs));
        }
        AppendElement(html, "h2", "文本位置");
        foreach (string nodeId in lesson.NodeIds)
        {
            KnowledgeNodeDefinition node = pack.GetNode(nodeId);
            AppendParagraph(
                html,
                $"相关论点：{node.ReaderTitle}。");
            foreach (string evidenceId in node.EvidenceLinkIds)
            {
                EvidenceLinkDefinition evidence =
                    pack.EvidenceLinks.Single(link =>
                        string.Equals(
                            link.Id,
                            evidenceId,
                            StringComparison.Ordinal));
                AppendParagraph(
                    html,
                    $"作品：{evidence.WorkId}。版本：{evidence.Edition}。位置：{evidence.Locator}。");
            }
        }

        AppendParagraph(
            html,
            "正文结束。按 Tab 离开正文，进入理解题。");
        html.Append("</article></main></body></html>");
        return html.ToString();
    }

    public static string CreateFeedback(
        KnowledgeCheckDefinition check,
        CheckOptionDefinition option)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(option);
        if (!check.Options.Any(candidate =>
                string.Equals(
                    candidate.Id,
                    option.Id,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "反馈选项不属于该理解检查。",
                nameof(option));
        }

        StringBuilder html = StartDocument();
        html.Append("<article aria-labelledby=\"feedback-title\">");
        AppendElement(
            html,
            "h1",
            "理解检查反馈",
            " id=\"feedback-title\" tabindex=\"-1\"");
        AppendSection(html, "题目", [check.Prompt]);
        AppendSection(html, "你的选择", [option.Text]);
        AppendSection(html, "说明", [option.Feedback]);
        html.Append("</article></main></body></html>");
        return html.ToString();
    }

    private static StringBuilder StartDocument() =>
        new(
            """
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <style>
                :root { color-scheme: light; }
                body {
                  margin: 0;
                  background: #fffdf8;
                  color: #25231f;
                  font-family: "Microsoft YaHei UI", sans-serif;
                  font-size: 18px;
                  line-height: 1.75;
                }
                main { max-width: 52rem; margin: 0 auto; padding: 2rem 2.5rem 4rem; }
                h1 { color: #263e46; font-size: 2rem; line-height: 1.3; }
                h2 { color: #31584a; font-size: 1.35rem; margin-top: 2rem; }
                p { max-width: 46rem; margin-block: 0.85rem; }
                :focus { outline: 4px solid #173e49; outline-offset: 5px; }
              </style>
            </head>
            <body>
            <main>
            """);

    private static void AppendSection(
        StringBuilder html,
        string heading,
        IEnumerable<string> paragraphs)
    {
        string[] visible = paragraphs
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToArray();
        if (visible.Length == 0)
        {
            return;
        }

        AppendElement(html, "h2", heading);
        foreach (string paragraph in visible)
        {
            AppendParagraph(html, paragraph);
        }
    }

    private static IEnumerable<string> WithoutFormalDuplicates(
        IEnumerable<string> candidates,
        IReadOnlySet<string> formalParagraphs) =>
        candidates.Where(candidate =>
            !formalParagraphs.Contains(NormalizeReaderText(candidate)));

    private static void AppendParagraph(
        StringBuilder html,
        string text) =>
        AppendElement(html, "p", text);

    private static void AppendElement(
        StringBuilder html,
        string element,
        string text,
        string attributes = "")
    {
        html.Append('<').Append(element).Append(attributes).Append('>');
        html.Append(WebUtility.HtmlEncode(NormalizeReaderText(text)));
        html.Append("</").Append(element).Append('>');
    }

    public static string NormalizeReaderText(string text) =>
        text
            .Replace("整门课程", "整套思想", StringComparison.Ordinal)
            .Replace("后续课程", "后续思想", StringComparison.Ordinal)
            .Replace("课程规则", "使用规范", StringComparison.Ordinal)
            .Replace("教学边界", "需要守住的边界", StringComparison.Ordinal)
            .Replace("教学上的", "理解上的", StringComparison.Ordinal)
            .Replace("现代教学分析", "现代分析", StringComparison.Ordinal)
            .Replace("迁移练习", "换一个处境检验", StringComparison.Ordinal)
            .Replace("综合练习", "综合检验", StringComparison.Ordinal)
            .Replace("深入练习", "深入检验", StringComparison.Ordinal)
            .Replace("练习目标", "检验目标", StringComparison.Ordinal)
            .Replace("这个练习", "这种检验", StringComparison.Ordinal)
            .Replace("学完本章，你应能", "到这里，我们已经可以", StringComparison.Ordinal)
            .Replace("下一章", "接下来", StringComparison.Ordinal)
            .Replace("本章", "这里", StringComparison.Ordinal)
            .Replace("章节", "部分", StringComparison.Ordinal)
            .Replace("课程", "这里", StringComparison.Ordinal)
            .Replace("练习", "检验", StringComparison.Ordinal)
            .Replace("教学", "讲解", StringComparison.Ordinal)
            .Replace("用户", "使用者", StringComparison.Ordinal)
            .Replace("读者", "人们", StringComparison.Ordinal)
            .Replace("学习结果", "理解结果", StringComparison.Ordinal)
            .Replace("全课", "整体", StringComparison.Ordinal)
            .Replace("知识库", "思想资料", StringComparison.Ordinal);

}
