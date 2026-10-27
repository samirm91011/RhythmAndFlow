namespace RhythmFlow.Api.Services;

/// <summary>The client approved three levels: Beginner, Intermediate and Advanced (feedback of 20 Oct 2026). Nothing else is accepted.</summary>
public static class LessonLevels
{
    public static readonly string[] All = ["Beginner", "Intermediate", "Advanced"];

    /// <summary>A blank level becomes Beginner. Anything that is not one of the three is refused.</summary>
    public static bool TryNormalise(string? value, out string level)
    {
        level = All[0];
        if (string.IsNullOrWhiteSpace(value)) return true;
        var match = All.FirstOrDefault(l => string.Equals(l, value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null) return false;
        level = match;
        return true;
    }

    public const string Refusal = "Choose Beginner, Intermediate or Advanced.";
}
