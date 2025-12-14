namespace Acp.Contrib

open System
open System.Threading.Tasks
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting

/// Permission broker for handling permission requests.
/// Similar to Python SDK's permission handling functionality.
module Permissions =

    /// A pending permission request awaiting response.
    type PendingPermissionRequest =
        { requestId: string
          request: RequestPermissionParams
          enqueuedAt: DateTimeOffset }

    /// A recorded permission response.
    type PermissionResponse =
        { requestId: string
          request: RequestPermissionParams
          selectedOptionId: string
          wasCancelled: bool
          wasAutoResponded: bool
          respondedAt: DateTimeOffset }

    /// Internal state for a pending request with completion source.
    type private PendingState =
        { request: RequestPermissionParams
          enqueuedAt: DateTimeOffset
          completionSource: TaskCompletionSource<RequestPermissionOutcome> }

    /// Auto-response rule.
    type private AutoRule =
        { ruleId: string
          predicate: RequestPermissionParams -> bool
          optionId: string }

    /// Broker for managing permission requests and responses.
    type PermissionBroker() =
        let mutable pendingOrder: string list = []
        let mutable pending: Map<string, PendingState> = Map.empty
        let mutable history: PermissionResponse list = []
        let mutable autoRules: AutoRule list = []

        let mutable requestSubscribers: (PendingPermissionRequest -> unit) list = []
        let mutable responseSubscribers: (PermissionResponse -> unit) list = []

        let generateId () = Guid.NewGuid().ToString("N").[..7]

        let notifyRequestSubscribers (req: PendingPermissionRequest) =
            for callback in requestSubscribers do
                callback req

        let notifyResponseSubscribers (resp: PermissionResponse) =
            for callback in responseSubscribers do
                callback resp

        let recordResponse requestId request optionId wasCancelled wasAutoResponded =
            let response =
                { requestId = requestId
                  request = request
                  selectedOptionId = optionId
                  wasCancelled = wasCancelled
                  wasAutoResponded = wasAutoResponded
                  respondedAt = DateTimeOffset.UtcNow }

            history <- history @ [ response ]
            notifyResponseSubscribers response
            response

        let tryAutoRespond requestId (request: RequestPermissionParams) : bool =
            let matchingRule =
                autoRules
                |> List.tryFind (fun rule -> rule.predicate request)

            match matchingRule with
            | Some rule ->
                // Validate the option exists
                let validOption =
                    request.options |> List.exists (fun o -> o.optionId = rule.optionId)

                if validOption then
                    // Complete and record
                    match pending |> Map.tryFind requestId with
                    | Some state ->
                        state.completionSource.TrySetResult(RequestPermissionOutcome.Selected rule.optionId)
                        |> ignore

                        pending <- pending |> Map.remove requestId
                        pendingOrder <- pendingOrder |> List.filter ((<>) requestId)
                        recordResponse requestId request rule.optionId false true |> ignore
                        true
                    | None -> false
                else
                    false
            | None -> false

        /// Check if there are pending requests.
        member _.HasPending() : bool = not (List.isEmpty pendingOrder)

        /// Get all pending requests in order.
        member _.PendingRequests() : PendingPermissionRequest list =
            pendingOrder
            |> List.choose (fun id ->
                pending
                |> Map.tryFind id
                |> Option.map (fun state ->
                    { requestId = id
                      request = state.request
                      enqueuedAt = state.enqueuedAt }))

        /// Try to get a specific pending request.
        member _.TryGetPending(requestId: string) : PendingPermissionRequest option =
            pending
            |> Map.tryFind requestId
            |> Option.map (fun state ->
                { requestId = requestId
                  request = state.request
                  enqueuedAt = state.enqueuedAt })

        /// Enqueue a new permission request. Returns the request ID.
        member this.Enqueue(request: RequestPermissionParams) : string =
            let requestId = generateId ()

            let state =
                { request = request
                  enqueuedAt = DateTimeOffset.UtcNow
                  completionSource = TaskCompletionSource<RequestPermissionOutcome>() }

            pending <- pending |> Map.add requestId state
            pendingOrder <- pendingOrder @ [ requestId ]

            let pendingReq =
                { requestId = requestId
                  request = request
                  enqueuedAt = state.enqueuedAt }

            notifyRequestSubscribers pendingReq

            // Try auto-respond
            if tryAutoRespond requestId request then
                ()

            requestId

        /// Respond to a pending request with the selected option.
        member _.Respond(requestId: string, optionId: string) : Result<PermissionResponse, string> =
            match pending |> Map.tryFind requestId with
            | None -> Error $"Unknown request ID: {requestId}"
            | Some state ->
                // Validate option
                let validOption =
                    state.request.options |> List.exists (fun o -> o.optionId = optionId)

                if not validOption then
                    Error $"Invalid option ID: {optionId}"
                else
                    // Complete the task
                    state.completionSource.TrySetResult(RequestPermissionOutcome.Selected optionId)
                    |> ignore

                    // Remove from pending
                    pending <- pending |> Map.remove requestId
                    pendingOrder <- pendingOrder |> List.filter ((<>) requestId)

                    // Record response
                    let response = recordResponse requestId state.request optionId false false
                    Ok response

        /// Cancel a pending request.
        member _.Cancel(requestId: string) : bool =
            match pending |> Map.tryFind requestId with
            | None -> false
            | Some state ->
                // Complete the task with Cancelled
                state.completionSource.TrySetResult(RequestPermissionOutcome.Cancelled) |> ignore

                // Remove from pending
                pending <- pending |> Map.remove requestId
                pendingOrder <- pendingOrder |> List.filter ((<>) requestId)

                // Record cancellation
                recordResponse requestId state.request "" true false |> ignore
                true

        /// Wait for a response to a pending request.
        member _.WaitForResponseAsync(requestId: string) : Task<RequestPermissionOutcome option> =
            task {
                match pending |> Map.tryFind requestId with
                | None -> return None
                | Some state ->
                    let! outcome = state.completionSource.Task
                    return Some outcome
            }

        /// Get the response history.
        member _.ResponseHistory() : PermissionResponse list = history

        /// Add an auto-response rule. Returns the rule ID.
        member _.AddAutoRule(predicateAndOption: RequestPermissionParams -> bool * string) : string =
            let ruleId = generateId ()

            let rule =
                { ruleId = ruleId
                  predicate = fun req -> fst (predicateAndOption req)
                  optionId =
                    // Get the option ID from the tuple function applied to a dummy
                    // This is a bit hacky but works for the API design
                    snd (
                        predicateAndOption
                            { sessionId = SessionId ""
                              toolCall =
                                { toolCallId = ""
                                  title = None
                                  kind = None
                                  status = None
                                  content = None
                                  locations = None
                                  rawInput = None
                                  rawOutput = None }
                              options = [] }
                    ) }

            autoRules <- autoRules @ [ rule ]
            ruleId

        /// Remove an auto-response rule.
        member _.RemoveAutoRule(ruleId: string) =
            autoRules <- autoRules |> List.filter (fun r -> r.ruleId <> ruleId)

        /// Clear all pending requests and history.
        member _.Reset() =
            // Cancel all pending
            for KeyValue(_, state) in pending do
                state.completionSource.TrySetCanceled() |> ignore

            pending <- Map.empty
            pendingOrder <- []
            history <- []

        /// Subscribe to new request events. Returns unsubscribe function.
        member _.SubscribeToRequests(callback: PendingPermissionRequest -> unit) : unit -> unit =
            requestSubscribers <- requestSubscribers @ [ callback ]

            fun () ->
                requestSubscribers <-
                    requestSubscribers
                    |> List.filter (fun c -> not (Object.ReferenceEquals(c, callback)))

        /// Subscribe to response events. Returns unsubscribe function.
        member _.SubscribeToResponses(callback: PermissionResponse -> unit) : unit -> unit =
            responseSubscribers <- responseSubscribers @ [ callback ]

            fun () ->
                responseSubscribers <-
                    responseSubscribers
                    |> List.filter (fun c -> not (Object.ReferenceEquals(c, callback)))
