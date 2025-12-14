namespace Acp.Tests

open System
open System.IO
open System.Text.Json
open Xunit

open Acp
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Messaging
open Acp.Protocol
open Acp.Validation

module TraceReplayTests =

    let private traceDir =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "traces"))

    let private tryParseDirection (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "fromclient"
        | "client"
        | "c2a"
        | "c->a" -> Some Codec.Direction.FromClient
        | "fromagent"
        | "agent"
        | "a2c"
        | "a->c" -> Some Codec.Direction.FromAgent
        | _ -> None

    let private tryParseFrame (line: string) : (string * string) option =
        try
            use doc = JsonDocument.Parse(line)

            if doc.RootElement.ValueKind <> JsonValueKind.Object then
                None
            else
                let root = doc.RootElement

                let tryGetString (name: string) =
                    let mutable v = Unchecked.defaultof<JsonElement>

                    if root.TryGetProperty(name, &v) then
                        v.GetString()
                    else
                        null

                match tryGetString "direction", tryGetString "json" with
                | null, _
                | _, null -> None
                | dir, json -> Some(dir, json)
        with _ ->
            None

    let private renderFinding (f: ValidationFinding) =
        let lane = sprintf "%A" f.lane
        let sev = sprintf "%A" f.severity

        let subject =
            match f.subject with
            | Subject.Connection -> "connection"
            | Subject.Session sid -> $"session:{SessionId.value sid}"
            | Subject.PromptTurn(sid, turn) -> $"turn:{SessionId.value sid}#{turn}"
            | Subject.MessageAt(i, _) -> $"msg:{i}"
            | Subject.ToolCall toolCallId -> $"tool:{toolCallId}"

        match f.failure with
        | None ->
            let note = f.note |> Option.defaultValue ""
            $"[{lane}/{sev}] ({subject}) {note}"
        | Some failure -> $"[{lane}/{sev}] {failure.code} ({subject}) {failure.message}"

    [<Fact>]
    let ``trace fixtures replay without Error findings`` () =
        if not (Directory.Exists traceDir) then
            // No fixtures yet; treat as green.
            ()
        else
            let traces =
                Directory.EnumerateFiles(traceDir, "*.jsonl", SearchOption.TopDirectoryOnly)
                |> Seq.toList

            if traces.IsEmpty then
                ()
            else
                for path in traces do
                    let lines = File.ReadAllLines path

                    let mutable codec = Codec.CodecState.empty
                    let mutable messages: Message list = []
                    let decodeErrors = ResizeArray<string>()

                    for idx, line in lines |> Seq.indexed do
                        if String.IsNullOrWhiteSpace line then
                            ()
                        else
                            match tryParseFrame line with
                            | None -> decodeErrors.Add($"{path}:{idx + 1}: invalid trace frame JSON")
                            | Some(dirRaw, json) ->
                                match tryParseDirection dirRaw with
                                | None -> decodeErrors.Add($"{path}:{idx + 1}: unknown direction '{dirRaw}'")
                                | Some dir ->
                                    match Codec.decode dir codec json with
                                    | Error e -> decodeErrors.Add($"{path}:{idx + 1}: codec decode error {e}")
                                    | Ok(codec', msg) ->
                                        codec <- codec'
                                        messages <- messages @ [ msg ]

                    if decodeErrors.Count > 0 then
                        Assert.Fail("Trace decode failures:\n" + String.Join("\n", decodeErrors))

                    let sid = SessionId(Path.GetFileNameWithoutExtension path)
                    let r = runWithValidation sid spec messages false None None

                    let errors = r.findings |> List.filter (fun f -> f.severity = Severity.Error)

                    if not errors.IsEmpty then
                        let detail = errors |> List.map renderFinding |> String.concat "\n"

                        Assert.Fail($"{path}: found Error findings:\n{detail}")
