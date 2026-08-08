using System.Collections.Generic;

namespace CodeBrix.Texinfo2Html.Semantics;

/// <summary>
/// One index ready to be printed: every entry filed under its name, including the entries folded in
/// from other indices by <c>@syncodeindex</c> and <c>@synindex</c>, in the order they appear on the
/// page.
/// </summary>
internal sealed class PrintedIndex
{
    /// <summary>Creates a printed index.</summary>
    /// <param name="name">The two-letter index name, such as <c>cp</c>.</param>
    /// <param name="entries">The entries, already sorted.</param>
    public PrintedIndex(string name, IReadOnlyList<PrintedIndexEntry> entries)
    {
        Name = name ?? string.Empty;
        Entries = entries ?? new List<PrintedIndexEntry>();
    }

    /// <summary>The two-letter index name, such as <c>cp</c> or <c>fn</c>.</summary>
    public string Name { get; }

    /// <summary>The entries in printing order.</summary>
    public IReadOnlyList<PrintedIndexEntry> Entries { get; }

    /// <summary>Formats the index for diagnostics.</summary>
    public override string ToString() => $"index '{Name}' ({Entries.Count} entries)";
}
