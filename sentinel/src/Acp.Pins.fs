namespace Acp

/// Transport-agnostic pin shapes for attaching to findings and telemetry.
module Pins =

    [<Struct>]
    type PathId = PathId of string

    [<Struct>]
    type PathSliceId = PathSliceId of string

    [<Struct>]
    type GateCrossingId = GateCrossingId of string

    /// Minimal pin bundle for attaching optional metadata to findings.
    type PinBundle =
        { pathId: PathId
          pathSliceId: PathSliceId
          policyId: string option
          sentinelId: string option
          lane: string option }
