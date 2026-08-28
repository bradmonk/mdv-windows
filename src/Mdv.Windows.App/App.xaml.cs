using System.Windows;

namespace Mdv.Windows.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startupPath = e.Args.FirstOrDefault();
        var window = new MainWindow(startupPath);
        window.Show();
    }
}
