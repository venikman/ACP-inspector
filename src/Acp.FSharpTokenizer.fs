namespace Acp

open System

/// Small, dependency-free tokenizer for a useful F# subset.
/// Recognizes keywords, identifiers, numbers, chars, strings, operators,
/// delimiters, comments, whitespace, and unknown chars.
module FSharpTokenizer =

    [<RequireQualifiedAccess>]
    type Token =
        | Keyword of string
        | Identifier of string
        | Number of string
        | CharLit of string
        | StringLit of string
        | Operator of string
        | Delimiter of char
        | Directive of string
        | Comment of string
        | Whitespace of string
        | Unknown of char

    /// Absolute position in the original input.
    /// - `index` is a 0-based character offset into the input string.
    /// - `line` / `column` are 1-based.
    /// - CRLF (`\r\n`) is treated as a single newline for line/column accounting.
    /// - Tabs count as a single column step (no visual tab-width expansion).
    type Position =
        { index: int // 0-based absolute index
          line: int // 1-based line number
          column: int } // 1-based column number

    /// Span of a token in the original input.
    /// - End is exclusive at `start.index + length`.
    /// - Spans from `tokenizeWithSpans` are ordered, non-overlapping,
    ///   and cover the full input.
    type Span = { start: Position; length: int }

    type SpannedToken = { token: Token; span: Span }

    let tokenizeWithSpans (input: string) : SpannedToken list =
        let len = input.Length

        let keywords =
            set
                [ "let"
                  "in"
                  "do"
                  "if"
                  "then"
                  "else"
                  "match"
                  "with"
                  "type"
                  "module"
                  "namespace"
                  "open"
                  "rec"
                  "mutable"
                  "use"
                  "and"
                  "or"
                  "as"
                  "new"
                  "null"
                  "fun"
                  "function"
                  "yield"
                  "return"
                  "try"
                  "finally"
                  "for"
                  "while"
                  "to"
                  "downto"
                  "member"
                  "static"
                  "override"
                  "abstract"
                  "interface"
                  "inherit"
                  "inline"
                  "extern"
                  "val"
                  "sig"
                  "struct"
                  "begin"
                  "end"
                  "when"
                  "of"
                  "exception"
                  "upcast"
                  "downcast"
                  "lazy"
                  "true"
                  "false" ]

        // Order matters: longest operators first.
        let operators =
            [ "|||>"
              "||>"
              "<|||"
              "<||"
              "<|"
              "<<<"
              ">>>"
              ">>"
              "<<"
              "|||"
              "&&&"
              "^^^"
              "**"
              "!"
              "..."
              ".."
              "<|>"
              ">>="
              "<<="
              "|>>"
              "<||"
              "||"
              "<-"
              "->"
              "|>"
              "::"
              "&&"
              "=="
              "!="
              ">="
              "<="
              "??"
              "="
              ">"
              "<"
              "+"
              "-"
              "*"
              "/"
              "%"
              "@"
              "|"
              "&"
              ":"
              "."
              "^" ]
            |> List.sortByDescending (fun op -> op.Length)

        let delimiters = set [ '('; ')'; '['; ']'; '{'; '}'; ','; ';' ]

        let isIdentStart (c: char) = Char.IsLetter c || c = '_'

        let isIdentCont (c: char) =
            Char.IsLetterOrDigit c || c = '_' || c = '\''

        let startsWith (s: string) (i: int) =
            i + s.Length <= len && input.Substring(i, s.Length) = s

        let tryMatchOperator i =
            operators |> List.tryFind (fun op -> startsWith op i)

        let advancePos (pos: Position) (startIdx: int) (endIdx: int) =
            let mutable line = pos.line
            let mutable col = pos.column
            let mutable k = startIdx

            while k < endIdx do
                match input.[k] with
                | '\r' ->
                    if k + 1 < endIdx && input.[k + 1] = '\n' then
                        k <- k + 1

                    line <- line + 1
                    col <- 1
                | '\n' ->
                    line <- line + 1
                    col <- 1
                | _ -> col <- col + 1

                k <- k + 1

            { index = endIdx
              line = line
              column = col }

        let parseRegularString (quoteIndex: int) =
            let mutable j = quoteIndex + 1
            let mutable escaped = false
            let mutable closed = false

            while j < len && not closed do
                let ch = input.[j]

                if escaped then
                    escaped <- false
                    j <- j + 1
                elif ch = '\\' then
                    escaped <- true
                    j <- j + 1
                elif ch = '"' then
                    closed <- true
                    j <- j + 1
                else
                    j <- j + 1

            j

        let parseVerbatimString (quoteIndex: int) =
            let mutable j = quoteIndex + 1
            let mutable closed = false

            while j < len && not closed do
                if input.[j] = '"' then
                    if j + 1 < len && input.[j + 1] = '"' then
                        j <- j + 2 // doubled quote inside verbatim string
                    else
                        closed <- true
                        j <- j + 1
                else
                    j <- j + 1

            j

        let parseTripleQuoted (quoteIndex: int) =
            let searchFrom = quoteIndex + 3

            if searchFrom >= len then
                len
            else
                let endStart = input.IndexOf("\"\"\"", searchFrom, StringComparison.Ordinal)
                if endStart >= 0 then endStart + 3 else len

        let parseCharLiteral (start: int) =
            let mutable j = start + 1
            let mutable escaped = false
            let mutable closed = false

            while j < len && not closed do
                let ch = input.[j]

                if escaped then
                    escaped <- false
                    j <- j + 1
                elif ch = '\\' then
                    escaped <- true
                    j <- j + 1
                elif ch = '\'' then
                    closed <- true
                    j <- j + 1
                else
                    j <- j + 1

            if closed then Some j else None

        let parseNumber (start: int) =
            let isDecDigitOrUnderscore (c: char) = Char.IsDigit c || c = '_'

            let isHexDigitOrUnderscore (c: char) =
                Char.IsDigit c || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || c = '_'

            let isBinDigitOrUnderscore (c: char) = c = '0' || c = '1' || c = '_'
            let isOctDigitOrUnderscore (c: char) = (c >= '0' && c <= '7') || c = '_'

            let inline scanWhile j pred =
                let mutable k = j

                while k < len && pred input.[k] do
                    k <- k + 1

                k

            let mutable j = start

            if startsWith "0x" start || startsWith "0X" start then
                j <- scanWhile (start + 2) isHexDigitOrUnderscore
            elif startsWith "0b" start || startsWith "0B" start then
                j <- scanWhile (start + 2) isBinDigitOrUnderscore
            elif startsWith "0o" start || startsWith "0O" start then
                j <- scanWhile (start + 2) isOctDigitOrUnderscore
            else
                j <- scanWhile start isDecDigitOrUnderscore

                if j < len && input.[j] = '.' then
                    if j + 1 < len && input.[j + 1] = '.' then
                        ()
                    elif j + 1 < len && Char.IsDigit input.[j + 1] then
                        j <- scanWhile (j + 1) isDecDigitOrUnderscore
                    else
                        j <- j + 1 // allow trailing dot float (1.)

                if j < len && (input.[j] = 'e' || input.[j] = 'E') then
                    let mutable k = j + 1

                    if k < len && (input.[k] = '+' || input.[k] = '-') then
                        k <- k + 1

                    let kStart = k
                    k <- scanWhile k isDecDigitOrUnderscore

                    if k > kStart then
                        j <- k

            let mutable k = j

            while k < len && Char.IsLetter input.[k] do
                k <- k + 1

            k

        let isDirectiveStart (i: int) (pos: Position) =
            if i < 0 || i >= len || input.[i] <> '#' then
                false
            elif pos.column = 1 then
                true
            else
                // Only whitespace before '#' on the same line.
                let mutable j = i - 1
                let mutable onlyWs = true

                while j >= 0 && input.[j] <> '\n' && input.[j] <> '\r' do
                    if not (Char.IsWhiteSpace input.[j]) then
                        onlyWs <- false

                    j <- j - 1

                onlyWs

        let rec parseBlockComment i depth =
            if i >= len then
                len
            elif i + 1 < len && input.[i] = '(' && input.[i + 1] = '*' then
                parseBlockComment (i + 2) (depth + 1)
            elif i + 1 < len && input.[i] = '*' && input.[i + 1] = ')' then
                if depth = 1 then
                    i + 2
                else
                    parseBlockComment (i + 2) (depth - 1)
            else
                parseBlockComment (i + 1) depth

        let mkToken (startPos: Position) (endIdx: int) (tok: Token) acc =
            let span =
                { start = startPos
                  length = endIdx - startPos.index }

            { token = tok; span = span } :: acc

        let rec loop (pos: Position) acc =
            let i = pos.index

            if i >= len then
                List.rev acc
            else
                let startPos = pos
                let c = input.[i]

                // Whitespace (includes newlines)
                if Char.IsWhiteSpace c then
                    let mutable j = i

                    while j < len && Char.IsWhiteSpace input.[j] do
                        j <- j + 1

                    let ws = input.Substring(i, j - i)
                    let acc' = mkToken startPos j (Token.Whitespace ws) acc
                    loop (advancePos pos i j) acc'

                // F# interactive / preprocessor directive (#r, #load, #if, ...)
                elif c = '#' && isDirectiveStart i pos then
                    let mutable j = i + 1

                    while j < len && input.[j] <> '\n' && input.[j] <> '\r' do
                        j <- j + 1

                    let text = input.Substring(i, j - i)
                    let acc' = mkToken startPos j (Token.Directive text) acc
                    loop (advancePos pos i j) acc'

                // Line comment //
                elif c = '/' && i + 1 < len && input.[i + 1] = '/' then
                    let mutable j = i + 2

                    while j < len && input.[j] <> '\n' do
                        j <- j + 1

                    let text = input.Substring(i, j - i)
                    let acc' = mkToken startPos j (Token.Comment text) acc
                    loop (advancePos pos i j) acc'

                // Block comment (* ... *) (nested)
                elif c = '(' && i + 1 < len && input.[i + 1] = '*' then
                    let endIdx = parseBlockComment (i + 2) 1
                    let text = input.Substring(i, endIdx - i)
                    let acc' = mkToken startPos endIdx (Token.Comment text) acc
                    loop (advancePos pos i endIdx) acc'

                // Interpolated raw triple-quoted string $"""..."""
                elif startsWith "$\"\"\"" i then
                    let quoteIndex = i + 1
                    let endIdx = parseTripleQuoted quoteIndex
                    let text = input.Substring(i, endIdx - i)
                    let acc' = mkToken startPos endIdx (Token.StringLit text) acc
                    loop (advancePos pos i endIdx) acc'

                // Raw triple-quoted string """..."""
                elif startsWith "\"\"\"" i then
                    let quoteIndex = i
                    let endIdx = parseTripleQuoted quoteIndex
                    let text = input.Substring(i, endIdx - i)
                    let acc' = mkToken startPos endIdx (Token.StringLit text) acc
                    loop (advancePos pos i endIdx) acc'

                // Interpolated verbatim string $@"...""..."
                elif startsWith "$@\"" i then
                    let quoteIndex = i + 2
                    let endIdx = parseVerbatimString quoteIndex
                    let text = input.Substring(i, endIdx - i)
                    let acc' = mkToken startPos endIdx (Token.StringLit text) acc
                    loop (advancePos pos i endIdx) acc'

                // Verbatim string @"...""..."
                elif startsWith "@\"" i then
                    let quoteIndex = i + 1
                    let endIdx = parseVerbatimString quoteIndex
                    let text = input.Substring(i, endIdx - i)
                    let acc' = mkToken startPos endIdx (Token.StringLit text) acc
                    loop (advancePos pos i endIdx) acc'

                // Interpolated regular string $"..."
                elif startsWith "$\"" i then
                    let quoteIndex = i + 1
                    let endIdx = parseRegularString quoteIndex
                    let text = input.Substring(i, endIdx - i)
                    let acc' = mkToken startPos endIdx (Token.StringLit text) acc
                    loop (advancePos pos i endIdx) acc'

                // Regular string "..."
                elif c = '"' then
                    let quoteIndex = i
                    let endIdx = parseRegularString quoteIndex
                    let text = input.Substring(i, endIdx - i)
                    let acc' = mkToken startPos endIdx (Token.StringLit text) acc
                    loop (advancePos pos i endIdx) acc'

                // Char literal 'a' or '\n'. If no closing quote, treat as type-var/identifier.
                elif c = '\'' then
                    match parseCharLiteral i with
                    | Some endIdx ->
                        let text = input.Substring(i, endIdx - i)
                        let acc' = mkToken startPos endIdx (Token.CharLit text) acc
                        loop (advancePos pos i endIdx) acc'
                    | None when i + 1 < len && isIdentStart input.[i + 1] ->
                        let mutable j = i + 2

                        while j < len && isIdentCont input.[j] do
                            j <- j + 1

                        let id = input.Substring(i, j - i)
                        let acc' = mkToken startPos j (Token.Identifier id) acc
                        loop (advancePos pos i j) acc'
                    | None ->
                        let acc' = mkToken startPos (i + 1) (Token.Unknown c) acc
                        loop (advancePos pos i (i + 1)) acc'

                // Number literal (int/float/hex/bin/oct w/ underscores & suffixes)
                elif Char.IsDigit c then
                    let endIdx = parseNumber i
                    let num = input.Substring(i, endIdx - i)
                    let acc' = mkToken startPos endIdx (Token.Number num) acc
                    loop (advancePos pos i endIdx) acc'

                // Identifier or keyword
                elif isIdentStart c then
                    let mutable j = i + 1

                    while j < len && isIdentCont input.[j] do
                        j <- j + 1

                    let id = input.Substring(i, j - i)

                    let tok =
                        if keywords.Contains id then
                            Token.Keyword id
                        else
                            Token.Identifier id

                    let acc' = mkToken startPos j tok acc
                    loop (advancePos pos i j) acc'

                // Backtick identifier `foo bar`
                elif c = '`' then
                    let endTick = input.IndexOf('`', i + 1)

                    if endTick > i then
                        let id = input.Substring(i, endTick - i + 1)
                        let endIdx = endTick + 1
                        let acc' = mkToken startPos endIdx (Token.Identifier id) acc
                        loop (advancePos pos i endIdx) acc'
                    else
                        let acc' = mkToken startPos (i + 1) (Token.Unknown c) acc
                        loop (advancePos pos i (i + 1)) acc'

                // Operators / delimiters / unknown
                else
                    match tryMatchOperator i with
                    | Some op ->
                        let endIdx = i + op.Length
                        let acc' = mkToken startPos endIdx (Token.Operator op) acc
                        loop (advancePos pos i endIdx) acc'
                    | None when delimiters.Contains c ->
                        let endIdx = i + 1
                        let acc' = mkToken startPos endIdx (Token.Delimiter c) acc
                        loop (advancePos pos i endIdx) acc'
                    | None ->
                        let endIdx = i + 1
                        let acc' = mkToken startPos endIdx (Token.Unknown c) acc
                        loop (advancePos pos i endIdx) acc'

        loop { index = 0; line = 1; column = 1 } []

    let tokenize (input: string) : Token list =
        tokenizeWithSpans input |> List.map (fun st -> st.token)
