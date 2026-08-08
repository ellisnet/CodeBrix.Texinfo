using CodeBrix.Texinfo2Html.Model;

namespace CodeBrix.Texinfo2Html.Semantics;

/// <summary>
/// One line of a printed index: the entry as the document wrote it, the key it sorts under, the
/// identifier the index links back to, and the letter it files beneath.
/// </summary>
internal sealed class PrintedIndexEntry
{
    /// <summary>Creates an index line.</summary>
    /// <param name="source">The index entry the line was built from.</param>
    /// <param name="sortKey">The key the line sorts under, already normalized.</param>
    /// <param name="letter">The letter heading the line files beneath.</param>
    /// <param name="elementId">The identifier of the marker emitted where the entry was written.</param>
    /// <param name="useCodeFont">True when the entry prints in a fixed-width font.</param>
    public PrintedIndexEntry(IndexEntryNode source, string sortKey, string letter, string elementId,
        bool useCodeFont)
    {
        Source = source;
        SortKey = sortKey ?? string.Empty;
        Letter = letter ?? string.Empty;
        ElementId = elementId ?? string.Empty;
        UseCodeFont = useCodeFont;
    }

    /// <summary>The index entry the line was built from.</summary>
    public IndexEntryNode Source { get; }

    /// <summary>The key the line sorts under, with the ignored characters already removed.</summary>
    public string SortKey { get; }

    /// <summary>The letter heading the line files beneath, or an empty string for one that has none.</summary>
    public string Letter { get; }

    /// <summary>The identifier of the marker the emitter left where the entry was written.</summary>
    public string ElementId { get; }

    /// <summary>
    /// True when the entry prints in a fixed-width font. Concept-index entries are set in the body
    /// font and every other index is set in code, which is also what <c>@syncodeindex</c> asks for
    /// when it folds one index into another.
    /// </summary>
    public bool UseCodeFont { get; }

    /// <summary>Formats the entry for diagnostics.</summary>
    public override string ToString() => $"{SortKey} -> #{ElementId}";
}
