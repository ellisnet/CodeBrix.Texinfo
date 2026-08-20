using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBrix.Texinfo2Html.Emit;

/// <summary>
/// Turns the file name in an <c>@image</c> command into a picture the generated document can carry
/// with it: it finds the file on the search path and gives it a place inside the document's own
/// image folder, so the markup never points outside the directory it is written into.
/// </summary>
/// <remarks>
/// Texinfo image references are written without an extension, because the same document is expected
/// to render to several output formats that each prefer a different one, so the extension has to be
/// found rather than read. Two pictures found in different directories can share a file name, so
/// the second one to arrive is numbered.
/// </remarks>
internal sealed class ImageReferenceResolver
{
    //Every format the PDF stage can place: the CodeBrix.Imaging decoder set plus SVG,
    //which Html2Pdf rasterizes itself. Never add .pdf here - a manual that keeps
    //pdf/NAME variants for its TeX branch would then hand Html2Pdf a file it cannot
    //decode.
    private static readonly string[] CandidateExtensions =
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg",
        ".webp", ".tif", ".tiff", ".tga", ".ppm", ".pgm", ".pbm",
    };

    private readonly IReadOnlyList<string> _searchPaths;
    private readonly string _folderName;
    private readonly Dictionary<string, string> _bySourcePath =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> _usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<TexinfoImageReference> _images = new List<TexinfoImageReference>();
    private int _generatedCount;

    /// <summary>Creates a resolver that looks in the given directories, in order.</summary>
    /// <param name="searchPaths">The directories to search; may be empty.</param>
    /// <param name="folderName">The folder the pictures are gathered into beside the document.</param>
    public ImageReferenceResolver(IReadOnlyList<string> searchPaths, string folderName)
    {
        _searchPaths = searchPaths ?? new List<string>();
        _folderName = string.IsNullOrWhiteSpace(folderName) ? "images" : folderName.Trim();
    }

    /// <summary>Every picture the document refers to, in the order it was first referred to.</summary>
    public IReadOnlyList<TexinfoImageReference> Images => _images;

    /// <summary>Finds the file an image reference names and reports where the markup points at it.</summary>
    /// <param name="fileName">The name from the command, with or without an extension.</param>
    /// <param name="declaredExtension">The command's explicit extension argument, or an empty string.</param>
    /// <param name="relativePath">
    /// Receives the path the markup uses, relative to the directory the document is written into.
    /// </param>
    public bool TryResolve(string fileName, string declaredExtension, out string relativePath)
    {
        relativePath = string.Empty;
        if (!TryFind(fileName, declaredExtension, out string fullPath))
        {
            return false;
        }
        if (_bySourcePath.TryGetValue(fullPath, out string existing))
        {
            relativePath = existing;
            return true;
        }
        relativePath = _folderName + "/" + UniqueName(Path.GetFileName(fullPath));
        _bySourcePath[fullPath] = relativePath;
        _images.Add(new TexinfoImageReference(fullPath, relativePath, isGenerated: false));
        return true;
    }

    /// <summary>
    /// Gives a picture that a snippet renderer produced a place in the document's image folder, so
    /// it travels with the document exactly as a picture found on disk does.
    /// </summary>
    /// <param name="image">The picture, either a file the renderer wrote or bytes it handed over.</param>
    /// <returns>The path the markup should point at, relative to the document.</returns>
    public string RegisterGenerated(LilypondSnippetImage image)
    {
        if (image.HasContent)
        {
            _generatedCount++;
            string name = "snippet-"
                + _generatedCount.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)
                + image.FileExtension;
            string generatedPath = _folderName + "/" + UniqueName(name);
            _images.Add(new TexinfoImageReference(image.ContentDirect, generatedPath));
            return generatedPath;
        }
        //A renderer that wrote the picture to disk keeps its own name, which is what makes an
        //engraving traceable back to the run that produced it.
        string fullPath = Path.GetFullPath(image.FilePath);
        if (_bySourcePath.TryGetValue(fullPath, out string existing))
        {
            return existing;
        }
        _generatedCount++;
        string fileName = Path.GetFileName(fullPath);
        if (fileName.Length == 0)
        {
            fileName = "snippet-"
                + _generatedCount.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)
                + image.FileExtension;
        }
        string relative = _folderName + "/" + UniqueName(fileName);
        _bySourcePath[fullPath] = relative;
        _images.Add(new TexinfoImageReference(fullPath, relative, isGenerated: true));
        return relative;
    }

    private string UniqueName(string fileName)
    {
        string name = string.IsNullOrEmpty(fileName) ? "image" : fileName;
        if (_usedNames.Add(name))
        {
            return name;
        }
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);
        for (int suffix = 2; ; suffix++)
        {
            string candidate = stem + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + extension;
            if (_usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private bool TryFind(string fileName, string declaredExtension, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }
        string trimmed = fileName.Trim();
        foreach (string candidate in NameCandidates(trimmed, declaredExtension))
        {
            if (Path.IsPathRooted(candidate))
            {
                if (File.Exists(candidate))
                {
                    fullPath = Path.GetFullPath(candidate);
                    return true;
                }
                continue;
            }
            foreach (string directory in _searchPaths)
            {
                string combined = Path.Combine(directory, candidate);
                if (File.Exists(combined))
                {
                    fullPath = Path.GetFullPath(combined);
                    return true;
                }
            }
        }
        return false;
    }

    private static IEnumerable<string> NameCandidates(string fileName, string declaredExtension)
    {
        if (!string.IsNullOrWhiteSpace(declaredExtension))
        {
            string extension = declaredExtension.Trim();
            yield return fileName + (extension.StartsWith(".", StringComparison.Ordinal)
                ? extension
                : "." + extension);
        }
        //The name may already carry its extension, which is how a .texi written for one output
        //format usually spells it.
        yield return fileName;
        foreach (string extension in CandidateExtensions)
        {
            yield return fileName + extension;
        }
    }
}
