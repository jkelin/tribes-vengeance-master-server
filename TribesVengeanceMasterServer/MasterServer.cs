using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace TribesVengeanceMasterServer
{
    public class MasterServer : IDisposable
    {
        public static int MaxConnectionsPerIp = 10;

        private readonly IPEndPoint endPoint;
        private readonly GameServerStorage gameServerStorage;
        private readonly TcpListener listener;
        private readonly ConcurrentDictionary<TcpClient, MasterServerAgent> Agents = new ConcurrentDictionary<TcpClient, MasterServerAgent>();

        public MasterServer(IPEndPoint endPoint, GameServerStorage gameServerStorage)
        {
            this.endPoint = endPoint;
            this.gameServerStorage = gameServerStorage;
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
            TcpClient client = listener.EndAcceptTcpClient(ar);
            listener.BeginAcceptTcpClient(AcceptedTcpClient, null);

            if(Agents.Keys.Count(x => ((IPEndPoint)x.Client.RemoteEndPoint).Address == ((IPEndPoint)client.Client.RemoteEndPoint).Address) > MaxConnectionsPerIp)
            {
                client.Dispose();
                return;
            }

            var agent = new MasterServerAgent(client, gameServerStorage, () => RemoveAgent(client));
            Agents[client] = agent;
        }

        private void RemoveAgent(TcpClient client)
        {
            if(Agents.TryRemove(client, out var agent))
            {
                agent.Dispose();
            }
        }

        public void Dispose()
        {
            Agents.Values.ToList().ForEach(x => x.Dispose());
            listener.Stop();
        }
    }
}
