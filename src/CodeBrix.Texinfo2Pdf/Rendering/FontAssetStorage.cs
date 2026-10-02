using System;
using System.IO;
using System.Security.Cryptography;

namespace CodeBrix.Texinfo2Pdf.Rendering;

internal static class FontAssetStorage
{
    internal static string Extract(string assetName, Func<string, Stream> openAsset, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);
        string extension = Path.GetExtension(assetName).ToLowerInvariant();
        if (extension != ".ttf" && extension != ".otf")
        {
            throw new ArgumentException("Font assets must have a .ttf or .otf extension.", nameof(assetName));
        }

        string root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        string temporary = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var input = openAsset(assetName)
                ?? throw new InvalidOperationException($"No stream was returned for font asset '{assetName}'."))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }

            string hash;
            using (var input = File.OpenRead(temporary))
            {
                hash = Convert.ToHexString(SHA256.HashData(input));
            }
            string destination = Path.Combine(root, hash + extension);
            try
            {
                File.Move(temporary, destination);
            }
            catch (IOException) when (File.Exists(destination))
            {
                // Another call may already have extracted these same bytes.
            }
            return destination;
        }
        finally
        {
            if (File.Exists(temporary)) { File.Delete(temporary); }
        }
    }
}
