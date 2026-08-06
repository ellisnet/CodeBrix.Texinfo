using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Pdf.Tests;

//The CodeBrix.Texinfo2Pdf library has no public API yet - this repository is
//currently a scaffold. These tests exercise the packaging and assembly wiring
//that the scaffold does establish - including the project reference to
//CodeBrix.Texinfo2Html and the package reference to CodeBrix.PdfDocCreate.Html2Pdf
//- so the suite is green and meaningful until the conversion functionality lands.
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
    public void texinfo2html_assembly_flows_through_the_project_reference()
        => Assembly.Load(new AssemblyName(Texinfo2HtmlAssemblyName)).Should().NotBeNull();

    [Fact]
    public void html2pdf_assembly_flows_through_the_package_reference()
        => Assembly.Load(new AssemblyName(Html2PdfAssemblyName)).Should().NotBeNull();
}
