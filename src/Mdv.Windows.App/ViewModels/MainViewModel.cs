using CommunityToolkit.Mvvm.ComponentModel;
using Mdv.Windows.Core.Models;
using Mdv.Windows.Core.Services;
using System.IO;

namespace Mdv.Windows.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IMarkdownRendererService _renderer;
    private readonly IStorageService _storage;

    [ObservableProperty] private string? currentFilePath;
    [ObservableProperty] private string renderedHtmlDocument = string.Empty;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string statusText = "Ready";
    [ObservableProperty] private string currentTheme = "Dark";

    public MainViewModel(IMarkdownRendererService renderer, IStorageService storage)
    {
        _renderer = renderer;
        _storage = storage;
    }

    public List<string> AvailableThemes { get; } = ["Dark", "Light", "Reading"];
    public List<RecentFileEntry> RecentFiles { get; private set; } = [];
    public List<TocItem> TocItems { get; private set; } = [];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _storage.InitializeAsync(cancellationToken);
        var preferences = await _storage.LoadPreferencesAsync(cancellationToken);
        CurrentTheme = preferences.Theme;
        await RefreshRecentFilesAsync(cancellationToken);
    }

    public async Task OpenMarkdownFileAsync(string filePath, double scrollPosition = 0, CancellationToken cancellationToken = default)
    {
        var markdown = await File.ReadAllTextAsync(filePath, cancellationToken);
        var renderResult = _renderer.Render(markdown);

        CurrentFilePath = filePath;
        TocItems = renderResult.TocItems.ToList();
        RenderedHtmlDocument = WrapHtml(filePath, renderResult.HtmlBody, CurrentTheme);

        await _storage.UpsertRecentFileAsync(filePath, scrollPosition, cancellationToken);
        await RefreshRecentFilesAsync(cancellationToken);

        StatusText = $"Opened {Path.GetFileName(filePath)}";
    }

    public async Task UpdateScrollPositionAsync(double scrollPosition, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            return;
        }

        await _storage.UpsertRecentFileAsync(CurrentFilePath, scrollPosition, cancellationToken);
    }

    public async Task DeleteRecentFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _storage.DeleteRecentFileAsync(filePath, cancellationToken);
        await RefreshRecentFilesAsync(cancellationToken);
    }

    public async Task SaveBookmarkAsync(int slot, double scrollPosition, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            return;
        }

        await _storage.SaveBookmarkAsync(slot, CurrentFilePath, scrollPosition, cancellationToken);
        StatusText = slot == 0 ? "Saved transient bookmark" : $"Saved bookmark {slot}";
    }

    public async Task<double?> LoadBookmarkAsync(int slot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            return null;
        }

        var bookmark = await _storage.GetBookmarkAsync(slot, CurrentFilePath, cancellationToken);
        if (bookmark is null)
        {
            StatusText = slot == 0 ? "Transient bookmark not set" : $"Bookmark {slot} not found";
            return null;
        }

        StatusText = slot == 0 ? "Restored transient bookmark" : $"Restored bookmark {slot}";
        return bookmark.ScrollPosition;
    }

    public async Task SetThemeAsync(string theme, CancellationToken cancellationToken = default)
    {
        CurrentTheme = theme;

        if (!string.IsNullOrWhiteSpace(CurrentFilePath) && File.Exists(CurrentFilePath))
        {
            var currentPath = CurrentFilePath!;
            var markdown = await File.ReadAllTextAsync(currentPath, cancellationToken);
            var renderResult = _renderer.Render(markdown);
            TocItems = renderResult.TocItems.ToList();
            RenderedHtmlDocument = WrapHtml(currentPath, renderResult.HtmlBody, CurrentTheme);
        }

        await _storage.SavePreferencesAsync(new UserPreferences { Theme = CurrentTheme }, cancellationToken);
    }

    private async Task RefreshRecentFilesAsync(CancellationToken cancellationToken = default)
    {
        RecentFiles = (await _storage.GetRecentFilesAsync(cancellationToken: cancellationToken)).ToList();
        OnPropertyChanged(nameof(RecentFiles));
        OnPropertyChanged(nameof(TocItems));
    }

    private static string WrapHtml(string filePath, string markdownBody, string theme)
    {
        var title = System.Net.WebUtility.HtmlEncode(Path.GetFileName(filePath));
        var css = theme switch
        {
            "Light" => "body{background:#ffffff;color:#24292e;} a{color:#005cc5;} pre{background:#f6f8fa;color:#24292e;} code{background:#f1f1f1;color:#c7254e;}",
            "Reading" => "body{background:#fdf6e3;color:#3b3529;font-family:Georgia,serif;line-height:1.7;} a{color:#6c4a00;} pre{background:#f5e8c8;color:#2d2418;} code{background:#eee2c1;color:#542f00;}",
            _ => "body{background:#1e1e1e;color:#d4d4d4;} a{color:#4fc1ff;} pre{background:#252526;color:#dcdcdc;} code{background:#2d2d30;color:#ce9178;}"
        };

        const string template = """
                                <!DOCTYPE html>
                                <html>
                                <head>
                                  <meta charset="utf-8" />
                                  <title>__TITLE__</title>
                                  <style>
                                    html, body { margin: 0; padding: 0 12px 24px 12px; }
                                    body { font-family: 'Segoe UI', sans-serif; line-height: 1.5; }
                                    h1, h2, h3 { margin-top: 1.2em; }
                                    table { border-collapse: collapse; width: 100%; margin: 12px 0; }
                                    th, td { border: 1px solid #6b6b6b; padding: 6px 8px; text-align: left; }
                                    .task-list-item { list-style-type: none; }
                                    .mdv-find-hit { background: #ffeb3b; color: #000; }
                                    .mdv-find-current { background: #ff9800; color: #000; }
                                    __THEME_CSS__
                                  </style>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/prism.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-bash.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-c.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-go.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-javascript.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-python.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-ruby.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-rust.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-toml.min.js"></script>
                                  <script src="https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-yaml.min.js"></script>
                                </head>
                                <body>
                                  <div id="mdv-content">__BODY__</div>
                                  <script>
                                    (function() {
                                      const content = document.getElementById('mdv-content');
                                      const original = content.innerHTML;
                                      let matchIndex = -1;

                                      function clearCurrent() {
                                        document.querySelectorAll('.mdv-find-current').forEach(m => m.classList.remove('mdv-find-current'));
                                      }

                                      function escapeRegex(value) {
                                        return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                                      }

                                      window.mdvSearchUpdate = function(query) {
                                        content.innerHTML = original;
                                        matchIndex = -1;

                                        if (!query || query.length === 0) {
                                          return 0;
                                        }

                                        const pattern = new RegExp(escapeRegex(query), 'gi');
                                        content.innerHTML = content.innerHTML.replace(pattern, m => '<span class=\"mdv-find-hit\">' + m + '</span>');

                                        const matches = document.querySelectorAll('.mdv-find-hit');
                                        if (matches.length > 0) {
                                          matchIndex = 0;
                                          matches[0].classList.add('mdv-find-current');
                                          matches[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                                        }

                                        return matches.length;
                                      };

                                      window.mdvSearchNext = function() {
                                        const matches = document.querySelectorAll('.mdv-find-hit');
                                        if (matches.length === 0) {
                                          return false;
                                        }

                                        clearCurrent();
                                        matchIndex = (matchIndex + 1) % matches.length;
                                        matches[matchIndex].classList.add('mdv-find-current');
                                        matches[matchIndex].scrollIntoView({ behavior: 'smooth', block: 'center' });
                                        return true;
                                      };

                                      window.mdvSearchPrevious = function() {
                                        const matches = document.querySelectorAll('.mdv-find-hit');
                                        if (matches.length === 0) {
                                          return false;
                                        }

                                        clearCurrent();
                                        matchIndex = (matchIndex - 1 + matches.length) % matches.length;
                                        matches[matchIndex].classList.add('mdv-find-current');
                                        matches[matchIndex].scrollIntoView({ behavior: 'smooth', block: 'center' });
                                        return true;
                                      };

                                      window.mdvScrollToAnchor = function(id) {
                                        const target = document.getElementById(id);
                                        if (target) {
                                          target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                                        }
                                      };

                                      window.mdvGetScrollPosition = function() {
                                        return window.scrollY || document.documentElement.scrollTop || document.body.scrollTop || 0;
                                      };

                                      window.mdvSetScrollPosition = function(position) {
                                        window.scrollTo(0, Number(position || 0));
                                      };

                                      Prism.highlightAll();
                                    })();
                                  </script>
                                </body>
                                </html>
                                """;

        return template.Replace("__TITLE__", title)
            .Replace("__THEME_CSS__", css)
            .Replace("__BODY__", markdownBody);
    }
}
