
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace T41Debug;

internal class Program {
  static Socket client;
  static IPEndPoint ipEndPoint = new(IPAddress.Parse("127.0.0.1"), 48005);

  static async Task Main(string[] args) {
    byte[] buffer = new byte[1_024];
    int received;
    int port;
    string response;

    client = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

    client.Connect(ipEndPoint);
    received = client.Receive(buffer, SocketFlags.None);

    Console.Title = "T41 Debug Window #" + buffer[0].ToString();

    // *** TODO: add two way comms ***
    while (true) {
      received = await client.ReceiveAsync(buffer, SocketFlags.None);
      response = Encoding.UTF8.GetString(buffer, 0, received);
      Console.WriteLine(response);
    }

    client.Shutdown(SocketShutdown.Both);
  }
}
