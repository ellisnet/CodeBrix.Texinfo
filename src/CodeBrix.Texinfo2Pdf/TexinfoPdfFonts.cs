using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.PdfDocCreate.Html2Pdf.Fonts;
using CodeBrix.Texinfo2Pdf.Rendering;

namespace CodeBrix.Texinfo2Pdf;

/// <summary>
/// Registers fonts for PDF rendering. The PDF stage renders all text with registered
/// fonts - the CodeBrix.Platform.Fonts packages this library brings along, plus
/// anything registered here - and never with operating-system fonts. This class
/// forwards to the underlying Html2Pdf font registry, so a consumer of this package
/// can register fonts without naming CodeBrix.PdfDocCreate.Html2Pdf anywhere.
/// </summary>
/// <remarks>
/// Registration is process-global and serves every render that follows it: a
/// registered font is usable from the generated markup's font families, from SVG
/// text inside placed pictures, and - when opted in - from the per-glyph fallback
/// chain consulted for characters the styled font lacks. All methods are idempotent
/// and may be called before or after renders have happened; additions take effect on
/// the next render.
/// </remarks>
public static class TexinfoPdfFonts
{
    /// <summary>
    /// Extracts and registers the PDF font assets included by this package in Android apps.
    /// </summary>
    /// <param name="openAsset">Opens an asset by its logical name, for example Android AssetManager.Open.
    /// The returned streams are disposed by this method.</param>
    /// <param name="storageDirectory">A persistent app-private directory for extracted fonts.</param>
    /// <returns>The extracted font paths. Keep these files for the lifetime of PDF rendering.</returns>
    /// <remarks>Call once at startup before rendering. Requires this package's Android build assets;
    /// no Android types are referenced by this portable API. All supplied font families are added
    /// to the fallback chain. The first registration of a family wins in the underlying registry.</remarks>
    public static IReadOnlyList<string> AddPackagedFontAssets(Func<string, Stream> openAsset,
        string storageDirectory)
    {
        ArgumentNullException.ThrowIfNull(openAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        using var manifest = openAsset("CodeBrix.Texinfo2Pdf.Fonts/fonts.txt")
            ?? throw new InvalidOperationException("The PDF font asset opener returned no manifest stream.");
        using var reader = new StreamReader(manifest);
        var names = new List<string>();
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line)) { names.Add(line.Trim()); }
        }
        if (names.Count == 0)
        {
            throw new InvalidOperationException("The packaged PDF font manifest is empty.");
        }
        return AddFontAssets(names, openAsset, storageDirectory, includeInFallback: true);
    }

    /// <summary>
    /// Extracts TTF/OTF asset streams to persistent storage and registers all faces together.
    /// </summary>
    /// <param name="assetNames">Asset names ending in .ttf or .otf. Include all faces of a family in one call.</param>
    /// <param name="openAsset">Opens each named asset at its beginning. Returned streams are disposed here;
    /// non-seekable streams are supported.</param>
    /// <param name="storageDirectory">An app-private directory, created if needed. Extracted files are named
    /// by their content hash and reused on repeated calls; asset paths cannot escape this directory.</param>
    /// <param name="includeInFallback">Whether to add the families to the per-glyph fallback chain.</param>
    /// <returns>The extracted paths, in asset order.</returns>
    /// <remarks>Registration is process-global. Keep extracted files available while rendering; this method
    /// does not delete old versions. Stream, filesystem and invalid-font errors propagate. On failure,
    /// completed extractions remain reusable and incomplete temporary files are removed. Register the full
    /// set of faces before the first render; existing family registrations are not replaced.</remarks>
    public static IReadOnlyList<string> AddFontAssets(IEnumerable<string> assetNames,
        Func<string, Stream> openAsset, string storageDirectory, bool includeInFallback = false)
    {
        ArgumentNullException.ThrowIfNull(assetNames);
        ArgumentNullException.ThrowIfNull(openAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        var paths = new List<string>();
        foreach (var name in assetNames)
        {
            paths.Add(FontAssetStorage.Extract(name, openAsset, storageDirectory));
        }
        Html2PdfFonts.AddFontFiles(paths, includeInFallback);
        return paths.AsReadOnly();
    }

    /// <summary>
    /// Adds a directory to probe for <c>CodeBrix.Platform.Fonts.*</c> package folders
    /// (the <c>&lt;Name&gt;/Fonts/*.ttf</c> + manifest layout the font packages ship).
    /// </summary>
    /// <param name="directory">The directory holding the package folders.</param>
    public static void AddFontDirectory(string directory)
        => Html2PdfFonts.AddFontDirectory(directory);

    /// <summary>
    /// Registers a single loose .ttf or .otf font file. No manifest is needed - the
    /// family name, weight and style are read from the font's own tables.
    /// </summary>
    /// <param name="filePath">The font file. Either path separator style works.</param>
    /// <param name="includeInFallback">
    /// True to also add the font's family to the per-glyph fallback chain consulted
    /// for characters the styled font lacks.
    /// </param>
    public static void AddFontFile(string filePath, bool includeInFallback = false)
        => Html2PdfFonts.AddFontFile(filePath, includeInFallback);

    /// <summary>
    /// Registers several loose .ttf/.otf font files together, grouping faces that
    /// share a family name into one family. See <see cref="AddFontFile"/>.
    /// </summary>
    /// <param name="filePaths">The font files.</param>
    /// <param name="includeInFallback">
    /// True to also add the fonts' families to the per-glyph fallback chain.
    /// </param>
    public static void AddFontFiles(IEnumerable<string> filePaths, bool includeInFallback = false)
        => Html2PdfFonts.AddFontFiles(filePaths, includeInFallback);

    /// <summary>
    /// Registers every .ttf/.otf file found directly in a directory (no manifest
    /// needed). See <see cref="AddFontFile"/>.
    /// </summary>
    /// <param name="directory">The directory holding the font files.</param>
    /// <param name="includeInFallback">
    /// True to also add the fonts' families to the per-glyph fallback chain.
    /// </param>
    public static void AddFontFilesFromDirectory(string directory, bool includeInFallback = false)
        => Html2PdfFonts.AddFontFilesFromDirectory(directory, includeInFallback);

    /// <summary>
    /// Appends an already-registered family to the per-glyph fallback chain: when a
    /// character has no glyph in the font a run resolved to, the fallback families
    /// are consulted in registration order and the first one covering the character
    /// renders it. Fallback families never substitute whole runs - only individual
    /// characters.
    /// </summary>
    /// <param name="familyName">The registered family to append.</param>
    public static void AddFallbackFamily(string familyName)
        => Html2PdfFonts.AddFallbackFamily(familyName);
}
