namespace Mdv.Windows.Core.Models;

public sealed class UserPreferences
{
    public string Theme { get; init; } = "Dark";
    public double WindowWidth { get; init; } = 1400;
    public double WindowHeight { get; init; } = 900;
}
