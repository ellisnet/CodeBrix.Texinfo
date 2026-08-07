using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// Wave 3 corpus gate at the markup level: every manual of the English LilyPond documentation must
/// render to HTML with a complete sectioning tree, a usable table of contents, and only the
/// warnings a source checkout is expected to produce. The corpus is read locally from
/// ~/GitHome/lilypond and never committed; these tests skip cleanly when it is not present.
/// </summary>
/// <remarks>
/// Four warning kinds are expected here and nothing else is tolerated. Raw <c>@tex</c> blocks are
/// skipped by design; one macro warning is the deliberate refusal to let LilyPond's TeX-branch
/// <c>@macro cindex</c> shadow the built-in; the include warnings name files the LilyPond build
/// generates and a source checkout therefore lacks; and the emit warnings are this wave's known
/// gaps, which are the index and the engraving of music snippets.
/// </remarks>
public class LilypondEmitterCorpusGateTests
{
    private static readonly string[] Manuals =
    {
        "changes.tely", "essay.tely", "extending.tely", "learning.tely",
        "music-glossary.tely", "usage.tely", "notation.tely", "snippets.tely"
    };

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
    /// Renders one manual in place, with a temporary stand-in for the build-generated
    /// version.itexi that macros.itexi includes.
    /// </summary>
    private static TexinfoHtmlResult RenderManual(string manualFileName)
    {
        string standIn = Directory.CreateTempSubdirectory("texinfo-corpus-emit-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(standIn, "version.itexi"),
                "@c Test stand-in for LilyPond's build-generated version.itexi.\n"
                + "@macro version\n2.25.99\n@end macro\n"
                + "@macro versionStable\n2.24.99\n@end macro\n"
                + "@macro versionDevel\n2.25.99\n@end macro\n");
            TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
            renderer.Options.IncludeSearchPaths.Add(standIn);
            return renderer.GenerateFromFile(Path.Combine(CorpusRoot, "en", manualFileName));
        }
        finally
        {
            Directory.Delete(standIn, recursive: true);
        }
    }

    private static bool IsExpected(string message)
        => message.StartsWith("RawBlockSkipped:", StringComparison.Ordinal)
           || (message.StartsWith("Macro:", StringComparison.Ordinal) && message.Contains("cindex"))
           //Files and pictures the LilyPond build generates, absent from a source checkout.
           || (message.StartsWith("Include:", StringComparison.Ordinal)
               && message.Contains("was not found on the search path"))
           //This wave's known gaps, plus the one degradation the design accepts outright:
           //mathematics has no typesetter here and never will have.
           || (message.StartsWith("Emit:", StringComparison.Ordinal)
               && (message.Contains("@printindex") || message.Contains("music snippet")
                   || message.Contains("@math")));

    [Theory]
    [InlineData("changes.tely", "LilyPond Changes", 10)]
    [InlineData("essay.tely", "Essay on automated music engraving", 20)]
    [InlineData("extending.tely", "Extending LilyPond", 70)]
    [InlineData("learning.tely", "LilyPond Learning Manual", 165)]
    [InlineData("music-glossary.tely", "LilyPond Music Glossary", 340)]
    [InlineData("usage.tely", "LilyPond Application Usage", 70)]
    [InlineData("notation.tely", "LilyPond Notation Reference", 550)]
    [InlineData("snippets.tely", "LilyPond snippets", 5)]
    public void Manual_renders_to_markup_with_only_expected_warnings(string manualFileName,
        string title, int minimumHeadings)
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        TexinfoHtmlResult result = RenderManual(manualFileName);

        //Assert
        string unexpected = string.Join(Environment.NewLine,
            result.Warnings.Messages.Where(m => !IsExpected(m)));
        unexpected.Should().Be(string.Empty);
        result.Title.Should().Be(title);
        //Every sectioning unit becomes an h-element carrying the identifier it is linked by.
        CountOf(result.BodyHtml, "<h").Should().BeGreaterThanOrEqualTo(minimumHeadings);
        result.BodyHtml.Contains("id=\"Top\"").Should().BeTrue();
        result.Html.StartsWith("<!DOCTYPE html>", StringComparison.Ordinal).Should().BeTrue();
        result.Css.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Every_manual_builds_a_contents_whose_links_all_have_a_destination()
    {
        SkipUnlessCorpusPresent();

        //Arrange
        List<string> broken = new List<string>();
        int entries = 0;

        //Act
        foreach (string manual in Manuals)
        {
            string body = RenderManual(manual).BodyHtml;
            foreach (string target in ContentsTargets(body))
            {
                entries++;
                //Every entry must point at an identifier the same document defines.
                if (!body.Contains("id=\"" + target + "\"", StringComparison.Ordinal))
                {
                    broken.Add($"{manual}: #{target}");
                }
            }
        }

        //Assert
        string.Join(Environment.NewLine, broken).Should().Be(string.Empty);
        //Seven of the eight manuals ask for a contents; music-glossary.tely has its @contents
        //commented out in the source, so its 361 sections contribute no entries here.
        entries.Should().BeGreaterThanOrEqualTo(900);
    }

    [Fact]
    public void The_largest_manual_renders_every_structure_the_emitter_produces()
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        string body = RenderManual("notation.tely").BodyHtml;

        //Assert - the 51,000-line notation reference exercises the whole block vocabulary.
        CountOf(body, "<pre class=\"texinfo-example\"").Should().BeGreaterThan(200);
        CountOf(body, "<pre class=\"texinfo-lilypond\"").Should().BeGreaterThan(500);
        CountOf(body, "<table class=\"texinfo-multitable\"").Should().BeGreaterThan(30);
        CountOf(body, "<dl class=\"texinfo-table\"").Should().BeGreaterThan(10);
        CountOf(body, "<ul>").Should().BeGreaterThan(50);
        CountOf(body, "<blockquote").Should().BeGreaterThan(10);
        CountOf(body, "texinfo-secnum").Should().BeGreaterThan(500);
        //Nothing may leak out as an unhandled container.
        CountOf(body, "texinfo-unknown").Should().Be(0);
    }

    private static int CountOf(string text, string value)
        => text.Split(value).Length - 1;

    private static IEnumerable<string> ContentsTargets(string body)
    {
        const string marker = "<p class=\"texinfo-toc-";
        int index = 0;
        while (true)
        {
            index = body.IndexOf(marker, index, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }
            int hrefStart = body.IndexOf("href=\"#", index, StringComparison.Ordinal);
            if (hrefStart < 0)
            {
                yield break;
            }
            hrefStart += 7;
            int hrefEnd = body.IndexOf('"', hrefStart);
            yield return body.Substring(hrefStart, hrefEnd - hrefStart);
            index = hrefEnd;
        }
    }
}
