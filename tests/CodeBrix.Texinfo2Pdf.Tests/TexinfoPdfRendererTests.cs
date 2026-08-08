using System;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Pdf.Tests;

/// <summary>
/// The public API of CodeBrix.Texinfo2Pdf: both workflows, the options handed through to the two
/// libraries underneath, the warnings merged out of them, and the things a caller can get wrong.
/// </summary>
/// <remarks>
/// Every test here works from source written for it, so none of them needs the LilyPond corpus.
/// What the corpus is for is scale, and that is <see cref="NotationStressTests"/>.
/// </remarks>
public class TexinfoPdfRendererTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("texinfo-pdf-api-").FullName;

    /// <summary>A short document that exercises the ordinary shape of a manual.</summary>
    private const string Source = "@settitle Pocket Manual\n"
        + "@titlepage\n@title Pocket Manual\n@author A. Writer\n@end titlepage\n"
        + "@contents\n"
        + "@node Top\n@top Pocket Manual\n"
        + "@node Basics\n@chapter Basics\n"
        + "A paragraph with @code{code} and @emph{emphasis}.\n\n"
        + "@itemize @bullet\n@item\nFirst\n@item\nSecond\n@end itemize\n"
        + "@node More\n@chapter More\n"
        + "Another chapter, so the document runs to more than one page.\n";

    /// <summary>
    /// One chapter of running prose, long enough that how many pages it takes is decided by the
    /// size of its type rather than by where the chapters start. That is what makes a change to the
    /// stylesheet something a test can count.
    /// </summary>
    private static readonly string LongSource = BuildLongSource();

    private static string BuildLongSource()
    {
        StringBuilder builder = new StringBuilder("@settitle Long Manual\n@node Top\n@top Long Manual\n"
            + "@node Body\n@chapter Body\n");
        for (int paragraph = 0; paragraph < 40; paragraph++)
        {
            builder.Append("Paragraph ").Append(paragraph)
                .Append(" of a document long enough that the size of its type is what decides how ")
                .Append("many pages the whole of it comes to.\n\n");
        }
        return builder.ToString();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string In(string fileName) => Path.Combine(_directory, fileName);

    private string WriteSource(string fileName, string text)
    {
        string path = In(fileName);
        File.WriteAllText(path, text);
        return path;
    }

    // ----- workflow one: source in, PDF out -------------------------------------------------------

    [Fact]
    public void RenderFile_writes_the_pdf_beside_the_source_when_no_output_path_is_given()
    {
        //Arrange
        string sourcePath = WriteSource("pocket.texi", Source);

        //Act
        TexinfoPdfResult result = new TexinfoPdfRenderer().RenderFile(sourcePath);

        //Assert
        result.OutputFilePath.Should().Be(In("pocket.pdf"));
        File.Exists(result.OutputFilePath).Should().BeTrue();
        result.PageCount.Should().BeGreaterThanOrEqualTo(2);
        result.Title.Should().Be("Pocket Manual");
        result.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void RenderFile_creates_the_output_directory_it_was_given()
    {
        //Arrange - a caller who asks for "out/manual.pdf" means the directory too.
        string sourcePath = WriteSource("pocket.texi", Source);
        string outputPath = Path.Combine(_directory, "out", "nested", "manual.pdf");

        //Act
        TexinfoPdfResult result = new TexinfoPdfRenderer().RenderFile(sourcePath, outputPath);

        //Assert
        File.Exists(outputPath).Should().BeTrue();
        result.OutputFilePath.Should().Be(outputPath);
    }

    [Fact]
    public void RenderTexinfo_renders_source_held_in_memory()
    {
        //Arrange + Act
        TexinfoPdfResult result =
            new TexinfoPdfRenderer().RenderTexinfo(Source, In("memory.pdf"));

        //Assert
        result.Title.Should().Be("Pocket Manual");
        result.PageCount.Should().BeGreaterThanOrEqualTo(2);
        new FileInfo(result.OutputFilePath).Length.Should().BeGreaterThan(1_000);
    }

    [Fact]
    public void RenderFileToBytes_produces_the_document_without_writing_it_anywhere()
    {
        //Arrange
        string sourcePath = WriteSource("pocket.texi", Source);

        //Act
        TexinfoPdfResult result = new TexinfoPdfRenderer().RenderFileToBytes(sourcePath);

        //Assert - the bytes really are a PDF, and nothing landed beside the source.
        result.PdfBytes.Length.Should().BeGreaterThan(1_000);
        System.Text.Encoding.ASCII.GetString(result.PdfBytes, 0, 5).Should().Be("%PDF-");
        result.OutputFilePath.Should().Be(string.Empty);
        Directory.GetFiles(_directory).Length.Should().Be(1);
    }

    [Fact]
    public void RenderTexinfoToBytes_produces_the_document_without_touching_disk()
    {
        //Arrange + Act
        TexinfoPdfResult result = new TexinfoPdfRenderer().RenderTexinfoToBytes(Source);

        //Assert
        result.PdfBytes.Length.Should().BeGreaterThan(1_000);
        result.Title.Should().Be("Pocket Manual");
        Directory.GetFileSystemEntries(_directory).Length.Should().Be(0);
    }

    [Fact]
    public void A_one_shot_conversion_keeps_the_intermediate_it_went_through()
    {
        //Arrange + Act
        TexinfoPdfResult result = new TexinfoPdfRenderer().RenderTexinfo(Source, In("kept.pdf"));

        //Assert
        result.Intermediate.Should().NotBeNull();
        result.Intermediate.Title.Should().Be("Pocket Manual");
        result.Intermediate.BodyHtml.Should().Contain("Basics");
        result.Intermediate.Css.Should().NotBeEmpty();
    }

    // ----- workflow two: markup out, tweaked, markup back in --------------------------------------

    [Fact]
    public void GenerateHtml_hands_back_the_markup_and_the_stylesheet_apart()
    {
        //Arrange + Act
        TexinfoHtmlResult html = new TexinfoPdfRenderer().GenerateHtml(Source);

        //Assert
        html.Title.Should().Be("Pocket Manual");
        html.Author.Should().Be("A. Writer");
        html.Css.Should().Contain("texinfo");
        html.Html.Should().Contain("Pocket Manual");
    }

    [Fact]
    public void GenerateHtmlFromFile_reads_the_file_and_renders_no_pdf()
    {
        //Arrange
        string sourcePath = WriteSource("pocket.texi", Source);

        //Act
        TexinfoHtmlResult html = new TexinfoPdfRenderer().GenerateHtmlFromFile(sourcePath);

        //Assert
        html.Title.Should().Be("Pocket Manual");
        Directory.GetFiles(_directory, "*.pdf").Length.Should().Be(0);
    }

    [Fact]
    public void RenderHtml_takes_the_markup_back_and_finishes_the_job()
    {
        //Arrange
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        TexinfoHtmlResult html = renderer.GenerateHtml(Source);

        //Act
        TexinfoPdfResult result = renderer.RenderHtml(html, In("two-step.pdf"));

        //Assert - the same document as the one-shot workflow produces.
        result.Title.Should().Be("Pocket Manual");
        result.PageCount.Should().BeGreaterThanOrEqualTo(2);
        result.Intermediate.Should().BeSameAs(html);
    }

    [Fact]
    public void RenderHtml_honours_a_stylesheet_the_caller_changed()
    {
        //Arrange - the point of the tweak loop: a change to the CSS must reach the PDF. Larger
        //body text is the change whose effect can be counted rather than looked at.
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        TexinfoHtmlResult html = renderer.GenerateHtml(LongSource);
        int asGenerated = renderer.RenderHtml(html, In("as-generated.pdf")).PageCount;

        //Act
        TexinfoPdfResult tweaked = renderer.RenderHtml(html, In("tweaked.pdf"),
            html.Css + "\nbody, p { font-size: 32pt; }\n");

        //Assert
        tweaked.PageCount.Should().BeGreaterThan(asGenerated);
    }

    [Fact]
    public void RenderHtmlFile_renders_a_written_out_pair_including_its_edited_stylesheet()
    {
        //Arrange - written out, then edited on disk, which is the other way round the tweak loop.
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        TexinfoHtmlResult html = renderer.GenerateHtml(LongSource);
        string htmlPath = html.WriteToDirectory(_directory, "pocket");
        int asWritten = renderer.RenderHtmlFile(htmlPath, In("as-written.pdf")).PageCount;
        File.AppendAllText(Path.Combine(_directory, html.CssFileName),
            "\nbody, p { font-size: 32pt; }\n");

        //Act
        TexinfoPdfResult result = renderer.RenderHtmlFile(htmlPath, In("edited.pdf"));

        //Assert - the linked stylesheet was followed, so the edit to it changed the document.
        result.PageCount.Should().BeGreaterThan(asWritten);
        //Nothing was read from a Texinfo source this time, so there is no intermediate to report.
        result.Intermediate.Should().BeNull();
        result.Warnings.TexinfoMessages.Count.Should().Be(0);
    }

    [Fact]
    public void RenderHtmlDocument_renders_markup_the_caller_assembled()
    {
        //Arrange - the body dropped into a page of the caller's own.
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        TexinfoHtmlResult html = renderer.GenerateHtml(Source);
        string page = "<html><head><title>Assembled</title><style>" + html.Css
            + "</style></head><body>" + html.BodyHtml + "</body></html>";

        //Act
        TexinfoPdfResult result = renderer.RenderHtmlDocument(page, In("assembled.pdf"));

        //Assert
        result.Title.Should().Be("Assembled");
        result.PageCount.Should().BeGreaterThanOrEqualTo(2);
        result.Intermediate.Should().BeNull();
    }

    // ----- pictures ---------------------------------------------------------------------------------

    [Fact]
    public void A_documents_pictures_reach_the_pdf_without_being_left_behind()
    {
        //Arrange - a document whose picture the render has to find somewhere.
        File.WriteAllBytes(In("logo.png"), TestPng.Build(40, 20));
        string sourcePath = WriteSource("illustrated.texi",
            "@settitle Illustrated\n@node Top\n@top Illustrated\n"
            + "@node Picture\n@chapter Picture\n@image{logo}\n");
        string outputDirectory = Path.Combine(_directory, "out");

        //Act
        TexinfoPdfResult result = new TexinfoPdfRenderer()
            .RenderFile(sourcePath, Path.Combine(outputDirectory, "illustrated.pdf"));

        //Assert - the picture was found and used...
        result.Intermediate.Images.Count.Should().Be(1);
        result.Warnings.Count.Should().Be(0);
        //...and the caller who asked for one PDF got one PDF, with the staging swept up after it.
        Directory.GetFileSystemEntries(outputDirectory).Should()
            .BeEquivalentTo(new[] { Path.Combine(outputDirectory, "illustrated.pdf") });
    }

    // ----- options ----------------------------------------------------------------------------------

    [Fact]
    public void The_defaults_are_the_ones_a_printed_manual_wants()
    {
        //Arrange + Act
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();

        //Assert
        renderer.Options.Html.HeaderText.Should().Be("{title}");
        renderer.Options.Html.FooterText.Should().Be("{page} / {pages}");
        renderer.Options.Texinfo.ConditionalProfile.Should().Be(TexinfoConditionalProfile.Print);
    }

    [Fact]
    public void Texinfo_options_reach_the_reading_stage()
    {
        //Arrange
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        renderer.Options.Texinfo.PredefinedValues["edition"] = "Third";

        //Act
        TexinfoHtmlResult html = renderer.GenerateHtml(
            "@settitle Versioned\n@node Top\n@top Versioned\n"
            + "@node Body\n@chapter Body\nThe @value{edition} edition.\n");

        //Assert
        html.BodyHtml.Should().Contain("The Third edition.");
        html.Warnings.Count.Should().Be(0);
    }

    [Fact]
    public void Pdf_options_reach_the_writing_stage_and_are_left_as_the_caller_set_them()
    {
        //Arrange - a title the caller set explicitly must win over the document's own @settitle.
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        renderer.Options.Html.DocumentTitle = "Set By The Caller";

        //Act
        TexinfoPdfResult result = renderer.RenderTexinfo(Source, In("titled.pdf"));

        //Assert
        result.Title.Should().Be("Set By The Caller");
        renderer.Options.Html.DocumentTitle.Should().Be("Set By The Caller");
    }

    [Fact]
    public void The_documents_own_title_and_author_fill_the_metadata_that_was_left_empty()
    {
        //Arrange
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();

        //Act
        TexinfoPdfResult result = renderer.RenderTexinfo(Source, In("metadata.pdf"));

        //Assert - the document supplied both...
        result.Title.Should().Be("Pocket Manual");
        result.Intermediate.Author.Should().Be("A. Writer");
        //...and neither was left behind on the caller's options afterwards, which is what keeps a
        //reused renderer from carrying one document's metadata to the next.
        renderer.Options.Html.DocumentTitle.Should().BeNullOrEmpty();
        renderer.Options.Html.DocumentAuthor.Should().BeNullOrEmpty();
    }

    [Fact]
    public void One_renderer_does_not_carry_a_documents_metadata_to_the_next_one()
    {
        //Arrange - the reason the fill-in above has to be undone after every render.
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        renderer.RenderTexinfo(Source, In("first.pdf"));

        //Act - a second document that names no title of its own.
        TexinfoPdfResult second = renderer.RenderTexinfo(
            "@node Top\n@top Untitled\n@node Body\n@chapter Body\nNothing named it.\n",
            In("second.pdf"));

        //Assert
        second.Title.Should().NotBe("Pocket Manual");
    }

    // ----- warnings ---------------------------------------------------------------------------------

    [Fact]
    public void Warnings_from_both_stages_are_merged_and_tagged_with_the_stage_that_said_them()
    {
        //Arrange - a @tex block the Texinfo stage must skip, and a character no package font
        //carries, which is what the PDF stage reports.
        const string source = "@settitle Two Complaints\n@node Top\n@top Two Complaints\n"
            + "@node Body\n@chapter Body\n"
            + "@tex\n\\special{something for TeX}\n@end tex\n"
            + "A character outside the fonts: 日.\n";

        //Act
        TexinfoPdfResult result = new TexinfoPdfRenderer().RenderTexinfo(source, In("both.pdf"));

        //Assert - each stage reported, each list holds its own untagged, and the merged list is
        //everything with a tag in front of it.
        result.Warnings.TexinfoMessages.Should().ContainSingle(m => m.StartsWith("RawBlockSkipped"));
        result.Warnings.PdfMessages.Should().ContainSingle(m => m.Contains("U+65E5"));
        result.Warnings.Count.Should()
            .Be(result.Warnings.TexinfoMessages.Count + result.Warnings.PdfMessages.Count);
        result.Warnings.Messages.Count(m => m.StartsWith(TexinfoPdfWarnings.TexinfoStageTag))
            .Should().Be(result.Warnings.TexinfoMessages.Count);
        result.Warnings.Messages.Count(m => m.StartsWith(TexinfoPdfWarnings.PdfStageTag))
            .Should().Be(result.Warnings.PdfMessages.Count);
        //The Texinfo stage ran first, so it is reported first.
        result.Warnings.Messages[0].Should().StartWith(TexinfoPdfWarnings.TexinfoStageTag);
        result.Warnings.ToString().Should().Contain(TexinfoPdfWarnings.PdfStageTag);
    }

    [Fact]
    public void A_clean_document_reports_nothing_from_either_stage()
        => new TexinfoPdfRenderer().RenderTexinfo(Source, In("clean.pdf"))
            .Warnings.Count.Should().Be(0);

    // ----- the caller's own mistakes ----------------------------------------------------------------

    [Fact]
    public void RenderFile_rejects_a_blank_path()
        => Assert.Throws<ArgumentException>(() => new TexinfoPdfRenderer().RenderFile("  "));

    [Fact]
    public void RenderFile_reports_a_source_file_that_is_not_there()
        => Assert.Throws<FileNotFoundException>(
            () => new TexinfoPdfRenderer().RenderFile(In("no-such-manual.texi")));

    [Fact]
    public void RenderTexinfo_rejects_a_null_source()
        => Assert.Throws<ArgumentNullException>(
            () => new TexinfoPdfRenderer().RenderTexinfo(null, In("nothing.pdf")));

    [Fact]
    public void RenderTexinfo_rejects_a_blank_output_path()
        => Assert.Throws<ArgumentException>(
            () => new TexinfoPdfRenderer().RenderTexinfo(Source, ""));

    [Fact]
    public void RenderHtml_rejects_a_null_result()
        => Assert.Throws<ArgumentNullException>(
            () => new TexinfoPdfRenderer().RenderHtml(null, In("nothing.pdf")));

    [Fact]
    public void RenderHtmlFile_reports_a_markup_file_that_is_not_there()
        => Assert.Throws<FileNotFoundException>(() =>
            new TexinfoPdfRenderer().RenderHtmlFile(In("no-such-page.html"), In("nothing.pdf")));
}
