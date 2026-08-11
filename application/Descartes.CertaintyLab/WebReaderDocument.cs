using System.Net;
using System.Text;

namespace Descartes.CertaintyLab;

public static class WebReaderDocument
{
    public static string Create(KnowledgeDetailViewModel detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var html = new StringBuilder(
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
                p { max-width: 46rem; }
                .reader-item { margin-block: 0.85rem; }
                .original-name { color: #5f5a52; }
                .source-note { border-top: 1px solid #c8bca8; padding-top: 1rem; }
                :focus { outline: 4px solid #173e49; outline-offset: 5px; }
              </style>
            </head>
            <body>
            <main>
            <article aria-labelledby="article-title">
            """);

        AppendElement(
            html,
            "h1",
            detail.Title,
            " id=\"article-title\" tabindex=\"-1\"");
        AppendParagraph(html, detail.OriginalName, "original-name");
        AppendParagraph(html, detail.Positioning);
        AppendListSection(html, "核心问题", detail.Questions);
        AppendListSection(html, "关键思想", detail.KeyIdeas);
        if (detail.KeyIdeas.Count == 0)
        {
            AppendTextSection(html, "关键思想", detail.Interpretation);
        }
        AppendListSection(html, "争议与容易误解之处", detail.Cautions);
        AppendTextSection(html, "人生与思想", detail.LifeAndThought);
        AppendListSection(html, "相关作品", detail.Works);
        AppendTextSection(html, "思想怎样相连", detail.RelationText);
        AppendTextSection(html, "阅读说明", detail.BoundaryNote);
        if (!string.IsNullOrWhiteSpace(detail.SourceNote))
        {
            AppendElement(html, "h2", "内容依据");
            AppendParagraph(html, detail.SourceNote, "source-note");
        }

        html.Append(
            """
            </article>
            </main>
            </body>
            </html>
            """);
        return html.ToString();
    }

    private static void AppendTextSection(
        StringBuilder html,
        string heading,
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        AppendElement(html, "h2", heading);
        AppendParagraph(html, text);
    }

    private static void AppendListSection(
        StringBuilder html,
        string heading,
        IReadOnlyList<string> items)
    {
        IReadOnlyList<string> visibleItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList()
            .AsReadOnly();
        if (visibleItems.Count == 0)
        {
            return;
        }

        AppendElement(html, "h2", heading);
        foreach (string item in visibleItems)
        {
            AppendParagraph(html, item, "reader-item");
        }
    }

    private static void AppendParagraph(
        StringBuilder html,
        string text,
        string? cssClass = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string attributes = cssClass is null
            ? string.Empty
            : $" class=\"{WebUtility.HtmlEncode(cssClass)}\"";
        AppendElement(html, "p", text, attributes);
    }

    private static void AppendElement(
        StringBuilder html,
        string element,
        string text,
        string attributes = "")
    {
        html.Append('<').Append(element).Append(attributes).Append('>');
        html.Append(WebUtility.HtmlEncode(text));
        html.Append("</").Append(element).Append('>');
    }
}
