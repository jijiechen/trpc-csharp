using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;

namespace TrpcSharp.Protocol.Framing
{
    internal static class Extensions
    {
        public static ByteString ToByteString(this string str)
        {
            return str == null ? ByteString.Empty : ByteString.CopyFrom(Encoding.UTF8.GetBytes(str));
        }

        public static Dictionary<string, TrpcAdditionalData> ToAdditionalData(this MapField<string, ByteString> transInfo)
        {
            if (transInfo == null)
            {
                return new Dictionary<string, TrpcAdditionalData>();
            }
            
            return transInfo
                       .ToDictionary(i => i.Key, 
                           i=> new TrpcAdditionalData(i.Value.Memory));
        }

        public static void CopyTo(this IReadOnlyDictionary<string, TrpcAdditionalData> additionalData, MapField<string, ByteString> pbMap)
        {
            foreach (var key in additionalData.Keys)
            {
                var item = additionalData[key].AsBytes();
                pbMap[key] = ByteString.CopyFrom(item.Span);
            }
        }

        public static void WriteTo(this Stream inputStream, IBufferWriter<byte> output)
        {
            const int bufferSize = 4096;
            while (true)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                var bytesRead = inputStream.Read(buffer, 0, bufferSize);
                if (bytesRead == 0)
                {
                    break;
                }

                output.Write(buffer.AsSpan(0, bytesRead));
                ArrayPool<byte>.Shared.Return(buffer);
                if (bytesRead < bufferSize)
                {
                    break;
                }
            }
        }
    }
}