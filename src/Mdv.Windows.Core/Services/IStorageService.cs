using Mdv.Windows.Core.Models;

namespace Mdv.Windows.Core.Services;

public interface IStorageService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecentFileEntry>> GetRecentFilesAsync(int maxCount = 100, CancellationToken cancellationToken = default);
    Task UpsertRecentFileAsync(string filePath, double scrollPosition, CancellationToken cancellationToken = default);
    Task DeleteRecentFileAsync(string filePath, CancellationToken cancellationToken = default);

    Task SaveBookmarkAsync(int slot, string filePath, double scrollPosition, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookmarkEntry>> GetBookmarksAsync(string filePath, CancellationToken cancellationToken = default);
    Task<BookmarkEntry?> GetBookmarkAsync(int slot, string filePath, CancellationToken cancellationToken = default);

    Task SavePreferencesAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
    Task<UserPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default);
}
