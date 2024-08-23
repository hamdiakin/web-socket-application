using System.Net.WebSockets;
using System.Text;

namespace Client
{
    public class WebSocketClient : IDisposable
    {
        private readonly ClientWebSocket _clientWebSocket;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<string> OnMessage;
        public event EventHandler OnClose;
        public event EventHandler<Exception> OnError;

        public WebSocketClient()
        {
            _clientWebSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync(string uri)
        {
            try
            {
                await _clientWebSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
                Console.WriteLine("Connected to server.");
                _ = Task.Run(() => ReceiveMessagesAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token); // Start listening for messages in the background
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_clientWebSocket.State != WebSocketState.Open)
            {
                OnError?.Invoke(this, new InvalidOperationException("Cannot send message. The connection is not open."));
                return;
            }

            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await _clientWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                Console.WriteLine("Message sent.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            var receiveBuffer = new byte[4096];
            try
            {
                while (_clientWebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var receiveResult = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken);
                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        var receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
                        OnMessage?.Invoke(this, receivedMessage);
                    }
                    else if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        OnClose?.Invoke(this, EventArgs.Empty);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("Receive operation was canceled.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _clientWebSocket.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}
