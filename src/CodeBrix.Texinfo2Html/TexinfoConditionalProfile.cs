namespace CodeBrix.Texinfo2Html;

/// <summary>
/// Which set of Texinfo format conditionals a document is read with. A Texinfo source describes
/// several output formats at once, marking the parts meant for each, so the same file yields
/// different documents depending on which format is being produced. Raw output blocks
/// (<c>@tex</c>, <c>@html</c> and the like) are skipped with a warning under either profile,
/// because their content would bypass the HTML subset this library targets.
/// </summary>
public enum TexinfoConditionalProfile
{
    /// <summary>
    /// The profile for a printed document, and the right one for PDF output. Every portable
    /// branch is read - <c>@ifnottex</c>, <c>@ifnothtml</c>, <c>@ifnotinfo</c> and the rest - and
    /// <c>@iftex</c> is read as well, while <c>@ifhtml</c>, <c>@ifinfo</c> and the other
    /// format-specific branches are skipped.
    /// </summary>
    /// <remarks>
    /// Reading both <c>@iftex</c> and <c>@ifnottex</c> is deliberate. Real manuals keep their
    /// TeX-only machinery inside raw <c>@tex</c> blocks, which are skipped anyway, and put the
    /// portable equivalents under <c>@ifnottex</c>; reading both branches is what yields the
    /// complete set of definitions. A document that writes the same visible content into both
    /// branches will contribute it twice.
    /// </remarks>
    Print,

    /// <summary>
    /// The conditional set a standard HTML generation run would use: <c>@ifhtml</c> is read,
    /// <c>@ifnothtml</c> is skipped, every other format is skipped and its <c>@ifnot...</c> branch
    /// is read.
    /// </summary>
    Html
}
