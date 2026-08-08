using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// Tests for the music-snippet seam: what a <c>.tely</c> document does with its <c>@lilypond</c>,
/// <c>@lilypondfile</c> and <c>@musicxmlfile</c> environments, with and without an engraver
/// registered. Every fixture is original Texinfo and original LilyPond written for the test.
/// </summary>
public class LilypondSnippetTests
{
    /// <summary>An engraver that records what it was asked for and answers however the test says.</summary>
    private sealed class FakeRenderer : ILilypondSnippetRenderer
    {
        private readonly Func<LilypondSnippet, LilypondSnippetResult> _answer;

        public FakeRenderer(Func<LilypondSnippet, LilypondSnippetResult> answer)
        {
            _answer = answer;
        }

        public List<LilypondSnippet> Seen { get; } = new List<LilypondSnippet>();

        public LilypondSnippetResult Render(LilypondSnippet snippet)
        {
            Seen.Add(snippet);
            return _answer(snippet);
        }
    }

    private static readonly byte[] PictureBytes = Encoding.ASCII.GetBytes("not really a picture");

    private static LilypondSnippetResult OnePicture(LilypondSnippet snippet)
        => LilypondSnippetResult.FromContent(PictureBytes, "png");

    private static TexinfoHtmlResult Render(string source, ILilypondSnippetRenderer renderer)
    {
        TexinfoHtmlRenderer texinfo = new TexinfoHtmlRenderer();
        texinfo.Options.SnippetRenderer = renderer;
        return texinfo.Generate(source);
    }

    // ----- with no engraver registered --------------------------------------------------------

    [Fact]
    public void A_snippet_shows_its_source_when_no_renderer_is_registered()
    {
        //Arrange + Act
        TexinfoHtmlResult result = new TexinfoHtmlRenderer()
            .Generate("@lilypond[verbatim]\nc4 d4 e4\n@end lilypond\n");

        //Assert
        result.BodyHtml.Contains("<pre class=\"texinfo-lilypond\"").Should().BeTrue();
        result.BodyHtml.Contains("c4 d4 e4").Should().BeTrue();
        result.BodyHtml.Contains("<img").Should().BeFalse();
        result.Warnings.Messages.Any(m => m.Contains("no snippet renderer is registered"))
            .Should().BeTrue();
    }

    [Fact]
    public void A_snippet_inside_a_paragraph_stays_inside_it()
    {
        //Arrange + Act
        string body = new TexinfoHtmlRenderer()
            .Generate("Play @lilypond[inline]{c4} and stop.\n").BodyHtml;

        //Assert - the brace form is written mid-sentence, so it may not break the paragraph.
        body.Contains("<code class=\"texinfo-lilypond\">c4</code>").Should().BeTrue();
        body.Split("<p>").Length.Should().Be(2);
    }

    // ----- with an engraver registered --------------------------------------------------------

    [Fact]
    public void A_registered_renderer_turns_a_snippet_into_a_picture()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(OnePicture);

        //Act
        TexinfoHtmlResult result = Render("@lilypond[quote]\nc4 d4\n@end lilypond\n", renderer);

