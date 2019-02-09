using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace TribesVengeanceMasterServer
{
    public class HeartBeatServer : IDisposable
    {
        public static int MaxServersPerIp = 10;

        private readonly ConcurrentDictionary<IPEndPoint, HeartBeatAgent> ServerAgents = new ConcurrentDictionary<IPEndPoint, HeartBeatAgent>();
        private readonly UdpClient socket;
        private readonly IPEndPoint endpoint;
        private readonly GameServerStorage storage;
        private bool IsDisposed = false;

        public HeartBeatServer(IPEndPoint endpoint, GameServerStorage storage)
        {
            this.endpoint = endpoint;
            this.storage = storage;
            socket = new UdpClient(endpoint);
        }

        public void Listen()
        {
            foreach(var server in storage.Servers.Keys)
            {
                TryAddAgent(server, out var _);
            }

            // TODO remove in final version
            TryAddAgent(new IPEndPoint(IPAddress.Parse("176.9.148.187"), 7788), out var _);
            TryAddAgent(new IPEndPoint(IPAddress.Parse("94.23.249.178"), 7778), out var _);

            Console.WriteLine("Starting HeartBeatServer UDP {0}", endpoint);
            socket.BeginReceive(Received, null);
            Console.WriteLine("HeartBeatServer started");
        }

        public void Received(IAsyncResult ar)
        {
            if(IsDisposed)
            {
                return;
            }

            IPEndPoint remote = null;
            byte[] data;

            lock (socket)
            {
                data = socket.EndReceive(ar, ref remote);
                socket.BeginReceive(Received, null);
            }

            if(data.Length == 0)
            {
                return;
            }

            if (TryAddAgent(remote, out var agent))
            {
                Console.WriteLine("HeartBeatServer received {0} bytes from {1}", data.Length, remote);
                agent.Received(data);
            }
        }

        private bool TryAddAgent(IPEndPoint remote, out HeartBeatAgent agent)
        {
            agent = null;

            if (ServerAgents.Keys.Count(x => x.Address.Equals(remote.Address)) > MaxServersPerIp)
            {
                return false;
            }

            agent = ServerAgents.GetOrAdd(remote, key => new HeartBeatAgent(key, this, storage, () => RemoveAgent(key)));
            return true;
        }

        private void RemoveAgent(IPEndPoint ep)
        {
            Console.WriteLine("Remote {0} removed", ep);
            if (ServerAgents.TryRemove(ep, out var agent))
            {
                agent.Dispose();
            }
        }

        public void Send(IPEndPoint remote, byte[] data)
        {
            Console.WriteLine("HeartBeatServer sending {0} bytes to {1}", data.Length, remote);
            lock (socket)
            {
                socket.Send(data, data.Length, remote);
            }
        }

        public void Dispose()
        {
            IsDisposed = true;

            ServerAgents.Values.ToList().ForEach(x => x.Dispose());
            socket.Dispose();
        }
    }
}
