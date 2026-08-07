using System.Collections.Generic;
using System.Text;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Lexing;

namespace CodeBrix.Texinfo2Html.Preprocessing;

/// <summary>
/// The result of a <see cref="TexinfoPreprocessor"/> run: the fully expanded token stream
/// (includes spliced, conditionals resolved, values substituted, macros expanded, comments and
/// skipped raw blocks removed) plus the tables the run built up and the warnings it collected.
/// </summary>
internal sealed class PreprocessedDocument
{
    /// <summary>Creates the result. Instances are built only by <see cref="TexinfoPreprocessor"/>.</summary>
    /// <param name="tokens">The expanded token stream.</param>
    /// <param name="warnings">All warnings collected during lexing and preprocessing.</param>
    /// <param name="macros">The final macro table state.</param>
    /// <param name="values">The final <c>@set</c> flag table state.</param>
    /// <param name="aliases">The final <c>@alias</c> table state.</param>
    /// <param name="documentEncoding">The last <c>@documentencoding</c> value, or empty.</param>
    public PreprocessedDocument(IReadOnlyList<TexinfoToken> tokens, TexinfoWarningCollection warnings,
        IReadOnlyDictionary<string, MacroDefinition> macros, IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> aliases, string documentEncoding)
    {
        Tokens = tokens;
        Warnings = warnings;
        Macros = macros;
        Values = values;
        Aliases = aliases;
        DocumentEncoding = documentEncoding;
    }

    /// <summary>The expanded token stream, ending with an EndOfInput token.</summary>
    public IReadOnlyList<TexinfoToken> Tokens { get; }

    /// <summary>All warnings collected during lexing and preprocessing, in order.</summary>
    public TexinfoWarningCollection Warnings { get; }

    /// <summary>The macro table after processing: every macro still defined at end of input.</summary>
    public IReadOnlyDictionary<string, MacroDefinition> Macros { get; }

    /// <summary>The flag table after processing: every <c>@set</c> flag still set at end of input.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>The alias table after processing.</summary>
    public IReadOnlyDictionary<string, string> Aliases { get; }

    /// <summary>The last <c>@documentencoding</c> value seen, or an empty string.</summary>
    public string DocumentEncoding { get; }

    /// <summary>
    /// Reconstructs Texinfo source text from the expanded token stream. This is the expansion
    /// dump used by tests and debugging to verify exactly what the later pipeline stages see.
    /// </summary>
    public string DumpExpandedSource()
    {
        StringBuilder builder = new StringBuilder();
        foreach (TexinfoToken token in Tokens)
        {
            builder.Append(token.ToSourceText());
        }
        return builder.ToString();
    }
}
