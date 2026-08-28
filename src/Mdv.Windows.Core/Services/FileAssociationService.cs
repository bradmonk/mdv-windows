using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Mdv.Windows.Core.Services;

public sealed class FileAssociationService(string executablePath) : IFileAssociationService
{
    public bool TryAssociateMarkdownFiles(out string? error)
    {
        if (!OperatingSystem.IsWindows())
        {
            error = "File association is only supported on Windows.";
            return false;
        }

        try
        {
            AssociateMarkdownFilesWindows();

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private void AssociateMarkdownFilesWindows()
    {
        using var extensionKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\.md");
        extensionKey?.SetValue(string.Empty, "mdv.windows");

        using var appKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\mdv.windows");
        appKey?.SetValue(string.Empty, "Markdown Document");

        using var iconKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\mdv.windows\\DefaultIcon");
        iconKey?.SetValue(string.Empty, $"\"{executablePath}\",0");

        using var commandKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\mdv.windows\\shell\\open\\command");
        commandKey?.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
    }
}
