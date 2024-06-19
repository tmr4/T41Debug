using System.Net;

using SocketHelper;
using T41.Client;
using T41.Client.Debug;

namespace T41ClientApp;

internal class Program {
  static void Main(string[] args) {
    SocketSettings clientSettings = new("127.0.0.1", 48005, 512);
    T41Debug client = new T41Debug(clientSettings);
  }
}
