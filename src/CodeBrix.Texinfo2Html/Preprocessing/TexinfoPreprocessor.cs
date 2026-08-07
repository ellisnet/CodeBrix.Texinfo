using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodeBrix.Texinfo2Html.Diagnostics;
using CodeBrix.Texinfo2Html.Lexing;
using CodeBrix.Texinfo2Html.Sources;

namespace CodeBrix.Texinfo2Html.Preprocessing;

/// <summary>
/// The Texinfo preprocessing engine: splices <c>@include</c> files (with search paths),
/// maintains <c>@set</c>/<c>@clear</c> flags and substitutes <c>@value</c>, evaluates format
/// conditionals against a <see cref="ConditionalProfile"/>, skips raw output blocks with a
/// warning, drops comments and <c>@ignore</c> blocks, and expands <c>@macro</c>/<c>@rmacro</c>
/// definitions (with <c>@unmacro</c> and <c>@alias</c>). Problems produce warnings, never
/// exceptions. The output is an expanded token stream ready for parsing.
/// </summary>
internal sealed class TexinfoPreprocessor
{
    private static readonly HashSet<string> FormatNames =
        new HashSet<string>(StringComparer.Ordinal) { "tex", "html", "info", "plaintext", "xml", "docbook", "latex" };

    private static readonly HashSet<string> SkippedRawBlocks =
        new HashSet<string>(StringComparer.Ordinal) { "tex", "html", "xml", "docbook", "latex" };

    private sealed class Frame
    {
        public Frame(List<TexinfoToken> tokens)
        {
            Tokens = tokens;
        }

        public List<TexinfoToken> Tokens { get; }
        public int Index;
        public string FilePath;
        public string MacroName;
        public bool SuppressFirstLineStart;
        public readonly List<string> OpenConditionals = new List<string>();
    }

    private readonly PreprocessorOptions _options;
    private readonly TexinfoWarningCollection _warnings = new TexinfoWarningCollection();
    private readonly Dictionary<string, MacroDefinition> _macros = new Dictionary<string, MacroDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<Frame> _frames = new List<Frame>();
    private readonly List<TexinfoToken> _output = new List<TexinfoToken>();
    private readonly List<string> _defaultSearchPaths = new List<string>();
    private string _documentEncoding = string.Empty;

