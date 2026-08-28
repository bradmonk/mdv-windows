namespace Mdv.Windows.Core.Models;

public sealed class MarkdownRenderResult
{
    public required string HtmlBody { get; init; }
    public required IReadOnlyList<TocItem> TocItems { get; init; }
}
