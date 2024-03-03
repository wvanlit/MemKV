namespace MemKV.Service

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging


type Worker(logger: ILogger<Worker>) =
    inherit BackgroundService()

    let DEFAULT_REDIS_PORT = 6379
    let BUFFER_SIZE = 1024
    
    member private _.HandleClient(client: TcpClient, ct: CancellationToken) =
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
                    logger.LogInformation("Received: {message}", message.Replace("\r\n", "\\r\\n"))
                    
                    let response = "+PONG\r\n"
                    let responseBuffer = System.Text.Encoding.ASCII.GetBytes(response)
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
    
    override this.ExecuteAsync(ct: CancellationToken) =
        task {
            do! this.ListenToTcp(ct)
        }