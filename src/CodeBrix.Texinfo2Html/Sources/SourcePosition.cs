using System;

namespace CodeBrix.Texinfo2Html.Sources;

/// <summary>
/// A location in a Texinfo source - the name of the source (usually a file path, but also a
/// macro-expansion description) plus a one-based line and column within it.
/// </summary>
internal readonly struct SourcePosition : IEquatable<SourcePosition>
{
    /// <summary>Creates a position within the named source.</summary>
    /// <param name="sourceName">The source name; a file path or an expansion description.</param>
    /// <param name="line">One-based line number.</param>
    /// <param name="column">One-based column number.</param>
    public SourcePosition(string sourceName, int line, int column)
    {
        SourceName = sourceName ?? string.Empty;
        Line = line;
        Column = column;
    }

    /// <summary>The source name; a file path or a macro-expansion description.</summary>
    public string SourceName { get; }

    /// <summary>One-based line number within the source.</summary>
    public int Line { get; }

    /// <summary>One-based column number within the line.</summary>
    public int Column { get; }

    /// <summary>Formats the position as <c>name:line:column</c>.</summary>
    public override string ToString() => $"{SourceName}:{Line}:{Column}";

    /// <inheritdoc/>
    public bool Equals(SourcePosition other)
        => string.Equals(SourceName, other.SourceName, StringComparison.Ordinal)
           && Line == other.Line
           && Column == other.Column;

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is SourcePosition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(SourceName, Line, Column);
}
