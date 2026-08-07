using System;
using System.Collections.Generic;

namespace CodeBrix.Texinfo2Html;

/// <summary>
/// Settings that govern how a Texinfo document is read and what shape the generated markup takes.
/// Every one has a default chosen for the common case - a single <c>.texi</c> or <c>.tely</c> file
/// rendered to a printable document - so a caller who sets nothing still gets a sensible result.
/// </summary>
public sealed class TexinfoHtmlOptions
{
    /// <summary>
    /// True to produce one self-contained HTML file with the stylesheet embedded in it. The
    /// default, false, produces an HTML file that links to a stylesheet file beside it, which is
    /// the easier pair to restyle by hand.
    /// </summary>
    public bool EmitSingleFile { get; set; }

    /// <summary>
    /// Which set of Texinfo format conditionals the source is read with. Defaults to
    /// <see cref="TexinfoConditionalProfile.Print"/>, the profile that suits PDF output.
    /// </summary>
    public TexinfoConditionalProfile ConditionalProfile { get; set; } = TexinfoConditionalProfile.Print;

    /// <summary>
    /// Extra directories searched by <c>@include</c>, after the source file's own directory and
    /// that directory's parent, which are always searched first.
    /// </summary>
    public List<string> IncludeSearchPaths { get; } = new List<string>();

    /// <summary>
    /// Extra directories searched for the files named by <c>@image</c>, after the directories
    /// <c>@include</c> searches. Texinfo image references carry no extension, so each directory is
    /// tried with the usual image extensions in turn.
    /// </summary>
    public List<string> ImageSearchPaths { get; } = new List<string>();

    /// <summary>
    /// Values that are set before the document is read, as though the source opened with
    /// <c>@set name value</c>. Useful for the version and date strings that a manual's build
    /// normally generates into an included file.
    /// </summary>
    public Dictionary<string, string> PredefinedValues { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// True, the default, to number chapters and sections the way Texinfo does. False leaves every
    /// heading unnumbered, which suits a short document that has no need of them.
    /// </summary>
    public bool NumberSections { get; set; } = true;

    /// <summary>
    /// Stylesheet text appended to the built-in one, so a caller can adjust the look without
    /// having to reproduce the whole stylesheet. Later rules win over earlier ones of equal
    /// specificity, so an appended rule overrides the built-in rule it repeats.
    /// </summary>
    public string ExtraCss { get; set; } = string.Empty;

    /// <summary>
    /// The name the stylesheet file is written under, and the name the generated HTML links to.
    /// When left empty it is derived from the source file's name, or is <c>texinfo.css</c> for a
    /// document rendered from a string.
    /// </summary>
    public string CssFileName { get; set; } = string.Empty;
}
