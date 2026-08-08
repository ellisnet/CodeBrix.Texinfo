using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// Renders a manual through the shipped CodeBrix.Texinfo2Pdf API, which is what makes this a
    /// gate on what a consumer actually gets rather than on a chain assembled by the test. The
    /// intermediate is written out beside the PDF as well, so a failure can be looked at.
    /// </summary>
    private static TexinfoPdfResult RenderManual(string manualFileName)
    {
        string standIn = Directory.CreateTempSubdirectory("texinfo-gate-version-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(standIn, "version.itexi"),
                "@c Test stand-in for LilyPond's build-generated version.itexi.\n"
                + "@macro version\n2.25.99\n@end macro\n"
                + "@macro versionStable\n2.24.99\n@end macro\n"
                + "@macro versionDevel\n2.25.99\n@end macro\n");
            TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
            renderer.Options.Texinfo.IncludeSearchPaths.Add(standIn);
            TexinfoHtmlResult texinfo = renderer.GenerateHtmlFromFile(
                Path.Combine(CorpusRoot, "en", manualFileName));

            string baseName = Path.GetFileNameWithoutExtension(manualFileName);
            texinfo.WriteToDirectory(OutputDirectory, baseName);
            return renderer.RenderHtml(texinfo, Path.Combine(OutputDirectory, baseName + ".pdf"));
        }
        finally
        {
            Directory.Delete(standIn, recursive: true);
        }
    }

    [Theory]
    [InlineData("essay.tely", "Essay on automated music engraving", 30)]
    [InlineData("changes.tely", "LilyPond Changes", 5)]
    [InlineData("music-glossary.tely", "LilyPond Music Glossary", 120)]
    [InlineData("usage.tely", "LilyPond Application Usage", 80)]
    //The two manuals whose music is the point of them, and so the two the snippet layer is gated on.
    [InlineData("learning.tely", "LilyPond Learning Manual", 150)]
    [InlineData("extending.tely", "Extending LilyPond", 60)]
    public void Manual_renders_all_the_way_to_a_pdf(string manualFileName, string title,
        int minimumPages)
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        TexinfoPdfResult pdf = RenderManual(manualFileName);

        //Assert
        pdf.Intermediate.Title.Should().Be(title);
        pdf.Title.Should().Be(title);
        pdf.PageCount.Should().BeGreaterThanOrEqualTo(minimumPages);
        File.Exists(pdf.OutputFilePath).Should().BeTrue();
        new FileInfo(pdf.OutputFilePath).Length.Should().BeGreaterThan(10_000);

        //Html2Pdf must find nothing to complain about in the markup: an unsupported element or a
        //CSS property outside its dialect would show up here, and that is exactly what this gate
        //is for. Font-coverage messages are the one exception - a music manual quotes symbols no
        //text font carries, and dropping them is the documented behaviour.
        string unexpected = string.Join(Environment.NewLine,
            pdf.Warnings.PdfMessages.Where(m => !m.StartsWith("[font]", StringComparison.Ordinal)));
        unexpected.Should().Be(string.Empty);
    }

    /// <summary>
    /// The scripts and symbol ranges the CodeBrix.Platform.Fonts packages carry. CodeBrix never
    /// falls back to a system font, so a character outside them is dropped from the PDF with a
    /// warning - which is the right behaviour, and which makes the warnings a precise statement of
    /// what a document lost.
    /// </summary>
    private static bool IsCoveredByThePackageFonts(int codePoint)
        //Latin through Latin Extended-B, then Greek and Cyrillic, then the general punctuation
        //that the text conventions produce: the dashes and the directed quotation marks.
        => codePoint <= 0x024F
           || (codePoint >= 0x0370 && codePoint <= 0x04FF)
           || (codePoint >= 0x2000 && codePoint <= 0x206F);

    [Theory]
    [InlineData("music-glossary.tely")]
    [InlineData("notation.tely")]
    public void Every_character_the_package_fonts_cover_survives_into_the_pdf(string manualFileName)
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        TexinfoPdfResult pdf = RenderManual(manualFileName);

        //Assert - the characters Html2Pdf dropped may only be ones no package font carries. The
        //two manuals between them drop the musical accidental signs and a Hebrew lyric written
        //into a snippet; a Latin, Greek or Cyrillic character here would be a real fault.
        string lost = string.Join(", ", DroppedCodePoints(pdf.Warnings.PdfMessages)
            .Where(IsCoveredByThePackageFonts)
            .Select(c => "U+" + c.ToString("X4", CultureInfo.InvariantCulture)));
        lost.Should().Be(string.Empty);
        //And nothing but font coverage may be reported at all.
        pdf.Warnings.PdfMessages.Where(m => !m.StartsWith("[font]", StringComparison.Ordinal))
            .Count().Should().Be(0);
    }

    [Fact]
    public void The_encoding_torture_test_keeps_its_accents()
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act - music-glossary is the manual that quotes every European language.
        TexinfoPdfResult pdf = RenderManual("music-glossary.tely");

        //Assert - the markup really does carry the characters this test is about, so that the
        //assertion below is a statement about them rather than about an empty set.
        pdf.Intermediate.BodyHtml.Any(c => c >= 0x00C0 && c <= 0x024F).Should().BeTrue();
        pdf.Intermediate.BodyHtml.Contains('—').Should().BeTrue();
        pdf.Intermediate.BodyHtml.Contains('’').Should().BeTrue();
        //Only the two musical accidental signs are outside the package fonts.
        DroppedCodePoints(pdf.Warnings.PdfMessages).OrderBy(c => c).Should()
            .BeEquivalentTo(new[] { 0x266D, 0x266F });
    }

    [Fact]
    public void The_notation_reference_keeps_the_cyrillic_lyric_it_quotes()
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act - the notation reference's non-Latin lyric example is where the corpus
        //writes Cyrillic and Hebrew side by side.
        TexinfoPdfResult pdf = RenderManual("notation.tely");

        //Assert
        pdf.Intermediate.BodyHtml.Any(c => c >= 0x0400 && c <= 0x04FF).Should().BeTrue();
        //The Hebrew of the same example is the only script the package fonts cannot set, so it is
        //the only thing dropped; the Cyrillic beside it comes through.
        DroppedCodePoints(pdf.Warnings.PdfMessages).Distinct()
            .All(c => c >= 0x0590 && c <= 0x05FF).Should().BeTrue();
    }

    private static IEnumerable<int> DroppedCodePoints(IEnumerable<string> messages)
    {
        foreach (string message in messages)
        {
            int index = message.IndexOf("U+", StringComparison.Ordinal);
            if (index >= 0 && int.TryParse(message.AsSpan(index + 2, 4), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out int codePoint))
            {
                yield return codePoint;
            }
        }
    }

    [Fact]
    public void A_manual_that_is_written_out_takes_its_pictures_with_it()
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act - the essay is the manual with figures in it.
        TexinfoPdfResult pdf = RenderManual("essay.tely");

        //Assert - every picture the markup names was copied beside the document, which is what
        //lets Html2Pdf find them from the written file rather than from the source tree.
        pdf.Intermediate.Images.Count.Should().BeGreaterThan(20);
        foreach (TexinfoImageReference image in pdf.Intermediate.Images)
        {
            File.Exists(Path.Combine(OutputDirectory,
                image.RelativePath.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
        }
        pdf.PageCount.Should().BeGreaterThanOrEqualTo(30);
    }

    /// <summary>
    /// Every construct of the general-Texinfo set in one document. The LilyPond corpus uses almost
    /// none of these, so nothing in the corpus gates can show that Html2Pdf accepts the markup they
    /// produce - which is exactly what this proves.
    /// </summary>
    private const string WildTexinfo =
        "@settitle Wild Texinfo\n"
        + "@shorttitlepage Wild Texinfo\n"
        + "@defcodeindex cd\n"
        + "@node Top\n@top Wild Texinfo\n"
        + "@contents\n"
        + "@node Definitions\n@chapter Definitions\n"
        + "@deffn {Interactive Command} isearch-forward count\n"
        + "@deffnx {Interactive Command} isearch-backward count\n"
        + "Searches, in one direction or the other.\n"
        + "@end deffn\n"
        + "@deftypefn {Library Function} int foobar (int @var{foo}, float @var{bar})\n"
        + "Returns something.\n"
        + "@end deftypefn\n"
        + "@defmethod Window expose sides\n"
        + "Exposes the window.\n"
        + "@end defmethod\n"
        + "@defvr {User Option} fill-column\n"
        + "The column that filling stops at.\n"
        + "@end defvr\n"
        + "@deftypevar int frame-count\n"
        + "How many frames there are.\n"
        + "@end deftypevar\n"
        + "@defcv {Class Option} Window border-pattern\n"
        + "A class option.\n"
        + "@end defcv\n"
        + "@deftp {Data type} pair car cdr\n"
        + "A pair.\n"
        + "@end deftp\n"
        + "@defblock\n"
        + "@defline Macro mac (arg1, arg2)\n"
        + "A macro.\n"
        + "@end defblock\n"
        + "@node Floats\n@chapter Floats\n"
        + "@float Figure,fig:one\n"
        + "@example\nA picture would go here.\n@end example\n"
        + "@caption{The only figure}\n"
        + "@shortcaption{The figure}\n"
        + "@end float\n"
        + "See @ref{fig:one} for the picture.\n"
        + "@listoffloats Figure\n"
        + "@node Text\n@chapter Text\n"
        + "Accents: @'e @`a @^o @\"u @~n @,{c} @=a @v{s} @H{o} @dotless{i} @ringaccent{a}.\n"
        + "@acronym{NASA, National Aeronautics and Space Administration} and @abbr{Comput., Computer}.\n"
        + "Literal text: @verb{|@code{x} --- kept|}.\n"
        + "@inlinefmt{tex, This branch is for print.}\n"
        + "@quotation\n"
        + "Indented.\n"
        + "@exdent Standing clear of the indent.\n"
        + "@end quotation\n"
        + "@ftable @code\n@item alpha\nThe first.\n@item beta\nThe second.\n@end ftable\n"
        + "@vtable @code\n@item gamma\nA variable.\n@end vtable\n"
        + "@cdindex a-user-index-entry\n"
        + "@kindex C-x C-f\n@pindex a-program\n@tindex a-type\n"
        + "@node Indices\n@appendix Indices\n"
        + "@printindex fn\n@printindex vr\n@printindex tp\n@printindex ky\n"
        + "@printindex pg\n@printindex cd\n";

    [Fact]
    public void The_general_texinfo_set_renders_all_the_way_to_a_pdf()
    {
        //Arrange
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();

        //Act - the intermediate is written out as well, so a failure can be looked at.
        TexinfoHtmlResult texinfo = renderer.GenerateHtml(WildTexinfo);
        texinfo.WriteToDirectory(OutputDirectory, "wild-texinfo");
        TexinfoPdfResult pdf = renderer.RenderHtml(texinfo,
            Path.Combine(OutputDirectory, "wild-texinfo.pdf"));

        //Assert - the Texinfo side has nothing to report about a document that uses only
        //implemented commands...
        string texinfoWarnings = string.Join(Environment.NewLine, pdf.Warnings.TexinfoMessages);
        texinfoWarnings.Should().Be(string.Empty);
        //...and neither has Html2Pdf, which is what says the new markup is inside its subset.
        string pdfWarnings = string.Join(Environment.NewLine,
            pdf.Warnings.PdfMessages.Where(m => !m.StartsWith("[font]", StringComparison.Ordinal)));
        pdfWarnings.Should().Be(string.Empty);
        pdf.Title.Should().Be("Wild Texinfo");
        //Four chapters, each starting a fresh page, plus the title page and the contents.
        pdf.PageCount.Should().BeGreaterThanOrEqualTo(5);
        File.Exists(pdf.OutputFilePath).Should().BeTrue();
    }

    [Fact]
    public void No_accent_the_language_can_write_is_dropped_from_the_pdf()
    {
        //Arrange + Act
        TexinfoPdfResult pdf = new TexinfoPdfRenderer().RenderTexinfo(WildTexinfo,
            Path.Combine(OutputDirectory, "wild-texinfo-accents.pdf"));

        //Assert - composing an accent yields the precomposed character wherever Unicode has one,
        //and those are the ones the package fonts carry. Anything the fonts do cover must survive.
        string lost = string.Join(", ", DroppedCodePoints(pdf.Warnings.PdfMessages)
            .Where(IsCoveredByThePackageFonts)
            .Select(c => "U+" + c.ToString("X4", CultureInfo.InvariantCulture)));
        lost.Should().Be(string.Empty);
        pdf.Intermediate.BodyHtml.Contains("é").Should().BeTrue();
        pdf.Intermediate.BodyHtml.Contains("š").Should().BeTrue();
        pdf.Intermediate.BodyHtml.Contains("ő").Should().BeTrue();
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
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        renderer.Options.Texinfo.EmitSingleFile = true;
        string directory = Directory.CreateTempSubdirectory("texinfo-single-pdf-").FullName;

        try
        {
            //Act
            TexinfoPdfResult pdf = renderer.RenderTexinfo(source,
                Path.Combine(directory, "guide.pdf"));

            //Assert - one file in the directory, which is also what says a conversion leaves
            //nothing of its own behind.
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
