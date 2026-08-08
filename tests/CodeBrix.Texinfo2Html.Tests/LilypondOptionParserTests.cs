using System.Linq;
using CodeBrix.Texinfo2Html;
using CodeBrix.Texinfo2Html.Snippets;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// Tests for the lilypond-book option list. The vocabulary covered is the one measured across the
/// English LilyPond documentation, so these cases are written from what real documents contain
/// rather than from what the syntax would allow.
/// </summary>
public class LilypondOptionParserTests
{
    [Fact]
    public void Parse_reads_the_flags_a_snippet_is_usually_given()
    {
        //Arrange + Act
        LilypondSnippetOptions options = LilypondOptionParser.Parse("[verbatim,quote]");

        //Assert
        options.Verbatim.Should().BeTrue();
        options.Quote.Should().BeTrue();
        options.Inline.Should().BeFalse();
        options.Unrecognized.Count.Should().Be(0);
    }

    [Fact]
    public void Parse_ignores_the_spaces_a_document_may_leave_between_options()
    {
        //Arrange + Act
        LilypondSnippetOptions options = LilypondOptionParser.Parse("[verbatim, ragged-right, quote]");

        //Assert
        options.Verbatim.Should().BeTrue();
        options.RaggedRight.Should().Be(true);
        options.Quote.Should().BeTrue();
    }

    [Fact]
    public void Parse_reads_ragged_right_as_a_setting_that_can_be_turned_off()
    {
        //Arrange + Act
        LilypondSnippetOptions on = LilypondOptionParser.Parse("[ragged-right]");
        LilypondSnippetOptions off = LilypondOptionParser.Parse("[noragged-right]");
        LilypondSnippetOptions unsaid = LilypondOptionParser.Parse("[quote]");

        //Assert - the third state matters: it is what leaves the engraver's own default alone.
        on.RaggedRight.Should().Be(true);
        off.RaggedRight.Should().Be(false);
        unsaid.RaggedRight.Should().BeNull();
    }

    [Fact]
    public void Parse_reads_fragment_as_a_setting_that_can_be_turned_off()
    {
        //Arrange + Act
        LilypondSnippetOptions on = LilypondOptionParser.Parse("[fragment]");
        LilypondSnippetOptions off = LilypondOptionParser.Parse("[nofragment]");

        //Assert
        on.Fragment.Should().Be(true);
        off.Fragment.Should().Be(false);
    }

    [Theory]
    [InlineData("[relative=1]", 1)]
    [InlineData("[relative=2]", 2)]
    //A bare 'relative' is relative=1, which is what lilypond-book takes it for.
    [InlineData("[relative]", 1)]
    public void Parse_reads_the_octave_a_fragment_is_written_relative_to(string raw, int expected)
        => LilypondOptionParser.Parse(raw).Relative.Should().Be(expected);

    [Fact]
    public void Parse_reads_a_fractional_staff_size()
    {
        //Arrange + Act
        LilypondSnippetOptions options = LilypondOptionParser.Parse("[quote,staffsize=19.5]");

        //Assert
        options.StaffSize.Should().Be(19.5);
        options.Quote.Should().BeTrue();
    }

    [Fact]
    public void Parse_keeps_a_dimension_exactly_as_the_document_wrote_it()
    {
        //Arrange + Act
        LilypondSnippetOptions options =
            LilypondOptionParser.Parse("[inline,line-width=3\\cm,indent=0\\cm]");

        //Assert - these are LilyPond's own units and mean nothing outside an engraver, so
        //converting them here could only lose information.
        options.LineWidth.Should().Be("3\\cm");
        options.Indent.Should().Be("0\\cm");
        options.Inline.Should().BeTrue();
    }

    [Fact]
    public void Parse_reads_the_paper_options()
    {
        //Arrange + Act
        LilypondSnippetOptions options =
            LilypondOptionParser.Parse("[papersize=a8landscape,paper-width=5\\cm,paper-height=2\\cm]");

        //Assert
        options.PaperSize.Should().Be("a8landscape");
        options.PaperWidth.Should().Be("5\\cm");
        options.PaperHeight.Should().Be("2\\cm");
    }

    [Fact]
    public void Parse_reads_the_options_that_only_a_renderer_can_act_on()
    {
        //Arrange + Act
        LilypondSnippetOptions options =
            LilypondOptionParser.Parse("[verbatim,quote,texidoc,doctitle,notime,noindent]");

        //Assert
        options.TexiDoc.Should().BeTrue();
        options.DocTitle.Should().BeTrue();
        options.NoTime.Should().BeTrue();
        options.NoIndent.Should().BeTrue();
        options.Unrecognized.Count.Should().Be(0);
    }

    [Fact]
    public void Parse_keeps_an_option_it_has_no_name_for_and_lists_it()
    {
        //Arrange + Act - the usage manual documents the syntax with placeholders like these.
        LilypondSnippetOptions options = LilypondOptionParser.Parse("[quote,@var{options}]");

        //Assert - an option this library cannot name still reaches a renderer that can.
        options.Quote.Should().BeTrue();
        options.Unrecognized.Should().BeEquivalentTo(new[] { "@var{options}" });
        options.All.Should().BeEquivalentTo(new[] { "quote", "@var{options}" });
    }

    [Fact]
    public void Parse_lists_a_known_option_whose_value_makes_no_sense()
    {
        //Arrange + Act
        LilypondSnippetOptions options = LilypondOptionParser.Parse("[relative=high,staffsize=big]");

        //Assert - reporting it is more use than recording a value that was never given.
        options.Relative.Should().BeNull();
        options.StaffSize.Should().BeNull();
        options.Unrecognized.Count.Should().Be(2);
    }

    [Fact]
    public void Parse_keeps_the_options_in_the_order_they_were_written()
    {
        //Arrange + Act
        LilypondSnippetOptions options =
            LilypondOptionParser.Parse("[inline,line-width=3\\cm,notime,ragged-right,relative=1]");

        //Assert
        options.All.ToArray().Should().BeEquivalentTo(
            new[] { "inline", "line-width=3\\cm", "notime", "ragged-right", "relative=1" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("   ")]
    public void Parse_reads_an_absent_or_empty_list_as_no_options(string raw)
        => LilypondOptionParser.Parse(raw).All.Count.Should().Be(0);

    [Fact]
    public void Parse_ignores_whatever_followed_the_closing_bracket()
    {
        //Arrange + Act - the lexer keeps the rest of the opening line, which is not an option list.
        LilypondSnippetOptions options = LilypondOptionParser.Parse("[quote]  stray text");

        //Assert
        options.Quote.Should().BeTrue();
        options.All.Count.Should().Be(1);
    }
}
