using System;
using System.IO;
using System.Linq;
using CodeBrix.PdfDocCreate.Html2Pdf;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Pdf.Tests;

/// <summary>
/// The end-to-end gate: Texinfo source rendered to HTML and CSS by CodeBrix.Texinfo2Html, and that
/// markup rendered to a PDF by CodeBrix.PdfDocCreate.Html2Pdf without a single complaint from
/// either side. This is what proves the two libraries agree on the markup subset, which no test
/// inside CodeBrix.Texinfo2Html on its own can show.
/// </summary>
/// <remarks>
/// The corpus tests read the English LilyPond documentation from ~/GitHome/lilypond, which is
/// GFDL-licensed and therefore never committed here; they skip cleanly when it is absent. The
/// generated PDFs are left in the temporary directory named by
/// <see cref="OutputDirectory"/> so they can be looked at after a run.
/// </remarks>
public class TexinfoToPdfGateTests
{
    private static string OutputDirectory
        => Path.Combine(Path.GetTempPath(), "codebrix-texinfo-gate");

    private static string CorpusRoot
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "GitHome", "lilypond", "Documentation");

    private static void SkipUnlessCorpusPresent()
    {
        if (!File.Exists(Path.Combine(CorpusRoot, "en", "macros.itexi")))
        {
            Assert.Skip($"LilyPond documentation corpus not present under {CorpusRoot}.");
        }
    }

    /// <summary>Renders a manual to HTML, writes it out, and renders that file to a PDF.</summary>
    private static (TexinfoHtmlResult Texinfo, HtmlRenderResult Pdf) RenderManual(string manualFileName)
    {
        string standIn = Directory.CreateTempSubdirectory("texinfo-gate-version-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(standIn, "version.itexi"),
                "@c Test stand-in for LilyPond's build-generated version.itexi.\n"
                + "@macro version\n2.25.99\n@end macro\n"
                + "@macro versionStable\n2.24.99\n@end macro\n"
                + "@macro versionDevel\n2.25.99\n@end macro\n");
            TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
            renderer.Options.IncludeSearchPaths.Add(standIn);
            TexinfoHtmlResult texinfo = renderer.GenerateFromFile(
                Path.Combine(CorpusRoot, "en", manualFileName));

            string baseName = Path.GetFileNameWithoutExtension(manualFileName);
            //The stylesheet is written beside the markup, and Html2Pdf follows the link to it.
            string htmlPath = texinfo.WriteToDirectory(OutputDirectory, baseName);

            HtmlPdfRenderer pdfRenderer = new HtmlPdfRenderer();
            pdfRenderer.Options.FooterText = "{page} / {pages}";
            HtmlRenderResult pdf = pdfRenderer.RenderFile(htmlPath,
                Path.Combine(OutputDirectory, baseName + ".pdf"));
            return (texinfo, pdf);
        }
        finally
        {
            Directory.Delete(standIn, recursive: true);
        }
    }

    [Theory]
    [InlineData("essay.tely", "Essay on automated music engraving", 30)]
    [InlineData("changes.tely", "LilyPond Changes", 5)]
    public void Manual_renders_all_the_way_to_a_pdf(string manualFileName, string title,
        int minimumPages)
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        (TexinfoHtmlResult texinfo, HtmlRenderResult pdf) = RenderManual(manualFileName);

        //Assert
        texinfo.Title.Should().Be(title);
        pdf.Title.Should().Be(title);
        pdf.PageCount.Should().BeGreaterThanOrEqualTo(minimumPages);
        File.Exists(pdf.OutputFilePath).Should().BeTrue();
        new FileInfo(pdf.OutputFilePath).Length.Should().BeGreaterThan(10_000);

        //Html2Pdf must find nothing to complain about in the markup: an unsupported element or a
        //CSS property outside its dialect would show up here, and that is exactly what this gate
        //is for. Font-coverage messages are the one exception - a music manual quotes symbols no
        //text font carries, and dropping them is the documented behaviour.
        string unexpected = string.Join(Environment.NewLine,
            pdf.Warnings.Messages.Where(m => !m.StartsWith("[font]", StringComparison.Ordinal)));
        unexpected.Should().Be(string.Empty);
    }

    [Fact]
    public void A_self_contained_document_renders_to_a_pdf_without_a_stylesheet_file()
    {
        //Arrange - no corpus needed: this checks the single-file shape carries its own styling.
        const string source = "@settitle Pocket Guide\n"
            + "@titlepage\n@title Pocket Guide\n@author A. Writer\n@end titlepage\n"
            + "@contents\n"
            + "@node Basics\n@chapter Basics\n"
            + "A paragraph with @code{code}, @emph{emphasis} and a @uref{https://example.org, link}.\n\n"
            + "@example\nsome  spaced  code\n@end example\n"
            + "@itemize @bullet\n@item\nFirst\n@item\nSecond\n@end itemize\n"
            + "@multitable @columnfractions .3 .7\n@headitem Name @tab Meaning\n"
            + "@item Alpha @tab The first letter.\n@end multitable\n"
            + "@quotation Note\nMind the gap.@footnote{Or fall in.}\n@end quotation\n";
        TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
        renderer.Options.EmitSingleFile = true;
        string directory = Directory.CreateTempSubdirectory("texinfo-single-pdf-").FullName;

        try
        {
            //Act
            TexinfoHtmlResult texinfo = renderer.Generate(source);
            HtmlRenderResult pdf = new HtmlPdfRenderer().RenderHtml(texinfo.Html,
                Path.Combine(directory, "guide.pdf"));

            //Assert
            Directory.GetFiles(directory).Length.Should().Be(1);
            pdf.Title.Should().Be("Pocket Guide");
            pdf.PageCount.Should().BeGreaterThanOrEqualTo(1);
            pdf.Warnings.Count.Should().Be(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
