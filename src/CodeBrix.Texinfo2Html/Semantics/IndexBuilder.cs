using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CodeBrix.Texinfo2Html.Model;
using CodeBrix.Texinfo2Html.Parsing;

namespace CodeBrix.Texinfo2Html.Semantics;

/// <summary>
/// Turns the index entries the parser collected into the indices the document asks to print:
/// applies the <c>@syncodeindex</c> and <c>@synindex</c> merges, works out each entry's sort key,
/// orders the result, and hands out the identifier the printed line links back to.
/// </summary>
/// <remarks>
/// Only the indices a <c>@printindex</c> actually names are built. That is not just an economy: an
/// entry is linked to through a marker the emitter leaves where the entry was written, and a manual
/// with three thousand concept entries and no index would otherwise carry three thousand markers
/// nothing points at.
/// </remarks>
internal sealed class IndexBuilder
{
    /// <summary>
    /// The predefined indices whose entries print in a fixed-width font. The concept index is the
    /// one that does not; the Texinfo manual states the rule that way round.
    /// </summary>
    private static readonly HashSet<string> CodeIndexes =
        new HashSet<string>(StringComparer.Ordinal) { "fn", "vr", "ky", "pg", "tp" };

    /// <summary>
    /// Texinfo's flags for characters an index sorts as though they were not there, and the
    /// character each one drops. A manual whose entries all begin with a backslash sets the first
    /// of these so they file under their letters instead of collecting under one symbol.
    /// </summary>
    private static readonly (string Flag, char Character)[] IgnoredCharacterFlags =
    {
        ("txiindexbackslashignore", '\\'),
        ("txiindexhyphenignore", '-'),
        ("txiindexlessthanignore", '<'),
        ("txiindexatsignignore", '@')
    };

    private sealed class Candidate
    {
        public Candidate(IndexEntryNode source, string sortKey, bool useCodeFont, int order)
        {
            Source = source;
            SortKey = sortKey;
            UseCodeFont = useCodeFont;
            Order = order;
        }

        public IndexEntryNode Source { get; }

        public string SortKey { get; }

        public bool UseCodeFont { get; }

        public int Order { get; }
    }

    private readonly TexinfoDocument _document;
    private readonly ElementIdAllocator _allocator;
    private readonly Dictionary<string, (string Target, bool UseCodeFont)> _merges =
        new Dictionary<string, (string, bool)>(StringComparer.Ordinal);
    private readonly Dictionary<string, PrintedIndex> _indexes =
        new Dictionary<string, PrintedIndex>(StringComparer.Ordinal);
    private readonly Dictionary<IndexEntryNode, string> _entryIds =
        new Dictionary<IndexEntryNode, string>();
    private readonly List<char> _ignoredCharacters = new List<char>();

    /// <summary>Creates a builder for one document.</summary>
    /// <param name="document">The parsed document, with its index entries and merges filled in.</param>
    /// <param name="allocator">The allocator the rest of the document's identifiers come from.</param>
    public IndexBuilder(TexinfoDocument document, ElementIdAllocator allocator)
    {
        _document = document;
        _allocator = allocator;
    }

    /// <summary>The indices that were built, keyed by the name <c>@printindex</c> asked for.</summary>
    public IReadOnlyDictionary<string, PrintedIndex> Indexes => _indexes;

    /// <summary>
    /// The identifier of the marker each printed entry links back to. An entry that no printed
    /// index contains is absent, and the emitter leaves no marker for it.
    /// </summary>
    public IReadOnlyDictionary<IndexEntryNode, string> EntryIds => _entryIds;

