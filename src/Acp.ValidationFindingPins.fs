namespace Acp

open Domain.Metadata
open Validation
open Pins

/// Helpers for attaching optional pins to validator findings.
module ValidationFindingPins =

    type ValidationFindingWithPins =
        { finding: ValidationFinding
          pins: PinBundle option }

    /// Attach pins to a finding when the runtime profile provides them.
    /// Today the runtime profile does not carry pins, so this is a thin wrapper.
    let attachPins (_profile: RuntimeProfile option) (finding: ValidationFinding) : ValidationFindingWithPins =
        { finding = finding; pins = None }
