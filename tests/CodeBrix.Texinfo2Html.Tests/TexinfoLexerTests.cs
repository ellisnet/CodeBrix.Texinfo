using System.Collections.Generic;
using System.Linq;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Lexing;
using CodeBrix.Texinfo2Html.Sources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

public class TexinfoLexerTests
{
    private static List<TexinfoToken> Lex(string source, TexinfoWarningCollection warnings = null)
    {
        TexinfoLexer lexer = new TexinfoLexer(
            TexinfoSourceText.FromString("test.texi", source),
            warnings ?? new TexinfoWarningCollection());
        return lexer.Lex();
    }

    private static string Roundtrip(string source)
        => string.Concat(Lex(source).Select(t => t.ToSourceText()));

    [Fact]
    public void Lex_plain_text_becomes_single_text_token()
    {
        //Arrange
        string source = "plain words";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        tokens.Count.Should().Be(2);
        tokens[0].Kind.Should().Be(TexinfoTokenKind.Text);
        tokens[0].Value.Should().Be("plain words");
        tokens[1].Kind.Should().Be(TexinfoTokenKind.EndOfInput);
    }

    [Fact]
    public void Lex_brace_command_produces_command_and_brace_tokens()
    {
        //Arrange
        string source = "@code{x}";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        tokens[0].Kind.Should().Be(TexinfoTokenKind.Command);
        tokens[0].Value.Should().Be("code");
        tokens[1].Kind.Should().Be(TexinfoTokenKind.OpenBrace);
        tokens[2].Kind.Should().Be(TexinfoTokenKind.Text);
        tokens[2].Value.Should().Be("x");
        tokens[3].Kind.Should().Be(TexinfoTokenKind.CloseBrace);
    }

    [Fact]
    public void Lex_single_character_commands_are_recognized()
    {
        //Arrange
        string source = "a@@b@{c@}d";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        tokens.Count(t => t.Kind == TexinfoTokenKind.Command).Should().Be(3);
        tokens.First(t => t.Kind == TexinfoTokenKind.Command).Value.Should().Be("@");
        tokens.Count(t => t.Kind == TexinfoTokenKind.OpenBrace).Should().Be(0);
    }

    [Fact]
    public void Lex_end_command_captures_block_name()
    {
        //Arrange
        string source = "@end quotation\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        tokens[0].Kind.Should().Be(TexinfoTokenKind.EndCommand);
        tokens[0].Value.Should().Be("quotation");
    }

    [Fact]
    public void Lex_whole_line_comment_swallows_its_newline()
    {
        //Arrange
        string source = "before\n@c a comment\nafter\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken comment = tokens.Single(t => t.Kind == TexinfoTokenKind.Comment);
        comment.IsWholeLineComment.Should().BeTrue();
        tokens.Count(t => t.Kind == TexinfoTokenKind.Newline).Should().Be(2);
    }

    [Fact]
    public void Lex_trailing_comment_keeps_the_line_terminator()
    {
        //Arrange
        string source = "text @c trailing\nmore\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken comment = tokens.Single(t => t.Kind == TexinfoTokenKind.Comment);
        comment.IsWholeLineComment.Should().BeFalse();
        tokens.Count(t => t.Kind == TexinfoTokenKind.Newline).Should().Be(2);
    }

    [Fact]
    public void Lex_code_command_is_not_mistaken_for_comment()
    {
        //Arrange
        string source = "@code{x}\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        tokens.Count(t => t.Kind == TexinfoTokenKind.Comment).Should().Be(0);
    }

    [Fact]
    public void Lex_verbatim_block_is_captured_raw()
    {
        //Arrange
        string source = "@verbatim\n@code{not a command}\n@end verbatim\nafter\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken raw = tokens.Single(t => t.Kind == TexinfoTokenKind.RawBlock);
        raw.Value.Should().Be("verbatim");
        raw.RawContent.Should().Be("@code{not a command}\n");
    }

    [Fact]
    public void Lex_macro_block_captures_definition_line_and_body()
    {
        //Arrange
        string source = "@macro q{TEXT}\n@quoteleft{}\\TEXT\\@quoteright{}\n@end macro\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken raw = tokens.Single(t => t.Kind == TexinfoTokenKind.RawBlock);
        raw.Value.Should().Be("macro");
        raw.RawArgument.Should().Be(" q{TEXT}");
        raw.RawContent.Should().Be("@quoteleft{}\\TEXT\\@quoteright{}\n");
    }

