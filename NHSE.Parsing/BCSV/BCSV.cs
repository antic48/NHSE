﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHSE.Parsing
{
    public class BCSV
    {
        public const int MAGIC = 0x42435356; // BCSV

        public readonly byte[] Data;

        public readonly uint EntryCount;
        public readonly uint EntryLength;
        public readonly ushort FieldCount;
        public readonly bool HasBCSVHeader;
        public readonly bool Flag2;

        public readonly uint Magic;
        public readonly int Unknown;
        public readonly int Unknown1;
        public readonly int Unknown2;

        private readonly int FieldTableStart;
        public readonly IReadOnlyList<BCSVFieldParam> FieldOffsets;

        public BCSV(byte[] data)
        {
            Data = data;

            EntryCount = BitConverter.ToUInt32(data, 0x0);
            EntryLength = BitConverter.ToUInt32(data, 0x4);
            FieldCount = BitConverter.ToUInt16(data, 0x8);
            HasBCSVHeader = data[0xA] == 1;
            Flag2 = data[0xB] == 1;
            if (HasBCSVHeader)
            {
                Magic = BitConverter.ToUInt32(data, 0xC);
                if (Magic != MAGIC)
                    throw new ArgumentException(nameof(Magic));
                Unknown = BitConverter.ToInt32(data, 0x10);
                Unknown1 = BitConverter.ToInt32(data, 0x14);
                Unknown2 = BitConverter.ToInt32(data, 0x18);
                FieldTableStart = 0x1C;
            }
            else
            {
                FieldTableStart = 0x0C;
            }

            var fields = new BCSVFieldParam[FieldCount];
            for (int i = 0; i < fields.Length; i++)
            {
                var ofs = FieldTableStart + (i * BCSVFieldParam.SIZE);
                var ident = BitConverter.ToUInt32(data, ofs);
                var fo = BitConverter.ToInt32(data, ofs + 4);

                fields[i] = new BCSVFieldParam(ident, fo, i);
            }

            FieldOffsets = fields;
        }

        private int GetFirstEntryOffset() => FieldTableStart + (FieldCount * BCSVFieldParam.SIZE);
        private int GetEntryOffset(int start, int entry) => start + (entry * (int)EntryLength);

        private string ReadFieldUnknownType(in int offset, in int fieldIndex)
        {
            var length = GetFieldLength(fieldIndex);
            // ReSharper disable once ConvertSwitchStatementToSwitchExpression
#pragma warning disable IDE0066 // Convert switch statement to expression
            switch (length)
#pragma warning restore IDE0066 // Convert switch statement to expression
            {
                case 1: return Data[offset].ToString();
                case 2: return BitConverter.ToInt16(Data, offset).ToString();
                case 4: return $"0x{BitConverter.ToUInt32(Data, offset):X8}";
                case 8: return $"0x{BitConverter.ToUInt64(Data, offset):X16}";

                default: return Encoding.UTF8.GetString(Data, offset, length);
            }
        }

        private int GetFieldLength(in int i)
        {
            var next = (i + 1 == FieldCount) ? (int)(EntryLength) : FieldOffsets[i + 1].Offset;
            var ofs = FieldOffsets[i].Offset;
            return next - ofs;
        }

        public string[] ReadCSV(string delim = "\t")
        {
            var result = new string[EntryCount + 1];
            result[0] = string.Join(delim, FieldOffsets.Select(z => $"0x{z.ColumnKey:X8}"));

            var start = GetFirstEntryOffset();
            for (int entry = 0; entry < EntryCount; entry++)
            {
                var ofs = GetEntryOffset(start, entry);
                string[] fields = new string[FieldCount];
                for (int f = 0; f < fields.Length; f++)
                {
                    var fo = ofs + FieldOffsets[f].Offset;
                    fields[f] = ReadFieldUnknownType(fo, f);
                }
                result[entry + 1] = string.Join(delim, fields);
            }

            return result;
        }

        public string ReadValue(int entry, BCSVFieldParam f)
        {
            var start = GetFirstEntryOffset();
            var ofs = GetEntryOffset(start, entry);
            return ReadFieldUnknownType(ofs + f.Offset, f.Index);
        }
    }
}