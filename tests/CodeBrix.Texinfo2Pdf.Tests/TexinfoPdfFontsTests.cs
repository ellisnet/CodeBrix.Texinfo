using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.Texinfo2Pdf.Rendering;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Texinfo2Pdf.Tests;

public class TexinfoPdfFontsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("texinfo-assets-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AddPackagedFontAssets_reads_manifest_extracts_fonts_and_renders()
    {
        //Arrange
        var opened = new List<string>();
        var streams = new List<Stream>();
        Stream Open(string name)
        {
            opened.Add(name);
            Stream source = name.EndsWith("fonts.txt", StringComparison.Ordinal)
                ? new MemoryStream(Encoding.UTF8.GetBytes("\nfonts/Roboto-Regular.ttf\n"))
                : typeof(TexinfoPdfFontsTests).Assembly.GetManifestResourceStream("TestFont.ttf");
            source.Should().NotBeNull();
            var stream = new NonSeekableStream(source);
            streams.Add(stream);
            return stream;
        }

        //Act
        var paths = TexinfoPdfFonts.AddPackagedFontAssets(Open, _directory);
        var repeated = TexinfoPdfFonts.AddPackagedFontAssets(Open, _directory);
        var pdf = new TexinfoPdfRenderer().RenderTexinfoToBytes("@node Top\n@top Test\nFont asset text.\n");

        //Assert
        opened[0].Should().Be("CodeBrix.Texinfo2Pdf.Fonts/fonts.txt");
        paths.Should().Equal(repeated);
        File.Exists(paths.Single()).Should().BeTrue();
        Directory.GetFiles(_directory).Should().HaveCount(1);
        streams.All(s => !s.CanRead).Should().BeTrue();
        Encoding.ASCII.GetString(pdf.PdfBytes, 0, 5).Should().Be("%PDF-");
        pdf.PageCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Extract_uses_content_names_and_keeps_paths_inside_storage()
    {
        //Arrange
        byte[] first = { 1, 2, 3 };
        byte[] second = { 4, 5, 6 };

        //Act
        string path = FontAssetStorage.Extract("../../outside.ttf", _ => new MemoryStream(first), _directory);
        string same = FontAssetStorage.Extract("another-name.ttf", _ => new MemoryStream(first), _directory);
        string changed = FontAssetStorage.Extract("../../outside.ttf", _ => new MemoryStream(second), _directory);

        //Assert
        Path.GetDirectoryName(path).Should().Be(_directory);
        path.Should().Be(same);
        changed.Should().NotBe(path);
        File.ReadAllBytes(path).Should().Equal(first);
        File.ReadAllBytes(changed).Should().Equal(second);
        Directory.GetFiles(_directory, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Extract_cleans_partial_files_and_disposes_a_failed_stream()
    {
        //Arrange
        var stream = new FailingStream();
        Action extract = () => FontAssetStorage.Extract("broken.ttf", _ => stream, _directory);

        //Act and Assert
        extract.Should().Throw<IOException>();
        stream.CanRead.Should().BeFalse();
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    [Fact]
    public void AddFontAssets_rejects_invalid_fonts_and_disposes_streams()
    {
        //Arrange
        var stream = new MemoryStream(new byte[] { 0, 1, 2 });
        Action register = () => TexinfoPdfFonts.AddFontAssets(new[] { "invalid.ttf" }, _ => stream, _directory);

        //Act and Assert
        register.Should().Throw<InvalidOperationException>();
        stream.CanRead.Should().BeFalse();
        Directory.GetFiles(_directory, "*.tmp").Should().BeEmpty();
    }

    [Theory]
    [InlineData("font.txt")]
    [InlineData("")]
    [InlineData(null)]
    public void AddFontAssets_rejects_invalid_names_before_opening(string name)
    {
        //Arrange
        bool opened = false;
        Action register = () => TexinfoPdfFonts.AddFontAssets(new[] { name }, _ =>
        {
            opened = true;
            return new MemoryStream();
        }, _directory);

        //Act and Assert
        register.Should().Throw<ArgumentException>();
        opened.Should().BeFalse();
    }

    private sealed class FailingStream : MemoryStream
    {
        public override void CopyTo(Stream destination, int bufferSize)
        {
            destination.WriteByte(42);
            throw new IOException("Simulated interrupted asset read.");
        }
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        private bool _disposed;
        public override bool CanRead => !_disposed && inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); }
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
