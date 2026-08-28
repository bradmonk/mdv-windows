using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Mdv.Windows.App.ViewModels;
using Mdv.Windows.Core.Models;
using Mdv.Windows.Core.Services;

namespace Mdv.Windows.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly string? _startupPath;
    private readonly DispatcherTimer _reloadTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    private FileSystemWatcher? _watcher;
    private bool _browserReady;

    public MainWindow(string? startupPath)
    {
        InitializeComponent();

        _startupPath = startupPath;

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var databasePath = Path.Combine(localData, "mdv-windows", "mdv.db");

        _viewModel = new MainViewModel(new MarkdownRendererService(), new SqliteStorageService(databasePath));
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        DataContext = _viewModel;

        _reloadTimer.Tick += ReloadTimer_Tick;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        await TryOpenFileAsync(_startupPath);
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        try
        {
            await PersistCurrentScrollAsync();
        }
        catch
        {
            // Ignore shutdown persistence errors.
        }

        _watcher?.Dispose();
        _reloadTimer.Stop();
    }

    private async Task TryOpenFileAsync(string? filePath, bool preserveScroll = true)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || !filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previousScroll = preserveScroll ? await TryGetBrowserScrollAsync() : 0;
        await _viewModel.OpenMarkdownFileAsync(filePath, previousScroll ?? 0);
        ConfigureWatcher(filePath);
    }

    private void ConfigureWatcher(string path)
    {
        _watcher?.Dispose();

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);

        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += (_, _) => QueueReload();
        _watcher.Renamed += (_, _) => QueueReload();
        _watcher.Created += (_, _) => QueueReload();
    }

    private void QueueReload()
    {
        Dispatcher.Invoke(() =>
        {
            _reloadTimer.Stop();
            _reloadTimer.Start();
        });
    }

    private async void ReloadTimer_Tick(object? sender, EventArgs e)
    {
        _reloadTimer.Stop();

        if (string.IsNullOrWhiteSpace(_viewModel.CurrentFilePath) || !File.Exists(_viewModel.CurrentFilePath))
        {
            return;
        }

        var scroll = await TryGetBrowserScrollAsync();
        await _viewModel.OpenMarkdownFileAsync(_viewModel.CurrentFilePath, scroll ?? 0);
        _viewModel.StatusText = $"Live reloaded {Path.GetFileName(_viewModel.CurrentFilePath)}";
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RenderedHtmlDocument))
        {
            _browserReady = false;
            DocumentBrowser.NavigateToString(_viewModel.RenderedHtmlDocument);
        }
    }

    private async void DocumentBrowser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
    {
        _browserReady = true;
        await ApplySearchAsync();
    }

    private async Task<double?> TryGetBrowserScrollAsync()
    {
        if (!_browserReady)
        {
            return null;
        }

        try
        {
            var result = DocumentBrowser.InvokeScript("mdvGetScrollPosition");
            return result is null ? null : Convert.ToDouble(result);
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistCurrentScrollAsync()
    {
        var scroll = await TryGetBrowserScrollAsync();
        if (scroll.HasValue)
        {
            await _viewModel.UpdateScrollPositionAsync(scroll.Value);
        }
    }

    private async Task ApplySearchAsync()
    {
        if (!_browserReady)
        {
            return;
        }

        try
        {
            var result = DocumentBrowser.InvokeScript("mdvSearchUpdate", _viewModel.SearchText ?? string.Empty);
            if (result is not null)
            {
                _viewModel.StatusText = $"{result} matches";
            }
        }
        catch
        {
            _viewModel.StatusText = "Search unavailable for current document.";
        }

        await Task.CompletedTask;
    }

    private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await PersistCurrentScrollAsync();
            await TryOpenFileAsync(dialog.FileName, preserveScroll: false);
        }
    }

    private async void HistoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryListBox.SelectedItem is RecentFileEntry selected)
        {
            await PersistCurrentScrollAsync();
            await TryOpenFileAsync(selected.Path, preserveScroll: false);
        }
    }

    private async void DeleteHistoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryListBox.SelectedItem is RecentFileEntry selected)
        {
            await _viewModel.DeleteRecentFileAsync(selected.Path);
        }
    }

    private async void HistoryListBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && HistoryListBox.SelectedItem is RecentFileEntry selected)
        {
            await _viewModel.DeleteRecentFileAsync(selected.Path);
        }
    }

    private void TocListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_browserReady || TocListBox.SelectedItem is not TocItem item)
        {
            return;
        }

        DocumentBrowser.InvokeScript("mdvScrollToAnchor", item.Id);
    }

    private async void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _viewModel.SearchText = SearchTextBox.Text;
        await ApplySearchAsync();
    }

    private void SearchNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_browserReady)
        {
            DocumentBrowser.InvokeScript("mdvSearchNext");
        }
    }

    private void SearchPreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (_browserReady)
        {
            DocumentBrowser.InvokeScript("mdvSearchPrevious");
        }
    }

    private async void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string theme })
        {
            await _viewModel.SetThemeAsync(theme);
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AssociateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var service = new FileAssociationService(Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "mdv-windows.exe");
        var success = service.TryAssociateMarkdownFiles(out var error);
        MessageBox.Show(this, success ? "Associated .md files with mdv-windows." : $"Could not set association: {error}", "mdv-windows", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var firstMarkdown = paths.FirstOrDefault(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
            await PersistCurrentScrollAsync();
            await TryOpenFileAsync(firstMarkdown, preserveScroll: false);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            OpenMenuItem_Click(sender, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        var slot = KeyToSlot(e.Key);
        if (slot is null)
        {
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            var target = await _viewModel.LoadBookmarkAsync(slot.Value);
            if (target.HasValue && _browserReady)
            {
                DocumentBrowser.InvokeScript("mdvSetScrollPosition", target.Value);
            }

            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var scroll = await TryGetBrowserScrollAsync();
            await _viewModel.SaveBookmarkAsync(slot.Value, scroll ?? 0);
            e.Handled = true;
        }
    }

    private static int? KeyToSlot(Key key)
    {
        return key switch
        {
            Key.D0 or Key.NumPad0 => 0,
            Key.D1 or Key.NumPad1 => 1,
            Key.D2 or Key.NumPad2 => 2,
            Key.D3 or Key.NumPad3 => 3,
            Key.D4 or Key.NumPad4 => 4,
            Key.D5 or Key.NumPad5 => 5,
            _ => null
        };
    }
}
