using System;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class WebSocketServer
{
    public event EventHandler<string> OnMessage;
    public event EventHandler OnClose;
    public event EventHandler<Exception> OnError;

    private WebSocket _currentWebSocket;

    public async Task StartAsync(string uriPrefix)
    {
        var httpListener = new HttpListener();
        httpListener.Prefixes.Add(uriPrefix);
        httpListener.Start();
        Console.WriteLine($"Listening for WebSocket connections on {uriPrefix}...");

        while (true)
        {
            try
            {
                var context = await httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
                    _currentWebSocket = webSocketContext.WebSocket;
                    Console.WriteLine("Client connected");

                    await HandleClientAsync(_currentWebSocket);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (_currentWebSocket != null && _currentWebSocket.State == WebSocketState.Open)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await _currentWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine($"Sent: {message}");
        }
    }

    private async Task HandleClientAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    OnMessage?.Invoke(this, receivedMessage);

                    // Echo the message back to the client
                    await SendMessageAsync("Echo: " + receivedMessage + " Received from the client says the server!!");
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    OnClose?.Invoke(this, EventArgs.Empty);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex);
        }
    }

    public void HandleMessage(object sender, string message)
    {
        Console.WriteLine($"Received: {message}");
    }

    public void HandleClose(object sender, EventArgs args)
    {
        Console.WriteLine("Connection closed.");
    }

    public void HandleError(object sender, Exception ex)
    {
        Console.WriteLine($"Error occurred: {ex.Message}");
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var server = new WebSocketServer();

        server.OnMessage += server.HandleMessage;
        server.OnClose += server.HandleClose;
        server.OnError += server.HandleError;

        await server.StartAsync("http://localhost:9006/");
    }
}
