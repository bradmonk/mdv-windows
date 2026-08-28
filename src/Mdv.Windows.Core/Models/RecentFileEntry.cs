namespace Mdv.Windows.Core.Models;

public sealed class RecentFileEntry
{
    public required string Path { get; init; }
    public required DateTimeOffset LastOpenedAt { get; init; }
    public double LastScrollPosition { get; init; }
}
