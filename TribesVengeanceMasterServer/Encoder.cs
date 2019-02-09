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

        public static bool TryParseQueryResponse(ReadOnlySpan<byte> bytes, ImmutableDictionary<string, string> previousData, out ImmutableDictionary<string, string> data, byte separator = 92)
        {
            data = previousData;

            if(bytes.Length == 0)
            {
                return false;
            }

            if(bytes[0] != separator)
            {
                // This response is completely invalid
                return false;
            }

            string currentKey = null;
            int currentValueStart = 1;
            while(currentValueStart < bytes.Length - 1)
            {
                var value = ReadStringFromData(bytes.Slice(currentValueStart), out var shift, separator);
                currentValueStart += shift + 1;

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
            }

            return currentKey == null;
        }

        static ushort ToUInt16(ReadOnlySpan<byte> data, int offset)
        {
            var slice = data.Slice(offset, 2);
            if (BitConverter.IsLittleEndian)
            {
                unchecked
                {
                    return (ushort)(slice[offset] << 8 | slice[1]);
                }
            }

            return BitConverter.ToUInt16(slice);
        }

        static byte[] GetBytes(ushort data)
        {
            return BitConverter.GetBytes(data).Reverse().ToArray();
        }

        private static string ReadStringFromData(ReadOnlySpan<byte> data, out int endOffset, byte separator = 92)
        {
            for (endOffset = 0; endOffset < data.Length; endOffset++)
            {
                if(data[endOffset] == separator)
                {
                    return Encoding.ASCII.GetString(data.Slice(0, endOffset));
                }
            }

            return Encoding.ASCII.GetString(data);
        }

        private static ReadOnlySpan<byte> ReadSpanFromData(ReadOnlySpan<byte> data, out int endOffset, byte separator = 92)
        {
            for (endOffset = 0; endOffset < data.Length; endOffset++)
            {
                if (data[endOffset] == separator)
                {
                    return data.Slice(0, endOffset);
                }
            }

            return data;
        }

        public static bool TryDecodeClientRequest(ReadOnlySpan<byte> data, out MasterServerRequestInfo info)
        {
            info = new MasterServerRequestInfo();
            var i = 0;

            var len = ToUInt16(data, i);
            i += 2;

            var magicBytes = data.Slice(i, 4);
            if (magicBytes[0] != 0 || magicBytes[1] != 1 || magicBytes[2] != 3 || magicBytes[3] != 0)
            {
                return false; // this aint valid
            }
            i += 4;

            i += 3; // idk what this is


            info.Game1 = ReadStringFromData(data.Slice(i), out var shift, 0);
            i += shift + 1;

            info.Game2 = ReadStringFromData(data.Slice(i), out var shift2, 0);
            i += shift2 + 1;

            info.Validate = ReadSpanFromData(data.Slice(i), out var shift3, 0).ToArray();
            i += shift3 + 1;

            info.Query = ReadStringFromData(data.Slice(i), out var shift4, 0);
            i += shift4 + 1;

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
        public string Game1;

        public string Game2;

        public byte[] Validate;

        public string Query;

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
