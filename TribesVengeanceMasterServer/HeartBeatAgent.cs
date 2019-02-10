using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Threading;

namespace TribesVengeanceMasterServer
{
    public class HeartBeatAgent : IDisposable
    {
        public static TimeSpan BasicResponseTimeout = TimeSpan.FromSeconds(3);
        public static TimeSpan TimeOut = TimeSpan.FromSeconds(60);
        public static TimeSpan BasicMinInterval = TimeSpan.FromSeconds(1);
        public static string GameName = "tribesv";

        private static readonly byte[] Basic = Encoding.ASCII.GetBytes("\\basic\\");

        private readonly HeartBeatServer server;
        private readonly IPEndPoint remote;
        private readonly GameServerStorage storage;
        private readonly Action disposeServer;
        private readonly DateTime AddedAt;
        private DateTime LastMessageAt = DateTime.MinValue;
        private DateTime LastResponseAt = DateTime.MinValue;
        private DateTime LastRequestAt = DateTime.MinValue;
        private byte[] LastResponseCache = null;

        public HeartBeatAgent(IPEndPoint remote, HeartBeatServer server, GameServerStorage storage, Action disposeServer)
        {
            this.remote = remote;
            this.server = server;
            this.storage = storage;
            this.disposeServer = disposeServer;
            AddedAt = DateTime.UtcNow;

            Console.WriteLine("Created HeartBeatAgent for {0}", remote);
        }

        public void Received(byte[] data)
        {
            LastMessageAt = DateTime.UtcNow;

            var currentData = ImmutableDictionary<string, string>.Empty;
            if(storage.Servers.TryGetValue(remote, out var server) && server.Data != null)
            {
                currentData = server.Data;
            }

            ImmutableDictionary<string, string> newData = null;
            if (LastResponseCache != null && LastResponseCache.AsSpan().SequenceEqual(data))
            {
                newData = currentData;
            }
            else if (Encoder.TryParseQueryResponse(data, currentData, out var dict) && dict["gamename"] == GameName)
            {
                newData = dict;
            }

            if (newData != null)
            {
                ResponseReceived(newData);
                LastResponseCache = data;
            }

            Tick();
        }

        public void Tick()
        {
            var now = DateTime.UtcNow;
            var added = AddedAt + TimeOut;
            var liveTimeout = (AddedAt > LastResponseAt ? AddedAt : LastResponseAt) + BasicResponseTimeout;
            var serverTimeout = (AddedAt > LastMessageAt ? AddedAt : LastMessageAt) + TimeOut;

            if (liveTimeout < now)
            {
                storage?.ServerIsOffline(remote);
            }

            if (serverTimeout < now)
            {
                disposeServer?.Invoke();
                return;
            }

            if (now - LastRequestAt > BasicMinInterval && (LastResponseAt > AddedAt || LastRequestAt < AddedAt))
            {
                LastRequestAt = DateTime.UtcNow;
                server.Send(remote, Basic);
            }
        }

        private void ResponseReceived(ImmutableDictionary<string, string> data)
        {
            LastResponseAt = DateTime.UtcNow;
            storage.ServerIsAlive(remote, data);
        }

        public void Dispose()
        {
            storage?.ServerIsOffline(remote);
        }
    }
}
