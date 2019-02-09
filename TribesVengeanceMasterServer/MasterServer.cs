﻿using System;
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
        private bool IsDisposed = false;

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
            if(IsDisposed)
            {
                return;
            }

            TcpClient client = listener.EndAcceptTcpClient(ar);
            listener.BeginAcceptTcpClient(AcceptedTcpClient, null);
            var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

            if (Agents.Keys.Select(x => ((IPEndPoint)x.Client.RemoteEndPoint).Address).Count(x => x.Equals(remoteIp)) > MaxConnectionsPerIp)
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
            IsDisposed = true;

            Agents.Values.ToList().ForEach(x => x.Dispose());
            listener.Stop();
        }
    }
}
