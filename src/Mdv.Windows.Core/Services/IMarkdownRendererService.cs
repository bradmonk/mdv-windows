using Mdv.Windows.Core.Models;

namespace Mdv.Windows.Core.Services;

public interface IMarkdownRendererService
{
    MarkdownRenderResult Render(string markdown);
}
