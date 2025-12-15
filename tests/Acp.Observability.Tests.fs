namespace Acp.Tests

open System
open System.Diagnostics
open System.Diagnostics.Metrics
open Xunit

open Acp

module ObservabilityTests =

    [<Fact>]
    let ``startActivity creates activity and sets tags`` () =
        let tagValue = Guid.NewGuid().ToString("n")

        use listener = new ActivityListener()

        listener.ShouldListenTo <- fun source -> source.Name = Observability.ActivitySourceName

        listener.Sample <- fun _ -> ActivitySamplingResult.AllDataAndRecorded

        ActivitySource.AddActivityListener(listener)

        use activity =
            Observability.startActivity "acp.test.activity" ActivityKind.Internal [ Observability.MethodTag, tagValue ]

        Assert.False(isNull activity)
        Assert.Equal(tagValue, activity.GetTagItem(Observability.MethodTag) :?> string)

    [<Fact>]
    let ``recordTransportSend emits send metrics with tags`` () =
        let transportName = "ObsTestTransport"
        let direction = "fromClient"
        let methodName = "obs-test-method-" + Guid.NewGuid().ToString("n")
        let requestId = "obs-test-id-" + Guid.NewGuid().ToString("n")

        let mutable sawCount = false
        let mutable sawBytes = false
        let mutable sawDuration = false

        let hasTag (tags: System.Collections.Generic.KeyValuePair<string, obj>[]) (key: string) (value: string) =
            tags
            |> Array.exists (fun kv ->
                if kv.Key <> key then
                    false
                else
                    match kv.Value with
                    | :? string as s -> s = value
                    | _ -> false)

        use listener = new MeterListener()

        listener.InstrumentPublished <-
            fun instrument listener ->
                if instrument.Meter.Name = Observability.MeterName then
                    listener.EnableMeasurementEvents(instrument)

        listener.SetMeasurementEventCallback<int64>(fun instrument measurement tags _ ->
            let tagsArray = tags.ToArray()

            if
                hasTag tagsArray Observability.TransportTag transportName
                && hasTag tagsArray Observability.DirectionTag direction
                && hasTag tagsArray Observability.MethodTag methodName
                && hasTag tagsArray Observability.JsonRpcIdTag requestId
            then
                match instrument.Name with
                | "acp.transport.send.count" ->
                    if measurement = 1L then
                        sawCount <- true
                | "acp.transport.send.bytes" ->
                    if measurement = 123L then
                        sawBytes <- true
                | _ -> ())

        listener.SetMeasurementEventCallback<double>(fun instrument measurement tags _ ->
            let tagsArray = tags.ToArray()

            if
                hasTag tagsArray Observability.TransportTag transportName
                && hasTag tagsArray Observability.DirectionTag direction
                && hasTag tagsArray Observability.MethodTag methodName
                && hasTag tagsArray Observability.JsonRpcIdTag requestId
            then
                match instrument.Name with
                | "acp.transport.send.duration_ms" ->
                    if Math.Abs(measurement - 5.0) < 0.0001 then
                        sawDuration <- true
                | _ -> ())

        listener.Start()

        Observability.recordTransportSend transportName direction (Some methodName) (Some requestId) 123L 5.0

        Assert.True(sawCount, "expected acp.transport.send.count measurement")
        Assert.True(sawBytes, "expected acp.transport.send.bytes measurement")
        Assert.True(sawDuration, "expected acp.transport.send.duration_ms measurement")
