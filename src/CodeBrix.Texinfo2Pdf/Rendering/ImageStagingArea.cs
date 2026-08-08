using System;
using System.IO;
using CodeBrix.Texinfo2Html;

namespace CodeBrix.Texinfo2Pdf.Rendering;

/// <summary>
/// A throwaway directory holding a document's pictures for the length of one PDF render.
/// </summary>
/// <remarks>
/// <para>
/// The markup CodeBrix.Texinfo2Html emits points at its pictures relative to wherever the document
/// is put, which is what makes a written-out document complete. Rendering that markup from memory
/// therefore needs somewhere for those relative paths to lead, and it must not be the caller's
/// output directory: a caller who asked for one PDF should get one PDF, not a PDF and a folder of
/// pictures. So the pictures are staged here, the render is pointed at this directory, and the
/// whole thing goes away again.
/// </para>
/// <para>
/// A document with no pictures stages nothing and creates no directory, which is the usual case
/// and costs nothing.
/// </para>
/// </remarks>
internal sealed class ImageStagingArea : IDisposable
{
    private readonly bool _created;

    private ImageStagingArea(string directory, bool created)
    {
        BaseDirectory = directory;
        _created = created;
    }

    /// <summary>The directory to render against, or null when the document had no pictures.</summary>
    public string BaseDirectory { get; }

    /// <summary>Stages the pictures of a rendered document, if it has any.</summary>
    public static ImageStagingArea For(TexinfoHtmlResult html)
    {
        if (html.Images.Count == 0)
        {
            return new ImageStagingArea(null, created: false);
        }
        string directory = Directory.CreateTempSubdirectory("codebrix-texinfo2pdf-").FullName;
        html.CopyImagesTo(directory);
        return new ImageStagingArea(directory, created: true);
    }

    /// <summary>Removes the staged pictures. A directory that will not delete is left alone.</summary>
    public void Dispose()
    {
        if (!_created || !Directory.Exists(BaseDirectory))
        {
            return;
        }
        try
        {
            Directory.Delete(BaseDirectory, recursive: true);
        }
        catch (IOException)
        {
            //Losing a temporary directory is not worth losing the PDF that was just produced.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