    [Fact]
    public void Lex_nested_macro_definitions_are_captured_as_one_block()
    {
        //Arrange
        string source = "@macro outer\n@macro inner\nx\n@end macro\n@end macro\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken raw = tokens.Single(t => t.Kind == TexinfoTokenKind.RawBlock);
        raw.RawContent.Should().Be("@macro inner\nx\n@end macro\n");
    }

    [Fact]
    public void Lex_unterminated_raw_block_warns_and_captures_rest()
    {
        //Arrange
        TexinfoWarningCollection warnings = new TexinfoWarningCollection();
        string source = "@verbatim\nno end\n";

        //Act
        List<TexinfoToken> tokens = Lex(source, warnings);

        //Assert
        tokens.Single(t => t.Kind == TexinfoTokenKind.RawBlock).RawContent.Should().Be("no end\n");
        warnings.Count.Should().Be(1);
        warnings[0].Category.Should().Be(TexinfoWarningCategory.Syntax);
    }

    [Fact]
    public void Lex_lilypond_block_form_is_captured_to_end_lilypond()
    {
        //Arrange
        string source = "@lilypond[quote,verbatim]\nc'4 d'4\n@end lilypond\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken raw = tokens.Single(t => t.Kind == TexinfoTokenKind.RawBlock);
        raw.Value.Should().Be("lilypond");
        raw.RawArgument.Should().Be("[quote,verbatim]");
        raw.RawContent.Should().Be("c'4 d'4\n");
        raw.IsBraceRawBlock.Should().BeFalse();
    }

    [Fact]
    public void Lex_lilypond_brace_form_is_captured_to_matching_brace()
    {
        //Arrange
        string source = "@lilypond[quote,fragment,staffsize=11]{<c' e' g'>} tail\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken raw = tokens.Single(t => t.Kind == TexinfoTokenKind.RawBlock);
        raw.IsBraceRawBlock.Should().BeTrue();
        raw.RawArgument.Should().Be("[quote,fragment,staffsize=11]");
        raw.RawContent.Should().Be("<c' e' g'>");
        tokens.Count(t => t.Kind == TexinfoTokenKind.Text && t.Value == " tail").Should().Be(1);
    }

    [Fact]
    public void Lex_lilypond_brace_form_counts_nested_braces()
    {
        //Arrange
        string source = "@lilypond[inline]{\\relative { c'4 { d'4 } }}\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        tokens.Single(t => t.Kind == TexinfoTokenKind.RawBlock)
            .RawContent.Should().Be("\\relative { c'4 { d'4 } }");
    }

    [Fact]
    public void Lex_lilypondfile_brace_form_is_captured()
    {
        //Arrange
        string source = "@lilypondfile[quote]{music.ly}\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken raw = tokens.Single(t => t.Kind == TexinfoTokenKind.RawBlock);
        raw.Value.Should().Be("lilypondfile");
        raw.RawContent.Should().Be("music.ly");
    }

    [Fact]
    public void Lex_at_line_start_is_tracked_through_leading_whitespace()
    {
        //Arrange
        string source = "  @item one\nx @item two\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        List<TexinfoToken> items = tokens.Where(t => t.Kind == TexinfoTokenKind.Command && t.Value == "item").ToList();
        items[0].AtLineStart.Should().BeTrue();
        items[1].AtLineStart.Should().BeFalse();
    }

    [Fact]
    public void Lex_positions_report_one_based_line_and_column()
    {
        //Arrange
        string source = "one\n@node Two\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        TexinfoToken node = tokens.Single(t => t.Kind == TexinfoTokenKind.Command && t.Value == "node");
        node.Position.Line.Should().Be(2);
        node.Position.Column.Should().Be(1);
    }

    [Fact]
    public void Lex_token_stream_roundtrips_to_original_source()
    {
        //Arrange
        string source = "@node Top\n@top Title\n\nText with @code{stuff} and @@at.\n"
            + "@verbatim\nraw { } @content\n@end verbatim\n"
            + "@macro m{A}\nbody \\A\\\n@end macro\n"
            + "@c whole line comment\n"
            + "tail @c trailing comment\n"
            + "@lilypond[quote]{c'4}\ndone\n";

        //Act
        string rebuilt = Roundtrip(source);

        //Assert
        rebuilt.Should().Be(source);
    }

    [Fact]
    public void Lex_crlf_input_is_normalized_to_newlines()
    {
        //Arrange
        string source = "a\r\nb\r\n";

        //Act
        List<TexinfoToken> tokens = Lex(source);

        //Assert
        tokens.Count(t => t.Kind == TexinfoTokenKind.Newline).Should().Be(2);
        tokens.Count(t => t.Kind == TexinfoTokenKind.Text && t.Value.Contains('\r')).Should().Be(0);
    }
}
