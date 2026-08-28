namespace Mdv.Windows.Core.Services;

public interface IFileAssociationService
{
    bool TryAssociateMarkdownFiles(out string? error);
}
