using System.IO;
using System.Linq;
using CodeBrix.PdfDocCreate.Html2Pdf;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Pdf.Tests;

/// <summary>
/// The end-to-end gate for the music-snippet seam: a <c>.tely</c> document whose snippets are
/// engraved by a registered renderer must come out of Html2Pdf as a PDF with the pictures in it.
/// Nothing inside CodeBrix.Texinfo2Html can show this, because the question is whether the markup
/// and the picture files it points at are ones Html2Pdf can actually use.
/// </summary>
/// <remarks>
/// The pictures are built here rather than committed, so the repository stays free of binary
/// fixtures: <see cref="TestPng"/> writes a real, decodable PNG from first principles.
/// </remarks>
public class SnippetToPdfGateTests
{
    /// <summary>An engraver that answers every snippet with the same small picture.</summary>
    private sealed class ConstantRenderer : ILilypondSnippetRenderer
    {
        private readonly byte[] _picture;

        public ConstantRenderer(byte[] picture)
        {
            _picture = picture;
        }

        public int Calls { get; private set; }

        public LilypondSnippetResult Render(LilypondSnippet snippet)
        {
            Calls++;
            return LilypondSnippetResult.FromContent(_picture, "png");
        }
    }

    private const string Source = "@settitle Snippet Gate\n"
        + "@node Top\n@top Snippet Gate\n"
        + "@node Tunes\n@chapter Tunes\n"
        + "A tune, engraved and shown:\n\n"
        + "@lilypond[verbatim,quote]\n\\relative c' { c4 d4 e4 f4 }\n@end lilypond\n\n"
        + "The same tune again, engraved only:\n\n"
        + "@lilypond[quote,ragged-right]\n\\relative c' { g4 a4 b4 c4 }\n@end lilypond\n\n"
        + "And one written into the sentence: @lilypond[inline,staffsize=14]{c4} like that.\n";

    [Fact]
    public void An_engraved_snippet_travels_all_the_way_into_a_pdf()
    {
        //Arrange
        ConstantRenderer renderer = new ConstantRenderer(TestPng.Build(24, 12));
        TexinfoHtmlRenderer texinfo = new TexinfoHtmlRenderer();
        texinfo.Options.SnippetRenderer = renderer;
        string directory = Directory.CreateTempSubdirectory("texinfo-snippet-pdf-").FullName;

        try
        {
            //Act
            TexinfoHtmlResult result = texinfo.Generate(Source);
            string htmlPath = result.WriteToDirectory(directory, "tunes");
            HtmlRenderResult pdf = new HtmlPdfRenderer().RenderFile(htmlPath,
                Path.Combine(directory, "tunes.pdf"));

            //Assert - three distinct snippets, each engraved once and each written out as a file
            //Html2Pdf could find and decode.
            renderer.Calls.Should().Be(3);
            result.Images.Count.Should().Be(3);
            foreach (TexinfoImageReference image in result.Images)
            {
                File.Exists(Path.Combine(directory,
                    image.RelativePath.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
            }
            pdf.Title.Should().Be("Snippet Gate");
            pdf.PageCount.Should().BeGreaterThanOrEqualTo(1);
            //A picture Html2Pdf could not decode would be reported here, which is the whole point.
            pdf.Warnings.Count.Should().Be(0);
            new FileInfo(pdf.OutputFilePath).Length.Should().BeGreaterThan(1_000);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_document_with_no_engraver_still_renders_its_snippets_as_source()
    {
        //Arrange
        string directory = Directory.CreateTempSubdirectory("texinfo-snippet-source-pdf-").FullName;

        try
        {
            //Act - the default behaviour, which is what every corpus manual is rendered under.
            TexinfoHtmlResult result = new TexinfoHtmlRenderer().Generate(Source);
            string htmlPath = result.WriteToDirectory(directory, "tunes");
            HtmlRenderResult pdf = new HtmlPdfRenderer().RenderFile(htmlPath,
                Path.Combine(directory, "tunes.pdf"));

            //Assert
            result.Images.Count.Should().Be(0);
            result.Warnings.Messages.Count(m => m.Contains("music snippet")).Should().Be(1);
            pdf.PageCount.Should().BeGreaterThanOrEqualTo(1);
            pdf.Warnings.Count.Should().Be(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
