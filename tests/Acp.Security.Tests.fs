namespace Acp.Tests

open System
open System.IO
open Xunit

module SecurityTests =

    let private isSuspectBidiOrZeroWidth (c: char) =
        let code = int c

        (code >= 0x202A && code <= 0x202E) // bidi embedding/override controls
        || (code >= 0x2066 && code <= 0x2069) // bidi isolate controls
        || code = 0x200B // zero width space
        || code = 0x200C // zero width non-joiner
        || code = 0x200D // zero width joiner
        || code = 0xFEFF // BOM / zero width no-break space

    let private isTextFile (path: string) =
        match Path.GetExtension(path).ToLowerInvariant() with
        | ".fs"
        | ".fsproj"
        | ".md"
        | ".sh"
        | ".yaml"
        | ".yml"
        | ".json"
        | ".txt" -> true
        | _ -> false

    [<Fact>]
    let ``repo contains no bidi or zero-width control characters`` () =
        let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

        let excludedParts =
            [ "/bin/"; "/obj/"; "/.git/"; "/vendor/"; "/tests/TestResults/" ]

        let files =
            Directory.EnumerateFiles(repoRoot, "*.*", SearchOption.AllDirectories)
            |> Seq.filter isTextFile
            |> Seq.filter (fun p ->
                let norm = p.Replace('\\', '/')
                excludedParts |> List.forall (fun part -> not (norm.Contains(part))))
            |> Seq.toList

        let offenders =
            files
            |> List.choose (fun path ->
                try
                    let text = File.ReadAllText path

                    if text |> Seq.exists isSuspectBidiOrZeroWidth then
                        Some path
                    else
                        None
                with _ ->
                    None)

        Assert.True(offenders.IsEmpty, "Found bidi / zero-width controls in:\n" + String.Join("\n", offenders))
