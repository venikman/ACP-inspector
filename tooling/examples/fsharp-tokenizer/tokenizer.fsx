#I "../../../src/bin/Debug/net9.0"
#r "ACP.dll"

open Acp.FSharpTokenizer

// ---- Example usage ----

let sample =
    String.concat
        "\n"
        [ "module Demo"
          ""
          "let add x y ="
          "    // Adds two numbers"
          "    x + y"
          ""
          "let quoted = \"hello \\\"world\\\"\""
          "let verbatim = @\"c:\\temp\\file.txt\""
          "let raw = \"\"\"raw \\\"string\\\" ok\"\"\""
          "let c = 'x'"
          "let r = [1..3]"
          ""
          "let pipeline ="
          "    [1;2;3]"
          "    |> List.map (fun n -> n + 1)" ]

let tokens = tokenize sample

printfn "Tokens:"

tokens
|> List.iter (fun t ->
    match t with
    | Token.Keyword k -> printfn "Keyword(%s)" k
    | Token.Identifier i -> printfn "Identifier(%s)" i
    | Token.Number n -> printfn "Number(%s)" n
    | Token.CharLit ch -> printfn "Char(%s)" ch
    | Token.StringLit s -> printfn "String(%s)" s
    | Token.Operator op -> printfn "Operator(%s)" op
    | Token.Delimiter d -> printfn "Delimiter(%c)" d
    | Token.Directive d -> printfn "Directive(%s)" d
    | Token.Comment c -> printfn "Comment(%s)" c
    | Token.Whitespace _ -> () // skip in display
    | Token.Unknown ch -> printfn "Unknown(%c)" ch)
