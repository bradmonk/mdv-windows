namespace Mdv.Windows.Core.Models;

public sealed class TocItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int Level { get; init; }
}
