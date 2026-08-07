using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// An image from <c>@image{name, width, height, alt, extension}</c>. Texinfo names the image
/// without an extension and lets each output format pick one; the parser keeps the name and the
/// optional explicit extension, and locating the actual file is left to the emitter.
/// </summary>
internal sealed class ImageNode : TexinfoNode
{
    /// <summary>Creates an image node.</summary>
    /// <param name="fileName">The image's file name, usually without an extension.</param>
    /// <param name="width">The requested width as written, or an empty string.</param>
    /// <param name="height">The requested height as written, or an empty string.</param>
    /// <param name="alternateText">Text describing the image, or an empty string.</param>
    /// <param name="extension">An explicit file extension, or an empty string.</param>
    /// <param name="position">Where the command started in the source.</param>
    public ImageNode(string fileName, string width, string height, string alternateText,
        string extension, SourcePosition position) : base(position)
    {
        FileName = fileName ?? string.Empty;
        Width = width ?? string.Empty;
        Height = height ?? string.Empty;
        AlternateText = alternateText ?? string.Empty;
        Extension = extension ?? string.Empty;
    }

    /// <summary>The image's file name, usually written without an extension.</summary>
    public string FileName { get; }

    /// <summary>The requested width as written, including its unit; empty when not given.</summary>
    public string Width { get; }

    /// <summary>The requested height as written, including its unit; empty when not given.</summary>
    public string Height { get; }

    /// <summary>Text describing the image; empty when not given.</summary>
    public string AlternateText { get; }

    /// <summary>An explicit file extension; empty when the emitter should choose one.</summary>
    public string Extension { get; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Both;

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@image{{{FileName}}}";
}
