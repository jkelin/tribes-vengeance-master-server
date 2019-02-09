using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TribesVengeanceMasterServer
{
    public class MasterServerAgent : IDisposable
    {
        public static byte[] Key = Encoding.ASCII.GetBytes("y3D28k");

        private readonly TcpClient client;
        private readonly NetworkStream networkStream;
        private readonly GameServerStorage gameServerStorage;
        private readonly Action done;
        private readonly byte[] buffer = new byte[1024];
        private int readLen = 0;
        private MasterServerRequestInfo info;

        public MasterServerAgent(TcpClient client, GameServerStorage gameServerStorage, Action done)
        {
            this.client = client;
            this.gameServerStorage = gameServerStorage;
            this.done = done;
            networkStream = client.GetStream();
            BeginRead();

            Console.WriteLine("Created MasterServerAgent for {0}", client.Client.RemoteEndPoint);
        }

        private void BeginRead()
        {
            networkStream.BeginRead(buffer, readLen, buffer.Length, ReadCallback, null);
        }

        public void ReadCallback(IAsyncResult ar)
        {
            lock (networkStream)
            {
                readLen = readLen + networkStream.EndRead(ar);
                byte[] read = buffer.Take(readLen).ToArray();

                if (!Encoder.TryDecodeClientRequest(read, out info))
                {
                    BeginRead();
                    return;
                }
            }

            Console.WriteLine("MasterServerAgent responding to {0}", client.Client.RemoteEndPoint);

            var servers = gameServerStorage.Servers.Keys;
            var resp = Encoder.EncodeClientResponse((IPEndPoint)client.Client.RemoteEndPoint, servers, info.Params);
            var encrypted = Encoder.Encrypt(Key, info.Validate, resp);

            networkStream.Write(encrypted);
            networkStream.Flush();
            done?.Invoke();
        }

        public void Dispose()
        {
            networkStream.Dispose();
            client.Close();
            client.Dispose();
        }
    }
}
