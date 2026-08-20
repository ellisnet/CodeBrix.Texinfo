using System.Collections.Generic;
using CodeBrix.PdfDocCreate.Html2Pdf.Fonts;

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
