using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TribesVengeanceMasterServer
{
    public class MasterServer : IDisposable
    {
        public static int MaxConnectionsPerIp = 10;
        public static int MasterServerBufferSize = 256;
        public static byte[] Key = Encoding.ASCII.GetBytes("y3D28k");
        public static TimeSpan MasterServerClientTimeout = TimeSpan.FromSeconds(5);

        private readonly IPEndPoint endPoint;
        private readonly GameServerStorage gameServerStorage;
        private readonly CancellationToken masterCt;
        private readonly TcpListener listener;
        private bool IsDisposed = false;

        public MasterServer(IPEndPoint endPoint, GameServerStorage gameServerStorage, CancellationToken masterCt)
        {
            this.endPoint = endPoint;
            this.gameServerStorage = gameServerStorage;
            this.masterCt = masterCt;
            listener = new TcpListener(endPoint);
        }

        public void Listen()
        {
            Console.WriteLine("Starting MasterServer on TCP {0}", endPoint);
            listener.Start();
            listener.BeginAcceptTcpClient(AcceptedTcpClient, null);
            Console.WriteLine("MasterServer started");
        }

        public void AcceptedTcpClient(IAsyncResult ar)
        {
            if(IsDisposed)
            {
                return;
            }

            TcpClient client = listener.EndAcceptTcpClient(ar);
            listener.BeginAcceptTcpClient(AcceptedTcpClient, null);
            var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

            //if (Agents.Keys.Select(x => ((IPEndPoint)x.Client.RemoteEndPoint).Address).Count(x => x.Equals(remoteIp)) > MaxConnectionsPerIp)
            //{
            //    client.Dispose();
            //    return;
            //}

            TaskPool.Run(HandleMasterServerClient(client, gameServerStorage, masterCt));
        }

        private static async Task HandleMasterServerClient(TcpClient client, GameServerStorage gameServerStorage, CancellationToken masterCt)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(MasterServerBufferSize);
            var bufferMemory = buffer.AsMemory();
            var readLen = 0;
            var ct = CancellationTokenSource.CreateLinkedTokenSource(new CancellationTokenSource(MasterServerClientTimeout).Token, masterCt).Token;

            try
            {
                using (client)
                using (var networkStream = client.GetStream())
                {
                    while (true)
                    {
                        readLen = readLen + await networkStream.ReadAsync(bufferMemory.Slice(readLen), ct);
                        if (readLen == 0)
                        {
                            continue;
                        }

                        if (readLen >= buffer.Length - 1)
                        {
                            return;
                        }

                        if (!Encoder.TryDecodeClientRequest(bufferMemory.Slice(0, readLen).Span, out MasterServerRequestInfo info))
                        {
                            continue;
                        }

                        Console.WriteLine("MasterServerAgent responding to {0}", client.Client.RemoteEndPoint);

                        var servers = gameServerStorage.Servers.Keys;
                        var resp = Encoder.EncodeClientResponse((IPEndPoint)client.Client.RemoteEndPoint, servers, info.Params);
                        var encrypted = Encoder.Encrypt(Key, info.Validate, resp);

                        await networkStream.WriteAsync(encrypted, ct);
                        await networkStream.FlushAsync(ct);
                        return;
                    }
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer.ToArray());
            }
        }

        public void Dispose()
        {
            IsDisposed = true;

            listener.Stop();
        }
    }
}
