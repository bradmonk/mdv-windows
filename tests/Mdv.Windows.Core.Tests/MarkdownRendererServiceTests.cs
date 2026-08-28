using Mdv.Windows.Core.Services;

namespace Mdv.Windows.Core.Tests;

public sealed class MarkdownRendererServiceTests
{
    [Fact]
    public void Render_IncludesGfmAndFootnotes_AndBuildsToc()
    {
        var markdown = """
                       # Title

                       ## Table Section

                       | Col A | Col B |
                       |------:|:------|
                       | One   | Two   |

                       - [x] done
                       - [ ] todo

                       Paragraph with footnote.[^1]

                       ```python
                       print('hello')
                       ```

                       [^1]: Footnote text.
                       """;

        var sut = new MarkdownRendererService();

        var result = sut.Render(markdown);

        Assert.Contains("<table>", result.HtmlBody);
        Assert.Contains("task-list-item", result.HtmlBody);
        Assert.Contains("footnote", result.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("language-python", result.HtmlBody);
        Assert.Collection(result.TocItems,
            item =>
            {
                Assert.Equal("title", item.Id);
                Assert.Equal(1, item.Level);
            },
            item =>
            {
                Assert.Equal("table-section", item.Id);
                Assert.Equal(2, item.Level);
            });
    }
}
