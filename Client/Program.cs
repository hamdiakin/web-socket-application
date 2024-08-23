using Client;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var client = new WebSocketClient();

        client.OnMessage += HandleMessage;
        client.OnClose += HandleClose;
        client.OnError += HandleError;

        await client.StartAsync("ws://localhost:9006");

        Console.WriteLine("Press 'm' to send a message, or any other key to exit.");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.M)
            {
                Console.Write("Enter a message to send: ");
                var message = Console.ReadLine();
                await client.SendMessageAsync(message);
            }
            else
            {
                break;
            }
        }
    }

    private static void HandleMessage(object sender, string message)
    {
        Console.WriteLine($"Message from server: {message}");
    }

    private static void HandleClose(object sender, EventArgs args)
    {
        Console.WriteLine("Server connection closed.");
    }

    private static void HandleError(object sender, Exception ex)
    {
        Console.WriteLine($"Error occurred: {ex.Message}");
    }
}