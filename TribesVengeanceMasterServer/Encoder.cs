using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace TribesVengeanceMasterServer
{
    public static class Encoder
    {
#if Windows
        const string DLL_PATH = "encrypex_decoder.x64.dll";
#else
        const string DLL_PATH = "encrypex_decoder.so";
#endif

        [DllImport(DLL_PATH, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        static extern int enctypex_quick_encrypt(byte[] key, byte[] validate, byte[] data, int size);

        public static byte[] Encrypt(byte[] key, byte[] validate, byte[] data)
        {
            var dataOut = new byte[data.Length + 23];
            int len = data.Length;
            data.CopyTo(dataOut, 0);

            enctypex_quick_encrypt(key, validate, dataOut, len);

            return dataOut;
        }

        public static bool TryParseQueryResponse(ReadOnlySpan<byte> bytes, ImmutableDictionary<string, string> previousData, out ImmutableDictionary<string, string> data, char separator = '\\')
        {
            data = previousData;

            if(bytes[0] != separator)
            {
                // This response is completely invalid
                return false;
            }

            string currentKey = null;
            int currentValueStart = 1;
            for (int i = 1; i < bytes.Length; i++)
            {
                if(bytes[i] == separator)
                {
                    var value = Encoding.ASCII.GetString(bytes.Slice(currentValueStart, i - currentValueStart));
                    if (currentKey == null)
                    {
                        currentKey = value;

                        if (string.IsNullOrEmpty(currentKey))
                        {
                            // Keys cannot be empty
                            return false;
                        }
                    }
                    else
                    {
                        if (!data.TryGetValue(currentKey, out var existingValue) || existingValue != value)
                        {
                            data = data.SetItem(currentKey, value);
                        }

                        currentKey = null;
                    }

                    currentValueStart = i + 1;
                }
            }

            if(currentKey != null)
            {
                var value = Encoding.ASCII.GetString(bytes.Slice(currentValueStart, bytes.Length - currentValueStart));

                if (!data.TryGetValue(currentKey, out var existingValue) || existingValue != value)
                {
                    data = data.SetItem(currentKey, value);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        static ushort ToUInt16(byte[] data, int offset)
        {
            if (BitConverter.IsLittleEndian)
                return BitConverter.ToUInt16(BitConverter.IsLittleEndian ? data.Skip(offset).Take(2).Reverse().ToArray() : data, 0);
            return BitConverter.ToUInt16(data, offset);
        }

        static byte[] GetBytes(ushort data)
        {
            return BitConverter.GetBytes(data).Reverse().ToArray();
        }

        public static bool TryDecodeClientRequest(byte[] data, out MasterServerRequestInfo info)
        {
            info = new MasterServerRequestInfo();
            var i = 0;

            var len = ToUInt16(data, i);
            i += 2;

            var magicBytes = data.Skip(i).Take(4).ToArray();
            if (magicBytes[0] != 0 || magicBytes[1] != 1 || magicBytes[2] != 3 || magicBytes[3] != 0)
            {
                return false; // this aint valid
            }
            i += 4;

            i += 3; // idk what this is

            while (true)
            {
                info.Game1 += Encoding.ASCII.GetString(data, i, 1);
                i++;

                if (i >= data.Length)
                {
                    return false; // no terminating nullbyte
                }

                if (data[i] == 0)
                {
                    i++;
                    break;
                }
            }

            while (true)
            {
                info.Game2 += Encoding.ASCII.GetString(data, i, 1);
                i++;

                if (i >= data.Length)
                {
                    return false; // no terminating nullbyte
                }

                if (data[i] == 0)
                {
                    i++;
                    break;
                }
            }

            while (true)
            {
                info.Validate = info.Validate.Concat(new byte[] { data[i] }).ToArray();
                i++;

                if (i >= data.Length)
                {
                    return false; // no terminating nullbyte
                }

                if (data[i] == 0)
                {
                    i++;
                    break;
                }
            }

            while (true)
            {
                info.Query += Encoding.ASCII.GetString(data, i, 1);
                i++;

                if (i >= data.Length)
                {
                    return false; // no terminating nullbyte
                }

                if (data[i] == 0)
                {
                    i++;
                    break;
                }
            }

            return true;
        }

        public static byte[] EncodeClientResponse(IPEndPoint remote, IEnumerable<IPEndPoint> servers, IEnumerable<string> requestedArguments)
        {
            using (var ms = new MemoryStream())
            {
                var addressBytes = remote.Address.GetAddressBytes();
                ms.Write(addressBytes, 0, addressBytes.Length);

                var portBytes = GetBytes((ushort)remote.Port);
                ms.Write(portBytes, 0, portBytes.Length);

                ms.WriteByte((byte)requestedArguments.Count());

                ms.WriteByte(0);

                foreach (var param in requestedArguments)
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
                    ms.Write(GetBytes((ushort)server.Port), 0, 2);
                }

                ms.WriteByte(0); // end servers array

                return ms.ToArray();
            }
        }
    }

    public class MasterServerRequestInfo
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
}
