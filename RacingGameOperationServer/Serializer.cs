using System;
using System.IO;
using System.Text;

public class Serializer
{
    public bool IsLittleEndian => BitConverter.IsLittleEndian;

    private MemoryStream _buffer;
    private int _offset;

    public Serializer()
    {
        _offset = 0;
        _buffer = new MemoryStream();
    }

    public void Clear()
    {
        byte[] buffer = _buffer.GetBuffer();
        Array.Clear(buffer, 0, buffer.Length);
        _buffer.Position = 0;
        _buffer.SetLength(0);
        _offset = 0;
    }

    public byte[] GetBuffer() => _buffer.ToArray();
    
    public bool SetBuffer(byte[] buffer)
    {
        Clear();

        try
        {
            _buffer.Write(buffer, 0, buffer.Length);
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    public bool Serialize(byte[] value)
    {
        if (IsLittleEndian)
        {
            Array.Reverse(value);
        }

        return WriteBuffer(value, value.Length);
    }

    public bool Serialize(short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        return WriteBuffer(bytes, sizeof(short));
    }

    public bool Serialize(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        return WriteBuffer(bytes, sizeof(int));
    }

    public bool Serialize(long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        return WriteBuffer(bytes, sizeof(long));
    }

    public bool Serialize(char value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        return WriteBuffer(bytes, sizeof(char));
    }

    public bool Serialize(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        
        if (IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return WriteBuffer(bytes, bytes.Length);
    }

    public bool Serialize(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        return WriteBuffer(bytes, sizeof(float));
    }

    public bool Serialize(double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        return WriteBuffer(bytes, sizeof(double));
    }

    public bool Serialize(bool value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        return WriteBuffer(bytes, sizeof(bool));
    }

    public bool Deserialize(ref short ret)
    {
        int size = sizeof(short);
        byte[] data = new byte[size];

        bool success = ReadBuffer(ref data, data.Length);
        if (success)
        {
            ret = BitConverter.ToInt16(data, 0);
        }

        return success;
    }
    
    public bool Deserialize(ref int ret)
    {
        int size = sizeof(int);
        byte[] data = new byte[size];

        bool success = ReadBuffer(ref data, data.Length);
        if (success)
        {
            ret = BitConverter.ToInt32(data, 0);
        }

        return success;
    }
    
    public bool Deserialize(ref long ret)
    {
        int size = sizeof(long);
        byte[] data = new byte[size];

        bool success = ReadBuffer(ref data, data.Length);
        if (success)
        {
            ret = BitConverter.ToInt64(data, 0);
        }

        return success;
    }
    
    public bool Deserialize(ref char ret)
    {
        int size = sizeof(char);
        byte[] data = new byte[size];

        bool success = ReadBuffer(ref data, data.Length);
        if (success)
        {
            ret = BitConverter.ToChar(data, 0);
        }

        return success;
    }
    
    public bool Deserialize(ref string ret, int length)
    {
        byte[] data = new byte[length];

        bool success = ReadBuffer(ref data, data.Length);
        if (success)
        {
            if (IsLittleEndian) {
                Array.Reverse(data);	
            }
            ret = Encoding.UTF8.GetString(data);
        }

        return success;
    }
    
    public bool Deserialize(ref float ret)
    {
        int size = sizeof(float);
        byte[] data = new byte[size];

        bool success = ReadBuffer(ref data, data.Length);
        if (success)
        {
            ret = BitConverter.ToSingle(data, 0);
        }

        return success;
    }
    
    public bool Deserialize(ref double ret)
    {
        int size = sizeof(double);
        byte[] data = new byte[size];

        bool success = ReadBuffer(ref data, data.Length);
        if (success)
        {
            ret = BitConverter.ToDouble(data, 0);
        }

        return success;
    }
    
    public bool Deserialize(ref bool ret)
    {
        int size = sizeof(bool);
        byte[] data = new byte[size];

        bool success = ReadBuffer(ref data, data.Length);
        if (success)
        {
            ret = BitConverter.ToBoolean(data, 0);
        }

        return success;
    }
    
    private bool ReadBuffer(ref byte[] bytes, int size)
    {
        try
        {
            _buffer.Position = _offset;
            _buffer.Read(bytes, 0, size);
            _offset += size;
        }
        catch (Exception)
        {
            return false;
        }

        if (IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return true;
    }

    private bool WriteBuffer(byte[] bytes, int size)
    {
        if (IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        try
        {
            _buffer.Position = _offset;
            _buffer.Write(bytes, 0, size);
            _offset += size;
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }
}

public class PacketHeaderSerializer : Serializer
{
    public bool Serialize(PacketHeader header)
    {
        Clear();
        
        bool ret = true;

        ret &= Serialize((int)header.PacketType);
        ret &= Serialize(header.PacketId);
        
        return ret;
    }

    public bool Deserialize(byte[] bytes, ref PacketHeader header)
    {
        bool ret = true;

        int packetType = 0;
        int packetId = 0;
        
        ret &= SetBuffer(bytes);
        ret &= Deserialize(ref packetType);
        ret &= Deserialize(ref packetId);

        header.PacketType = (PacketType) packetType;
        header.PacketId = packetId;
        
        return ret;
    }
}