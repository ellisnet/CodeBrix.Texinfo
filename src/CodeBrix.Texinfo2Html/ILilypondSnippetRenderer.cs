namespace CodeBrix.Texinfo2Html;

/// <summary>
/// Engraves the LilyPond music environments of a <c>.tely</c> document into pictures. Register an
/// implementation on <see cref="TexinfoHtmlOptions.SnippetRenderer"/> to have the music appear as
/// music; with none registered the document shows every snippet as its source text instead.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place CodeBrix.Texinfo2Html deliberately stops short. Engraving music means
/// running LilyPond, and this library will not take on that dependency - so it defines the seam and
/// leaves it to a consumer who already has an engraver to fill in.
/// </para>
/// <para>
/// An implementation is called once for each distinct snippet: two snippets with the same music and
/// the same options are engraved once and the picture is used twice, so a manual with two thousand
/// snippets does not pay for the ones it repeats. Implementations should still be cheap to call and
/// must be safe to call from the thread that is rendering the document.
/// </para>
/// <para>
/// Never throw from an implementation. Report trouble with
/// <see cref="LilypondSnippetResult.Failed"/> so the document can carry on and the reason reaches
/// the caller as a warning. An exception that escapes is caught and turned into exactly that, but a
/// returned failure says what went wrong far better than a stack trace does.
/// </para>
/// </remarks>
public interface ILilypondSnippetRenderer
{
    /// <summary>Engraves one snippet.</summary>
    /// <param name="snippet">The music, its options, and where it came from.</param>
    /// <returns>
    /// The pictures it engraved to; <see cref="LilypondSnippetResult.NotRendered"/> to decline it
    /// quietly; or <see cref="LilypondSnippetResult.Failed"/> to report why it could not be done.
    /// </returns>
    LilypondSnippetResult Render(LilypondSnippet snippet);
}
