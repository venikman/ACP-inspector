namespace Acp

open System
open System.Diagnostics
open System.Diagnostics.Metrics

[<RequireQualifiedAccess>]
module Observability =

    [<Literal>]
    let ActivitySourceName = "ACP.Sentinel"

    [<Literal>]
    let MeterName = "ACP.Sentinel"

    [<Literal>]
    let ServiceNameTag = "service.name"

    [<Literal>]
    let TransportTag = "acp.transport"

    [<Literal>]
    let DirectionTag = "acp.direction"

    [<Literal>]
    let MethodTag = "acp.method"

    [<Literal>]
    let SessionIdTag = "acp.session_id"

    [<Literal>]
    let JsonRpcIdTag = "jsonrpc.id"

    [<Literal>]
    let ValidationLaneTag = "acp.validation.lane"

    [<Literal>]
    let ValidationSeverityTag = "acp.validation.severity"

    [<Literal>]
    let ErrorTag = "error"

    [<Literal>]
    let ErrorTypeTag = "error.type"

    [<Literal>]
    let ErrorMessageTag = "error.message"

    let activitySource = new ActivitySource(ActivitySourceName)
    let meter = new Meter(MeterName)

    let startActivity (name: string) (kind: ActivityKind) (tags: (string * obj) list) : Activity | null =
        if activitySource.HasListeners() then
            match activitySource.StartActivity(name, kind) with
            | null -> null
            | activity ->
                for (k, v) in tags do
                    activity.SetTag(k, v) |> ignore

                activity
        else
            null

    /// Sanitize exception message to avoid leaking sensitive data in telemetry.
    /// Truncates long messages and removes potential credential patterns.
    let sanitizeExceptionMessage (message: string) =
        if String.IsNullOrEmpty(message) then
            "[empty]"
        else
            // Truncate to reasonable length
            let truncated =
                if message.Length > 200 then
                    message.Substring(0, 200) + "..."
                else
                    message
            // Remove potential sensitive patterns (basic sanitization)
            truncated.Replace(
                Environment.GetEnvironmentVariable("HOME")
                |> Option.ofObj
                |> Option.defaultValue "~",
                "~"
            )

    let inline recordException (activity: Activity | null) (ex: exn) =
        match activity with
        | null -> ()
        | activity ->
            activity.SetTag(ErrorTag, true) |> ignore
            activity.SetTag(ErrorTypeTag, ex.GetType().Name) |> ignore
            activity.SetTag(ErrorMessageTag, sanitizeExceptionMessage ex.Message) |> ignore

    module Metrics =

        let transportSendCount =
            meter.CreateCounter<int64>("acp.transport.send.count", unit = "{message}", description = "Messages sent.")

        let transportSendBytes =
            meter.CreateCounter<int64>("acp.transport.send.bytes", unit = "By", description = "Bytes sent (UTF-8).")

        let transportSendDurationMs =
            meter.CreateHistogram<double>(
                "acp.transport.send.duration_ms",
                unit = "ms",
                description = "Time spent in ITransport.SendAsync."
            )

        let transportReceiveCount =
            meter.CreateCounter<int64>(
                "acp.transport.receive.count",
                unit = "{message}",
                description = "Messages received."
            )

        let transportReceiveBytes =
            meter.CreateCounter<int64>(
                "acp.transport.receive.bytes",
                unit = "By",
                description = "Bytes received (UTF-8)."
            )

        let transportReceiveDurationMs =
            meter.CreateHistogram<double>(
                "acp.transport.receive.duration_ms",
                unit = "ms",
                description = "Time spent in ITransport.ReceiveAsync."
            )

        let codecEncodeErrorCount =
            meter.CreateCounter<int64>(
                "acp.codec.encode.error.count",
                unit = "{error}",
                description = "Codec encode errors."
            )

        let codecDecodeErrorCount =
            meter.CreateCounter<int64>(
                "acp.codec.decode.error.count",
                unit = "{error}",
                description = "Codec decode errors."
            )

        let connectionRequestDurationMs =
            meter.CreateHistogram<double>(
                "acp.connection.request.duration_ms",
                unit = "ms",
                description = "End-to-end request duration (encode+send+receive+decode)."
            )

        let validationRunDurationMs =
            meter.CreateHistogram<double>(
                "acp.validation.run.duration_ms",
                unit = "ms",
                description = "Time spent in Validation.runWithValidation."
            )

        let validationFindingCount =
            meter.CreateCounter<int64>(
                "acp.validation.finding.count",
                unit = "{finding}",
                description = "Validation findings emitted by lane and severity."
            )

    let private buildTagList
        (transportName: string option)
        (direction: string option)
        (methodName: string option)
        (requestId: string option)
        (sessionId: string option)
        =
        let mutable tags = TagList()

        transportName |> Option.iter (fun v -> tags.Add(TransportTag, v))
        direction |> Option.iter (fun v -> tags.Add(DirectionTag, v))
        methodName |> Option.iter (fun v -> tags.Add(MethodTag, v))
        requestId |> Option.iter (fun v -> tags.Add(JsonRpcIdTag, v))
        sessionId |> Option.iter (fun v -> tags.Add(SessionIdTag, v))

        tags

    let recordTransportSend
        (transportName: string)
        (direction: string)
        (methodName: string option)
        (requestId: string option)
        (bytes: int64)
        (durationMs: double)
        =
        let mutable tags =
            buildTagList (Some transportName) (Some direction) methodName requestId None

        Metrics.transportSendCount.Add(1L, &tags)
        Metrics.transportSendBytes.Add(bytes, &tags)
        Metrics.transportSendDurationMs.Record(durationMs, &tags)

    let recordTransportSendDuration
        (transportName: string)
        (direction: string)
        (methodName: string option)
        (requestId: string option)
        (durationMs: double)
        =
        let mutable tags =
            buildTagList (Some transportName) (Some direction) methodName requestId None

        Metrics.transportSendDurationMs.Record(durationMs, &tags)

    let recordTransportReceive
        (transportName: string)
        (direction: string)
        (methodName: string option)
        (requestId: string option)
        (bytesOpt: int64 option)
        (durationMs: double)
        =
        let mutable tags =
            buildTagList (Some transportName) (Some direction) methodName requestId None

        Metrics.transportReceiveDurationMs.Record(durationMs, &tags)

        match bytesOpt with
        | None -> ()
        | Some bytes ->
            Metrics.transportReceiveCount.Add(1L, &tags)
            Metrics.transportReceiveBytes.Add(bytes, &tags)

    let recordCodecEncodeError
        (transportName: string option)
        (direction: string option)
        (methodName: string option)
        (requestId: string option)
        =
        let mutable tags = buildTagList transportName direction methodName requestId None
        Metrics.codecEncodeErrorCount.Add(1L, &tags)

    let recordCodecDecodeError
        (transportName: string option)
        (direction: string option)
        (methodName: string option)
        (requestId: string option)
        =
        let mutable tags = buildTagList transportName direction methodName requestId None
        Metrics.codecDecodeErrorCount.Add(1L, &tags)

    let recordConnectionRequestDuration
        (transportName: string)
        (direction: string)
        (methodName: string option)
        (requestId: string option)
        (durationMs: double)
        =
        let mutable tags =
            buildTagList (Some transportName) (Some direction) methodName requestId None

        Metrics.connectionRequestDurationMs.Record(durationMs, &tags)

    let recordValidationRun (sessionId: string) (durationMs: double) =
        let mutable tags = buildTagList None None None None (Some sessionId)
        Metrics.validationRunDurationMs.Record(durationMs, &tags)

    let recordValidationFinding (sessionId: string) (lane: string) (severity: string) =
        let mutable tags = buildTagList None None None None (Some sessionId)
        tags.Add(ValidationLaneTag, lane)
        tags.Add(ValidationSeverityTag, severity)
        Metrics.validationFindingCount.Add(1L, &tags)

    let dispose () =
        activitySource.Dispose()
        meter.Dispose()
