using CodeBrix.PdfDocCreate.Html2Pdf;
using CodeBrix.Texinfo2Html;

namespace CodeBrix.Texinfo2Pdf;

/// <summary>
/// Everything that governs a Texinfo-to-PDF conversion, in the two groups the conversion actually
/// has: how the Texinfo source is read, and what the finished PDF looks like.
/// </summary>
/// <remarks>
/// Neither group is a copy. <see cref="Texinfo"/> is the live options object of the
/// CodeBrix.Texinfo2Html renderer doing the reading and <see cref="Html"/> is the live options
/// object of the CodeBrix.PdfDocCreate.Html2Pdf renderer doing the writing, so every setting either
/// library has is reachable here - including any it gains later - without a second package
/// reference and without anything having to be kept in step.
/// </remarks>
public sealed class TexinfoPdfOptions
{
    internal TexinfoPdfOptions(TexinfoHtmlOptions texinfo, HtmlRenderOptions html)
    {
        Texinfo = texinfo;
        Html = html;
        //The defaults a printed manual wants: its title running across the top of every page and
        //page numbers along the bottom. Either goes away again when set to an empty string.
        Html.HeaderText = "{title}";
        Html.FooterText = "{page} / {pages}";
    }

    /// <summary>
    /// How the Texinfo source is read and what markup comes out of it: the include and image search
    /// paths, the conditional profile, predefined <c>@set</c> values, the music-snippet renderer,
    /// and the rest.
    /// </summary>
    public TexinfoHtmlOptions Texinfo { get; }

    /// <summary>
    /// What the finished PDF looks like: page size and orientation, margins, the running header and
    /// footer, and the document metadata. <see cref="HtmlRenderOptions.DocumentTitle"/> and
    /// <see cref="HtmlRenderOptions.DocumentAuthor"/> are filled in from the document's own
    /// <c>@settitle</c> and <c>@author</c> when they are left empty, and are left alone when they
    /// are not.
    /// </summary>
    public HtmlRenderOptions Html { get; }
}
