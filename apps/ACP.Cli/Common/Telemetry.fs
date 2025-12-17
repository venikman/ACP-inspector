module Acp.Cli.Common.Telemetry

// Suppress FS3261: Nullness warnings for interop with OpenTelemetry APIs
#nowarn "3261"

open System
open System.Collections.Generic
open System.Diagnostics
open OpenTelemetry
open OpenTelemetry.Metrics
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open OpenTelemetry.Instrumentation.Runtime

type TelemetryConfig =
    { ServiceName: string
      EnableConsole: bool
      OtlpEndpoint: string option }

module TelemetryConfig =
    let defaultConfig =
        { ServiceName = "acp-cli"
          EnableConsole = false
          OtlpEndpoint = None }

let mutable private tracerProvider: TracerProvider option = None
let mutable private meterProvider: MeterProvider option = None

/// Module-level ActivitySource singleton for creating telemetry spans.
/// ActivitySource is designed to be long-lived and reused across many activities.
let private activitySource = new ActivitySource("Acp.Cli")

/// Initialize OpenTelemetry with the given configuration
let initialize (config: TelemetryConfig) =
    // Build resource attributes
    let resourceBuilder =
        ResourceBuilder
            .CreateDefault()
            .AddService(serviceName = config.ServiceName, serviceVersion = "0.1.0")
            .AddAttributes(
                [ KeyValuePair("deployment.environment", "local")
                  KeyValuePair("telemetry.sdk.language", "fsharp") ]
            )

    // Configure tracing
    let tracerBuilder =
        Sdk.CreateTracerProviderBuilder().SetResourceBuilder(resourceBuilder).AddSource("Acp.*")

    let tracerBuilder =
        if config.EnableConsole then
            tracerBuilder.AddConsoleExporter()
        else
            tracerBuilder

    let tracerBuilder =
        match config.OtlpEndpoint with
        | Some endpoint -> tracerBuilder.AddOtlpExporter(fun options -> options.Endpoint <- Uri(endpoint))
        | None -> tracerBuilder

    tracerProvider <- Some(tracerBuilder.Build())

    // Configure metrics
    let meterBuilder =
        Sdk.CreateMeterProviderBuilder().SetResourceBuilder(resourceBuilder).AddMeter("Acp.*")

    let meterBuilder =
        if config.EnableConsole then
            meterBuilder.AddConsoleExporter()
        else
            meterBuilder

    let meterBuilder =
        match config.OtlpEndpoint with
        | Some endpoint -> meterBuilder.AddOtlpExporter(fun options -> options.Endpoint <- Uri(endpoint))
        | None -> meterBuilder

    meterProvider <- Some(meterBuilder.Build())

/// Shutdown telemetry providers gracefully
let shutdown () =
    match tracerProvider with
    | Some provider -> provider.Dispose()
    | None -> ()

    match meterProvider with
    | Some provider -> provider.Dispose()
    | None -> ()

/// Create a telemetry span for the given operation.
/// Uses the module-level ActivitySource singleton to avoid resource leaks.
let inline withSpan<'T> (name: string) (operation: unit -> 'T) : 'T =
    use activity = activitySource.StartActivity(name)
    operation ()
