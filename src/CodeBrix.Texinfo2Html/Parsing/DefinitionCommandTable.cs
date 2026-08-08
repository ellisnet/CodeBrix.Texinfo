using System;
using System.Collections.Generic;

namespace CodeBrix.Texinfo2Html.Parsing;

/// <summary>
/// The shape of every Texinfo definition command: which parts its heading line carries, in what
/// order, and which index the name is filed in. Twenty-two commands differ only along these few
/// axes, so one table describes them all and the parser reads a heading line the same way for
/// every one of them.
/// </summary>
/// <remarks>
/// A heading line is a run of words - a braced group counting as one word - laid out as
/// category, class, data type, name, and then everything left over as the arguments. A command
/// that fixes its own category (<c>@defun</c> is <c>@deffn Function</c>) simply supplies that
/// word instead of reading one.
/// </remarks>
internal static class DefinitionCommandTable
{
    /// <summary>How one definition command's heading line is laid out.</summary>
    internal sealed class DefinitionShape
    {
        /// <summary>Creates a shape.</summary>
        /// <param name="commandName">The command name without <c>@</c>.</param>
        /// <param name="fixedCategory">The category the command supplies, or an empty string when the line gives one.</param>
        /// <param name="hasClass">True when the line names a class after the category.</param>
        /// <param name="hasDataType">True when the line names a data type before the name.</param>
        /// <param name="indexName">The index the name is filed in, or an empty string for no entry.</param>
        /// <param name="classPreposition">The word joining category to class - "of" or "on".</param>
        public DefinitionShape(string commandName, string fixedCategory, bool hasClass,
            bool hasDataType, string indexName, string classPreposition)
        {
            CommandName = commandName;
            FixedCategory = fixedCategory;
            HasClass = hasClass;
            HasDataType = hasDataType;
            IndexName = indexName;
            ClassPreposition = classPreposition;
        }

        /// <summary>The command name without <c>@</c>.</summary>
        public string CommandName { get; }

        /// <summary>The category the command supplies; empty when the heading line gives one.</summary>
        public string FixedCategory { get; }

        /// <summary>True when the heading line names a class after the category.</summary>
        public bool HasClass { get; }

        /// <summary>True when the heading line names a data type before the entity name.</summary>
        public bool HasDataType { get; }

        /// <summary>
        /// The two-letter index the entity name is filed in, or an empty string for
        /// <c>@defline</c> and <c>@deftypeline</c>, which exist to make no index entry at all.
        /// </summary>
        public string IndexName { get; }

        /// <summary>
        /// The word joining the category to the class when the line names one: "of" for the
        /// variable-like commands, "on" for the method-like ones.
        /// </summary>
        public string ClassPreposition { get; }
    }

    private static readonly Dictionary<string, DefinitionShape> Shapes = Build();

    private static Dictionary<string, DefinitionShape> Build()
    {
        Dictionary<string, DefinitionShape> shapes =
            new Dictionary<string, DefinitionShape>(StringComparer.Ordinal);
        void Add(string name, string category, bool hasClass, bool hasType, string index,
            string preposition)
            => shapes[name] = new DefinitionShape(name, category, hasClass, hasType, index, preposition);

        //Functions and similar entities, filed in the index of functions.
        Add("deffn", string.Empty, false, false, "fn", string.Empty);
        Add("defun", "Function", false, false, "fn", string.Empty);
        Add("defmac", "Macro", false, false, "fn", string.Empty);
        Add("defspec", "Special Form", false, false, "fn", string.Empty);
        Add("deftypefn", string.Empty, false, true, "fn", string.Empty);
        Add("deftypefun", "Function", false, true, "fn", string.Empty);

        //Variables and similar entities, filed in the index of variables.
        Add("defvr", string.Empty, false, false, "vr", string.Empty);
        Add("defvar", "Variable", false, false, "vr", string.Empty);
        Add("defopt", "User Option", false, false, "vr", string.Empty);
        Add("deftypevr", string.Empty, false, true, "vr", string.Empty);
        Add("deftypevar", "Variable", false, true, "vr", string.Empty);

        //Data types, filed in the index of data types.
        Add("deftp", string.Empty, false, false, "tp", string.Empty);

        //Object-oriented variables belong to a class and are filed in the index of variables.
        Add("defcv", string.Empty, true, false, "vr", "of");
        Add("defivar", "Instance Variable", true, false, "vr", "of");
        Add("deftypecv", string.Empty, true, true, "vr", "of");
        Add("deftypeivar", "Instance Variable", true, true, "vr", "of");

        //Object-oriented methods act on a class and are filed in the index of functions.
        Add("defop", string.Empty, true, false, "fn", "on");
        Add("defmethod", "Method", true, false, "fn", "on");
        Add("deftypeop", string.Empty, true, true, "fn", "on");
        Add("deftypemethod", "Method", true, true, "fn", "on");

        //The generic pair, used inside @defblock: the same heading lines with no index entry.
        Add("defline", string.Empty, false, false, string.Empty, string.Empty);
        Add("deftypeline", string.Empty, false, true, string.Empty, string.Empty);
        return shapes;
    }

    /// <summary>
    /// Looks up a definition command that opens an environment - everything except the two
    /// <c>@defblock</c> line commands, which head a definition without opening one.
    /// </summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="shape">Receives the command's heading-line shape.</param>
    public static bool TryGetEnvironment(string name, out DefinitionShape shape)
        => Shapes.TryGetValue(name, out shape) && !IsBlockLine(name);

    /// <summary>
    /// Looks up one of the <c>@defblock</c> line commands, which write a heading line inside a
    /// <c>@defblock</c> rather than opening an environment of their own.
    /// </summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="shape">Receives the command's heading-line shape.</param>
    public static bool TryGetBlockLine(string name, out DefinitionShape shape)
    {
        if (IsBlockLine(name))
        {
            return Shapes.TryGetValue(name, out shape);
        }
        shape = null;
        return false;
    }

    /// <summary>
    /// Looks up an <c>x</c> continuation form, such as <c>@deffnx</c>, which adds a further
    /// heading line to the definition already open.
    /// </summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    /// <param name="shape">Receives the shape of the command it continues.</param>
    public static bool TryGetContinuation(string name, out DefinitionShape shape)
    {
        if (name.Length > 1 && name[name.Length - 1] == 'x')
        {
            return TryGetEnvironment(name.Substring(0, name.Length - 1), out shape);
        }
        shape = null;
        return false;
    }

    /// <summary>True when the name is any definition command, in any of its forms.</summary>
    /// <param name="name">A command name without <c>@</c>.</param>
    public static bool IsDefinitionCommand(string name)
        => Shapes.ContainsKey(name) || TryGetContinuation(name, out _);

    private static bool IsBlockLine(string name)
        => string.Equals(name, "defline", StringComparison.Ordinal)
           || string.Equals(name, "deftypeline", StringComparison.Ordinal);
}
