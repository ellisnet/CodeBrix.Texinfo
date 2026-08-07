using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Diagnostics;

/// <summary>
/// A single non-fatal problem encountered while processing Texinfo source. The engine never
/// throws for document problems; it collects warnings like this one and degrades gracefully.
/// </summary>
internal sealed class TexinfoWarning
{
    /// <summary>Creates a warning of the given category at the given position.</summary>
    /// <param name="category">The kind of problem being reported.</param>
    /// <param name="position">Where in the source the problem was found.</param>
    /// <param name="message">Human-readable description of the problem.</param>
    public TexinfoWarning(TexinfoWarningCategory category, SourcePosition position, string message)
    {
        Category = category;
        Position = position;
        Message = message ?? string.Empty;
    }

    /// <summary>The kind of problem being reported.</summary>
    public TexinfoWarningCategory Category { get; }

    /// <summary>Where in the source the problem was found.</summary>
    public SourcePosition Position { get; }

    /// <summary>Human-readable description of the problem.</summary>
    public string Message { get; }

    /// <summary>Formats the warning as <c>category: message (at position)</c>.</summary>
    public override string ToString() => $"{Category}: {Message} (at {Position})";
}
