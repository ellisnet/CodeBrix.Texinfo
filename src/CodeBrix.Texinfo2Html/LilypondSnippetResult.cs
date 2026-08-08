using System;
using System.Collections.Generic;

namespace CodeBrix.Texinfo2Html;

/// <summary>
/// What an <see cref="ILilypondSnippetRenderer"/> made of one snippet: the picture or pictures it
/// engraved, or the reason it could not. A renderer that engraves nothing is not a failure - a score
/// that runs to several pages produces several pictures, and one that a renderer simply does not
/// handle produces none.
/// </summary>
public sealed class LilypondSnippetResult
{
    private static readonly LilypondSnippetResult NothingRendered =
        new LilypondSnippetResult(Array.Empty<LilypondSnippetImage>(), string.Empty);

    private LilypondSnippetResult(IReadOnlyList<LilypondSnippetImage> images, string errorMessage)
    {
        Images = images ?? Array.Empty<LilypondSnippetImage>();
        ErrorMessage = errorMessage ?? string.Empty;
    }

    /// <summary>
    /// The result for a snippet the renderer chose not to engrave. The document falls back to
    /// showing the music source, and no warning is raised: declining is a decision, not a fault.
    /// </summary>
    public static LilypondSnippetResult NotRendered => NothingRendered;

    /// <summary>Returns a result carrying one picture the renderer wrote to disk.</summary>
    /// <param name="imagePath">Path of the image file.</param>
    public static LilypondSnippetResult FromFile(string imagePath)
        => new LilypondSnippetResult(new[] { LilypondSnippetImage.FromFile(imagePath) }, string.Empty);

    /// <summary>Returns a result carrying one picture the renderer holds in memory.</summary>
    /// <param name="content">The encoded image bytes.</param>
    /// <param name="fileExtension">The image format as a file extension, with or without its dot.</param>
    public static LilypondSnippetResult FromContent(byte[] content, string fileExtension)
        => new LilypondSnippetResult(
            new[] { LilypondSnippetImage.FromContent(content, fileExtension) }, string.Empty);

    /// <summary>
    /// Returns a result carrying several pictures, which is what a score longer than one page
    /// engraves to. They are placed in the order given.
    /// </summary>
    /// <param name="images">The pictures, in the order they should appear.</param>
    public static LilypondSnippetResult FromImages(IEnumerable<LilypondSnippetImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        List<LilypondSnippetImage> collected = new List<LilypondSnippetImage>();
        foreach (LilypondSnippetImage image in images)
        {
            if (image != null)
            {
                collected.Add(image);
            }
        }
        return new LilypondSnippetResult(collected, string.Empty);
    }

    /// <summary>
    /// Returns a result reporting that the snippet could not be engraved. The document shows the
    /// music source instead and collects one warning naming the reason.
    /// </summary>
    /// <param name="message">Why the snippet could not be engraved.</param>
    public static LilypondSnippetResult Failed(string message)
        => new LilypondSnippetResult(Array.Empty<LilypondSnippetImage>(),
            string.IsNullOrWhiteSpace(message) ? "The snippet could not be engraved." : message.Trim());

    /// <summary>The pictures the snippet engraved to, in the order they should appear.</summary>
    public IReadOnlyList<LilypondSnippetImage> Images { get; }

    /// <summary>Why the snippet could not be engraved, or an empty string when nothing went wrong.</summary>
    public string ErrorMessage { get; }

    /// <summary>True when the renderer produced at least one picture.</summary>
    public bool IsRendered => Images.Count > 0;

    /// <summary>True when the renderer reported a reason it could not engrave the snippet.</summary>
    public bool IsFailure => ErrorMessage.Length > 0;

    /// <summary>Formats the result for diagnostics.</summary>
    public override string ToString()
    {
        if (IsFailure)
        {
            return "failed: " + ErrorMessage;
        }
        return IsRendered ? $"{Images.Count} image(s)" : "not rendered";
    }
}
