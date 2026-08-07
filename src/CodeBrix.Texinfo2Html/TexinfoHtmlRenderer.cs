using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Texinfo2Html.Emit;
using CodeBrix.Texinfo2Html.Model;
using CodeBrix.Texinfo2Html.Parsing;
using CodeBrix.Texinfo2Html.Preprocessing;
using CodeBrix.Texinfo2Html.Semantics;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html;

/// <summary>
/// Renders GNU Texinfo source - a standard <c>.texi</c> document, or the <c>.tely</c> dialect
/// LilyPond and CodeBrix.LilyPort write - into HTML and CSS meant for producing a PDF rather than
/// for a browser. The markup stays inside the subset CodeBrix.PdfDocCreate.Html2Pdf implements, so
/// the output can be handed straight to that library.
/// </summary>
/// <remarks>
/// <para>
/// Rendering never throws over the contents of a document. Anything unsupported, malformed or
/// missing becomes a message in the result's warnings and the nearest readable degradation, so a
/// manual with one broken construct still produces the other ten thousand lines. Exceptions are
/// reserved for the caller's own mistakes, such as naming a file that does not exist.
/// </para>
/// <para>
/// One renderer can be reused for many documents; set <see cref="Options"/> before calling.
/// </para>
/// </remarks>
public sealed class TexinfoHtmlRenderer
{
    /// <summary>Settings for the next render; change them before calling a generate method.</summary>
    public TexinfoHtmlOptions Options { get; } = new TexinfoHtmlOptions();

    /// <summary>Renders a Texinfo file.</summary>
    /// <param name="texinfoFilePath">
    /// Path of the <c>.texi</c> or <c>.tely</c> file. Its directory and that directory's parent
    /// become the first places <c>@include</c> and <c>@image</c> look, which is what lets a manual
    /// written as a tree of included files render from its top-level source.
    /// </param>
    public TexinfoHtmlResult GenerateFromFile(string texinfoFilePath)
    {
        if (string.IsNullOrWhiteSpace(texinfoFilePath))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(texinfoFilePath));
        }
        string fullPath = Path.GetFullPath(texinfoFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Texinfo source file was not found.", fullPath);
        }
        TexinfoPreprocessor preprocessor = new TexinfoPreprocessor(BuildPreprocessorOptions());
        PreprocessedDocument preprocessed = preprocessor.ProcessFile(fullPath);
        return Render(preprocessed, Path.GetDirectoryName(fullPath),
            Path.GetFileNameWithoutExtension(fullPath));
    }

    /// <summary>Renders Texinfo source held in memory.</summary>
    /// <param name="texinfoSource">The Texinfo source text.</param>
    /// <param name="baseDirectory">
    /// Directory that <c>@include</c> and <c>@image</c> references resolve against, or null when
    /// the source needs no files of its own.
    /// </param>
    public TexinfoHtmlResult Generate(string texinfoSource, string baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(texinfoSource);
        TexinfoPreprocessor preprocessor = new TexinfoPreprocessor(BuildPreprocessorOptions());
        PreprocessedDocument preprocessed = preprocessor.Process(
            TexinfoSourceText.FromString("(source)", texinfoSource), baseDirectory);
        return Render(preprocessed, baseDirectory, null);
    }

    private PreprocessorOptions BuildPreprocessorOptions()
    {
        PreprocessorOptions options = new PreprocessorOptions
        {
            Profile = Options.ConditionalProfile == TexinfoConditionalProfile.Html
                ? ConditionalProfile.Html
                : ConditionalProfile.Print
        };
        options.IncludeSearchPaths.AddRange(Options.IncludeSearchPaths);
        foreach (KeyValuePair<string, string> pair in Options.PredefinedValues)
        {
            options.PredefinedValues[pair.Key] = pair.Value;
        }
        return options;
    }

    private TexinfoHtmlResult Render(PreprocessedDocument preprocessed, string baseDirectory,
        string sourceBaseName)
    {
        TexinfoDocument document = new TexinfoParser(preprocessed).Parse();
        DocumentSemantics semantics = DocumentSemantics.Analyze(document, Options.NumberSections);
        ImageReferenceResolver images = new ImageReferenceResolver(BuildImageSearchPaths(baseDirectory));
        string bodyHtml = new HtmlEmitter(document, semantics, images).EmitBody();

        string css = DefaultStylesheet.Css;
        if (!string.IsNullOrWhiteSpace(Options.ExtraCss))
        {
            css = css + "\n/* ExtraCss from the caller */\n" + Options.ExtraCss + "\n";
        }
        return new TexinfoHtmlResult(
            bodyHtml,
            css,
            document.Title,
            baseDirectory,
            ResolveCssFileName(sourceBaseName),
            sourceBaseName,
            Options.EmitSingleFile,
            //Warnings are snapshotted last: the emitter adds to the same collection the lexer,
            //preprocessor and parser filled, so the whole run is in one list by this point.
            new TexinfoRenderWarnings(document.Warnings));
    }

    private string ResolveCssFileName(string sourceBaseName)
    {
        if (!string.IsNullOrWhiteSpace(Options.CssFileName))
        {
            return Options.CssFileName.Trim();
        }
        return string.IsNullOrWhiteSpace(sourceBaseName) ? "texinfo.css" : sourceBaseName + ".css";
    }

    private List<string> BuildImageSearchPaths(string baseDirectory)
    {
        //@image references are written relative to wherever the manual keeps its pictures, which is
        //usually a sibling of the source directory rather than the source directory itself - hence
        //the parent, matching where @include already looks.
        List<string> paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            string full = Path.GetFullPath(baseDirectory);
            paths.Add(full);
            string parent = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(parent))
            {
                paths.Add(parent);
            }
        }
        foreach (string path in Options.IncludeSearchPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }
        foreach (string path in Options.ImageSearchPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }
        return paths;
    }
}
