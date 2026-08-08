using System.Collections.Generic;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Model;

/// <summary>
/// One heading line of a definition - the line a <c>@deffn</c>, <c>@deftypefn</c> or one of their
/// relatives writes, naming the entity being described. A definition may carry several of these,
/// because the <c>x</c> forms (<c>@deffnx</c> and friends) add further heading lines to the same
/// description.
/// </summary>
/// <remarks>
/// The parts are kept apart rather than merged into one run of text because each is set
/// differently: the category labels the line, the data type and the name are computer text, and
/// the arguments of an untyped definition are metasyntactic variables.
/// </remarks>
internal sealed class DefinitionHeaderNode : TexinfoNode
{
    /// <summary>Creates a definition heading line.</summary>
    /// <param name="commandName">The command that produced the line, without <c>@</c>.</param>
    /// <param name="category">The category of the entity, such as "Function".</param>
    /// <param name="className">The class the entity belongs to; empty for the commands with none.</param>
    /// <param name="dataType">The entity's data type; empty for the untyped commands.</param>
    /// <param name="name">The name of the entity being defined.</param>
    /// <param name="arguments">The entity's arguments or attributes; empty when it takes none.</param>
    /// <param name="classPreposition">The word joining the category to the class - "of" or "on".</param>
    /// <param name="isTyped">True when the command is one of the <c>@deftype...</c> family.</param>
    /// <param name="position">Where the command started in the source.</param>
    public DefinitionHeaderNode(string commandName, IReadOnlyList<TexinfoNode> category,
        IReadOnlyList<TexinfoNode> className, IReadOnlyList<TexinfoNode> dataType,
        IReadOnlyList<TexinfoNode> name, IReadOnlyList<TexinfoNode> arguments,
        string classPreposition, bool isTyped, SourcePosition position) : base(position)
    {
        CommandName = commandName ?? string.Empty;
        Category = category ?? new List<TexinfoNode>();
        ClassName = className ?? new List<TexinfoNode>();
        DataType = dataType ?? new List<TexinfoNode>();
        Name = name ?? new List<TexinfoNode>();
        Arguments = arguments ?? new List<TexinfoNode>();
        ClassPreposition = classPreposition ?? string.Empty;
        IsTyped = isTyped;
    }

    /// <summary>The command that produced the line, without <c>@</c>.</summary>
    public string CommandName { get; }

    /// <summary>The category of the entity, such as "Function" or "User Option".</summary>
    public IReadOnlyList<TexinfoNode> Category { get; }

    /// <summary>The class the entity belongs to; empty for the commands that name none.</summary>
    public IReadOnlyList<TexinfoNode> ClassName { get; }

    /// <summary>The entity's data type; empty for the untyped definition commands.</summary>
    public IReadOnlyList<TexinfoNode> DataType { get; }

    /// <summary>The name of the entity being defined.</summary>
    public IReadOnlyList<TexinfoNode> Name { get; }

    /// <summary>The entity's arguments or attributes; empty when it takes none.</summary>
    public IReadOnlyList<TexinfoNode> Arguments { get; }

    /// <summary>The word joining the category to the class: "of" for variables, "on" for methods.</summary>
    public string ClassPreposition { get; }

    /// <summary>
    /// True for the <c>@deftype...</c> commands, whose whole heading line is computer text; the
    /// untyped commands set their arguments as metasyntactic variables instead.
    /// </summary>
    public bool IsTyped { get; }

    /// <summary>
    /// The index entry this line files, or null for <c>@defline</c> and <c>@deftypeline</c>, which
    /// exist precisely so that a definition can be written without one.
    /// </summary>
    /// <remarks>
    /// It is one of the <see cref="ChildNodes"/> so that every entry the document collects is
    /// reachable from the tree, which is what guarantees a printed index has a marker to link back
    /// to. Its content is the same node instances as <see cref="Name"/> and
    /// <see cref="ClassName"/>, so a walk meets them twice; every walk in this library either
    /// collects into a set or is idempotent, and the emitter writes the heading line from the
    /// parts rather than by walking.
    /// </remarks>
    public IndexEntryNode IndexEntry { get; set; }

    /// <inheritdoc/>
    public override TexinfoNodePlacement Placement => TexinfoNodePlacement.Block;

    /// <inheritdoc/>
    public override IEnumerable<TexinfoNode> ChildNodes
    {
        get
        {
            foreach (TexinfoNode node in Category)
            {
                yield return node;
            }
            foreach (TexinfoNode node in ClassName)
            {
                yield return node;
            }
            foreach (TexinfoNode node in DataType)
            {
                yield return node;
            }
            foreach (TexinfoNode node in Name)
            {
                yield return node;
            }
            foreach (TexinfoNode node in Arguments)
            {
                yield return node;
            }
            if (IndexEntry != null)
            {
                yield return IndexEntry;
            }
        }
    }

    /// <summary>Formats the node for diagnostics.</summary>
    public override string ToString() => $"@{CommandName} {InlineText(Name)}".TrimEnd();

    private static string InlineText(IReadOnlyList<TexinfoNode> nodes)
    {
        foreach (TexinfoNode node in nodes)
        {
            if (node is TextNode text)
            {
                return text.Text;
            }
        }
        return string.Empty;
    }
}
