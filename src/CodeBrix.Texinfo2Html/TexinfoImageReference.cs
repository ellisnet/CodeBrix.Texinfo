using System;

namespace CodeBrix.Texinfo2Html;

/// <summary>
/// One picture the generated markup refers to: where it came from when the document was rendered,
/// and the path the markup points at, which is relative to wherever the document is written.
/// </summary>
/// <remarks>
/// Texinfo image references carry no directory and usually no extension, and a manual keeps its
/// pictures wherever it likes. Recording both ends of the resolution is what lets the generated
/// document be moved somewhere else with its pictures alongside it. A picture engraved from a music
/// snippet joins the same list, and may have no file behind it at all - see <see cref="HasContent"/>.
/// </remarks>
public sealed class TexinfoImageReference
{
    private readonly byte[] _content;

    internal TexinfoImageReference(string sourcePath, string relativePath, bool isGenerated)
    {
        SourcePath = sourcePath ?? string.Empty;
        RelativePath = relativePath ?? string.Empty;
        IsGenerated = isGenerated;
    }

    internal TexinfoImageReference(byte[] content, string relativePath)
    {
        SourcePath = string.Empty;
        RelativePath = relativePath ?? string.Empty;
        IsGenerated = true;
        _content = content;
    }

    /// <summary>
    /// The full path the picture was read from, or an empty string for one that a snippet renderer
    /// handed over as bytes and that was therefore never a file.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// The path the markup refers to, written with forward slashes and relative to the directory
    /// the document is written into.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// True for a picture engraved from a music snippet, false for one an <c>@image</c> command
    /// named and the search path found.
    /// </summary>
    public bool IsGenerated { get; }

    /// <summary>
    /// True when the picture is held in memory rather than existing as a file. Writing the document
    /// out writes it; there is nothing to copy from.
    /// </summary>
    public bool HasContent => _content != null;

    /// <summary>
    /// Returns a copy of the picture's bytes, or an empty array when the picture is a file on disk
    /// and <see cref="SourcePath"/> is where to read it from.
    /// </summary>
    public byte[] GetContent()
        => _content == null ? Array.Empty<byte>() : (byte[])_content.Clone();

    /// <summary>Hands out the bytes without copying them, for writing them straight to a file.</summary>
    internal byte[] ContentDirect => _content;

    /// <summary>Formats the reference for diagnostics.</summary>
    public override string ToString()
        => HasContent
            ? $"{RelativePath} <- {_content.Length} bytes"
            : $"{RelativePath} <- {SourcePath}";
}
