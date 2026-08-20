using System;
using System.IO;
using System.Linq;
using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Pdf.Tests;

/// <summary>
/// The PDF-stage capabilities a consumer reaches through this package alone: SVG and
/// every bitmap format as @image pictures, music glyphs rendered through the Noto
/// Music fallback family, the live Options.Html pass-through, and font registration
/// via <see cref="TexinfoPdfFonts"/> - all without naming Html2Pdf anywhere.
/// </summary>
public class PdfFeaturePassThroughTests
{
    private const string RedSquareSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'>"
        + "<rect width='32' height='32' fill='#cc0000'/></svg>";

    private static string Manual(string body) =>
        "@settitle Pass Through\n@node Top\n@top Pass Through\n"
        + "@node One\n@chapter One\n" + body + "\n";

    [Fact]
    public void An_svg_image_referenced_by_a_manual_travels_into_the_pdf()
    {
        //Arrange - @image names the file without an extension; .svg must be probed,
        //staged beside the document, and rasterized by the PDF stage.
        string directory = Directory.CreateTempSubdirectory("texinfo-svg-image-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "note.svg"), RedSquareSvg);
            string sourcePath = Path.Combine(directory, "manual.texi");
            File.WriteAllText(sourcePath, Manual("A picture:\n\n@image{note}\n"));

            //Act
            TexinfoPdfResult pdf = new TexinfoPdfRenderer().RenderFile(sourcePath,
                Path.Combine(directory, "manual.pdf"));

            //Assert
            pdf.Intermediate.Images.Count.Should().Be(1);
            pdf.Intermediate.Images[0].RelativePath.EndsWith(".svg", StringComparison.Ordinal)
                .Should().BeTrue();
            pdf.Warnings.PdfMessages.Count.Should().Be(0);
            new FileInfo(pdf.OutputFilePath).Length.Should().BeGreaterThan(1_000);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_webp_image_referenced_by_a_manual_travels_into_the_pdf()
    {
        //Arrange - one of the bitmap formats the extension probe gained. The picture is
        //built at test time by the PDF stage's own imaging library, so the repository
        //carries no binary fixture and the claim tested is that the stage decodes it.
        byte[] webpBytes;
        using (Image<Rgba32> image = new Image<Rgba32>(8, 8))
        {
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    image[x, y] = new Rgba32(40, 90, 200);
                }
            }
            using MemoryStream stream = new MemoryStream();
            image.SaveAsWebp(stream);
            webpBytes = stream.ToArray();
        }
        string directory = Directory.CreateTempSubdirectory("texinfo-webp-image-").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "dot.webp"), webpBytes);
            string sourcePath = Path.Combine(directory, "manual.texi");
            File.WriteAllText(sourcePath, Manual("A picture:\n\n@image{dot}\n"));

            //Act
            TexinfoPdfResult pdf = new TexinfoPdfRenderer().RenderFile(sourcePath,
                Path.Combine(directory, "manual.pdf"));

            //Assert
            pdf.Intermediate.Images.Count.Should().Be(1);
            pdf.Warnings.PdfMessages.Count.Should().Be(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Music_glyphs_in_prose_render_without_being_dropped()
    {
        //Arrange - the accidental signs and a supplementary-plane music symbol; the
        //Noto Music fallback family the PDF stage brings along must render them all,
        //so no font warning may fire.
        string body = "The chord of B♭ major, the key of F♯ minor, "
            + "and a double sharp: \U0001D12A.";

        //Act
        TexinfoPdfResult pdf = new TexinfoPdfRenderer().RenderTexinfoToBytes(Manual(body));

        //Assert
        pdf.PdfBytes.Should().NotBeNull();
        pdf.Warnings.PdfMessages.Count.Should().Be(0);
    }

    [Fact]
    public void The_new_html_stage_options_are_reachable_through_the_live_pass_through()
    {
        //Arrange - Options.Html is the live Html2Pdf options object, so settings the
        //PDF stage gains are reachable here without a second package reference.
        TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
        renderer.Options.Html.SvgRasterScale = 3.0;
        renderer.Options.Html.KeepUncoveredCharacters = true;

        //Act
        TexinfoPdfResult pdf = renderer.RenderTexinfoToBytes(Manual("Prose with א kept as tofu."));

        //Assert - with the opt-in set, the uncovered character is kept (and reported
        //as kept) rather than removed; the structured item says so by code.
        pdf.PdfBytes.Should().NotBeNull();
        pdf.Warnings.PdfItems.Any(i => i.Code == "font.uncovered.kept" && i.CodePoint == 0x05D0)
            .Should().BeTrue();
        pdf.Warnings.PdfItems.Any(i => i.Code == "font.uncovered.removed").Should().BeFalse();
    }

    [Fact]
    public void Structured_pdf_warnings_carry_code_code_point_and_occurrences()
    {
        //Arrange - a drop baseline must be assertable as distinct code points AND
        //occurrence counts, which display prose cannot carry: the same shin twice and
        //one alef, in a script no registered font covers.
        string body = "Uncovered: ש then א then ש again.";

        //Act
        TexinfoPdfResult pdf = new TexinfoPdfRenderer().RenderTexinfoToBytes(Manual(body));

        //Assert
        var drops = pdf.Warnings.PdfItems
            .Where(i => i.Code == "font.uncovered.removed")
            .ToList();
        drops.Single(i => i.CodePoint == 0x05E9).Occurrences.Should().Be(2);
        drops.Single(i => i.CodePoint == 0x05D0).Occurrences.Should().Be(1);
    }

    [Fact]
    public void Font_registration_forwards_through_TexinfoPdfFonts()
    {
        //Arrange - a loose copy of a package font file, under an unrelated name, in a
        //directory the registry has never seen; either separator style must work.
        string robotoPath = Path.Combine(AppContext.BaseDirectory,
            "CodeBrix.Platform.Fonts.Roboto", "Fonts", "Roboto-Regular.ttf");
        Assert.SkipWhen(!File.Exists(robotoPath), "The package fonts are not beside the tests.");
        string directory = Directory.CreateTempSubdirectory("texinfo-fonts-").FullName;
        try
        {
            string loosePath = Path.Combine(directory, "some-loose-font.ttf");
            File.Copy(robotoPath, loosePath);

            //Act + Assert - all registration calls forward without throwing, before or
            //after renders have happened in this process.
            Record.Exception(() => TexinfoPdfFonts.AddFontFile(loosePath)).Should().BeNull();
            Record.Exception(() => TexinfoPdfFonts.AddFontFiles(new[] { loosePath })).Should().BeNull();
            Record.Exception(() => TexinfoPdfFonts.AddFontFilesFromDirectory(directory)).Should().BeNull();
            Record.Exception(() => TexinfoPdfFonts.AddFontDirectory(directory)).Should().BeNull();
            Record.Exception(() => TexinfoPdfFonts.AddFallbackFamily("Merriweather")).Should().BeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
