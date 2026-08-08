using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Html.Tests;

/// <summary>
/// Corpus gate at the markup level: every manual of the English LilyPond documentation must render
/// to HTML with a complete sectioning tree, a usable table of contents, an index that leads
/// somewhere, and only the warnings a source checkout is expected to produce. The corpus is read
/// locally from ~/GitHome/lilypond and never committed; these tests skip cleanly when it is not
/// present.
/// </summary>
/// <remarks>
/// <para>
/// Five warning kinds are expected here and nothing else is tolerated. Raw <c>@tex</c> blocks are
/// skipped by design; one macro warning is the deliberate refusal to let LilyPond's TeX-branch
/// <c>@macro cindex</c> shadow the built-in; the include warnings name files the LilyPond build
/// generates and a source checkout therefore lacks; the reference warning is the consequence of
/// those missing files, since a reference into a chapter that was never included has nothing to
/// point at; and the emit warnings are the remaining gaps, which are the engraving of music
/// snippets and mathematics.
/// </para>
/// <para>
/// The corpus is rendered with no snippet renderer registered, which is the default and the shape
/// every consumer gets out of the box. That the whole corpus produces no unrecognized-option
/// warning is itself a result: the measured option vocabulary really does cover what these manuals
/// write.
/// </para>
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
           //References into the chapters those missing files would have contributed.
           || (message.StartsWith("Reference:", StringComparison.Ordinal)
               && message.Contains("name a destination this document does not define"))
           //The remaining gap, plus the one degradation the design accepts outright: mathematics
           //has no typesetter here and never will have. The snippet message is matched on the
           //fallback it describes rather than on the words 'music snippet', so that a renderer
           //FAILING on a corpus manual could never be mistaken for one never having been asked.
           || (message.StartsWith("Emit:", StringComparison.Ordinal)
               && (message.Contains("emitted as their source text") || message.Contains("@math")));

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
    public void Every_link_a_manual_writes_has_a_destination_in_the_same_document()
    {
        SkipUnlessCorpusPresent();

        //Arrange
        List<string> broken = new List<string>();
        int links = 0;

        //Act - contents lines, cross references and index lines all end up here.
        foreach (string manual in Manuals)
        {
            string body = RenderManual(manual).BodyHtml;
            HashSet<string> defined = DefinedIdentifiers(body);
            foreach (string target in LinkTargets(body))
            {
                links++;
                if (!defined.Contains(target))
                {
                    broken.Add($"{manual}: #{target}");
                }
            }
        }

        //Assert
        string.Join(Environment.NewLine, broken.Take(10)).Should().Be(string.Empty);
        //Seven of the eight manuals ask for a contents, five print an index, and every manual
        //cross references itself, so this is a large number even before the index arrives.
        links.Should().BeGreaterThanOrEqualTo(10_000);
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
            HashSet<string> defined = DefinedIdentifiers(body);
            foreach (string target in TargetsUnder(body, "<p class=\"texinfo-toc-"))
            {
                entries++;
                //Every entry must point at an identifier the same document defines.
                if (!defined.Contains(target))
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

    [Theory]
    [InlineData("learning.tely", 300)]
    [InlineData("usage.tely", 80)]
    [InlineData("notation.tely", 3_000)]
    public void Manual_prints_an_index_of_the_entries_it_collected(string manualFileName,
        int minimumEntries)
    {
        SkipUnlessCorpusPresent();

        //Arrange + Act
        string body = RenderManual(manualFileName).BodyHtml;

        //Assert - LilyPond folds its function index into the concept one, so a single @printindex
        //prints both, and the code-font entries are what proves the merge happened.
        CountOf(body, "<div class=\"texinfo-index\">").Should().Be(1);
        CountOf(body, "<p class=\"texinfo-index-entry\">")
            .Should().BeGreaterThanOrEqualTo(minimumEntries);
        CountOf(body, "<p class=\"texinfo-index-letter\">").Should().BeGreaterThan(10);
        body.Contains("<code>\\").Should().BeTrue();
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

    private static HashSet<string> DefinedIdentifiers(string body)
    {
        HashSet<string> identifiers = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        while (true)
        {
            index = body.IndexOf(" id=\"", index, StringComparison.Ordinal);
            if (index < 0)
            {
                return identifiers;
            }
            index += 5;
            int end = body.IndexOf('"', index);
            identifiers.Add(body.Substring(index, end - index));
            index = end;
        }
    }

    private static IEnumerable<string> LinkTargets(string body)
    {
        int index = 0;
        while (true)
        {
            index = body.IndexOf("href=\"#", index, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }
            index += 7;
            int end = body.IndexOf('"', index);
            yield return body.Substring(index, end - index);
            index = end;
        }
    }

    private static IEnumerable<string> TargetsUnder(string body, string marker)
    {
        int index = 0;
        while (true)
        {
            index = body.IndexOf(marker, index, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }
            int start = body.IndexOf("href=\"#", index, StringComparison.Ordinal);
            if (start < 0)
            {
                yield break;
            }
            start += 7;
            int end = body.IndexOf('"', start);
            yield return body.Substring(start, end - start);
            index = end;
        }
    }
}
