using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace UR.Package
{
    public class PackageWriter
    {
        byte[] buffer = new byte[4096];
        int buffer_pos = 0;
        byte[] temp = new byte[8];

        public PackageWriter()
        {

        }

        public void Write(bool v)
        {
            buffer[buffer_pos] = (byte)(v ? 1 : 0);
            buffer_pos += 1;
        }

        public void Write(byte v)
        {
            buffer[buffer_pos] = v;
            buffer_pos += 1;
        }

        public void Write(ushort v)
        {
            BitConverter.TryWriteBytes(temp, v);
            buffer[buffer_pos] = temp[1];
            buffer[buffer_pos + 1] = temp[0];
            buffer_pos += 2;
        }

        public void Write(uint v)
        {
            BitConverter.TryWriteBytes(temp, v);
            buffer[buffer_pos] = temp[3];
            buffer[buffer_pos + 1] = temp[2];
            buffer[buffer_pos + 2] = temp[1];
            buffer[buffer_pos + 3] = temp[0];
            buffer_pos += 4;
        }

        public void Write(int v)
        {
            BitConverter.TryWriteBytes(temp, v);
            buffer[buffer_pos] = temp[3];
            buffer[buffer_pos + 1] = temp[2];
            buffer[buffer_pos + 2] = temp[1];
            buffer[buffer_pos + 3] = temp[0];
            buffer_pos += 4;
        }

        public void Write(ulong v)
        {
            BitConverter.TryWriteBytes(temp, v);
            buffer[buffer_pos] = temp[7];
            buffer[buffer_pos + 1] = temp[6];
            buffer[buffer_pos + 2] = temp[5];
            buffer[buffer_pos + 3] = temp[4];
            buffer[buffer_pos + 4] = temp[3];
            buffer[buffer_pos + 5] = temp[2];
            buffer[buffer_pos + 6] = temp[1];
            buffer[buffer_pos + 7] = temp[0];
            buffer_pos += 8;
        }

        public void Write(double v)
        {
            BitConverter.TryWriteBytes(temp, v);
            buffer[buffer_pos] = temp[7];
            buffer[buffer_pos + 1] = temp[6];
            buffer[buffer_pos + 2] = temp[5];
            buffer[buffer_pos + 3] = temp[4];
            buffer[buffer_pos + 4] = temp[3];
            buffer[buffer_pos + 5] = temp[2];
            buffer[buffer_pos + 6] = temp[1];
            buffer[buffer_pos + 7] = temp[0];
            buffer_pos += 8;
        }

        public void Write(double[] v)
        {
            foreach (var v1 in v)
            {
                Write(v1);
            }
        }

        public void Write(int[] v)
        {
            foreach (var v1 in v)
            {
                Write(v1);
            }
        }

        public void WriteString(string v)
        {
            var b = System.Text.UTF8Encoding.UTF8.GetBytes(v);
            Array.Copy(b, 0, buffer, buffer_pos, b.Length);
            buffer_pos += b.Length;
        }

        public ArraySegment<byte> GetBytes()
        {
            BitConverter.TryWriteBytes(temp, (ushort)buffer_pos);
            buffer[0] = temp[1];
            buffer[1] = temp[0];
            return new ArraySegment<byte>(buffer, 0, (int)buffer_pos);
        }

        public ArraySegment<byte> GetRawBytes()
        {
            return new ArraySegment<byte>(buffer, 0, (int)buffer_pos);
        }

        public void Begin(byte t)
        {
            buffer_pos = 0;
            Write((ushort)0);
            Write((byte)t);
        }

        public void Begin()
        {
            buffer_pos = 0;
        }
    }

    public class PackageReader
    {
        int buffer_pos = 0;
        int buffer_end = 0;
        byte[] buffer;

        byte[] temp = new byte[8];
        public byte PackageType { get; private set; }

        public int PackageRemaining => buffer_end - buffer_pos;

        public PackageReader BeginRtde(ArraySegment<byte> package_buffer)
        {
            buffer = package_buffer.Array;
            buffer_pos = package_buffer.Offset;
            buffer_end = package_buffer.Offset + package_buffer.Count;
            buffer_pos += 2;
            var t = ReadByte();
            PackageType = t;
            return this;
        }

        public PackageReader BeginController(ArraySegment<byte> package_buffer)
        {
            buffer = package_buffer.Array;
            buffer_pos = package_buffer.Offset;
            buffer_end = package_buffer.Offset + package_buffer.Count;
            buffer_pos += 4;
            PackageType = 0;
            return this;
        }

        void CheckAvailable(int len)
        {

        }

        public byte ReadByte()
        {
            CheckAvailable(1);
            var v = buffer[buffer_pos];
            buffer_pos++;
            return v;
        }

        public bool ReadBool()
        {
            return ReadByte() != 0 ? true : false;
        }

        public ushort ReadUInt16()
        {
            CheckAvailable(2);
            temp[1] = buffer[buffer_pos];
            temp[0] = buffer[buffer_pos + 1];
            buffer_pos += 2;

            return BitConverter.ToUInt16(temp, 0);
        }

        public uint ReadUInt32()
        {
            CheckAvailable(4);
            temp[3] = buffer[buffer_pos];
            temp[2] = buffer[buffer_pos + 1];
            temp[1] = buffer[buffer_pos + 2];
            temp[0] = buffer[buffer_pos + 3];
            buffer_pos += 4;
            return BitConverter.ToUInt32(temp, 0);
        }

        public int ReadInt32()
        {
            CheckAvailable(4);
            temp[3] = buffer[buffer_pos];
            temp[2] = buffer[buffer_pos + 1];
            temp[1] = buffer[buffer_pos + 2];
            temp[0] = buffer[buffer_pos + 3];
            buffer_pos += 4;
            return BitConverter.ToInt32(temp, 0);
        }

        public uint ReadUInt64()
        {
            CheckAvailable(8);
            temp[7] = buffer[buffer_pos];
            temp[6] = buffer[buffer_pos + 1];
            temp[5] = buffer[buffer_pos + 2];
            temp[4] = buffer[buffer_pos + 3];
            temp[3] = buffer[buffer_pos + 4];
            temp[2] = buffer[buffer_pos + 5];
            temp[1] = buffer[buffer_pos + 6];
            temp[0] = buffer[buffer_pos + 7];
            buffer_pos += 8;
            return BitConverter.ToUInt32(temp, 0);
        }

        public float ReadSingle()
        {
            CheckAvailable(4);
            temp[3] = buffer[buffer_pos];
            temp[2] = buffer[buffer_pos + 1];
            temp[1] = buffer[buffer_pos + 2];
            temp[0] = buffer[buffer_pos + 3];

            buffer_pos += 4;
            return BitConverter.ToSingle(temp, 0);
        }

        public double ReadDouble()
        {
            CheckAvailable(8);
            temp[7] = buffer[buffer_pos];
            temp[6] = buffer[buffer_pos + 1];
            temp[5] = buffer[buffer_pos + 2];
            temp[4] = buffer[buffer_pos + 3];
            temp[3] = buffer[buffer_pos + 4];
            temp[2] = buffer[buffer_pos + 5];
            temp[1] = buffer[buffer_pos + 6];
            temp[0] = buffer[buffer_pos + 7];
            buffer_pos += 8;
            return BitConverter.ToDouble(temp, 0);
        }

        public void ReadDoubleVec6(double[] v)
        {
            for (int i = 0; i < 6; i++)
            {
                v[i] = ReadDouble();
            }
        }

        public void ReadDoubleVec3(double[] v)
        {
            for (int i = 0; i < 3; i++)
            {
                v[i] = ReadDouble();
            }
        }

        public void ReadInt32Vec6(int[] v)
        {
            for (int i = 0; i < 6; i++)
            {
                v[i] = ReadInt32();
            }
        }

        public void ReadUInt32Vec6(uint[] v)
        {
            for (int i = 0; i < 6; i++)
            {
                v[i] = ReadUInt32();
            }
        }

        public string ReadStringRemaining()
        {
            return System.Text.UTF8Encoding.UTF8.GetString(buffer, buffer_pos, buffer_end - buffer_pos);
        }

        public void Skip(int count)
        {
            buffer_pos += count;
        }

        public PackageReader SubReader(out byte sub_package_type)
        {
            int rem = PackageRemaining;
            if (rem < 5)
            {
                throw new IOException("Invalid package subreader");
            }

            temp[3] = buffer[buffer_pos];
            temp[2] = buffer[buffer_pos + 1];
            temp[1] = buffer[buffer_pos + 2];
            temp[0] = buffer[buffer_pos + 3];
            int len = BitConverter.ToInt32(temp, 0);
            sub_package_type = buffer[buffer_pos + 4];

            if (len > rem)
            {
                throw new IOException("Invalid subpackage length");
            }

            var p = new PackageReader();
            p.BeginController(new ArraySegment<byte>(buffer, buffer_pos, len));
            buffer_pos += len;
            return p;
        }
    }
}
