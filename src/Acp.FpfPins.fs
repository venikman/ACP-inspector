namespace Acp

/// Transport-agnostic FPF pin shapes for attaching to findings and telemetry.
module FpfPins =

    [<Struct>]
    type PathId = PathId of string

    [<Struct>]
    type PathSliceId = PathSliceId of string

    [<Struct>]
    type GateCrossingId = GateCrossingId of string

    /// Minimal pin bundle aligned to FPF lane separation.
    type FpfPins =
        { pathId      : PathId
          pathSliceId : PathSliceId
          policyId    : string option
          sentinelId  : string option
          lane        : string option }

