namespace Acp

open System
open System.Collections.Concurrent
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks

/// Transport abstractions for ACP connections.
/// Provides ITransport interface and implementations for stdio, memory, and duplex pairs.
module Transport =

    /// Transport interface for sending and receiving JSON-RPC messages.
    type ITransport =
        /// Send a JSON-RPC message (as raw JSON string).
        abstract SendAsync: message: string -> Task<unit>

        /// Receive a JSON-RPC message. Returns None when transport is closed or EOF.
        abstract ReceiveAsync: unit -> Task<string option>

        /// Close the transport.
        abstract CloseAsync: unit -> Task<unit>

    // ============================================================
    // MemoryTransport - for testing
    // ============================================================

    /// In-memory transport for testing. Messages are queued.
    type MemoryTransport() =
        let inbound = ConcurrentQueue<string>()
        let outbound = ConcurrentQueue<string>()
        let mutable closed = false
        let receiveSignal = new SemaphoreSlim(0)

        /// Enqueue a message to be received (simulates remote sending).
        member _.Enqueue(message: string) =
            if not closed then
                inbound.Enqueue(message)
                receiveSignal.Release() |> ignore

        /// Dequeue a sent message (for test assertions).
        member _.DequeueSent() : string option =
            match outbound.TryDequeue() with
            | true, msg -> Some msg
            | false, _ -> None

        interface ITransport with
            member _.SendAsync(message: string) =
                task {
                    if not closed then
                        outbound.Enqueue(message)
                }

            member _.ReceiveAsync() =
                task {
                    let mutable done' = false
                    let mutable result' = None

                    while not done' do
                        if closed then
                            done' <- true
                            result' <- None
                        else
                            // Try immediate dequeue first
                            match inbound.TryDequeue() with
                            | true, msg ->
                                done' <- true
                                result' <- Some msg
                            | false, _ ->
                                // Block until signaled (message enqueued or transport closed).
                                do! receiveSignal.WaitAsync()

                    return result'
                }

            member _.CloseAsync() =
                task {
                    closed <- true
                    receiveSignal.Release() |> ignore
                }

        member this.SendAsync(message) = (this :> ITransport).SendAsync(message)
        member this.ReceiveAsync() = (this :> ITransport).ReceiveAsync()
        member this.CloseAsync() = (this :> ITransport).CloseAsync()

    // ============================================================
    // StdioTransport - newline-delimited JSON over streams
    // ============================================================

    /// Transport over stdin/stdout streams using newline-delimited JSON.
    type StdioTransport(inputStream: Stream, outputStream: Stream) =
        let reader = new StreamReader(inputStream, Encoding.UTF8)
        let writer = new StreamWriter(outputStream, Encoding.UTF8)
        let mutable closed = false

        /// Flush the output stream.
        member _.FlushAsync() =
            task {
                if not closed then
                    do! writer.FlushAsync()
            }

        interface ITransport with
            member _.SendAsync(message: string) =
                task {
                    if not closed then
                        do! writer.WriteLineAsync(message)
                }

            member _.ReceiveAsync() =
                task {
                    if closed then
                        return None
                    else
                        try
                            let! line = reader.ReadLineAsync()

                            match line with
                            | null -> return None
                            | s -> return Some s
                        with :? ObjectDisposedException ->
                            return None
                }

            member _.CloseAsync() =
                task {
                    closed <- true
                    writer.Dispose()
                    reader.Dispose()
                }

        member this.SendAsync(message) = (this :> ITransport).SendAsync(message)
        member this.ReceiveAsync() = (this :> ITransport).ReceiveAsync()
        member this.CloseAsync() = (this :> ITransport).CloseAsync()

    // ============================================================
    // DuplexTransport - connected pair for in-process testing
    // ============================================================

    /// One side of a duplex transport pair.
    type DuplexTransportSide
        internal (sendQueue: ConcurrentQueue<string>, receiveQueue: ConcurrentQueue<string>, closeFlag: bool ref) =
        let receiveSignal = new SemaphoreSlim(0)
        let mutable peerSignal: SemaphoreSlim option = None

        member internal _.SetPeerSignal(signal: SemaphoreSlim) = peerSignal <- Some signal

        member internal _.Signal = receiveSignal

        interface ITransport with
            member _.SendAsync(message: string) =
                task {
                    if not closeFlag.Value then
                        sendQueue.Enqueue(message)

                        match peerSignal with
                        | Some s -> s.Release() |> ignore
                        | None -> ()
                }

            member _.ReceiveAsync() =
                task {
                    let mutable done' = false
                    let mutable result' = None

                    while not done' do
                        if closeFlag.Value then
                            done' <- true
                            result' <- None
                        else
                            match receiveQueue.TryDequeue() with
                            | true, msg ->
                                done' <- true
                                result' <- Some msg
                            | false, _ ->
                                // Block until signaled (message enqueued or transport closed).
                                do! receiveSignal.WaitAsync()

                    return result'
                }

            member _.CloseAsync() =
                task {
                    closeFlag.Value <- true
                    receiveSignal.Release() |> ignore

                    match peerSignal with
                    | Some s -> s.Release() |> ignore
                    | None -> ()
                }

        member this.SendAsync(message) = (this :> ITransport).SendAsync(message)
        member this.ReceiveAsync() = (this :> ITransport).ReceiveAsync()
        member this.CloseAsync() = (this :> ITransport).CloseAsync()

    /// Factory for creating connected duplex transport pairs.
    module DuplexTransport =
        /// Create a connected pair of transports.
        /// Messages sent on one side are received on the other.
        let CreatePair () : DuplexTransportSide * DuplexTransportSide =
            let queue1 = ConcurrentQueue<string>()
            let queue2 = ConcurrentQueue<string>()
            let closeFlag = ref false

            // Side1 sends to queue1, receives from queue2
            // Side2 sends to queue2, receives from queue1
            let side1 = DuplexTransportSide(queue1, queue2, closeFlag)
            let side2 = DuplexTransportSide(queue2, queue1, closeFlag)

            // Wire up peer signals
            side1.SetPeerSignal(side2.Signal)
            side2.SetPeerSignal(side1.Signal)

            (side1, side2)
