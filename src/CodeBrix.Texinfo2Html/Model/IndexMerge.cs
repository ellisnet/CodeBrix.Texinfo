namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// A request from <c>@syncodeindex</c> or <c>@synindex</c> to fold one index into another, so
/// that a single <c>@printindex</c> prints entries from both. The code form additionally asks for
/// the merged entries to be set in a fixed-width font.
/// </summary>
internal sealed class IndexMerge
{
    /// <summary>Creates an index merge request.</summary>
    /// <param name="sourceIndex">The two-letter name of the index being folded away.</param>
    /// <param name="targetIndex">The two-letter name of the index receiving the entries.</param>
    /// <param name="useCodeFont">True for <c>@syncodeindex</c>, false for <c>@synindex</c>.</param>
    public IndexMerge(string sourceIndex, string targetIndex, bool useCodeFont)
    {
        SourceIndex = sourceIndex ?? string.Empty;
        TargetIndex = targetIndex ?? string.Empty;
        UseCodeFont = useCodeFont;
    }

    /// <summary>The two-letter name of the index being folded away.</summary>
    public string SourceIndex { get; }

    /// <summary>The two-letter name of the index receiving the entries.</summary>
    public string TargetIndex { get; }

    /// <summary>True when the merged entries are to be set in a fixed-width font.</summary>
    public bool UseCodeFont { get; }

    /// <summary>Formats the request for diagnostics.</summary>
    public override string ToString() => $"{SourceIndex} -> {TargetIndex}{(UseCodeFont ? " (code)" : string.Empty)}";
}
