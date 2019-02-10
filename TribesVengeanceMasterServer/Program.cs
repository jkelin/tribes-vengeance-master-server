using MessagePack.ImmutableCollection;
using MessagePack.Resolvers;
using System;
using System.IO;
using System.Net;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TribesVengeanceMasterServer
{
    class Program
    {
        public static string StorageFilePath = "./db";
        public static ushort HeartBeatServerPort = 27900;
        public static ushort MasterServerPort = 28910;

        static async Task Main(string[] args)
        {
            CompositeResolver.RegisterAndSetAsDefault(
                ImmutableCollectionResolver.Instance,
                StandardResolver.Instance
            );

            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += ctx =>
            {
                cts.Cancel();
            };
            Console.CancelKeyPress += (sender, cce) => {
                cce.Cancel = true;
                cts.Cancel();
            };

            var storageFile = new FileInfo(StorageFilePath);

            Console.WriteLine("Starting");

            using (var gameServerStorage = await ReadGameServerStorage(storageFile, cts.Token))
            using (var udpServer = new HeartBeatServer(new IPEndPoint(IPAddress.Any, HeartBeatServerPort), gameServerStorage))
            using (var tcpServer = new MasterServer(gameServerStorage))
            {
                udpServer.Listen();
                var masterTask = tcpServer.Listen(new IPEndPoint(IPAddress.Any, MasterServerPort), cts.Token);

                Console.WriteLine("Started");

                cts.Token.WaitHandle.WaitOne();

                Console.WriteLine("Shutting down");

                var saveTask = gameServerStorage.Save(new CancellationTokenSource(500).Token);

                await Task.WhenAll(masterTask, saveTask);
            }
        }

        static async Task<GameServerStorage> ReadGameServerStorage(FileInfo storageFile, CancellationToken ct)
        {
            try
            {
                Console.WriteLine("Reading {0}", storageFile.FullName);
                return await GameServerStorage.Deserialize(storageFile);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("{0} not found", storageFile.FullName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Could not deserialize {0} because", storageFile.FullName);
                Console.Error.WriteLine(ex);
            }

            return new GameServerStorage(ct, storageFile);
        }
    }
}