        //Assert
        renderer.Seen.Count.Should().Be(1);
        result.BodyHtml.Contains("<img src=\"texinfo-images/snippet-0001.png\"").Should().BeTrue();
        result.BodyHtml.Contains("class=\"texinfo-lilypond-image\"").Should().BeTrue();
        //Nothing asked for the source, so a document that engraves its music does not repeat it.
        result.BodyHtml.Contains("<pre class=\"texinfo-lilypond\"").Should().BeFalse();
        result.Warnings.Messages.Any(m => m.Contains("music snippet")).Should().BeFalse();
    }

    [Fact]
    public void Verbatim_shows_the_source_above_the_picture_it_engraved_to()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(OnePicture);

        //Act
        string body = Render("@lilypond[verbatim,quote]\nc4 d4\n@end lilypond\n", renderer).BodyHtml;

        //Assert - a manual shows the input first and what it produces underneath.
        int source = body.IndexOf("<pre class=\"texinfo-lilypond\"", StringComparison.Ordinal);
        int picture = body.IndexOf("<img", StringComparison.Ordinal);
        source.Should().BeGreaterThan(-1);
        picture.Should().BeGreaterThan(source);
    }

    [Fact]
    public void Quote_indents_the_picture_as_well_as_the_source()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(OnePicture);

        //Act
        string body = Render("@lilypond[verbatim,quote]\nc4\n@end lilypond\n", renderer).BodyHtml;

        //Assert - an inline style rather than a container, because Html2Pdf lays a bordered
        //container out as one box.
        body.Split("style=\"margin-left: 2em\"").Length.Should().Be(3);
    }

    [Fact]
    public void A_score_that_engraves_to_several_pages_places_them_all()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.FromImages(new[]
        {
            LilypondSnippetImage.FromContent(PictureBytes, "png"),
            LilypondSnippetImage.FromContent(PictureBytes, ".png")
        }));

        //Act
        TexinfoHtmlResult result = Render("@lilypond\nc4\n@end lilypond\n", renderer);

        //Assert
        result.BodyHtml.Split("<img").Length.Should().Be(3);
        result.Images.Count.Should().Be(2);
        result.Images.Select(i => i.RelativePath).Should().BeEquivalentTo(
            new[] { "texinfo-images/snippet-0001.png", "texinfo-images/snippet-0002.png" });
    }

    [Fact]
    public void An_engraved_picture_is_written_out_with_the_document()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(OnePicture);
        TexinfoHtmlResult result = Render("@lilypond\nc4\n@end lilypond\n", renderer);
        string directory = Directory.CreateTempSubdirectory("texinfo-snippet-").FullName;

        //Act
        try
        {
            result.WriteToDirectory(directory, "manual");

            //Assert - a picture that was never a file still has to become one.
            string written = Path.Combine(directory, "texinfo-images", "snippet-0001.png");
            File.Exists(written).Should().BeTrue();
            File.ReadAllBytes(written).Should().BeEquivalentTo(PictureBytes);
            result.Images[0].HasContent.Should().BeTrue();
            result.Images[0].IsGenerated.Should().BeTrue();
            result.Images[0].SourcePath.Should().Be(string.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_picture_the_renderer_wrote_to_disk_keeps_its_own_name()
    {
        //Arrange
        string directory = Directory.CreateTempSubdirectory("texinfo-snippet-file-").FullName;
        try
        {
            string picture = Path.Combine(directory, "engraved-score.png");
            File.WriteAllBytes(picture, PictureBytes);
            FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.FromFile(picture));

            //Act
            TexinfoHtmlResult result = Render("@lilypond\nc4\n@end lilypond\n", renderer);

            //Assert - keeping the renderer's own name is what makes an engraving traceable.
            result.BodyHtml.Contains("src=\"texinfo-images/engraved-score.png\"").Should().BeTrue();
            result.Images[0].SourcePath.Should().Be(picture);
            result.Images[0].IsGenerated.Should().BeTrue();
            result.Images[0].HasContent.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ----- what the renderer is told ----------------------------------------------------------

    [Fact]
    public void The_renderer_is_given_the_options_the_document_wrote()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.NotRendered);

        //Act
        Render("@lilypond[quote,verbatim,ragged-right,relative=2,line-width=3\\cm,staffsize=26]\n"
            + "c4 d4\n@end lilypond\n", renderer);

        //Assert
        LilypondSnippet snippet = renderer.Seen.Single();
        snippet.Kind.Should().Be(LilypondSnippetKind.Music);
        snippet.Source.Trim().Should().Be("c4 d4");
        snippet.IsInline.Should().BeFalse();
        snippet.Options.Quote.Should().BeTrue();
        snippet.Options.RaggedRight.Should().Be(true);
        snippet.Options.Relative.Should().Be(2);
        snippet.Options.LineWidth.Should().Be("3\\cm");
        snippet.Options.StaffSize.Should().Be(26);
    }

    [Fact]
    public void The_renderer_is_told_where_a_snippet_was_written()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.NotRendered);

        //Act
        Render("@chapter One\n\nText.\n\n@lilypond\nc4\n@end lilypond\n", renderer);

        //Assert - a renderer that cannot engrave something has to be able to say which one.
        renderer.Seen.Single().LineNumber.Should().Be(5);
    }

    [Fact]
    public void The_brace_form_is_reported_as_sitting_inside_the_text()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.NotRendered);

        //Act
        Render("Play @lilypond[inline]{c4} now.\n", renderer);

        //Assert - where it sits and the option asking for a small engraving are two different
        //things, and a renderer needs both.
        LilypondSnippet snippet = renderer.Seen.Single();
        snippet.IsInline.Should().BeTrue();
        snippet.Options.Inline.Should().BeTrue();
    }

    // ----- the same snippet twice -------------------------------------------------------------

    [Fact]
    public void An_identical_snippet_is_engraved_once_and_used_twice()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(OnePicture);

        //Act - the notation reference holds over eighteen hundred snippets and repeats many.
        TexinfoHtmlResult result = Render(
            "@lilypond[quote]\nc4 d4\n@end lilypond\n@lilypond[quote]\nc4 d4\n@end lilypond\n",
            renderer);

        //Assert
        renderer.Seen.Count.Should().Be(1);
        result.Images.Count.Should().Be(1);
        result.BodyHtml.Split("<img").Length.Should().Be(3);
    }

    [Fact]
    public void The_same_music_under_different_options_is_engraved_twice()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(OnePicture);

        //Act
        Render("@lilypond[quote]\nc4\n@end lilypond\n@lilypond[quote,staffsize=26]\nc4\n@end lilypond\n",
            renderer);

        //Assert - options change what is engraved, so they are part of what identifies a snippet.
        renderer.Seen.Count.Should().Be(2);
    }

    // ----- when engraving goes wrong ----------------------------------------------------------

    [Fact]
    public void A_renderer_that_declines_leaves_the_source_and_says_nothing_more()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.NotRendered);

        //Act
        TexinfoHtmlResult result = Render("@lilypond\nc4\n@end lilypond\n", renderer);

        //Assert - declining is a decision, not a fault; only the fallback is worth recording.
        result.BodyHtml.Contains("<pre class=\"texinfo-lilypond\"").Should().BeTrue();
        result.Warnings.Messages.Any(m => m.Contains("engraved no picture")).Should().BeTrue();
        result.Warnings.Messages.Any(m => m.Contains("failed on")).Should().BeFalse();
    }

    [Fact]
    public void A_renderer_that_reports_failure_is_quoted_once_for_the_document()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.Failed("no engraver"));

        //Act
        TexinfoHtmlResult result = Render(
            "@lilypond\nc4\n@end lilypond\n@lilypond\nd4\n@end lilypond\n", renderer);

        //Assert
        string failure = result.Warnings.Messages.Single(m => m.Contains("failed on"));
        failure.Contains("2 music snippet").Should().BeTrue();
        failure.Contains("no engraver").Should().BeTrue();
        result.BodyHtml.Contains("<pre class=\"texinfo-lilypond\"").Should().BeTrue();
    }

    [Fact]
    public void A_renderer_that_throws_costs_the_document_nothing_but_a_warning()
    {
        //Arrange
        FakeRenderer renderer =
            new FakeRenderer(_ => throw new InvalidOperationException("engraver exploded"));

        //Act
        TexinfoHtmlResult result = Render("@chapter One\n@lilypond\nc4\n@end lilypond\nAfter.\n",
            renderer);

        //Assert - someone else's exception may not cost a reader the rest of the manual.
        result.BodyHtml.Contains("After.").Should().BeTrue();
        result.BodyHtml.Contains("c4").Should().BeTrue();
        result.Warnings.Messages.Any(m => m.Contains("engraver exploded")).Should().BeTrue();
        result.Warnings.Messages.Any(m => m.Contains("InvalidOperationException")).Should().BeTrue();
    }

    [Fact]
    public void An_option_nobody_recognizes_is_reported_once_and_passed_on()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.NotRendered);

        //Act
        TexinfoHtmlResult result = Render("@lilypond[quote,fortissimo]\nc4\n@end lilypond\n", renderer);

        //Assert
        result.Warnings.Messages.Any(m => m.Contains("not recognized") && m.Contains("fortissimo"))
            .Should().BeTrue();
        renderer.Seen.Single().Options.Unrecognized.Should().BeEquivalentTo(new[] { "fortissimo" });
    }

    // ----- files ------------------------------------------------------------------------------

    [Fact]
    public void A_snippet_that_names_a_file_hands_the_renderer_the_path_it_was_found_at()
    {
        //Arrange
        string directory = Directory.CreateTempSubdirectory("texinfo-lyfile-").FullName;
        try
        {
            string music = Path.Combine(directory, "tune.ly");
            File.WriteAllText(music, "{ c4 d4 e4 f4 }\n");
            FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.NotRendered);
            TexinfoHtmlRenderer texinfo = new TexinfoHtmlRenderer();
            texinfo.Options.SnippetRenderer = renderer;

            //Act
            texinfo.Generate("@lilypondfile[quote]{tune.ly}\n", directory);

            //Assert
            LilypondSnippet snippet = renderer.Seen.Single();
            snippet.Kind.Should().Be(LilypondSnippetKind.LilypondFile);
            snippet.FileName.Should().Be("tune.ly");
            snippet.FilePath.Should().Be(music);
            snippet.Source.Should().Be(string.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Verbatim_on_a_file_shows_what_is_in_the_file()
    {
        //Arrange
        string directory = Directory.CreateTempSubdirectory("texinfo-lyverbatim-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "tune.ly"), "{ c4 d4 e4 f4 }\n");

            //Act - this is the one case where the library reads a music file rather than naming it.
            string body = new TexinfoHtmlRenderer()
                .Generate("@lilypondfile[verbatim,quote]{tune.ly}\n", directory).BodyHtml;

            //Assert
            body.Contains("{ c4 d4 e4 f4 }").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Verbatim_on_a_file_starts_where_the_file_says_its_preamble_ends()
    {
        //Arrange - the shape every one of LilyPond's own snippet files is written in.
        string directory = Directory.CreateTempSubdirectory("texinfo-lymarker-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "tune.ly"),
                "%% DO NOT EDIT this file manually.\n"
                + "\\version \"2.24.0\"\n"
                + "\\header {\n  doctitle = \"A tune\"\n} % begin verbatim\n"
                + "\n{ c4 d4 e4 f4 }\n");

            //Act
            string body = new TexinfoHtmlRenderer()
                .Generate("@lilypondfile[verbatim,quote]{tune.ly}\n", directory).BodyHtml;

            //Assert - the reader is shown the music, not the bookkeeping above it.
            body.Contains("{ c4 d4 e4 f4 }").Should().BeTrue();
            body.Contains("DO NOT EDIT").Should().BeFalse();
            body.Contains("doctitle").Should().BeFalse();
            body.Contains("begin verbatim").Should().BeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_file_that_is_not_there_names_itself_and_is_reported_only_to_a_renderer()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.NotRendered);

        //Act
        TexinfoHtmlResult withRenderer = Render("@lilypondfile[quote]{missing.ly}\n", renderer);
        TexinfoHtmlResult without = new TexinfoHtmlRenderer()
            .Generate("@lilypondfile[quote]{missing.ly}\n");

        //Assert - a document with no engraver never needed the file, so saying it is missing would
        //be noise; one that has an engraver was about to read it.
        withRenderer.Warnings.Messages.Any(m => m.Contains("missing.ly")).Should().BeTrue();
        without.Warnings.Messages.Any(m => m.Contains("missing.ly")).Should().BeFalse();
        without.BodyHtml.Contains("@lilypondfile{missing.ly}").Should().BeTrue();
        renderer.Seen.Single().FilePath.Should().Be(string.Empty);
    }

    [Fact]
    public void A_musicxml_file_is_recognized_as_its_own_kind()
    {
        //Arrange
        FakeRenderer renderer = new FakeRenderer(_ => LilypondSnippetResult.NotRendered);

        //Act
        Render("@musicxmlfile[quote]{score.xml}\n", renderer);

        //Assert
        renderer.Seen.Single().Kind.Should().Be(LilypondSnippetKind.MusicXmlFile);
    }
}
