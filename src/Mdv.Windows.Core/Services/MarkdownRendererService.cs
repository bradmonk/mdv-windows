using System.Text.RegularExpressions;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Mdv.Windows.Core.Models;

namespace Mdv.Windows.Core.Services;

public sealed class MarkdownRendererService : IMarkdownRendererService
{
    private static readonly Regex InvalidIdChars = new("[^a-z0-9\\-]", RegexOptions.Compiled);

    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public MarkdownRenderResult Render(string markdown)
    {
        markdown ??= string.Empty;

        var document = Markdown.Parse(markdown, _pipeline);
        var tocItems = BuildToc(document);

        var normalizedMarkdown = InjectHeadingIds(markdown, tocItems);
        var html = Markdown.ToHtml(normalizedMarkdown, _pipeline);

        return new MarkdownRenderResult
        {
            HtmlBody = html,
            TocItems = tocItems
        };
    }

    private static IReadOnlyList<TocItem> BuildToc(MarkdownDocument document)
    {
        var items = new List<TocItem>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in document)
        {
            if (block is not HeadingBlock heading || heading.Level is < 1 or > 3)
            {
                continue;
            }

            var text = ExtractInlineText(heading.Inline);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var id = ToStableId(text, usedIds);
            items.Add(new TocItem
            {
                Id = id,
                Title = text,
                Level = heading.Level
            });
        }

        return items;
    }

    private static string ExtractInlineText(ContainerInline? containerInline)
    {
        if (containerInline is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var current = containerInline.FirstChild;

        while (current is not null)
        {
            switch (current)
            {
                case LiteralInline literal:
                    parts.Add(literal.Content.ToString());
                    break;
                case CodeInline codeInline:
                    parts.Add(codeInline.Content);
                    break;
                case LinkInline link when link.FirstChild is not null:
                    parts.Add(ExtractInlineText(link));
                    break;
            }

            current = current.NextSibling;
        }

        return string.Join(string.Empty, parts).Trim();
    }

    private static string ToStableId(string title, ISet<string> usedIds)
    {
        var normalized = title.Trim().ToLowerInvariant().Replace(' ', '-');
        normalized = InvalidIdChars.Replace(normalized, string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "section";
        }

        var candidate = normalized;
        var suffix = 2;
        while (usedIds.Contains(candidate))
        {
            candidate = $"{normalized}-{suffix}";
            suffix++;
        }

        usedIds.Add(candidate);
        return candidate;
    }

    private static string InjectHeadingIds(string markdown, IReadOnlyList<TocItem> tocItems)
    {
        if (tocItems.Count == 0)
        {
            return markdown;
        }

        var queue = new Queue<TocItem>(tocItems);
        var reader = new StringReader(markdown);
        var output = new StringWriter();

        while (reader.ReadLine() is { } line)
        {
            if (queue.TryPeek(out var item) && IsMatchingHeadingLine(line, item.Level, item.Title))
            {
                output.WriteLine($"{new string('#', item.Level)} {item.Title} {{#{item.Id}}}");
                queue.Dequeue();
            }
            else
            {
                output.WriteLine(line);
            }
        }

        return output.ToString();
    }

    private static bool IsMatchingHeadingLine(string line, int level, string title)
    {
        var prefix = new string('#', level) + " ";
        return line.StartsWith(prefix, StringComparison.Ordinal) &&
               string.Equals(line[prefix.Length..].Trim(), title, StringComparison.Ordinal);
    }
}
