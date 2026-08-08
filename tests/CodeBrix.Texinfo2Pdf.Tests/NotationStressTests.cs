using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.Texinfo2Html;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Pdf.Tests;

/// <summary>
/// Scale. The LilyPond notation reference is the largest document either library will be asked to
/// render - some 51,000 lines across a hundred included files, a thousand nodes, four thousand
/// index entries and sixteen hundred music snippets - and it is the document that says whether the
/// pipeline holds up under a real manual rather than a fixture.
/// </summary>
/// <remarks>
/// <para>
/// The corpus test skips cleanly when the LilyPond documentation is not present, since it is
/// GFDL-licensed and so is never committed here. The shape test below it needs no corpus at all,
/// and is the one that would catch a structure going quadratic - which is the failure the corpus
/// test would only ever report as "slow today".
/// </para>
/// <para>
/// Both time bounds are deliberately loose. They exist to catch an order-of-magnitude regression,
/// not to police a few per cent on a machine that happens to be busy.
/// </para>
/// </remarks>
public class NotationStressTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>Creates the fixture with the output helper the measurements are reported through.</summary>
    /// <param name="output">Where the measured timings are written for a human to read.</param>
    public NotationStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

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

    [Fact]
    public void The_notation_reference_goes_all_the_way_to_a_pdf_within_the_time_it_is_given()
    {
        SkipUnlessCorpusPresent();

        //Arrange - LilyPond generates version.itexi during its build, so a stand-in supplies the
        //version macros the manual expects. It is written to a temporary directory, never here.
        string standIn = Directory.CreateTempSubdirectory("texinfo-stress-version-").FullName;
        string output = Path.Combine(Path.GetTempPath(), "codebrix-texinfo-stress");
        try
        {
            File.WriteAllText(Path.Combine(standIn, "version.itexi"),
                "@c Test stand-in for LilyPond's build-generated version.itexi.\n"
                + "@macro version\n2.25.99\n@end macro\n"
                + "@macro versionStable\n2.24.99\n@end macro\n"
                + "@macro versionDevel\n2.25.99\n@end macro\n");
            TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
            renderer.Options.Texinfo.IncludeSearchPaths.Add(standIn);

            //Act - the two stages timed apart, because they fail for different reasons.
            Stopwatch reading = Stopwatch.StartNew();
            TexinfoHtmlResult html = renderer.GenerateHtmlFromFile(
                Path.Combine(CorpusRoot, "en", "notation.tely"));
            reading.Stop();

            Stopwatch writing = Stopwatch.StartNew();
            TexinfoPdfResult pdf = renderer.RenderHtml(html,
                Path.Combine(output, "notation-stress.pdf"));
            writing.Stop();

            //Assert - the document really is the big one...
            html.BodyHtml.Length.Should().BeGreaterThan(2_500_000);
            pdf.PageCount.Should().BeGreaterThanOrEqualTo(900);
            pdf.Title.Should().Be("LilyPond Notation Reference");
            //...nothing but the expected degradations came out of either stage...
            pdf.Warnings.PdfMessages.Where(m => !m.StartsWith("[font]", StringComparison.Ordinal))
                .Should().BeEmpty();
            //...and both stages finished well inside their budgets. Measured on the development
            //machine the reading takes a fraction of a second and the writing a few seconds, so
            //these bounds leave one to two orders of magnitude of room: they are here to catch a
            //structure that has gone quadratic, not to police a busy machine.
            reading.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
            writing.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(3));
            _output.WriteLine(
                $"notation.tely: read {reading.Elapsed.TotalSeconds:N2}s, "
                + $"wrote {writing.Elapsed.TotalSeconds:N2}s, {pdf.PageCount} pages, "
                + $"{html.BodyHtml.Length:N0} markup characters.");
        }
        finally
        {
            Directory.Delete(standIn, recursive: true);
        }
    }

    [Fact]
    public void The_notation_reference_can_be_read_twice_over_without_the_second_pass_costing_more()
    {
        SkipUnlessCorpusPresent();

        //Arrange - a renderer is documented as reusable for many documents, and the manual that
        //would expose anything accumulating between runs is the largest one.
        string standIn = Directory.CreateTempSubdirectory("texinfo-stress-reuse-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(standIn, "version.itexi"),
                "@macro version\n2.25.99\n@end macro\n"
                + "@macro versionStable\n2.24.99\n@end macro\n"
                + "@macro versionDevel\n2.25.99\n@end macro\n");
            TexinfoPdfRenderer renderer = new TexinfoPdfRenderer();
            renderer.Options.Texinfo.IncludeSearchPaths.Add(standIn);
            string source = Path.Combine(CorpusRoot, "en", "notation.tely");

            //Act
            Stopwatch first = Stopwatch.StartNew();
            TexinfoHtmlResult one = renderer.GenerateHtmlFromFile(source);
            first.Stop();
            Stopwatch second = Stopwatch.StartNew();
            TexinfoHtmlResult two = renderer.GenerateHtmlFromFile(source);
            second.Stop();

            //Assert - the same document, at the same price. A table that was not cleared between
            //runs would show up as either a different result or a slower second pass.
            two.BodyHtml.Length.Should().Be(one.BodyHtml.Length);
            two.Warnings.Count.Should().Be(one.Warnings.Count);
            second.Elapsed.Should().BeLessThan(first.Elapsed + first.Elapsed + first.Elapsed);
        }
        finally
        {
            Directory.Delete(standIn, recursive: true);
        }
    }

    /// <summary>
    /// Builds a document of the given number of sections, each with a node to anchor it, an index
    /// entry to file, and a cross reference to another section - the three things that are looked
    /// up across the whole document, and so the three places an accidentally quadratic structure
    /// would hide.
    /// </summary>
    private static string SyntheticManual(int sections)
    {
        StringBuilder builder = new StringBuilder(sections * 200);
        builder.Append("@settitle Synthetic Manual\n@node Top\n@top Synthetic Manual\n@contents\n");
        for (int index = 0; index < sections; index++)
        {
            int other = (index + (sections / 2)) % sections;
            builder.Append("@node Section ").Append(index).Append('\n')
                .Append("@section Section ").Append(index).Append('\n')
                .Append("@cindex entry ").Append(index).Append('\n')
                .Append("@anchor{mark ").Append(index).Append("}\n")
                .Append("Prose that refers to @ref{Section ").Append(other)
                .Append("} and carries @code{markup} through a sentence of ordinary length.\n\n");
        }
        builder.Append("@node Index\n@appendix Index\n@printindex cp\n");
        return builder.ToString();
    }

    [Fact]
    public void Reading_a_document_costs_what_its_size_costs_and_not_the_square_of_it()
    {
        //Arrange - warm the code paths first so the measurement is of the work, not of the JIT.
        TexinfoHtmlRenderer renderer = new TexinfoHtmlRenderer();
        renderer.Generate(SyntheticManual(200));
        string small = SyntheticManual(500);
        string large = SyntheticManual(2000);

        //Act
        Stopwatch smallRun = Stopwatch.StartNew();
        TexinfoHtmlResult smallResult = renderer.Generate(small);
        smallRun.Stop();
        Stopwatch largeRun = Stopwatch.StartNew();
        TexinfoHtmlResult largeResult = renderer.Generate(large);
        largeRun.Stop();

        //Assert - both really were rendered, cross references and all...
        smallResult.Warnings.Count.Should().Be(0);
        largeResult.Warnings.Count.Should().Be(0);
        largeResult.BodyHtml.Length.Should().BeGreaterThan(smallResult.BodyHtml.Length * 3);
        //...and four times the document cost well under sixteen times the time, which is what
        //quadratic behaviour in the anchor, index or cross-reference tables would have cost.
        double ratio = largeRun.Elapsed.TotalMilliseconds
            / Math.Max(1.0, smallRun.Elapsed.TotalMilliseconds);
        _output.WriteLine(
            $"synthetic manual: 500 sections in {smallRun.Elapsed.TotalMilliseconds:N0}ms, "
            + $"2000 in {largeRun.Elapsed.TotalMilliseconds:N0}ms, ratio {ratio:N2}.");
        ratio.Should().BeLessThan(8.0);
    }
}