    /// <summary>Creates a preprocessor with the given options.</summary>
    /// <param name="options">Settings for the run; a fresh instance per run is expected.</param>
    public TexinfoPreprocessor(PreprocessorOptions options)
    {
        _options = options ?? new PreprocessorOptions();
        foreach (KeyValuePair<string, string> pair in _options.PredefinedValues)
        {
            _values[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// Processes a Texinfo file. The file's directory and its parent become the default
    /// include search paths, ahead of the paths configured in the options.
    /// </summary>
    /// <param name="filePath">Path of the main source file.</param>
    public PreprocessedDocument ProcessFile(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            _warnings.Add(TexinfoWarningCategory.Include, new SourcePosition(fullPath, 1, 1),
                "Source file does not exist.");
            return BuildResult();
        }
        AddDefaultSearchPathsFor(fullPath);
        TexinfoSourceText source = TexinfoSourceText.Load(fullPath);
        PushFrame(new Frame(new TexinfoLexer(source, _warnings).Lex()) { FilePath = fullPath });
        Run();
        return BuildResult();
    }

    /// <summary>Processes in-memory Texinfo source.</summary>
    /// <param name="source">The source text to process.</param>
    /// <param name="baseDirectory">
    /// Directory used to seed the default include search paths (itself and its parent), or
    /// null when includes should resolve only against the configured search paths.
    /// </param>
    public PreprocessedDocument Process(TexinfoSourceText source, string baseDirectory)
    {
        if (!string.IsNullOrEmpty(baseDirectory))
        {
            AddDefaultSearchPathsFor(Path.Combine(Path.GetFullPath(baseDirectory), "_"));
        }
        Frame frame = new Frame(new TexinfoLexer(source, _warnings).Lex());
        if (!string.IsNullOrEmpty(baseDirectory))
        {
            frame.FilePath = Path.Combine(Path.GetFullPath(baseDirectory), source.Name);
        }
        PushFrame(frame);
        Run();
        return BuildResult();
    }

    private void AddDefaultSearchPathsFor(string fullFilePath)
    {
        string directory = Path.GetDirectoryName(fullFilePath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }
        _defaultSearchPaths.Add(directory);
        string parent = Path.GetDirectoryName(directory);
        if (!string.IsNullOrEmpty(parent))
        {
            _defaultSearchPaths.Add(parent);
        }
    }

    private PreprocessedDocument BuildResult()
    {
        _output.Add(new TexinfoToken(TexinfoTokenKind.EndOfInput, string.Empty,
            new SourcePosition("(end)", 0, 0), atLineStart: true));
        return new PreprocessedDocument(_output, _warnings, _macros, _values, _aliases, _documentEncoding);
    }

    private void PushFrame(Frame frame) => _frames.Add(frame);

    private Frame CurrentFrame => _frames[_frames.Count - 1];

    private void Run()
    {
        while (_frames.Count > 0)
        {
            Frame frame = CurrentFrame;
            if (frame.Index >= frame.Tokens.Count)
            {
                PopFrame(frame);
                continue;
            }
            TexinfoToken token = ReadToken(frame);
            if (token.Kind == TexinfoTokenKind.EndOfInput)
            {
                PopFrame(frame);
                continue;
            }
            Dispatch(token, frame);
        }
    }

    private void PopFrame(Frame frame)
    {
        foreach (string conditional in frame.OpenConditionals)
        {
            _warnings.Add(TexinfoWarningCategory.Conditional,
                LastPosition(frame), $"'@{conditional}' is missing its '@end {conditional}'.");
        }
        _frames.RemoveAt(_frames.Count - 1);
    }

    private static SourcePosition LastPosition(Frame frame)
        => frame.Tokens.Count > 0 ? frame.Tokens[frame.Tokens.Count - 1].Position : default;

    private TexinfoToken ReadToken(Frame frame)
    {
        TexinfoToken token = frame.Tokens[frame.Index];
        frame.Index++;
        if (frame.SuppressFirstLineStart && token.AtLineStart && token.Position.Line == 1)
        {
            token = CloneWithoutLineStart(token);
        }
        return token;
    }

    private static TexinfoToken CloneWithoutLineStart(TexinfoToken token)
        => new TexinfoToken(token.Kind, token.Value, token.Position, atLineStart: false)
        {
            RawArgument = token.RawArgument,
            RawContent = token.RawContent,
            IsBraceRawBlock = token.IsBraceRawBlock,
            IsWholeLineComment = token.IsWholeLineComment,
            CommentCommand = token.CommentCommand
        };

    private void Dispatch(TexinfoToken token, Frame frame)
    {
        switch (token.Kind)
        {
            case TexinfoTokenKind.Comment:
                return;
            case TexinfoTokenKind.RawBlock:
                DispatchRawBlock(token);
                return;
            case TexinfoTokenKind.EndCommand:
                DispatchEndCommand(token, frame);
                return;
            case TexinfoTokenKind.Command:
                DispatchCommand(token, frame);
                return;
            default:
                _output.Add(token);
                return;
        }
    }

    private void DispatchRawBlock(TexinfoToken token)
    {
        if (token.Value == "macro" || token.Value == "rmacro")
        {
            DefineMacro(token);
            return;
        }
        if (token.Value == "ignore")
        {
            return;
        }
        if (SkippedRawBlocks.Contains(token.Value))
        {
            _warnings.Add(TexinfoWarningCategory.RawBlockSkipped, token.Position,
                $"Raw '@{token.Value}' block skipped; its content cannot be rendered.");
            return;
        }
        _output.Add(token);
    }

    private void DispatchEndCommand(TexinfoToken token, Frame frame)
    {
        if (!IsConditionalCommandName(token.Value))
        {
            _output.Add(token);
            return;
        }
        ConsumeRestOfLine(frame);
        if (frame.OpenConditionals.Count > 0
            && frame.OpenConditionals[frame.OpenConditionals.Count - 1] == token.Value)
        {
            frame.OpenConditionals.RemoveAt(frame.OpenConditionals.Count - 1);
            return;
        }
        _warnings.Add(TexinfoWarningCategory.Conditional, token.Position,
            $"'@end {token.Value}' does not match an open '@{token.Value}'.");
        int last = frame.OpenConditionals.LastIndexOf(token.Value);
        if (last >= 0)
        {
            frame.OpenConditionals.RemoveRange(last, frame.OpenConditionals.Count - last);
        }
    }

    private void DispatchCommand(TexinfoToken token, Frame frame)
    {
        string name = token.Value;

        if (token.AtLineStart)
        {
            switch (name)
            {
                case "include":
                    HandleInclude(token, frame);
                    return;
                case "verbatiminclude":
                    HandleVerbatimInclude(token, frame);
                    return;
                case "set":
                    HandleSet(token, frame);
                    return;
                case "clear":
                    HandleClear(frame);
                    return;
                case "unmacro":
                    HandleUnmacro(frame);
                    return;
                case "alias":
                    HandleAlias(token, frame);
                    return;
                case "documentencoding":
                    HandleDocumentEncoding(token, frame);
                    return;
            }
            if (IsConditionalCommandName(name))
            {
                HandleConditional(token, frame);
                return;
            }
        }

        if (name == "value")
        {
            HandleValue(token, frame);
            return;
        }

        string resolved = ResolveAlias(name, token.Position);
        if (_macros.TryGetValue(resolved, out MacroDefinition macro))
        {
            InvokeMacro(macro, token, frame);
            return;
        }
        if (!string.Equals(resolved, name, StringComparison.Ordinal))
        {
            _output.Add(new TexinfoToken(TexinfoTokenKind.Command, resolved, token.Position, token.AtLineStart));
            return;
        }
        _output.Add(token);
    }

    private string ResolveAlias(string name, SourcePosition position)
    {
        string current = name;
        int hops = 0;
        while (_aliases.TryGetValue(current, out string target))
        {
            current = target;
            hops++;
            if (hops > 16)
            {
                _warnings.Add(TexinfoWarningCategory.Macro, position,
                    $"Alias chain starting at '@{name}' is too deep or circular.");
                return name;
            }
        }
        return current;
    }

    // ----- Directive lines -------------------------------------------------------------------

    /// <summary>
    /// Consumes tokens up to and including the next newline, returning their source text
    /// (without the newline). Stops before EndOfInput.
    /// </summary>
    private static string ConsumeRestOfLine(Frame frame)
    {
        StringBuilder builder = new StringBuilder();
        while (frame.Index < frame.Tokens.Count)
        {
            TexinfoToken token = frame.Tokens[frame.Index];
            if (token.Kind == TexinfoTokenKind.EndOfInput)
            {
                break;
            }
            frame.Index++;
            if (token.Kind == TexinfoTokenKind.Newline)
            {
                break;
            }
            if (token.Kind == TexinfoTokenKind.Comment)
            {
                if (token.IsWholeLineComment)
                {
                    break;
                }
                continue;
            }
            builder.Append(token.ToSourceText());
        }
        return builder.ToString();
    }

    private void HandleInclude(TexinfoToken token, Frame frame)
    {
        string fileName = ConsumeRestOfLine(frame).Trim();
        if (fileName.Length == 0)
        {
            _warnings.Add(TexinfoWarningCategory.Include, token.Position, "'@include' has no file name.");
            return;
        }
        string resolved = ResolveIncludePath(fileName, frame);
        if (resolved == null)
        {
            _warnings.Add(TexinfoWarningCategory.Include, token.Position,
                $"Include file '{fileName}' was not found on the search path.");
            return;
        }
        foreach (Frame active in _frames)
        {
            if (active.FilePath != null
                && string.Equals(active.FilePath, resolved, StringComparison.Ordinal))
            {
                _warnings.Add(TexinfoWarningCategory.Include, token.Position,
                    $"Include file '{fileName}' includes itself; the nested include is skipped.");
                return;
            }
        }
        TexinfoSourceText source = TexinfoSourceText.Load(resolved);
        PushFrame(new Frame(new TexinfoLexer(source, _warnings).Lex()) { FilePath = resolved });
    }

    /// <summary>
    /// Resolves <c>@verbatiminclude</c> here rather than in the parser, because this is where the
    /// include search paths live. The file's content becomes a verbatim raw block, so the parser
    /// handles it exactly as if the document had written <c>@verbatim</c> around it.
    /// </summary>
    private void HandleVerbatimInclude(TexinfoToken token, Frame frame)
    {
        string fileName = ConsumeRestOfLine(frame).Trim();
        if (fileName.Length == 0)
        {
            _warnings.Add(TexinfoWarningCategory.Include, token.Position,
                "'@verbatiminclude' has no file name.");
            return;
        }
        string resolved = ResolveIncludePath(fileName, frame);
        if (resolved == null)
        {
            _warnings.Add(TexinfoWarningCategory.Include, token.Position,
                $"Verbatim include file '{fileName}' was not found on the search path.");
            return;
        }
        string content = TexinfoSourceText.Load(resolved).Text;
        if (content.Length > 0 && !content.EndsWith("\n", StringComparison.Ordinal))
        {
            content += "\n";
        }
        _output.Add(new TexinfoToken(TexinfoTokenKind.RawBlock, "verbatim", token.Position,
            atLineStart: true)
        {
            RawContent = content
        });
    }

    private string ResolveIncludePath(string fileName, Frame frame)
    {
        if (Path.IsPathRooted(fileName))
        {
            return File.Exists(fileName) ? Path.GetFullPath(fileName) : null;
        }
        List<string> directories = new List<string>();
        for (int i = _frames.Count - 1; i >= 0; i--)
        {
            if (_frames[i].FilePath != null)
            {
                string directory = Path.GetDirectoryName(_frames[i].FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    directories.Add(directory);
                }
                break;
            }
        }
        directories.AddRange(_defaultSearchPaths);
        directories.AddRange(_options.IncludeSearchPaths);
        foreach (string directory in directories)
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    private void HandleSet(TexinfoToken token, Frame frame)
    {
        string rest = ConsumeRestOfLine(frame).Trim();
        if (rest.Length == 0)
        {
            _warnings.Add(TexinfoWarningCategory.Value, token.Position, "'@set' has no flag name.");
            return;
        }
        int space = IndexOfWhitespace(rest);
        if (space < 0)
        {
            _values[rest] = string.Empty;
            return;
        }
        _values[rest.Substring(0, space)] = rest.Substring(space + 1).Trim();
    }

    private void HandleClear(Frame frame)
    {
        string name = FirstWord(ConsumeRestOfLine(frame));
        if (name.Length > 0)
        {
            _values.Remove(name);
        }
    }

    private void HandleUnmacro(Frame frame)
    {
        string name = FirstWord(ConsumeRestOfLine(frame));
        if (name.Length > 0)
        {
            _macros.Remove(name);
        }
    }

    private void HandleAlias(TexinfoToken token, Frame frame)
    {
        string rest = ConsumeRestOfLine(frame).Trim();
        int equals = rest.IndexOf('=');
        if (equals <= 0 || equals == rest.Length - 1)
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                "'@alias' requires the form 'new=existing'.");
            return;
        }
        string newName = rest.Substring(0, equals).Trim();
        string existingName = rest.Substring(equals + 1).Trim();
        if (newName.Length == 0 || existingName.Length == 0)
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                "'@alias' requires the form 'new=existing'.");
            return;
        }
        if (KnownTexinfoCommands.Contains(newName))
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"'@alias {newName}' would shadow the built-in '@{newName}'; keeping the built-in.");
            return;
        }
        _aliases[newName] = existingName;
    }

    private void HandleDocumentEncoding(TexinfoToken token, Frame frame)
    {
        string encoding = ConsumeRestOfLine(frame).Trim();
        _documentEncoding = encoding;
        if (encoding.Length > 0
            && !encoding.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)
            && !encoding.Equals("US-ASCII", StringComparison.OrdinalIgnoreCase))
        {
            _warnings.Add(TexinfoWarningCategory.Encoding, token.Position,
                $"'@documentencoding {encoding}' is not supported; text is read as UTF-8.");
        }
    }

    // ----- Conditionals ----------------------------------------------------------------------

    private static bool IsConditionalCommandName(string name)
    {
        if (name == "ifset" || name == "ifclear")
        {
            return true;
        }
        if (!name.StartsWith("if", StringComparison.Ordinal))
        {
            return false;
        }
        string format = name.StartsWith("ifnot", StringComparison.Ordinal)
            ? name.Substring(5)
            : name.Substring(2);
        return FormatNames.Contains(format);
    }

    private void HandleConditional(TexinfoToken token, Frame frame)
    {
        string argument = ConsumeRestOfLine(frame).Trim();
        bool active;
        if (token.Value == "ifset" || token.Value == "ifclear")
        {
            if (argument.Length == 0)
            {
                _warnings.Add(TexinfoWarningCategory.Conditional, token.Position,
                    $"'@{token.Value}' has no flag name; the block is skipped.");
                active = false;
            }
            else
            {
                bool isSet = _values.ContainsKey(FirstWord(argument));
                active = token.Value == "ifset" ? isSet : !isSet;
            }
        }
        else
        {
            active = EvaluateFormatConditional(token.Value);
        }

        if (active)
        {
            frame.OpenConditionals.Add(token.Value);
            return;
        }
        SkipConditionalBlock(token, frame);
    }

    private bool EvaluateFormatConditional(string name)
    {
        bool negated = name.StartsWith("ifnot", StringComparison.Ordinal);
        string format = negated ? name.Substring(5) : name.Substring(2);
        switch (_options.Profile)
        {
            case ConditionalProfile.Html:
                return negated ? format != "html" : format == "html";
            default:
                // Print: all portable @ifnot... branches are taken, and @iftex additionally is
                // (see ConditionalProfile.Print for why both @iftex and @ifnottex are entered).
                return negated || format == "tex";
        }
    }

    private void SkipConditionalBlock(TexinfoToken token, Frame frame)
    {
        int depth = 0;
        while (frame.Index < frame.Tokens.Count)
        {
            TexinfoToken current = frame.Tokens[frame.Index];
            if (current.Kind == TexinfoTokenKind.EndOfInput)
            {
                break;
            }
            frame.Index++;
            if (current.Kind == TexinfoTokenKind.Command
                && current.AtLineStart
                && current.Value == token.Value)
            {
                depth++;
            }
            else if (current.Kind == TexinfoTokenKind.EndCommand && current.Value == token.Value)
            {
                if (depth == 0)
                {
                    ConsumeRestOfLine(frame);
                    return;
                }
                depth--;
            }
        }
        _warnings.Add(TexinfoWarningCategory.Conditional, token.Position,
            $"'@{token.Value}' is missing its '@end {token.Value}'.");
    }

    // ----- Values ----------------------------------------------------------------------------

    private void HandleValue(TexinfoToken token, Frame frame)
    {
        // Expect exactly {NAME}; on any other shape, emit what was seen and warn.
        if (frame.Index + 2 < frame.Tokens.Count
            && frame.Tokens[frame.Index].Kind == TexinfoTokenKind.OpenBrace
            && frame.Tokens[frame.Index + 1].Kind == TexinfoTokenKind.Text
            && frame.Tokens[frame.Index + 2].Kind == TexinfoTokenKind.CloseBrace)
        {
            string name = frame.Tokens[frame.Index + 1].Value.Trim();
            frame.Index += 3;
            if (_values.TryGetValue(name, out string value))
            {
                if (value.Length > 0)
                {
                    PushExpansionFrame($"@value{{{name}}}", value, token, frame);
                }
                return;
            }
            _warnings.Add(TexinfoWarningCategory.Value, token.Position,
                $"'@value{{{name}}}' has no value; the flag was never set.");
            _output.Add(new TexinfoToken(TexinfoTokenKind.Text, $"{{No value for '{name}'}}",
                token.Position, token.AtLineStart));
            return;
        }
        _warnings.Add(TexinfoWarningCategory.Value, token.Position,
            "'@value' is not followed by a braced flag name.");
        _output.Add(token);
    }

    // ----- Macros ----------------------------------------------------------------------------

    private void DefineMacro(TexinfoToken token)
    {
        string argument = token.RawArgument.Trim();
        int nameLength = 0;
        while (nameLength < argument.Length && IsCommandNameChar(argument[nameLength], nameLength == 0))
        {
            nameLength++;
        }
        string name = argument.Substring(0, nameLength);
        if (name.Length == 0)
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"'@{token.Value}' definition has no macro name.");
            return;
        }
        if (KnownTexinfoCommands.Contains(name))
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"'@macro {name}' would redefine the built-in '@{name}'; keeping the built-in.");
            return;
        }

        List<string> parameters = new List<string>();
        string afterName = argument.Substring(nameLength).TrimStart();
        if (afterName.StartsWith("{", StringComparison.Ordinal))
        {
            int close = afterName.IndexOf('}');
            if (close < 0)
            {
                _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                    $"'@macro {name}' parameter list is missing its closing '}}'.");
                close = afterName.Length;
            }
            string parameterList = afterName.Substring(1, Math.Max(0, close - 1));
            foreach (string parameter in parameterList.Split(','))
            {
                string trimmed = parameter.Trim();
                if (trimmed.Length > 0)
                {
                    parameters.Add(trimmed);
                }
            }
        }
        else if (afterName.Length > 0)
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"Unexpected text after '@macro {name}'; it was ignored.");
        }

        string body = token.RawContent;
        if (body.EndsWith("\n", StringComparison.Ordinal))
        {
            body = body.Substring(0, body.Length - 1);
        }
        _macros[name] = new MacroDefinition(name, parameters, body,
            token.Value == "rmacro", token.Position);
    }

    private static bool IsCommandNameChar(char c, bool first)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (!first && c >= '0' && c <= '9');

    private void InvokeMacro(MacroDefinition macro, TexinfoToken token, Frame frame)
    {
        List<string> arguments;
        bool lineForm = false;

        TexinfoToken next = frame.Index < frame.Tokens.Count ? frame.Tokens[frame.Index] : null;
        if (next != null && next.Kind == TexinfoTokenKind.OpenBrace)
        {
            frame.Index++;
            string argumentText = CollectBraceGroupText(token, frame);
            if (macro.Parameters.Count == 0)
            {
                if (argumentText.Trim().Length > 0)
                {
                    _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                        $"'@{macro.Name}' takes no arguments; the argument text was ignored.");
                }
                arguments = new List<string>();
            }
            else
            {
                arguments = MacroArgumentParser.SplitBraceArguments(argumentText, macro.Parameters.Count);
            }
        }
        else if (macro.Parameters.Count == 0)
        {
            arguments = new List<string>();
        }
        else
        {
            arguments = MacroArgumentParser.SplitLineArguments(ConsumeRestOfLine(frame), macro.Parameters.Count);
            lineForm = true;
        }

        if (arguments.Count > macro.Parameters.Count)
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"'@{macro.Name}' was called with {arguments.Count} arguments but takes {macro.Parameters.Count}.");
        }
        else if (arguments.Count < macro.Parameters.Count
                 && !(arguments.Count == 1 && arguments[0].Length == 0))
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"'@{macro.Name}' was called with {arguments.Count} arguments but takes {macro.Parameters.Count}; missing arguments are empty.");
        }

        if (!macro.IsRecursive)
        {
            foreach (Frame active in _frames)
            {
                if (string.Equals(active.MacroName, macro.Name, StringComparison.Ordinal))
                {
                    _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                        $"'@{macro.Name}' calls itself but was defined with '@macro'; use '@rmacro' for recursion. The call was dropped.");
                    return;
                }
            }
        }
        int expansionDepth = 0;
        foreach (Frame active in _frames)
        {
            if (active.MacroName != null)
            {
                expansionDepth++;
            }
        }
        if (expansionDepth >= _options.MaxExpansionDepth)
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"Macro expansion deeper than {_options.MaxExpansionDepth} levels; '@{macro.Name}' was dropped.");
            return;
        }

        List<string> unknownReferences = new List<string>();
        string expansion = MacroArgumentParser.SubstituteBody(macro.Body, macro.Parameters, arguments, unknownReferences);
        foreach (string reference in unknownReferences)
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"Macro '@{macro.Name}' body refers to '\\{reference}\\', which is not one of its parameters.");
        }
        if (lineForm)
        {
            expansion += "\n";
        }
        if (expansion.Length == 0)
        {
            return;
        }
        PushExpansionFrame($"@{macro.Name} expanded at {token.Position}", expansion, token, frame, macro.Name);
    }

    /// <summary>
    /// Collects the source text of a brace group whose OpenBrace was already consumed, up to
    /// the matching CloseBrace, spanning lines when needed.
    /// </summary>
    private string CollectBraceGroupText(TexinfoToken token, Frame frame)
    {
        StringBuilder builder = new StringBuilder();
        int depth = 1;
        while (frame.Index < frame.Tokens.Count)
        {
            TexinfoToken current = frame.Tokens[frame.Index];
            if (current.Kind == TexinfoTokenKind.EndOfInput)
            {
                break;
            }
            frame.Index++;
            if (current.Kind == TexinfoTokenKind.OpenBrace)
            {
                depth++;
            }
            else if (current.Kind == TexinfoTokenKind.CloseBrace)
            {
                depth--;
                if (depth == 0)
                {
                    return builder.ToString();
                }
            }
            builder.Append(current.ToSourceText());
        }
        _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
            "Brace group is missing its closing '}'.");
        return builder.ToString();
    }

    private void PushExpansionFrame(string sourceLabel, string text, TexinfoToken token, Frame frame,
        string macroName = null)
    {
        // Backstop for expansions the per-macro recursion check cannot see, such as a pair of
        // flags whose values reference each other through @value.
        if (_frames.Count >= _options.MaxExpansionDepth + 16)
        {
            _warnings.Add(TexinfoWarningCategory.Macro, token.Position,
                $"Expansion nesting deeper than {_options.MaxExpansionDepth + 16} levels; the expansion was dropped.");
            return;
        }
        TexinfoSourceText source = TexinfoSourceText.FromString(sourceLabel, text);
        Frame expansion = new Frame(new TexinfoLexer(source, _warnings).Lex())
        {
            MacroName = macroName,
            SuppressFirstLineStart = !token.AtLineStart
        };
        PushFrame(expansion);
    }

    // ----- Small helpers ---------------------------------------------------------------------

    private static int IndexOfWhitespace(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }
        return -1;
    }

    private static string FirstWord(string text)
    {
        string trimmed = text.Trim();
        int space = IndexOfWhitespace(trimmed);
        return space < 0 ? trimmed : trimmed.Substring(0, space);
    }
}
