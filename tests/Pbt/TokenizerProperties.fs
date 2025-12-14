namespace Acp.Tests.Pbt

open System
open Xunit
open FsCheck

module P = FsCheck.FSharp.Prop
module G = FsCheck.FSharp.Gen
module A = FsCheck.FSharp.Arb

open Acp.FSharpTokenizer
open Acp.Tests.Pbt.Generators

/// Property-based invariants for FSharpTokenizer spans and trivia stability.
module TokenizerProperties =

    let private config = Generators.PbtConfig.config

    /// Small, mixed-character input generator biased toward useful ACP/F# text.
    let private genInput: Gen<string> =
        let ascii =
            [ 'a' .. 'z' ]
            @ [ 'A' .. 'Z' ]
            @ [ '0' .. '9' ]
            @ [ ' '; '\t'; '\n'; '\r' ]
            @ [ '('; ')'; '['; ']'; '{'; '}'; ','; ';' ]
            @ [ '+'; '-'; '*'; '/'; '='; '<'; '>'; '|'; '&'; ':'; '.'; '^'; '#' ]
            @ [ '\''; '"'; '_'; '`' ]

        let weird = [ '☃'; '§'; '→' ]

        G.sized (fun s ->
            let len = max 0 (min 200 (s * 2))

            G.listOfLength len (G.frequency [ 9, G.elements ascii; 1, G.elements weird ])
            |> G.map (fun cs -> String(cs |> List.toArray)))

    let private arbInput = A.fromGen genInput

    let private stripNoise =
        List.filter (function
            | Token.Whitespace _
            | Token.Comment _ -> false
            | _ -> true)

    let private triviaEdited (input: string) =
        tokenizeWithSpans input
        |> List.map (fun st ->
            match st.token with
            | Token.Whitespace ws -> if ws.Contains("\n") || ws.Contains("\r") then "\n" else " "
            | Token.Comment _ -> "(*c*)"
            | _ -> input.Substring(st.span.start.index, st.span.length))
        |> String.concat ""

    [<Fact>]
    let ``tokenizeWithSpans spans are ordered, contiguous, and cover the input`` () =
        let prop =
            P.forAll arbInput (fun input ->
                let ts = tokenizeWithSpans input

                let okLengths = ts |> List.forall (fun st -> st.span.length > 0)

                let okBounds =
                    ts
                    |> List.forall (fun st ->
                        let s = st.span.start.index
                        s >= 0 && s + st.span.length <= input.Length)

                let okStart =
                    match ts with
                    | [] -> input.Length = 0
                    | h :: _ -> h.span.start.index = 0

                let okContiguous =
                    ts
                    |> List.pairwise
                    |> List.forall (fun (a, b) ->
                        let aEnd = a.span.start.index + a.span.length
                        aEnd = b.span.start.index)

                let okEnd =
                    match ts with
                    | [] -> input.Length = 0
                    | _ ->
                        let last = ts |> List.last
                        last.span.start.index + last.span.length = input.Length

                okLengths && okBounds && okStart && okContiguous && okEnd)

        Check.One(config, prop)

    [<Fact>]
    let ``slicing by spans roundtrips to original text`` () =
        let prop =
            P.forAll arbInput (fun input ->
                let ts = tokenizeWithSpans input

                let rebuilt =
                    ts
                    |> List.map (fun st -> input.Substring(st.span.start.index, st.span.length))
                    |> String.concat ""

                rebuilt = input)

        Check.One(config, prop)

    [<Fact>]
    let ``non-trivia classification is stable under whitespace/comment-only edits`` () =
        let prop =
            P.forAll arbInput (fun input ->
                let edited = triviaEdited input
                let t1 = tokenize input |> stripNoise
                let t2 = tokenize edited |> stripNoise
                t1 = t2)

        Check.One(config, prop)
