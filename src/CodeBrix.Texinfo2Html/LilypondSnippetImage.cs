using System;
using System.IO;

namespace CodeBrix.Texinfo2Html;

/// <summary>
/// One picture an <see cref="ILilypondSnippetRenderer"/> produced for a snippet, given either as a
/// file the renderer wrote or as the bytes of one it never wrote down. Both travel with the
/// document the same way once they get here.
/// </summary>
public sealed class LilypondSnippetImage
{
    private readonly byte[] _content;

    private LilypondSnippetImage(string filePath, byte[] content, string fileExtension)
    {
        FilePath = filePath ?? string.Empty;
        _content = content;
        FileExtension = NormalizeExtension(fileExtension);
    }

    /// <summary>Names a picture the renderer wrote to disk.</summary>
    /// <param name="filePath">Path of the image file; its extension is taken from the name.</param>
    public static LilypondSnippetImage FromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(filePath));
        }
        string trimmed = filePath.Trim();
        return new LilypondSnippetImage(trimmed, null, Path.GetExtension(trimmed));
    }

    /// <summary>Supplies a picture the renderer holds in memory.</summary>
    /// <param name="content">The encoded image bytes.</param>
    /// <param name="fileExtension">
    /// The image format as a file extension, with or without its dot - <c>png</c>, <c>.svg</c>.
    /// This is what the picture is written under, so it has to say what the bytes actually are.
    /// </param>
    public static LilypondSnippetImage FromContent(byte[] content, string fileExtension)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(fileExtension));
        }
        return new LilypondSnippetImage(string.Empty, content, fileExtension);
    }

    /// <summary>
    /// The path of the file the renderer wrote, or an empty string when the picture was given as
    /// bytes.
    /// </summary>
    public string FilePath { get; }

    /// <summary>The image format as a file extension including its leading dot.</summary>
    public string FileExtension { get; }

    /// <summary>True when the picture was given as bytes rather than as a file on disk.</summary>
    public bool HasContent => _content != null;

    /// <summary>Returns a copy of the image bytes, or an empty array for a picture given as a file.</summary>
    public byte[] GetContent()
        => _content == null ? Array.Empty<byte>() : (byte[])_content.Clone();

    /// <summary>Hands out the bytes without copying them, for writing them straight to a file.</summary>
    internal byte[] ContentDirect => _content;

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".png";
        }
        string trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }

    /// <summary>Formats the picture for diagnostics.</summary>
    public override string ToString()
        => HasContent ? $"{_content.Length} bytes ({FileExtension})" : FilePath;
}
