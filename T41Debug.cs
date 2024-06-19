using System.Net;
using System.Net.Sockets;
using System.Text;
using SocketHelper;

namespace T41.Client.Debug;

// Implements a debug window client
// After connecting to an instance of the server, the client waits for a message entered by
// the user to be sent to the server or for a message from the server to be displayed.
// This pattern is continued until the client disconnects.
class T41Debug : SocketClient {
  bool acceptInput;

  // Create initialize and start client instance
  public T41Debug(SocketSettings theSettings, bool _acceptInput = true) : base(theSettings) {
    acceptInput = _acceptInput;

    Console.WriteLine("Connecting to server....");
    Console.WriteLine("Press Escape to terminate the process....");

    Start(settings.EndPoint);

    GetInput();
  }

  protected override void ConnectionFailed(SocketAsyncEventArgs e) {
    int connectRetryCount = 0;

    switch (e.SocketError) {
      case SocketError.ConnectionRefused:
        if (connectRetryCount < 10) {
          if (connectRetryCount == 0) {
            Console.WriteLine("Server connection refused.  Have you started T41 Server?");
            Console.Write("Retrying connection.");
          } else {
            Console.Write(".");
          }
          connectRetryCount++;
          Thread.Sleep(10000);
          StartConnect(e);
        } else {
          Console.WriteLine("Couldn't connect to socket.");
        }
        break;
      default:
        Console.WriteLine("Couldn't connect to socket. Error: {0}", e.SocketError);
        break;
    }
  }

  protected override void RespondToConnect(SocketAsyncEventArgs e) {
    serverSocket = (Socket)e.ConnectSocket;
    byte[] buffer = new byte[1_024];
    int received = serverSocket.Receive(buffer, SocketFlags.None);

    Console.Title = "T41 Debug Window #" + buffer[0].ToString();
    Response = ".... Connected to server";
    DataReceived = true;
  }

  // post received data to console
  protected override void ProcessData(SocketAsyncEventArgs e) {
    Response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
    DataReceived = true;
  }

  private void GetInput() {
    ConsoleKeyInfo cki;
    bool keyAvailable;
    char[] buffer = new char[256];
    int count = 0;
    int key;

    int height = Console.WindowHeight;
    int width = Console.WindowWidth;
    int msgX, msgY, cmdX, cmdY;

    (msgX, msgY)  = Console.GetCursorPosition();

    Console.SetCursorPosition(0, Console.WindowHeight - 1);
    Console.Write(">");
    (cmdX, cmdY)  = Console.GetCursorPosition();

    do {
      while(Console.KeyAvailable == false) {
        if(DataReceived) {
          Console.CursorVisible = false;
          (cmdX, cmdY)  = Console.GetCursorPosition();

          if(height != Console.WindowHeight) {
            int delta = height - Console.WindowHeight;
            if(delta > 0) msgY -= delta;
            if(msgY < 0) msgY = 1;
            height = Console.WindowHeight;
          }
          Console.SetCursorPosition(msgX, msgY);
          Console.WriteLine(Response);
          (msgX, msgY)  = Console.GetCursorPosition();

          Console.SetCursorPosition(cmdX, cmdY);
          Console.CursorVisible = true;
          DataReceived = false;
        }

        Thread.Sleep(100);
      }

      cki = Console.ReadKey();
      if(cki.Key == ConsoleKey.Enter) {
        buffer[count++] = (char)0;

        Console.Write("                                                  ");

        // send buffer to server
        SendToServer(new string(buffer));

        Console.SetCursorPosition(0, Console.WindowHeight - 1);
        Console.Write(">");
        Console.CursorVisible = true;
        Array.Fill(buffer, (char)0);
        count = 0;
      } else {
        buffer[count++] = cki.KeyChar;
      }
    } while(cki.Key != ConsoleKey.Escape);
  }

}
