module Acp.Cli.Common.Parsing

open Acp.Codec

/// Parse direction string into Codec.Direction.
/// Supports multiple format variations: "FromClient", "client", "C2A", "C->A", etc.
let parseDirection (dirStr: string) : Direction option =
    match dirStr.Trim().ToLowerInvariant() with
    | "fromclient"
    | "client"
    | "c2a"
    | "c->a" -> Some Direction.FromClient
    | "fromagent"
    | "agent"
    | "a2c"
    | "a->c" -> Some Direction.FromAgent
    | _ -> None
