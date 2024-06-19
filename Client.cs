using System.Net;
using System.Net.Sockets;
using System.Text;
using SocketHelper;

namespace T41.Client;

// Socket info: https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket?view=net-8.0

// modified from: https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socketasynceventargs?view=net-8.0

// Implements the connection logic for a socket client
// After connecting to the server, the client post a ReceiveAsync
// Client can be customized with the following virtual functions:
//    ConnectionFailed  called when Start fails to connect to server
//    RespondToConnect  called after a successful connection (default: do nothing)
//    ProcessData       called after data has been received (default: do nothing)
//    SendToServer      SendAsync to server (default: not called)
class SocketClient {
  protected SocketSettings settings;
  protected Socket clientSocket;

  protected Socket serverSocket;
  public string Response { get; set; } = "";
  public bool DataReceived { get; set; } = false;

  // represents a large reusable set of buffers for all socket operations
  BufferManager m_bufferManager;
  const int opsToPreAlloc = 2;    // read, write

  // pool of reusable SocketAsyncEventArgs objects for write and read socket operations
  SocketAsyncEventArgsPool m_readWritePool;

  // Create and initialize client instance
  // To connect to the server, call the Start method
  public SocketClient(SocketSettings theSettings, bool _hasWindow = true) {
    int receiveBufferSize = theSettings.BufSize;

    settings = theSettings;

    // allocate buffers such that the maximum number of sockets can have one outstanding read and
    //write posted to the socket simultaneously
    m_bufferManager = new BufferManager(receiveBufferSize * opsToPreAlloc, receiveBufferSize);

    m_readWritePool = new SocketAsyncEventArgsPool(1);

    // initialize client
    Init();
  }

  // Initializes the client by preallocating reusable buffers and context objects.
  public void Init() {
    // Allocates one large byte buffer which all I/O operations use a piece of.
    // This gaurds against memory fragmentation.
    m_bufferManager.InitBuffer();

    // preallocate pool of SocketAsyncEventArgs objects
    SocketAsyncEventArgs readWriteEventArg;

    // Pre-allocate a set of reusable SocketAsyncEventArgs
    readWriteEventArg = new SocketAsyncEventArgs();
    readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);

    // assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
    m_bufferManager.SetBuffer(readWriteEventArg);

    // add SocketAsyncEventArg to the pool
    m_readWritePool.Push(readWriteEventArg);
  }

  // Starts the client and connect to server
  public void Start(IPEndPoint localEndPoint)
  {
    // create the client socket
    clientSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

    // create new SocketAsyncEventArgs and set complete event handler and endpoint
    SocketAsyncEventArgs connectEventArg = new SocketAsyncEventArgs();
    connectEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(ConnectEventArg_Completed);
    connectEventArg.RemoteEndPoint = localEndPoint;

    // post connect on the server socket
    StartConnect(connectEventArg);
  }

  // Begins an asynchronous request for a connection to server
  public void StartConnect(SocketAsyncEventArgs connectEventArg) {
    bool willRaiseEvent = clientSocket.ConnectAsync(connectEventArg);
    if (!willRaiseEvent) {
      ProcessConnect(connectEventArg);
    }
  }

  // This method is the callback method associated with Socket.ConnectAsync
  // operations and is invoked when an connect operation is complete
  void ConnectEventArg_Completed(object sender, SocketAsyncEventArgs e) {
    ProcessConnect(e);
  }

  private void ProcessConnect(SocketAsyncEventArgs e) {
    if(e.SocketError == SocketError.Success) {
      RespondToConnect(e);

      // Get the socket for the server connection and put it into the
      // ReadEventArg object user token
      SocketAsyncEventArgs readEventArgs = m_readWritePool.Pop();
      readEventArgs.UserToken = e.ConnectSocket;

      // Post a receive to the connection
      bool willRaiseEvent = e.ConnectSocket.ReceiveAsync(readEventArgs);
      if (!willRaiseEvent) {
        ProcessReceive(readEventArgs);
      }
    } else {
      ConnectionFailed(e);
    }
  }

  protected virtual void ConnectionFailed(SocketAsyncEventArgs e) {
    int connectRetryCount = 0;

    switch (e.SocketError) {
      case SocketError.ConnectionRefused:
        if (connectRetryCount < 10) {
          connectRetryCount++;
          Thread.Sleep(10000);
          StartConnect(e);
        }
        break;
      default:
        break;
    }
  }

  protected virtual void RespondToConnect(SocketAsyncEventArgs e) {
  }

  // This method is called whenever a receive or send operation is completed on a socket
  void IO_Completed(object sender, SocketAsyncEventArgs e) {
    // determine which type of operation just completed and call the associated handler
    switch (e.LastOperation) {

      // *** TODO: check for other possibilities, like closed server socket ***

      case SocketAsyncOperation.Receive:
        ProcessReceive(e);
        break;
      case SocketAsyncOperation.Send:
        ProcessSend(e);
        break;
      default:
        throw new ArgumentException("The last operation completed on the socket was not a receive or send");
    }
  }

  // This method is invoked when an asynchronous receive operation completes.
  // Process any data received and post a new ReceiveAsync.
  // If the server closed the connection, then the socket is closed.
  private void ProcessReceive(SocketAsyncEventArgs e) {
    // check if the remote host closed the connection
    if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success) {
      // process data
      ProcessData(e);

      Socket socket = (Socket)e.UserToken;

      // Post a new receive to the connection
      bool willRaiseEvent = socket.ReceiveAsync(e);
      if (!willRaiseEvent) {
        ProcessReceive(e);
      }
    } else {

      // *** TODO: verify that this is necessary when server is closed before client ***
      // *** client windows are not closed when server is closed first, not sure I want this anyway ***
      // *** but still might want to close sockets ***

      CloseSocket(e);
    }
  }

  // process any data received
  protected virtual void ProcessData(SocketAsyncEventArgs e) {
  }

  // This method is invoked when an asynchronous send operation completes.
  // If the server closed the connection, then the socket is closed,
  // otherwise the SocketAsyncEventArgs is returned to the pool for reuse.
  private void ProcessSend(SocketAsyncEventArgs e) {
    if (e.SocketError == SocketError.Success) {
      // respond to send completion
      //AfterSend(e);

      // return SocketAsyncEventArgs to pool for reuse
      m_readWritePool.Push(e);
    } else {

      // *** TODO: verify that this is necessary when server is closed before client ***

      CloseSocket(e);
    }
  }

  // send string to server
  // *** TODO: consider using SocketAsyncEventArgs ***
  protected virtual void SendToServer(string msg) {
    serverSocket.SendAsync(Encoding.Default.GetBytes(msg), 0);
  }

  // *** TODO: verify that this is necessary when server is closed before client ***
  private void CloseSocket(SocketAsyncEventArgs e) {
    Socket socket = (Socket)e.UserToken;

    // close the socket associated with the server
    try {
        socket.Shutdown(SocketShutdown.Send);
    }
    // throws if server process has already closed
    catch (Exception) { }
    socket.Close();

    // Free the SocketAsyncEventArg so they can be reused by another client
    m_readWritePool.Push(e);
  }
}
