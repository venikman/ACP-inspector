namespace Acp.Tests

open Xunit

open Acp.FSharpTokenizer

module FSharpTokenizerTests =

    let private stripNoise (tokens: Token list) =
        tokens
        |> List.filter (function
            | Token.Whitespace _ -> false
            | Token.Comment _ -> false
            | _ -> true)

    [<Fact>]
    let ``tokenizes keywords identifiers operators and numbers`` () =
        let input = "let add x = x + 1"
        let actual = tokenize input |> stripNoise

        let expected =
            [ Token.Keyword "let"
              Token.Identifier "add"
              Token.Identifier "x"
              Token.Operator "="
              Token.Identifier "x"
              Token.Operator "+"
              Token.Number "1" ]

        Assert.Equal<Token list>(expected, actual)

    [<Fact>]
    let ``longest operator match wins`` () =
        let input = "a <|> b |> c"
        let actual = tokenize input |> stripNoise

        let expected =
            [ Token.Identifier "a"
              Token.Operator "<|>"
              Token.Identifier "b"
              Token.Operator "|>"
              Token.Identifier "c" ]

        Assert.Equal<Token list>(expected, actual)

    [<Fact>]
    let ``captures nested block comments`` () =
        let input = "(* outer (* inner *) outer2 *) let x = 1"
        let tokens = tokenize input

        match tokens with
        | Token.Comment c :: rest ->
            Assert.Contains("inner", c)
            let rest' = stripNoise rest

            let expectedTail =
                [ Token.Keyword "let"
                  Token.Identifier "x"
                  Token.Operator "="
                  Token.Number "1" ]

            Assert.Equal<Token list>(expectedTail, rest')
        | other -> failwithf "expected leading comment token, got %A" other

    [<Fact>]
    let ``handles regular and verbatim strings`` () =
        let input = "let s = \"hi \\\"there\\\"\" let v = @\"c:\\temp\\file.txt\""
        let actual = tokenize input |> stripNoise

        let strings =
            actual
            |> List.choose (function
                | Token.StringLit s -> Some s
                | _ -> None)

        Assert.Equal(2, strings.Length)
        Assert.StartsWith("\"hi", strings.[0])
        Assert.StartsWith("@\"", strings.[1])

    [<Fact>]
    let ``tokenizes char literals and type variables`` () =
        let input = "let c = 'x' let t : 'a = c"
        let actual = tokenize input |> stripNoise

        let expected =
            [ Token.Keyword "let"
              Token.Identifier "c"
              Token.Operator "="
              Token.CharLit "'x'"
              Token.Keyword "let"
              Token.Identifier "t"
              Token.Operator ":"
              Token.Identifier "'a"
              Token.Operator "="
              Token.Identifier "c" ]

        Assert.Equal<Token list>(expected, actual)

    [<Fact>]
    let ``parses numeric prefixes suffixes exponents and underscores`` () =
        let input = "let nums = [0xFFuy; 0b1010; 1_000_000L; 1.2e-3f; 1.]"
        let actual = tokenize input |> stripNoise

        let numbers =
            actual
            |> List.choose (function
                | Token.Number n -> Some n
                | _ -> None)

        Assert.Equal<string list>([ "0xFFuy"; "0b1010"; "1_000_000L"; "1.2e-3f"; "1." ], numbers)

    [<Fact>]
    let ``range operator not consumed by number`` () =
        let input = "[1..3]"
        let actual = tokenize input |> stripNoise

        let expected =
            [ Token.Delimiter '['
              Token.Number "1"
              Token.Operator ".."
              Token.Number "3"
              Token.Delimiter ']' ]

        Assert.Equal<Token list>(expected, actual)

    [<Fact>]
    let ``handles triple quoted and interpolated strings`` () =
        let input =
            "let a = \"\"\"hi\"\"\" let b = $\"yo\" let c = $@\"c:\\temp\\file.txt\""

        let actual = tokenize input |> stripNoise

        let strings =
            actual
            |> List.choose (function
                | Token.StringLit s -> Some s
                | _ -> None)

        Assert.Equal(3, strings.Length)
        Assert.Equal<string>("\"\"\"hi\"\"\"", strings.[0])
        Assert.Equal<string>("$\"yo\"", strings.[1])
        Assert.Equal<string>("$@\"c:\\temp\\file.txt\"", strings.[2])

    [<Fact>]
    let ``tokenizeWithSpans reports correct line and column`` () =
        let input = "let x = 1\n  let y = x + 2"

        let spanned =
            tokenizeWithSpans input
            |> List.filter (fun st ->
                match st.token with
                | Token.Whitespace _
                | Token.Comment _ -> false
                | _ -> true)

        let actualStarts =
            spanned
            |> List.map (fun st -> st.token, st.span.start.line, st.span.start.column)

        let expectedStarts =
            [ Token.Keyword "let", 1, 1
              Token.Identifier "x", 1, 5
              Token.Operator "=", 1, 7
              Token.Number "1", 1, 9
              Token.Keyword "let", 2, 3
              Token.Identifier "y", 2, 7
              Token.Operator "=", 2, 9
              Token.Identifier "x", 2, 11
              Token.Operator "+", 2, 13
              Token.Number "2", 2, 15 ]

        Assert.Equal<(Token * int * int) list>(expectedStarts, actualStarts)

    [<Fact>]
    let ``tokenizes fsi directives at line start`` () =
        let input = "#r \"foo\"\n  #if DEBUG\nlet x = 1"
        let actual = tokenize input |> stripNoise
        Assert.Contains(Token.Directive "#r \"foo\"", actual)
        Assert.Contains(Token.Directive "#if DEBUG", actual)
