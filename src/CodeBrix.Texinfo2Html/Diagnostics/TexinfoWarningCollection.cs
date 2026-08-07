using System.Collections;
using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Diagnostics;

/// <summary>
/// An append-only list of <see cref="TexinfoWarning"/> instances gathered during a processing
/// run. One collection is shared by every pipeline stage so a caller sees all warnings in the
/// order they occurred.
/// </summary>
internal sealed class TexinfoWarningCollection : IReadOnlyList<TexinfoWarning>
{
    private readonly List<TexinfoWarning> _warnings = new List<TexinfoWarning>();

    /// <summary>The number of warnings collected so far.</summary>
    public int Count => _warnings.Count;

    /// <summary>Gets the warning at the given index, oldest first.</summary>
    /// <param name="index">Zero-based index into the collection.</param>
    public TexinfoWarning this[int index] => _warnings[index];

    /// <summary>Records a new warning.</summary>
    /// <param name="category">The kind of problem being reported.</param>
    /// <param name="position">Where in the source the problem was found.</param>
    /// <param name="message">Human-readable description of the problem.</param>
    public void Add(TexinfoWarningCategory category, SourcePosition position, string message)
        => _warnings.Add(new TexinfoWarning(category, position, message));

    /// <inheritdoc/>
    public IEnumerator<TexinfoWarning> GetEnumerator() => _warnings.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
