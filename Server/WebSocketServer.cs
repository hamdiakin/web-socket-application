using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Server
{
    public class WebSocketServer : IDisposable
    {
        private readonly HttpListener _httpListener;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<string> OnMessage;
        public event EventHandler OnClose;
        public event EventHandler<Exception> OnError;

        public WebSocketServer()
        {
            _httpListener = new HttpListener();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync(string uriPrefix)
        {
            _httpListener.Prefixes.Add(uriPrefix);
            _httpListener.Start();
            Console.WriteLine($"Listening for WebSocket connections on {uriPrefix}...");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
                        var webSocket = webSocketContext.WebSocket;
                        Console.WriteLine("Client connected");

                        _ = Task.Run(() => HandleClientAsync(webSocket), _cancellationTokenSource.Token);
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

        public async Task SendMessageAsync(WebSocket webSocket, string message)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"Sent: {message}");
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, ex);
                }
            }
        }

        private async Task HandleClientAsync(WebSocket webSocket)
        {
            var buffer = new byte[4096];
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        OnMessage?.Invoke(this, receivedMessage);
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
            finally
            {
                webSocket.Dispose();
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _httpListener.Stop();
        }

        public void Dispose()
        {
            Stop();
            _httpListener.Close();
            _cancellationTokenSource.Dispose();
        }
    }
}
