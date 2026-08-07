using System.Collections.Generic;

namespace CodeBrix.Texinfo2Html.Preprocessing;

/// <summary>
/// Settings that govern a <see cref="TexinfoPreprocessor"/> run. The public rendering options
/// added in later waves map onto this internal type.
/// </summary>
internal sealed class PreprocessorOptions
{
    /// <summary>Which format conditionals are processed. Defaults to <see cref="ConditionalProfile.Print"/>.</summary>
    public ConditionalProfile Profile { get; set; } = ConditionalProfile.Print;

    /// <summary>
    /// Directories searched, in order, for <c>@include</c> files after the directory of the
    /// including file itself. Callers typically add the main source file's directory and its
    /// parent (which covers include paths like <c>en/macros.itexi</c> used from a sibling file).
    /// </summary>
    public List<string> IncludeSearchPaths { get; } = new List<string>();

    /// <summary>
    /// Flags predefined before processing starts, as if each had appeared in a leading
    /// <c>@set</c> line. Use an empty string value for a bare flag.
    /// </summary>
    public Dictionary<string, string> PredefinedValues { get; } =
        new Dictionary<string, string>(System.StringComparer.Ordinal);

    /// <summary>
    /// Maximum depth of nested macro/value expansions before the engine assumes runaway
    /// recursion, warns, and drops the expansion.
    /// </summary>
    public int MaxExpansionDepth { get; set; } = 100;
}
