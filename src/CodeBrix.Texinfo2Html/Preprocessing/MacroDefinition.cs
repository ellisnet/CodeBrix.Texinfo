using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Preprocessing;

/// <summary>
/// A user macro captured from <c>@macro</c>, <c>@rmacro</c> or <c>@linemacro</c>. The body is kept
/// as raw source text; parameter substitution and re-lexing happen at each invocation.
/// </summary>
internal sealed class MacroDefinition
{
    /// <summary>Creates a macro definition.</summary>
    /// <param name="name">The macro name, without <c>@</c>.</param>
    /// <param name="parameters">The formal parameter names, in order; may be empty.</param>
    /// <param name="body">The raw body text between the definition line and <c>@end macro</c>.</param>
    /// <param name="isRecursive">True for <c>@rmacro</c>, which may invoke itself.</param>
    /// <param name="isLineMacro">True for <c>@linemacro</c>, whose invocation reads a whole line.</param>
    /// <param name="definedAt">Where the definition appeared.</param>
    public MacroDefinition(string name, IReadOnlyList<string> parameters, string body,
        bool isRecursive, bool isLineMacro, SourcePosition definedAt)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
        IsRecursive = isRecursive;
        IsLineMacro = isLineMacro;
        DefinedAt = definedAt;
    }

    /// <summary>The macro name, without <c>@</c>.</summary>
    public string Name { get; }

    /// <summary>The formal parameter names, in order; empty for a parameterless macro.</summary>
    public IReadOnlyList<string> Parameters { get; }

    /// <summary>
    /// The raw body text, excluding the definition line and the terminator line, without a
    /// trailing newline (a line-form invocation supplies the newline of the line it replaces).
    /// </summary>
    public string Body { get; }

    /// <summary>True when defined with <c>@rmacro</c>, allowing recursive invocation.</summary>
    public bool IsRecursive { get; }

    /// <summary>
    /// True when defined with <c>@linemacro</c>. Such a macro is always called as a line command:
    /// it takes the rest of the line, split at spaces rather than at commas, and a following brace
    /// is an argument of its own rather than a brace-form argument list.
    /// </summary>
    public bool IsLineMacro { get; }

    /// <summary>Where the definition appeared.</summary>
    public SourcePosition DefinedAt { get; }
}
