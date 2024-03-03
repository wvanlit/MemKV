namespace MemKV.Service

open System.Net
open System.Net.Sockets
open System.Threading
open MemKV.Protocol
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging


type Worker(logger: ILogger<Worker>) =
    inherit BackgroundService()

    let DEFAULT_REDIS_PORT = 6379
    let BUFFER_SIZE = 1024
    
    let storage = System.Collections.Generic.SortedDictionary<string, string>()

    member private this.HandleCommand(command) =
        match command with
        | Ping opt ->
            match opt with
            | Some s -> BulkString (Some s)
            | None -> SimpleString "PONG"
        | Set(k, v) ->
            storage[k] <- v
            SimpleString "OK"
        | Get k ->
            match storage.TryGetValue k with
            | true, v -> BulkString (Some v)
            | false, _ -> BulkString None

    member private this.HandleClient(client: TcpClient, ct: CancellationToken) =
        task {
            let stream = client.GetStream()
            let buffer = Array.zeroCreate<byte> BUFFER_SIZE

            while not ct.IsCancellationRequested && client.Connected do
                let! bytesRead = stream.ReadAsync(buffer, 0, buffer.Length, ct)

                if bytesRead = 0 then
                    logger.LogInformation("Client disconnected: {client}", client.Client.RemoteEndPoint)
                    client.Close()

                else
                    let message = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead)

                    let protocolMsg = Message.parseMessage message

                    let response =
                        match protocolMsg with
                        | Cmd cmd -> this.HandleCommand cmd
                        | data -> data
                    
                    let responseBuffer = response |> Message.serialize |> System.Text.Encoding.ASCII.GetBytes

                    do! stream.WriteAsync(responseBuffer, 0, responseBuffer.Length, ct)
        }

    member private this.ListenToTcp(ct: CancellationToken) =
        task {
            let listener = new TcpListener(IPAddress.Any, DEFAULT_REDIS_PORT)
            listener.Start()

            while not ct.IsCancellationRequested do
                let! client = listener.AcceptTcpClientAsync(ct)
                logger.LogInformation("Client connected: {client}", client.Client.RemoteEndPoint)
                this.HandleClient(client, ct) |> ignore
        }

    override this.ExecuteAsync(ct: CancellationToken) = task { do! this.ListenToTcp(ct) }
