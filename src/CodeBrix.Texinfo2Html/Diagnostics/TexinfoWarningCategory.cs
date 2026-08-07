namespace CodeBrix.Texinfo2Html.Diagnostics;

/// <summary>
/// Classifies the kind of problem a <see cref="TexinfoWarning"/> reports, so consumers and
/// tests can filter expected degradations (such as skipped raw blocks) from genuine surprises.
/// </summary>
internal enum TexinfoWarningCategory
{
    /// <summary>An <c>@include</c> file could not be found, could not be read, or includes itself.</summary>
    Include,

    /// <summary>A conditional block (<c>@ifset</c>, <c>@iftex</c>, ...) is malformed or unbalanced.</summary>
    Conditional,

    /// <summary>A macro definition, redefinition, or expansion problem (<c>@macro</c>, <c>@rmacro</c>, <c>@alias</c>).</summary>
    Macro,

    /// <summary>A <c>@value</c> reference to a flag that has no value, or a malformed <c>@set</c>/<c>@value</c>.</summary>
    Value,

    /// <summary>A raw output block (<c>@tex</c>, <c>@html</c>, ...) was skipped because its content cannot be rendered.</summary>
    RawBlockSkipped,

    /// <summary>A <c>@documentencoding</c> value other than UTF-8 (or US-ASCII) was declared.</summary>
    Encoding,

    /// <summary>A lexical or structural problem in the Texinfo source itself.</summary>
    Syntax,

    /// <summary>
    /// A command the parser does not implement. Its argument text is still rendered, so the
    /// content survives even though its meaning is lost.
    /// </summary>
    UnknownCommand,

    /// <summary>A problem with a node, anchor or cross reference, such as a duplicate name.</summary>
    Reference,

    /// <summary>
    /// Something the emitter could not render as the source intended, and rendered some other way:
    /// an environment with no counterpart in the output subset, or a feature that is understood but
    /// not yet produced. Kept apart from <see cref="UnknownCommand"/> so a document that parsed
    /// cleanly can still be told apart from one that did not.
    /// </summary>
    Emit
}