    /// <summary>Builds every index the document prints.</summary>
    public void Build()
    {
        HashSet<string> requested = RequestedIndexNames();
        if (requested.Count == 0)
        {
            return;
        }
        foreach ((string flag, char character) in IgnoredCharacterFlags)
        {
            if (_document.Values.ContainsKey(flag))
            {
                _ignoredCharacters.Add(character);
            }
        }
        foreach (IndexMerge merge in _document.IndexMerges)
        {
            if (merge.SourceIndex.Length > 0 && merge.TargetIndex.Length > 0)
            {
                _merges[merge.SourceIndex] = (merge.TargetIndex, merge.UseCodeFont);
            }
        }

        Dictionary<string, List<Candidate>> collected = new Dictionary<string, List<Candidate>>(
            StringComparer.Ordinal);
        foreach (string name in requested)
        {
            collected[name] = new List<Candidate>();
        }
        int order = 0;
        foreach (IndexEntryNode entry in _document.IndexEntries)
        {
            (string target, bool useCodeFont) = ResolveTarget(entry.IndexName);
            if (!collected.TryGetValue(target, out List<Candidate> list))
            {
                continue;
            }
            list.Add(new Candidate(entry, SortKeyFor(entry), useCodeFont, order));
            order++;
        }

        foreach (KeyValuePair<string, List<Candidate>> pair in collected)
        {
            _indexes[pair.Key] = Materialize(pair.Key, pair.Value);
        }
    }

    private HashSet<string> RequestedIndexNames()
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        foreach (TexinfoNode node in _document.AllNodes())
        {
            if (node is DirectiveNode directive && directive.Kind == DirectiveKind.PrintIndex)
            {
                string name = directive.Argument.Trim();
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
        }
        return names;
    }

    /// <summary>
    /// Follows the merge chain from the index an entry was written into to the index it ends up
    /// printed in, and reports whether anything along the way asked for a fixed-width font.
    /// </summary>
    private (string Target, bool UseCodeFont) ResolveTarget(string indexName)
    {
        string current = indexName ?? string.Empty;
        bool useCodeFont = CodeIndexes.Contains(current) || _document.CodeIndexNames.Contains(current);
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { current };
        while (_merges.TryGetValue(current, out (string Target, bool UseCodeFont) merge))
        {
            useCodeFont = useCodeFont || merge.UseCodeFont;
            current = merge.Target;
            useCodeFont = useCodeFont || _document.CodeIndexNames.Contains(current);
            //A document that merges two indices into each other would otherwise loop here.
            if (!seen.Add(current))
            {
                break;
            }
        }
        return (current, useCodeFont);
    }

    private string SortKeyFor(IndexEntryNode entry)
    {
        string text = entry.SortKey.Length > 0
            ? entry.SortKey
            : InlineNodes.ToPlainText(entry.Content);
        if (_ignoredCharacters.Count == 0)
        {
            return text.Trim();
        }
        StringBuilder builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (!_ignoredCharacters.Contains(c))
            {
                builder.Append(c);
            }
        }
        return builder.ToString().Trim();
    }

    private PrintedIndex Materialize(string name, List<Candidate> candidates)
    {
        candidates.Sort(Compare);
        List<PrintedIndexEntry> entries = new List<PrintedIndexEntry>(candidates.Count);
        foreach (Candidate candidate in candidates)
        {
            string elementId = _allocator.Allocate("ix-"
                + (_entryIds.Count + 1).ToString(CultureInfo.InvariantCulture));
            _entryIds[candidate.Source] = elementId;
            entries.Add(new PrintedIndexEntry(candidate.Source, candidate.SortKey,
                LetterOf(candidate.SortKey), elementId, candidate.UseCodeFont));
        }
        return new PrintedIndex(name, entries);
    }

    private static int Compare(Candidate left, Candidate right)
    {
        //Case-insensitive first, so 'Beam' and 'beam' land together rather than in two different
        //parts of the alphabet; the ordinal pass then keeps a stable order between them, and the
        //document order behind that keeps two identical entries in the order they were written.
        int result = string.Compare(left.SortKey, right.SortKey,
            StringComparison.InvariantCultureIgnoreCase);
        if (result != 0)
        {
            return result;
        }
        result = string.CompareOrdinal(left.SortKey, right.SortKey);
        return result != 0 ? result : left.Order.CompareTo(right.Order);
    }

    private static string LetterOf(string sortKey)
    {
        if (sortKey.Length == 0)
        {
            return "Symbols";
        }
        char first = sortKey[0];
        if (char.IsLetter(first))
        {
            return char.ToUpperInvariant(first).ToString();
        }
        return char.IsDigit(first) ? "0-9" : "Symbols";
    }
}
