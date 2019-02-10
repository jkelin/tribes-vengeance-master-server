using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private readonly GameServerStorage gameServerStorage;
        private readonly ConcurrentDictionary<IPAddress, int> PerIpConnections = new ConcurrentDictionary<IPAddress, int>();

        public MasterServer(GameServerStorage gameServerStorage)
        {
            this.gameServerStorage = gameServerStorage;
        }

        public async Task Listen(IPEndPoint endPoint, CancellationToken ct)
        {
            var listener = new TcpListener(endPoint);

            try
            {
                Console.WriteLine("Starting MasterServer on TCP {0}", endPoint);
                listener.Start();
                Console.WriteLine("MasterServer started");

                while (!ct.IsCancellationRequested)
                {
                    var client = await Task.Run(() => listener.AcceptTcpClientAsync(), ct);
                    ct.ThrowIfCancellationRequested();
                    var ip = ((IPEndPoint)(client.Client.RemoteEndPoint)).Address;

                    if(PerIpConnections.TryGetValue(ip, out var totalIps) && totalIps > MaxConnectionsPerIp)
                    {
                        client.Dispose();
                        continue;
                    }

                    HandleMasterServerClient(client, ct).RunInBackground();
                }
            }
            catch(TaskCanceledException) { }
            finally
            {
                listener.Stop();
                Console.WriteLine("MasterServer stopped");
            }
        }

        private async Task HandleMasterServerClient(TcpClient client, CancellationToken masterCt)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(MasterServerBufferSize);
            var bufferMemory = buffer.AsMemory();
            var readLen = 0;
            var ct = CancellationTokenSource.CreateLinkedTokenSource(new CancellationTokenSource(MasterServerClientTimeout).Token, masterCt).Token;
            var ip = ((IPEndPoint)(client.Client.RemoteEndPoint)).Address;

            PerIpConnections.AddOrUpdate(ip, 0, (k, v) => v + 1);

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
                var value = PerIpConnections.AddOrUpdate(ip, 0, (k, v) => v - 1);
                if(value < 1)
                {
                    // TODO this really should be locked or done better somehow
                    PerIpConnections.TryRemove(ip, out value);
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
