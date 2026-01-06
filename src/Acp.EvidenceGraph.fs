namespace Acp

open System

/// BC-001: Evidence Graph (G.6, A.10)
/// Directed acyclic graph from claims to grounding evidence.
/// Implements weakest-link propagation (INV-ASR-02).
module EvidenceGraph =

    open Assurance

    // =====================
    // Core Types
    // =====================

    /// Unique identifier for a claim node
    [<Struct>]
    type ClaimId = ClaimId of string

    [<RequireQualifiedAccess>]
    module ClaimId =
        let value (ClaimId s) = s
        let create s = ClaimId s

    /// Unique identifier for an evidence node
    [<Struct>]
    type EvidenceId = EvidenceId of string

    [<RequireQualifiedAccess>]
    module EvidenceId =
        let value (EvidenceId s) = s
        let create s = EvidenceId s

    /// Node in the evidence graph
    [<RequireQualifiedAccess>]
    type EvidenceNode =
        | Claim of ClaimId * content: string
        | Evidence of EvidenceId * artifact: GroundingRef
        | GroundingHolon of holon: GroundingRef

    [<RequireQualifiedAccess>]
    module EvidenceNode =
        let id =
            function
            | EvidenceNode.Claim(ClaimId id, _) -> id
            | EvidenceNode.Evidence(EvidenceId id, _) -> id
            | EvidenceNode.GroundingHolon(GroundingRef uri) -> uri

    /// Edge in the evidence graph with assurance level
    type EvidenceEdge =
        { source: string // Node ID
          target: string // Node ID
          level: AssuranceLevel
          label: string option } // Optional label for edge type

    [<RequireQualifiedAccess>]
    module EvidenceEdge =
        let create source target level =
            { source = source
              target = target
              level = level
              label = None }

        let withLabel label edge = { edge with label = Some label }

    // =====================
    // Evidence Graph
    // =====================

    /// Directed acyclic graph of evidence
    type EvidenceGraph =
        { nodes: Map<string, EvidenceNode> // Node ID -> Node
          edges: Map<string, EvidenceEdge list> } // Source ID -> Edges

    [<RequireQualifiedAccess>]
    module EvidenceGraph =
        /// Create an empty evidence graph
        let empty = { nodes = Map.empty; edges = Map.empty }

        /// Add a node to the graph
        let addNode (node: EvidenceNode) (graph: EvidenceGraph) : EvidenceGraph =
            let nodeId = EvidenceNode.id node

            { graph with
                nodes = graph.nodes |> Map.add nodeId node }

        /// Add an edge to the graph
        let addEdge (edge: EvidenceEdge) (graph: EvidenceGraph) : EvidenceGraph =
            let existingEdges = graph.edges |> Map.tryFind edge.source |> Option.defaultValue []
            let newEdges = edge :: existingEdges

            { graph with
                edges = graph.edges |> Map.add edge.source newEdges }

        /// Get all nodes
        let nodes (graph: EvidenceGraph) : EvidenceNode list =
            graph.nodes |> Map.toList |> List.map snd

        /// Get all edges
        let edges (graph: EvidenceGraph) : EvidenceEdge list =
            graph.edges |> Map.toList |> List.collect snd

        /// Get edges originating from a node
        let outgoingEdges (nodeId: string) (graph: EvidenceGraph) : EvidenceEdge list =
            graph.edges |> Map.tryFind nodeId |> Option.defaultValue []

        /// Get all nodes that this node points to
        let successors (nodeId: string) (graph: EvidenceGraph) : string list =
            outgoingEdges nodeId graph |> List.map (fun e -> e.target)

        /// Check if the graph contains a node
        let hasNode (nodeId: string) (graph: EvidenceGraph) : bool = graph.nodes |> Map.containsKey nodeId

        /// Get a node by ID
        let tryGetNode (nodeId: string) (graph: EvidenceGraph) : EvidenceNode option = graph.nodes |> Map.tryFind nodeId

        // =====================
        // DAG Validation
        // =====================

        /// Detect cycles using depth-first search
        let private detectCycle (graph: EvidenceGraph) : string option =
            // Depth-first search with explicit tracking of "visiting" (gray) and "visited" (black) nodes.
            // Threads the visited set across disconnected components to avoid redundant work.
            let rec visit
                (nodeId: string)
                (visiting: Set<string>)
                (visited: Set<string>)
                : string option * Set<string> =
                if visiting |> Set.contains nodeId then
                    Some nodeId, visited // Cycle detected
                elif visited |> Set.contains nodeId then
                    None, visited // Already fully processed
                else
                    let visiting' = visiting |> Set.add nodeId

                    let rec visitTargets
                        (remainingTargets: string list)
                        (visited: Set<string>)
                        : string option * Set<string> =
                        match remainingTargets with
                        | [] ->
                            // All successors processed with no cycle; mark this node as fully visited.
                            None, (visited |> Set.add nodeId)
                        | target :: rest ->
                            let cycleOpt, visited' = visit target visiting' visited

                            match cycleOpt with
                            | Some _ -> cycleOpt, visited'
                            | None -> visitTargets rest visited'

                    visitTargets (successors nodeId graph) visited

            let nodeIds = graph.nodes |> Map.toList |> List.map fst

            let rec visitRoots (nodes: string list) (visited: Set<string>) : string option =
                match nodes with
                | [] -> None
                | nodeId :: rest ->
                    let cycleOpt, visited' = visit nodeId Set.empty visited

                    match cycleOpt with
                    | Some _ -> cycleOpt
                    | None -> visitRoots rest visited'

            visitRoots nodeIds Set.empty

        /// Validate that the graph is a DAG (acyclic)
        let isAcyclic (graph: EvidenceGraph) : bool = detectCycle graph |> Option.isNone

        /// Validate graph invariants
        let validate (graph: EvidenceGraph) : Result<unit, string> =
            // Check for cycles
            match detectCycle graph with
            | Some nodeId -> Error(sprintf "Cycle detected at node: %s" nodeId)
            | None ->
                // Check that all edge endpoints exist
                let allEdges = edges graph

                let missingNodes =
                    allEdges
                    |> List.collect (fun e -> [ e.source; e.target ])
                    |> List.distinct
                    |> List.filter (fun id -> not (hasNode id graph))

                if missingNodes.IsEmpty then
                    Ok()
                else
                    Error(sprintf "Missing nodes referenced in edges: %A" missingNodes)

        // =====================
        // Path Resolution & Weakest-Link
        // =====================

        /// Find all paths from source to any grounding holon
        let rec private findPaths
            (sourceId: string)
            (graph: EvidenceGraph)
            (visited: Set<string>)
            : (string list * AssuranceLevel) list =
            if visited |> Set.contains sourceId then
                [] // Avoid revisiting nodes
            else
                match tryGetNode sourceId graph with
                | Some(EvidenceNode.GroundingHolon _) ->
                    // Reached grounding - this is a complete path
                    [ ([ sourceId ], AssuranceLevel.L2) ] // Grounding provides L2
                | Some node ->
                    let visited' = visited |> Set.add sourceId
                    let edges = outgoingEdges sourceId graph

                    if edges.IsEmpty then
                        [] // Dead end, no path to grounding
                    else
                        edges
                        |> List.collect (fun edge ->
                            let subPaths = findPaths edge.target graph visited'
                            // Prepend current node to each path and take min level
                            subPaths
                            |> List.map (fun (path, level) -> (sourceId :: path, AssuranceLevel.min edge.level level)))
                | None -> [] // Node not found

        /// Compute weakest-link assurance level for a path from claim to grounding
        /// Returns None if no path exists
        let computeWeakestLink (pathId: PathId) (claimId: ClaimId) (graph: EvidenceGraph) : AssuranceLevel option =
            let claimIdStr = ClaimId.value claimId
            let paths = findPaths claimIdStr graph Set.empty

            if paths.IsEmpty then
                None // No path to grounding
            else
                // Take the best path (highest assurance level)
                paths |> List.map snd |> List.maxBy AssuranceLevel.toInt |> Some

        /// Get all evidence paths from a claim
        let getEvidencePaths (claimId: ClaimId) (graph: EvidenceGraph) : (string list * AssuranceLevel) list =
            let claimIdStr = ClaimId.value claimId
            findPaths claimIdStr graph Set.empty

        /// Check if a claim has a complete path to grounding
        let hasGrounding (claimId: ClaimId) (graph: EvidenceGraph) : bool =
            getEvidencePaths claimId graph |> List.isEmpty |> not

        // =====================
        // Serialization
        // =====================

        /// Convert node to JSON-compatible representation
        let private nodeToJson (node: EvidenceNode) : string =
            match node with
            | EvidenceNode.Claim(ClaimId id, content) ->
                sprintf "{\"type\":\"claim\",\"id\":\"%s\",\"content\":\"%s\"}" id content
            | EvidenceNode.Evidence(EvidenceId id, GroundingRef artifact) ->
                sprintf "{\"type\":\"evidence\",\"id\":\"%s\",\"artifact\":\"%s\"}" id artifact
            | EvidenceNode.GroundingHolon(GroundingRef holon) -> sprintf "{\"type\":\"grounding\",\"id\":\"%s\"}" holon

        /// Convert edge to JSON-compatible representation
        let private edgeToJson (edge: EvidenceEdge) : string =
            let labelPart =
                match edge.label with
                | Some l -> sprintf ",\"label\":\"%s\"" l
                | None -> ""

            sprintf
                "{\"source\":\"%s\",\"target\":\"%s\",\"level\":\"L%d\"%s}"
                edge.source
                edge.target
                (AssuranceLevel.toInt edge.level)
                labelPart

        /// Serialize graph to JSON (simplified, for debugging)
        let toJson (graph: EvidenceGraph) : string =
            let nodesJson = nodes graph |> List.map nodeToJson |> String.concat ","

            let edgesJson = edges graph |> List.map edgeToJson |> String.concat ","

            sprintf "{\"nodes\":[%s],\"edges\":[%s]}" nodesJson edgesJson

    // =====================
    // Builder API
    // =====================

    /// Fluent builder for constructing evidence graphs
    type EvidenceGraphBuilder() =
        let mutable graph = EvidenceGraph.empty

        /// Add a claim node
        member this.AddClaim(claimId: ClaimId, content: string) =
            graph <- EvidenceGraph.addNode (EvidenceNode.Claim(claimId, content)) graph
            this

        /// Add an evidence node
        member this.AddEvidence(evidenceId: EvidenceId, artifact: GroundingRef) =
            graph <- EvidenceGraph.addNode (EvidenceNode.Evidence(evidenceId, artifact)) graph
            this

        /// Add a grounding holon node
        member this.AddGrounding(holon: GroundingRef) =
            graph <- EvidenceGraph.addNode (EvidenceNode.GroundingHolon(holon)) graph
            this

        /// Add an edge between nodes
        member this.AddEdge(source: string, target: string, level: AssuranceLevel, ?label: string) =
            let edge =
                match label with
                | Some l -> EvidenceEdge.create source target level |> EvidenceEdge.withLabel l
                | None -> EvidenceEdge.create source target level

            graph <- EvidenceGraph.addEdge edge graph
            this

        /// Build and validate the graph
        member this.Build() : Result<EvidenceGraph, string> =
            match EvidenceGraph.validate graph with
            | Ok() -> Ok graph
            | Error msg -> Error msg

        /// Build without validation (unsafe)
        member this.BuildUnsafe() : EvidenceGraph = graph
