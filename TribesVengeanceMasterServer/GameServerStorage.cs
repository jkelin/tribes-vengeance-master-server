using MessagePack;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TribesVengeanceMasterServer
{
    public class GameServerStorage : IDisposable
    {
        public TimeSpan MaxAge = TimeSpan.FromSeconds(60);
        public TimeSpan SaveInterval = TimeSpan.FromSeconds(20);

        public ImmutableDictionary<IPEndPoint, GameServer> Servers { get; private set; }
        private readonly AsyncLock Lock = new AsyncLock();
        private readonly CancellationToken ct;
        private readonly FileInfo saveFile;
        private readonly Timer Timer;

        public GameServerStorage(CancellationToken ct, FileInfo saveFile)
        {
            this.ct = ct;
            this.saveFile = saveFile;
            Timer = new Timer(_ => Tick().Wait(), null, SaveInterval, SaveInterval);
            Servers = ImmutableDictionary<IPEndPoint, GameServer>.Empty;
        }

        private GameServerStorage(IEnumerable<GameServer> servers, FileInfo saveFile)
        {
            var minLastLive = DateTime.UtcNow - MaxAge;
            this.saveFile = saveFile;
            Servers = servers.Where(x => x.LastLive > minLastLive).ToImmutableDictionary(x => x.EndPoint);
            Timer = new Timer(_ => Tick().Wait(), null, SaveInterval, SaveInterval);

            Console.WriteLine("Starting GameServerStorage with {0} servers", Servers.Count);
        }

        private async Task Tick()
        {
            using (await Lock.LockAsync(ct))
            {
                var minLastLive = DateTime.UtcNow - MaxAge;
                Servers = Servers.RemoveRange(Servers.Where(x => x.Value.LastLive < minLastLive).Select(x => x.Key));
            }

            await Save();
        }

        public async void ServerIsAlive(IPEndPoint ep, ImmutableDictionary<string, string> data = null)
        {
            using(await Lock.LockAsync(ct))
            {
                if(Servers.TryGetValue(ep, out var server))
                {
                    server = server.Update(data, DateTime.UtcNow);
                }
                else
                {
                    server = new GameServer(ep.Address.GetAddressBytes(), (ushort)ep.Port, data, DateTime.UtcNow);
                    Console.WriteLine("Server registered {0}", ep);
                }

                Servers = Servers.SetItem(ep, server);
            }
        }

        public async void ServerIsOffline(IPEndPoint ep)
        {
            if (Servers.ContainsKey(ep))
            {
                Console.WriteLine("Server went offline {0}", ep);
            }

            using (await Lock.LockAsync(ct))
            {
                Servers = Servers.Remove(ep);
            }
        }

        public static async Task<GameServerStorage> Deserialize(FileInfo file)
        {
            var bytes = await File.ReadAllBytesAsync(file.FullName);
            var servers = LZ4MessagePackSerializer.Deserialize<IEnumerable<GameServer>>(bytes);
            return new GameServerStorage(servers, file);
        }

        private byte[] Serialize()
        {
            var servers = Servers.Values;
            Console.WriteLine("Serializing {0} servers", servers.Count());
            return LZ4MessagePackSerializer.Serialize(Servers.Values);
        }

        public async Task Save()
        {
            try
            {
                var tempPath = new FileInfo(Path.GetTempPath() + Guid.NewGuid().ToString());
                var data = Serialize();
                await File.WriteAllBytesAsync(tempPath.FullName, data, ct);
                tempPath.CopyTo(saveFile.FullName, true);
                tempPath.Delete();

                Console.WriteLine("Saved {0}", saveFile.FullName);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Could not save {0}", saveFile.FullName);
                Console.WriteLine(ex);
            }
        }

        public void Dispose()
        {
        }

        [MessagePackObject]
        public class GameServer
        {
            [Key(0)]
            public readonly byte[] Address;

            [Key(1)]
            public readonly ushort Port;

            [Key(2)]
            public readonly ImmutableDictionary<string, string> Data;

            [Key(3)]
            public readonly DateTime LastLive;

            public GameServer(byte[] address, ushort port, IDictionary<string, string> data, DateTime lastLive)
            {
                Address = address;
                Port = port;
                Data = data.ToImmutableDictionary();
                LastLive = lastLive;
            }

            public GameServer(byte[] address, ushort port, ImmutableDictionary<string, string> data, DateTime lastLive)
            {
                Address = address;
                Port = port;
                Data = data;
                LastLive = lastLive;
            }

            public GameServer Update(ImmutableDictionary<string, string> data, DateTime lastLive)
            {
                return new GameServer(Address, Port, data, lastLive);
            }

            [IgnoreMember]
            public IPEndPoint EndPoint
            {
                get
                {
                    return new IPEndPoint(new IPAddress(Address), Port);
                }
            }
        }
    }
}
