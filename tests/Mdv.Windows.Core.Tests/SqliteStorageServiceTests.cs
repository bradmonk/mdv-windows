using Mdv.Windows.Core.Models;
using Mdv.Windows.Core.Services;

namespace Mdv.Windows.Core.Tests;

public sealed class SqliteStorageServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsHistoryBookmarksAndPreferences()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("mdv-storage-tests-");
        try
        {
            var databasePath = Path.Combine(tempDirectory.FullName, "state.db");
            var sut = new SqliteStorageService(databasePath);

            await sut.InitializeAsync();
            await sut.UpsertRecentFileAsync("/tmp/example.md", 42);
            await sut.SaveBookmarkAsync(1, "/tmp/example.md", 30);
            await sut.SavePreferencesAsync(new UserPreferences { Theme = "Light", WindowHeight = 800, WindowWidth = 1200 });

            var recent = await sut.GetRecentFilesAsync();
            var bookmark = await sut.GetBookmarkAsync(1, "/tmp/example.md");
            var preferences = await sut.LoadPreferencesAsync();

            Assert.Single(recent);
            Assert.Equal("/tmp/example.md", recent[0].Path);
            Assert.Equal(42, recent[0].LastScrollPosition);

            Assert.NotNull(bookmark);
            Assert.Equal(30, bookmark!.ScrollPosition);

            Assert.Equal("Light", preferences.Theme);
            Assert.Equal(800, preferences.WindowHeight);
            Assert.Equal(1200, preferences.WindowWidth);
        }
        finally
        {
            Directory.Delete(tempDirectory.FullName, true);
        }
    }
}
