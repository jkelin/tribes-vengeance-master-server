using Comms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Comms.Enctypex;

namespace TestServer
{
    class Program
    {
        public static void Main()
        {
            var t1 = new Thread(QueryServer);
            var t2 = new Thread(HeartBeatServer);

            t1.Start();
            t2.Start();

            Console.WriteLine("Hit enter to stop...");
            Console.Read();

            t1.Abort();
            t2.Abort();
        }

        public static void HeartBeatServer()
        {
            UdpClient newsock = new UdpClient(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 27900));

            var sw = new Stopwatch();

            sw.Start();
            while (true)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                var data = newsock.Receive(ref sender);

                // clients report using qr2 gamespy api sdk. While this is nice, it is always better to verify that client is actually reachable,
                // so actual content of these packets is not as much relevant

                // full heartbeats seem to happen every 60 seconds
                // at 20 and 40 second mark, partial HBs happen
                // final, goodbye, packet is also sent

                Console.WriteLine(Encoding.ASCII.GetString(data, 0, data.Length));
                sw.Stop();

                Console.WriteLine("Elapsed since last packet: {0}", sw.Elapsed);
                sw.Restart();
            }
        }

        public static void QueryServer()
        {
            TcpListener server = null;
            try
            {
                server = new TcpListener(IPAddress.Parse("0.0.0.0"), 28910);


                // Start listening for client requests.
                server.Start();

                // Buffer for reading data
                Byte[] bytes = new Byte[256];
                String data = null;

                // Enter the listening loop.
                while (true)
                {
                    Console.Write("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    // You could also user server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    data = null;

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int i;

                    // Loop to receive all the data sent by the client.
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        // Translate data bytes to a ASCII string.
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        Console.WriteLine("Received: {0}", data);

                        var ps = Decode(bytes.Take(i).ToArray());

                        Console.WriteLine("Received ps: {0}", ps);

                        // Process the data sent by the client.
                        var resp = MakeResponse(ps);
                        var msg = EncryptResponse(resp, ps);
                        var testDec2 = DecryptResponsePinvoke(msg.ToArray(), ps); // this is only for quick debugging purposes
                        var testDec = DecryptResponseManaged(msg.ToArray(), ps); // this is only for quick debugging purposes

                        // Send back a response.
                        // stream.Write(new[] { (byte)msg.Length, (byte)0 }, 0, 2);
                        stream.Write(msg, 0, msg.Length);
                        Console.WriteLine("Sent: {0}", data);
                    }

                    // Shutdown and end connection
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }
        }

        public static byte[] MakeResponse(ClientRequestInfo ps)

        {
            IPAddress client = IPAddress.Parse("90.178.44.86");
            var servers = new[]
            {
                new IPEndPoint(IPAddress.Parse("176.9.148.187"), 7788),
                //new IPEndPoint(IPAddress.Parse("208.100.45.13"), 7778),
                //new IPEndPoint(IPAddress.Parse("208.94.242.194"), 8889),
                //new IPEndPoint(IPAddress.Parse("94.23.249.178"), 7778),
                new IPEndPoint(IPAddress.Parse("72.54.15.202"), 7778),
            };
            
            using(var ms = new MemoryStream())
            {
                ms.Write(client.GetAddressBytes(), 0, 4);

                ms.WriteByte(0); //idk what this is, port maybe?
                ms.WriteByte(0); //idk what this is, port maybe?

                ms.WriteByte((byte)ps.Params.Length); // numparams
                ms.WriteByte(0);

                foreach (var param in ps.Params)
                {
                    var bytes = Encoding.ASCII.GetBytes(param);
                    ms.Write(bytes, 0, bytes.Length);
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                }

                foreach (var server in servers)
                {
                    ms.WriteByte(21); // something tribes sent
                    ms.Write(server.Address.GetAddressBytes(), 0, 4);
                    ms.Write(BitConverterBE.GetBytes((ushort)server.Port), 0, 2);
                }

                ms.WriteByte(0); // end servers array

                return ms.ToArray();
            }


            // genuine response from master
            return new byte[]
            {
                90,178,44,86,30,98,14,0,109,97,112,110,97,109,101,0,0,110,117,109,112,108,97,121,101,114,115,0,0,109,97,120,112,108,97,121,101,114,115,0,0,104,111,115,116,110,97,109,101,0,0,104,111,115,116,112,111,114,116,0,0,103,97,109,101,116,121,112,101,0,0,103,97,109,101,118,101,114,0,0,112,97,115,115,119,111,114,100,0,0,103,97,109,101,110,97,109,101,0,0,103,97,109,101,109,111,100,101,0,0,103,97,109,101,118,97,114,105,97,110,116,0,0,116,114,97,99,107,105,110,103,115,116,97,116,115,0,0,100,101,100,105,99,97,116,101,100,0,0,109,105,110,118,101,114,0,0,21,208,100,45,13,30,98,21,208,94,242,194,34,185,21,176,9,148,187,30,108,21,94,23,249,178,30,98,21,72,54,15,202,30,98,0,255,255,255,255
            };
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct enctypex_data_t
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
            public string encxkey;
            public int offset;
            public int start;
        }

        [DllImport(@"encrypex_decoder.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static unsafe extern int enctypex_quick_encrypt(byte[] key, byte[] validate, byte[] data, int size);

        [DllImport(@"encrypex_decoder.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static unsafe extern IntPtr enctypex_decoder(byte[] key, byte[] validate, byte[] data, ref int datalen, ref enctypex_data_t a);

        public static byte[] DecryptResponseManaged(byte[] data, ClientRequestInfo ps)
        {
            Enctypex_data_t encx_data = null;
            long len = (long)data.Length;
            var xyz = Encoding.ASCII.GetBytes("y3D28k");
            var decoded = Enctypex.enctypex_decoder(ref xyz, ref ps.Validate, ref data, ref len, ref encx_data);

            return decoded;
        }

        public static byte[] DecryptResponsePinvoke(byte[] data, ClientRequestInfo ps)
        {
            enctypex_data_t enctypex_Data_T = new enctypex_data_t();

            var key = Encoding.ASCII.GetBytes("y3D28k");
            var len = data.Length;
            var something = enctypex_decoder(key, ps.Validate, data, ref len, ref enctypex_Data_T); // because refs are fast, right? right?
            //var something = Enctypex.enctypex_encoder(ref key, ref ps.Validate, ref dataOut, ref len, ref enctypex_Data_T); // because refs are fast, right? right?

            var dataOut = new byte[data.Length - 23];
            Marshal.Copy(something, dataOut, 0, data.Length - 23);

            return dataOut;
        }

        public static byte[] EncryptResponse(byte[] data, ClientRequestInfo ps)
        {
            var dataOut = new byte[data.Length + 23];
            int len = data.Length;
            data.CopyTo(dataOut, 0);

            var key = Encoding.ASCII.GetBytes("y3D28k");
            var something = enctypex_quick_encrypt(key, ps.Validate, dataOut, len); // because refs are fast, right? right?
            //var something = Enctypex.enctypex_encoder(ref key, ref ps.Validate, ref dataOut, ref len, ref enctypex_Data_T); // because refs are fast, right? right?

            return dataOut;
        }

        public class ClientRequestInfo
        {
            public string Game1 = "";

            public string Game2 = "";

            public byte[] Validate = new byte[0];

            public string Query = "";

            public string[] Params
            {
                get
                {
                    return Query.Split('\\').Where(x => !String.IsNullOrEmpty(x)).ToArray();
                }
            }

            public override string ToString()
            {
                return $"{Game1} {Game2} '{Encoding.ASCII.GetString(Validate)}' {Query}";
            }
        }

        static public ClientRequestInfo Decode(byte[] data)
        {
            var ps = new ClientRequestInfo();
            var i = 0;

            var len = BitConverterBE.ToUInt16(data, i);
            i += 2;

            var magicBytes = data.Skip(i).Take(4).ToArray();
            if (magicBytes[0] != 0 || magicBytes[1] != 1 || magicBytes[2] != 3 || magicBytes[3] != 0)
            {
                return null; // this shit aint tribes
            }
            i += 4;

            i += 3; // idk what this is

            while (true)
            {
                ps.Game1 += Encoding.ASCII.GetString(data, i, 1);
                i++;

                if (i >= data.Length)
                {
                    return null; // no terminating nullbyte
                }

                if (data[i] == 0)
                {
                    i++;
                    break;
                }
            }

            while (true)
            {
                ps.Game2 += Encoding.ASCII.GetString(data, i, 1);
                i++;

                if (i >= data.Length)
                {
                    return null; // no terminating nullbyte
                }

                if (data[i] == 0)
                {
                    i++;
                    break;
                }
            }

            while (true)
            {
                ps.Validate = ps.Validate.Concat(new byte[] { data[i] }).ToArray();
                i++;

                if (i >= data.Length)
                {
                    return null; // no terminating nullbyte
                }

                if (data[i] == 0)
                {
                    i++;
                    break;
                }
            }

            while (true)
            {
                ps.Query += Encoding.ASCII.GetString(data, i, 1);
                i++;

                if (i >= data.Length)
                {
                    return null; // no terminating nullbyte
                }

                if (data[i] == 0)
                {
                    i++;
                    break;
                }
            }

            return ps;
        }
    }

    public static class BitConverterBE
    {
        public static ushort ToUInt16(byte[] data, int offset)
        {
            if (BitConverter.IsLittleEndian)
                return BitConverter.ToUInt16(BitConverter.IsLittleEndian ? data.Skip(offset).Take(2).Reverse().ToArray() : data, 0);
            return BitConverter.ToUInt16(data, offset);
        }

        public static byte[] GetBytes(ushort data)
        {
            return BitConverter.GetBytes(data).Reverse().ToArray();
        }
    }
}
