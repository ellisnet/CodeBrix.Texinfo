using System.Collections.Generic;
using System.IO;

namespace CodeBrix.Texinfo2Html.Emit;

/// <summary>
/// Turns the file name in an <c>@image</c> command into a path that exists on disk. Texinfo image
/// references are written without an extension, because the same document is expected to render to
/// several output formats that each prefer a different one, so the extension has to be found
/// rather than read.
/// </summary>
internal sealed class ImageReferenceResolver
{
    private static readonly string[] CandidateExtensions =
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg" };

    private readonly IReadOnlyList<string> _searchPaths;

    /// <summary>Creates a resolver that looks in the given directories, in order.</summary>
    /// <param name="searchPaths">The directories to search; may be empty.</param>
    public ImageReferenceResolver(IReadOnlyList<string> searchPaths)
    {
        _searchPaths = searchPaths ?? new List<string>();
    }

    /// <summary>Finds the file an image reference names.</summary>
    /// <param name="fileName">The name from the command, with or without an extension.</param>
    /// <param name="declaredExtension">The command's explicit extension argument, or an empty string.</param>
    /// <param name="fullPath">Receives the full path of the file that was found.</param>
    public bool TryResolve(string fileName, string declaredExtension, out string fullPath)
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
            yield return fileName + (extension.StartsWith(".", System.StringComparison.Ordinal)
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
