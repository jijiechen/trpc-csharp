﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using TrpcSharp.Protocol.IO;

namespace TrpcSharp.Protocol.Framing
{
    internal static class Extensions
    {
        public static ByteString ToByteString(this string str)
        {
            return str == null ? ByteString.Empty : ByteString.CopyFrom(Encoding.UTF8.GetBytes(str));
        }

        public static Dictionary<string, TrpcMetadataData> ToMetadata(this MapField<string, ByteString> transInfo)
        {
            if (transInfo == null)
            {
                return new Dictionary<string, TrpcMetadataData>();
            }
            
            return transInfo
                       .ToDictionary(i => i.Key, 
                           i=> new TrpcMetadataData(i.Value.Memory));
        }

        public static void CopyTo(this IReadOnlyDictionary<string, TrpcMetadataData> metadata, MapField<string, ByteString> pbMap)
        {
            foreach (var key in metadata.Keys)
            {
                var item = metadata[key].AsBytes();
                pbMap[key] = ByteString.CopyFrom(item.Span);
            }
        }
        
                
        internal static ReadOnlySequence<byte> CopySequence(this ref ReadOnlySequence<byte> seq)
        {
            SequenceSegment head = null;
            SequenceSegment tail = null;

            foreach (var segment in seq)
            {                
                var newSegment = SequenceSegment.CopyFrom(segment);

                if (head == null)
                    tail = head = newSegment;
                else
                    tail = tail.SetNext(newSegment);
            }

            return new ReadOnlySequence<byte>(head, 0, tail, tail!.Memory.Length);
        }

    }
}