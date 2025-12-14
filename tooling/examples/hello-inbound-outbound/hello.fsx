#I "../../../src/bin/Debug/net9.0"
#r "ACP.dll"

open Acp
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Messaging
open Acp.RuntimeAdapter
open Acp.Validation
open Acp.Protocol

let session = SessionId "demo-hello-001"

// Example: override eval profile to allow empty prompt text (for demo only).
let evalProfile =
    { Eval.defaultProfile with
        requireNonEmptyInstruction = false }

let inbound =
    RuntimeAdapter.validateInboundWithEval
        session
        None
        (Some evalProfile)
        { rawByteLength = None
          message =
            Message.FromClient(
                ClientToAgentMessage.Initialize
                    { protocolVersion = 1
                      clientCapabilities =
                        { fs =
                            { readTextFile = true
                              writeTextFile = false }
                          terminal = false }
                      clientInfo = None }
            ) }
        false

let outbound =
    RuntimeAdapter.validateOutbound
        session
        None
        { rawByteLength = None
          message =
            Message.FromAgent(
                AgentToClientMessage.InitializeResult
                    { negotiatedVersion = 1
                      agentCapabilities =
                        { loadSession = true
                          mcpCapabilities = { http = false; sse = false }
                          promptCapabilities =
                            { audio = false
                              image = false
                              embeddedContext = false } }
                      agentInfo = None }
            ) }
        false

printfn "Inbound findings: %A" inbound.findings
printfn "Inbound phase: %A" inbound.phase
printfn "Outbound findings: %A" outbound.findings
printfn "Outbound phase: %A" outbound.phase

// End-to-end trace validation (ordered conversation)
let trace = [ inbound.message; outbound.message ]

let convo = Validation.runWithValidation session Protocol.spec trace false None None

printfn "\nConversation findings (ordered): %A" convo.findings
printfn "Conversation final phase: %A" convo.finalPhase
