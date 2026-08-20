using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Pdf.Tests;

//The packaging and assembly wiring of CodeBrix.Texinfo2Pdf: that it loads, that its version
//follows the family's date-stamped scheme, that the test project can see its internals, and that
//both libraries it composes - CodeBrix.Texinfo2Html through a project reference, and
//CodeBrix.PdfDocCreate.Html2Pdf through a package reference - really do arrive with it.
public class LibraryPackagingSmoke
{
    private const string LibraryAssemblyName = "CodeBrix.Texinfo2Pdf";

    private const string TestAssemblyName = "CodeBrix.Texinfo2Pdf.Tests";

    private const string Texinfo2HtmlAssemblyName = "CodeBrix.Texinfo2Html";

    private const string Html2PdfAssemblyName = "CodeBrix.PdfDocCreate.Html2Pdf";

    [Fact]
    public void library_assembly_can_be_loaded()
        => Assembly.Load(new AssemblyName(LibraryAssemblyName)).Should().NotBeNull();

    [Fact]
    public void library_assembly_carries_date_stamped_version()
    {
        //Arrange
        Assembly assembly = Assembly.Load(new AssemblyName(LibraryAssemblyName));

        //Act
        Version version = assembly.GetName().Version;

        //Assert
        version.Major.Should().Be(1);
        version.Minor.Should().BeGreaterThanOrEqualTo(0);
        version.Build.Should().BeInRange(1, 366);
        version.Revision.Should().BeInRange(0, 1439);
    }

    [Fact]
    public void library_internals_are_visible_to_this_test_assembly()
    {
        //Arrange
        Assembly assembly = Assembly.Load(new AssemblyName(LibraryAssemblyName));

        //Act
        var friends = assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .ToList();

        //Assert
        friends.Should().Contain(TestAssemblyName);
    }

    [Fact]
    public void the_public_surface_is_the_five_types_the_documentation_describes()
    {
        //Arrange - the composition layer is meant to be small, and a helper that drifts into
        //public is a promise nobody meant to make. The staging area is internal on purpose.
        Assembly assembly = Assembly.Load(new AssemblyName(LibraryAssemblyName));

        //Act
        string[] publicTypes = assembly.GetExportedTypes().Select(t => t.FullName).OrderBy(n => n)
            .ToArray();

        //Assert
        publicTypes.Should().BeEquivalentTo(new[]
        {
            "CodeBrix.Texinfo2Pdf.TexinfoPdfFonts",
            "CodeBrix.Texinfo2Pdf.TexinfoPdfOptions",
            "CodeBrix.Texinfo2Pdf.TexinfoPdfRenderer",
            "CodeBrix.Texinfo2Pdf.TexinfoPdfResult",
            "CodeBrix.Texinfo2Pdf.TexinfoPdfWarnings"
        });
    }

    [Fact]
    public void texinfo2html_assembly_flows_through_the_project_reference()
        => Assembly.Load(new AssemblyName(Texinfo2HtmlAssemblyName)).Should().NotBeNull();

    [Fact]
    public void html2pdf_assembly_flows_through_the_package_reference()
        => Assembly.Load(new AssemblyName(Html2PdfAssemblyName)).Should().NotBeNull();
}
