namespace CodeBrix.Texinfo2Html.Preprocessing;

/// <summary>
/// Selects which Texinfo format conditionals are processed. Raw output blocks (<c>@tex</c>,
/// <c>@html</c>, ...) are always skipped with a warning regardless of profile, because their
/// content would bypass the HTML subset the emitter targets.
/// </summary>
internal enum ConditionalProfile
{
    /// <summary>
    /// The default profile for print-shaped PDF output. Takes every portable branch
    /// (<c>@ifnottex</c>, <c>@ifnothtml</c>, <c>@ifnotinfo</c>, ...) and additionally enters
    /// <c>@iftex</c> branches, while skipping <c>@ifhtml</c>, <c>@ifinfo</c> and the other
    /// format-specific branches. Entering both <c>@iftex</c> and <c>@ifnottex</c> is deliberate:
    /// real manuals keep their TeX-only machinery in raw <c>@tex</c> blocks (which are skipped
    /// anyway) and define the portable equivalents under <c>@ifnottex</c>, so processing both
    /// branches - with last-definition-wins macro semantics - yields the complete portable
    /// definition set.
    /// </summary>
    Print,

    /// <summary>
    /// Mirrors the conditional set a standard HTML generation run would use: <c>@ifhtml</c>
    /// branches on, <c>@ifnothtml</c> branches off, all other formats off and their
    /// <c>@ifnot...</c> branches on.
    /// </summary>
    Html
}
