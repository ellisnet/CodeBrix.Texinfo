using System;
using System.IO;
using System.Text;
using CodeBrix.Texinfo2Html.Emit;

namespace CodeBrix.Texinfo2Html;

/// <summary>
/// The markup produced from one Texinfo document, along with the stylesheet it is written against
/// and everything that had to be degraded along the way. The HTML and the CSS are kept apart
/// whichever output shape was asked for, so a caller can restyle the document without picking a
/// stylesheet back out of the markup.
/// </summary>
public sealed class TexinfoHtmlResult
{
    private readonly bool _selfContained;

    internal TexinfoHtmlResult(string bodyHtml, string css, string title, string baseDirectory,
        string cssFileName, string defaultBaseName, bool selfContained,
        TexinfoRenderWarnings warnings)
    {
        BodyHtml = bodyHtml ?? string.Empty;
        Css = css ?? string.Empty;
        Title = title ?? string.Empty;
        BaseDirectory = baseDirectory ?? string.Empty;
        CssFileName = cssFileName ?? string.Empty;
        DefaultBaseName = defaultBaseName ?? "index";
        _selfContained = selfContained;
        Warnings = warnings;
        Html = selfContained
            ? HtmlDocumentBuilder.BuildSelfContained(BodyHtml, Css, Title)
            : HtmlDocumentBuilder.Build(BodyHtml, CssFileName, Title);
    }

    /// <summary>
    /// The complete HTML document. It carries the stylesheet inside it when the options asked for
    /// a single file, and otherwise links to the file named by <see cref="CssFileName"/>.
    /// </summary>
    public string Html { get; }

    /// <summary>
    /// The generated markup on its own, without the surrounding document, for a caller assembling
    /// a page of their own around it.
    /// </summary>
    public string BodyHtml { get; }

    /// <summary>The stylesheet the markup is written against, always kept separate.</summary>
    public string Css { get; }

    /// <summary>The document's title, from <c>@settitle</c>, or an empty string when it had none.</summary>
    public string Title { get; }

    /// <summary>
    /// The directory the source was read from, which is what relative references in the markup
    /// resolve against.
    /// </summary>
    public string BaseDirectory { get; }

    /// <summary>The file name the stylesheet is written under and the markup links to.</summary>
    public string CssFileName { get; }

    /// <summary>Everything that could not be rendered exactly as the source asked.</summary>
    public TexinfoRenderWarnings Warnings { get; }

    /// <summary>The base file name used when none is given to <see cref="WriteToDirectory"/>.</summary>
    internal string DefaultBaseName { get; }

    /// <summary>
    /// Rebuilds the complete document around a stylesheet of the caller's own, embedded in it. The
    /// hand-off point for restyling: take <see cref="Css"/>, change it or replace it outright, and
    /// pass it back here.
    /// </summary>
    /// <param name="replacementCss">The stylesheet to embed in place of the generated one.</param>
    public string ToHtmlDocument(string replacementCss)
        => HtmlDocumentBuilder.BuildSelfContained(BodyHtml, replacementCss ?? string.Empty, Title);

    /// <summary>
    /// Writes the document to a directory, creating it if it does not exist. Unless the options
    /// asked for a single file, the stylesheet is written beside the markup under
    /// <see cref="CssFileName"/>, which is the name the markup links to.
    /// </summary>
    /// <param name="directory">The directory to write into.</param>
    /// <param name="baseName">
    /// The file name to use, without an extension. Defaults to the source file's name, or to
    /// <c>index</c> for a document rendered from a string.
    /// </param>
    /// <returns>The full path of the HTML file that was written.</returns>
    public string WriteToDirectory(string directory, string baseName = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(directory));
        }
        string fullDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(fullDirectory);
        string name = string.IsNullOrWhiteSpace(baseName) ? DefaultBaseName : baseName.Trim();
        string htmlPath = Path.Combine(fullDirectory, name + ".html");
        UTF8Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(htmlPath, Html, encoding);
        if (!_selfContained && CssFileName.Length > 0)
        {
            File.WriteAllText(Path.Combine(fullDirectory, CssFileName), Css, encoding);
        }
        return htmlPath;
    }
}
