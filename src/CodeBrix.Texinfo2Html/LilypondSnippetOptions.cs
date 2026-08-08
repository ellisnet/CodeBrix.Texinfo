using System;
using System.Collections.Generic;

namespace CodeBrix.Texinfo2Html;

/// <summary>
/// The bracketed option list of a lilypond-book music environment, read into named properties. The
/// options that decide how the snippet is engraved are passed on to the renderer untouched; the two
/// that decide how the document places it - <see cref="Quote"/> and <see cref="Verbatim"/> - are
/// acted on here.
/// </summary>
/// <remarks>
/// <para>
/// The vocabulary understood is the one real documents use, measured across the whole English
/// LilyPond documentation set. Anything outside it is still handed on: it is kept in
/// <see cref="All"/> in the order it was written and listed again in <see cref="Unrecognized"/>, so
/// a renderer that knows an option this library does not can still act on it.
/// </para>
/// <para>
/// A dimension such as <c>line-width</c> is kept exactly as it was written, because it is written
/// in LilyPond's own units (<c>3\cm</c>, <c>6\in</c>) which mean nothing outside an engraver. Do
/// not try to convert them; hand them to the renderer as they stand.
/// </para>
/// </remarks>
public sealed class LilypondSnippetOptions
{
    internal LilypondSnippetOptions()
    {
    }

    /// <summary>True when <c>quote</c> was given: the snippet is indented from the margin.</summary>
    public bool Quote { get; internal set; }

    /// <summary>
    /// True when <c>verbatim</c> was given: the music source is shown as well as engraved. With no
    /// renderer registered the source is all there is to show, so this option changes nothing.
    /// </summary>
    public bool Verbatim { get; internal set; }

    /// <summary>
    /// True when <c>inline</c> was given: the snippet is a small fragment meant to sit in the run of
    /// the text rather than to stand on its own line.
    /// </summary>
    public bool Inline { get; internal set; }

    /// <summary>True when <c>notime</c> was given: the engraving omits the time signature.</summary>
    public bool NoTime { get; internal set; }

    /// <summary>
    /// True when <c>texidoc</c> was given: the file's own documentation text is wanted alongside the
    /// engraving. Only a renderer can read it, since it lives inside the music file.
    /// </summary>
    public bool TexiDoc { get; internal set; }

    /// <summary>
    /// True when <c>doctitle</c> was given: the file's own title is wanted with the engraving, and
    /// like <see cref="TexiDoc"/> only a renderer can supply it.
    /// </summary>
    public bool DocTitle { get; internal set; }

    /// <summary>True when <c>noindent</c> was given, which is <c>indent=0</c> said as a flag.</summary>
    public bool NoIndent { get; internal set; }

    /// <summary>
    /// True from <c>ragged-right</c>, false from <c>noragged-right</c>, and null when the document
    /// said neither and the engraver's own default should stand.
    /// </summary>
    public bool? RaggedRight { get; internal set; }

    /// <summary>
    /// True from <c>fragment</c>, false from <c>nofragment</c>, null when neither was given. A
    /// fragment is music without the surrounding braces and score plumbing, which the engraver
    /// supplies for it.
    /// </summary>
    public bool? Fragment { get; internal set; }

    /// <summary>
    /// The octave the music is written relative to, from <c>relative=N</c>. Bare <c>relative</c>
    /// counts as 1, which is what lilypond-book takes it for. Null when the option was absent.
    /// </summary>
    public int? Relative { get; internal set; }

    /// <summary>The staff size from <c>staffsize=N</c>, or null when the option was absent.</summary>
    public double? StaffSize { get; internal set; }

    /// <summary>
    /// The line width from <c>line-width=</c>, in LilyPond's own units and exactly as written
    /// (<c>3\cm</c>, <c>13.0\cm</c>, <c>6\in</c>); an empty string when the option was absent.
    /// </summary>
    public string LineWidth { get; internal set; } = string.Empty;

    /// <summary>The indent from <c>indent=</c> as written, or an empty string when absent.</summary>
    public string Indent { get; internal set; } = string.Empty;

    /// <summary>The paper size from <c>papersize=</c> (<c>a5</c>, <c>a8landscape</c>), or empty.</summary>
    public string PaperSize { get; internal set; } = string.Empty;

    /// <summary>The paper width from <c>paper-width=</c> as written, or an empty string.</summary>
    public string PaperWidth { get; internal set; } = string.Empty;

    /// <summary>The paper height from <c>paper-height=</c> as written, or an empty string.</summary>
    public string PaperHeight { get; internal set; } = string.Empty;

    /// <summary>
    /// Every option exactly as it was written, in the order it was written, whether or not this
    /// library recognizes it. A renderer that understands more than the properties above should read
    /// this rather than being limited by them.
    /// </summary>
    public IReadOnlyList<string> All { get; internal set; } = Array.Empty<string>();

    /// <summary>
    /// The options that no property above corresponds to. They are still present in
    /// <see cref="All"/>; this is the short list a renderer can check to see what it was given that
    /// this library had no name for.
    /// </summary>
    public IReadOnlyList<string> Unrecognized { get; internal set; } = Array.Empty<string>();

    /// <summary>Formats the option list for diagnostics.</summary>
    public override string ToString() => All.Count == 0 ? "(no options)" : string.Join(",", All);
}
