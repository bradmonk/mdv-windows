namespace Mdv.Windows.Core.Models;

public sealed class BookmarkEntry
{
    public int Slot { get; init; }
    public required string FilePath { get; init; }
    public double ScrollPosition { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
