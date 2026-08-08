using System;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// The accent commands. Each one puts a combining mark on the character it applies to and the
/// result is composed, so what reaches the output is the single precomposed character wherever
/// Unicode has one - which is what the font packages carry a glyph for.
/// </summary>
public class AccentCommandTests
{
    /// <summary>
    /// The fixture: every accent Texinfo has, written in whichever form its own manual uses.
    /// </summary>
    private const string EveryAccent =
        "Punctuation: @'e @`a @^o @\"u @~n @,{c} @=a\n"
        + "Braced: @'{E} @\"{o} @~{N}\n"
        + "Alphabetic: @dotaccent{a} @ringaccent{a} @u{a} @ubaraccent{a} @udotaccent{a}\n"
        + "@v{s} @H{o} @dotless{i} @dotless{j} @tieaccent{oo}\n";

    private static string Body(string source) => new TexinfoHtmlRenderer().Generate(source).BodyHtml;

    [Theory]
    //The seven punctuation accents, written without braces as the Texinfo manual writes them.
    [InlineData("@'e", "é")]
    [InlineData("@`a", "à")]
    [InlineData("@^o", "ô")]
    [InlineData("@\"u", "ü")]
    [InlineData("@~n", "ñ")]
    [InlineData("@,{c}", "ç")]
    [InlineData("@=a", "ā")]
    //The same commands with braces mean exactly the same thing.
    [InlineData("@'{e}", "é")]
    [InlineData("@'{E}", "É")]
    //The alphabetic accents, which need their braces.
    [InlineData("@dotaccent{a}", "ȧ")]
    [InlineData("@ringaccent{a}", "å")]
    [InlineData("@u{a}", "ă")]
    [InlineData("@v{s}", "š")]
    [InlineData("@H{o}", "ő")]
    //@dotless takes a mark away rather than adding one.
    [InlineData("@dotless{i}", "ı")]
    [InlineData("@dotless{j}", "ȷ")]
    public void An_accent_composes_with_the_letter_it_applies_to(string source, string expected)
        => Body(source + "\n").Contains(expected).Should().BeTrue();

    [Fact]
    public void An_accent_with_no_precomposed_form_keeps_its_combining_mark()
    {
        //Arrange + Act - Unicode has no single character for an underbarred 'a', so the composed
        //pair is the honest answer and dropping the mark would not be. An underdotted 'o' does
        //have one, so that pair composes away.
        string body = Body("@ubaraccent{a} @udotaccent{o}\n");

        //Assert - the underbar is U+0332 COMBINING LOW LINE, which is the mark Texinfo's own
        //output uses; U+0331 COMBINING MACRON BELOW is a different, lower mark.
        body.Contains("a̲").Should().BeTrue();
        body.Contains("a̱").Should().BeFalse();
        body.Contains("ọ").Should().BeTrue();
    }

    [Fact]
    public void A_tie_accent_sits_between_the_two_characters_it_spans()
    {
        //Arrange + Act
        string body = Body("@tieaccent{oo}\n");

        //Assert - the tie is a double diacritic: it follows the first character and reaches over
        //the second, so a mark written after both would draw in the wrong place.
        body.Contains("o͡o").Should().BeTrue();
    }

    [Fact]
    public void A_braceless_accent_takes_one_character_and_leaves_the_rest_of_the_word()
    {
        //Arrange + Act
        string body = Body("Se@~norita and caf@'e today\n");

        //Assert
        body.Contains("Señorita").Should().BeTrue();
        body.Contains("café today").Should().BeTrue();
    }

    [Fact]
    public void An_accent_inside_a_word_survives_being_a_node_name()
    {
        //Arrange + Act - accented names are ordinary in a manual written in any European language,
        //and a cross reference has to match one.
        string body = Body("@node R@'esum@'e\n@chapter R@'esum@'e\n"
            + "See @ref{R@'esum@'e} for more.\n");

        //Assert
        body.Contains("Résumé").Should().BeTrue();
        body.Contains("href=\"#").Should().BeTrue();
    }

    [Fact]
    public void Every_accent_the_language_has_renders_without_a_warning()
    {
        //Arrange + Act
        TexinfoHtmlResult result = new TexinfoHtmlRenderer().Generate(EveryAccent);

        //Assert
        string.Join(Environment.NewLine, result.Warnings.Messages).Should().Be(string.Empty);
        //Spot-check one from each of the three forms the fixture writes.
        result.BodyHtml.Contains("é").Should().BeTrue();
        result.BodyHtml.Contains("Ñ").Should().BeTrue();
        result.BodyHtml.Contains("ő").Should().BeTrue();
    }

    [Fact]
    public void An_accent_command_inside_a_code_context_still_composes()
    {
        //Arrange + Act
        string body = Body("@code{@'e}\n");

        //Assert
        body.Contains("<code>é</code>").Should().BeTrue();
    }

    [Theory]
    //The letters and marks a general manual reaches for that a music manual never does. Each
    //expected character was read off Texinfo's own output rather than guessed at.
    [InlineData("@dh{} @DH{} @th{} @TH{}", "ð Ð þ Þ")]
    [InlineData("@guilsinglleft{} @guilsinglright{}", "‹ ›")]
    [InlineData("@ogonek{a} @ogonek{e}", "ą ę")]
    [InlineData("@point{}", "⋆")]
    public void The_rest_of_the_glyph_table_renders(string source, string expected)
        => Body(source + "\n").Contains(expected).Should().BeTrue();

    [Theory]
    //The commands that name a character the Texinfo syntax has taken for itself.
    [InlineData("@hashchar{}", "#")]
    [InlineData("@ampchar{}", "&amp;")]
    [InlineData("@&", "&amp;")]
    [InlineData("@atchar{}", "@")]
    [InlineData("@lbracechar{}@rbracechar{}", "{}")]
    [InlineData("@backslashchar{}", "\\")]
    public void A_character_the_syntax_took_can_be_written_back(string source, string expected)
        => Body("x" + source + "y\n").Contains("x" + expected + "y").Should().BeTrue();

    [Fact]
    public void Displaymath_is_set_as_text_and_says_so()
    {
        //Arrange + Act - there is no mathematical typesetter here and there is not going to be
        //one, so this is a statement of what the reader gets rather than a gap to be filled.
        TexinfoHtmlResult result = new TexinfoHtmlRenderer()
            .Generate("@displaymath\nx^2 + y^2 = z^2\n@end displaymath\n");

        //Assert
        result.BodyHtml.Contains("<pre class=\"texinfo-displaymath\">").Should().BeTrue();
        result.BodyHtml.Contains("x^2 + y^2 = z^2").Should().BeTrue();
        result.Warnings.Messages.Count.Should().Be(1);
        result.Warnings.Messages[0].StartsWith("Emit:", StringComparison.Ordinal).Should().BeTrue();
    }

    [Fact]
    public void Dotless_reports_a_letter_that_has_no_dot_to_remove()
    {
        //Arrange + Act
        TexinfoHtmlResult result = new TexinfoHtmlRenderer().Generate("@dotless{a}\n");

        //Assert
        result.Warnings.Messages.Count.Should().Be(1);
        result.Warnings.Messages[0].StartsWith("Syntax:", StringComparison.Ordinal).Should().BeTrue();
        result.BodyHtml.Contains("a").Should().BeTrue();
    }
}
