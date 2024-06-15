using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Net;

public struct Vector3
{
    public float x;
    public float y;
    public float z;
}

public struct Quaternion
{
    public float x;
    public float y;
    public float z;
    public float w;
}

public static class Logger
{
    public static void LogInfo(string title, string contents)
    {
        LogCustom(title, contents, ConsoleColor.Green);
    }

    public static void LogWarning(string title, string contents)
    {
        LogCustom(title, contents, ConsoleColor.Yellow);
    }

    public static void LogError(string title, string contents)
    {
        LogCustom(title, contents, ConsoleColor.Red);
    }

    public static void LogCustom(string title, string contents,
        ConsoleColor foregroundColor = ConsoleColor.White)
    {
        Console.ForegroundColor = foregroundColor;
        Console.WriteLine(
$@"================================
[{DateTime.Now}] 
[{title}]
--------------------------------
{contents}
================================");
        Console.ForegroundColor = ConsoleColor.White;
    }
}

public enum Status
{
    Connected,
    Playing,
    EndPlay,
}

public class ClientInfo
{
    public string Id;
    public EndPoint ClientEndPoint;
    public Status CurrentStatus;
}

public enum PacketType
{
    Connect,
    Disconnect,
    
    StartGame,
    SyncTransform,
    GoalLine,
    EndGame,
}

public enum ResultType
{
    Success,
    Failed
}

public struct PacketHeader
{
    public ResultType ResultType;
    public PacketType PacketType;
}

public class PacketInfo
{
    public PacketHeader Header;
    public byte[] Buffer;
    public EndPoint ClientEndPoint;
}

public class ConnectionPacket : IPacket<ConnectionData>
{
    private class ConnectionDataSerializer : Serializer
    {
        public bool Serialize(ConnectionData data)
        {
            Clear();
        
            bool ret = true;

            ret &= Serialize(data.SessionId, ConnectionData.MaxLenSessionId);
            ret &= Serialize(data.PlayerId, ConnectionData.MaxLenPlayerId);
        
            return ret;
        }

        public bool Deserialize(byte[] bytes, ref ConnectionData data)
        {
            bool ret = true;

            string sessionId = string.Empty;
            string playerId = string.Empty;
            
            ret &= SetBuffer(bytes);
            ret &= Deserialize(ref sessionId, ConnectionData.MaxLenSessionId);
            ret &= Deserialize(ref playerId, ConnectionData.MaxLenPlayerId);
            
            data.SessionId = sessionId;
            data.PlayerId = playerId;
        
            return ret;
        }
    }

    private ConnectionData _connectionData;
    private ConnectionDataSerializer _serializer;
    
    public ConnectionPacket(ConnectionData data)
    {
        _serializer = new ConnectionDataSerializer();

        _connectionData = data;
    }
    
    public ConnectionPacket(byte[] data)
    {
        _connectionData = new ConnectionData();

        _serializer = new ConnectionDataSerializer();
        _serializer.Deserialize(data, ref _connectionData);
    }
    
    public byte[] GetBytes()
    {
        _serializer.Serialize(_connectionData);

        return _serializer.GetBuffer();
    }

    public ConnectionData GetData()
    {
        return _connectionData;
    }
}

public class ConnectionData
{
    public string SessionId;
    public string PlayerId;

    public const int MaxLenSessionId = 64;
    public const int MaxLenPlayerId = 64;
}

public class TransformPacket : IPacket<TransformData>
{
    private class TransformDataSerializer : Serializer
    {
        public bool Serialize(TransformData data)
        {
            Clear();
        
            bool ret = true;

            ret &= Serialize(data.SessionId, ConnectionData.MaxLenSessionId);
            ret &= Serialize(data.PlayerId, ConnectionData.MaxLenPlayerId);
            ret &= Serialize(data.Position.x);
            ret &= Serialize(data.Position.y);
            ret &= Serialize(data.Position.z);
            ret &= Serialize(data.Rotation.x);
            ret &= Serialize(data.Rotation.y);
            ret &= Serialize(data.Rotation.z);
            ret &= Serialize(data.Rotation.w);
        
            return ret;
        }

        public bool Deserialize(byte[] bytes, ref TransformData data)
        {
            bool ret = true;

            ret &= SetBuffer(bytes);
            ret &= Deserialize(ref data.SessionId, ConnectionData.MaxLenSessionId);
            ret &= Deserialize(ref data.PlayerId, ConnectionData.MaxLenPlayerId);
            ret &= Deserialize(ref data.Position.x);
            ret &= Deserialize(ref data.Position.y);
            ret &= Deserialize(ref data.Position.z);
            ret &= Deserialize(ref data.Rotation.x);
            ret &= Deserialize(ref data.Rotation.y);
            ret &= Deserialize(ref data.Rotation.z);
            ret &= Deserialize(ref data.Rotation.w);
            
            return ret;
        }
    }

    private TransformData _transformData;
    private TransformDataSerializer _serializer;
    
    public TransformPacket(TransformData data)
    {
        _serializer = new TransformDataSerializer();

        _transformData = data;
    }
    
    public TransformPacket(byte[] data)
    {
        _transformData = new TransformData();
        _serializer = new TransformDataSerializer();
        
        _serializer.Deserialize(data, ref _transformData);
    }
    
    public byte[] GetBytes()
    {
        _serializer.Serialize(_transformData);

        return _serializer.GetBuffer();
    }

    public TransformData GetData()
    {
        return _transformData;
    }
}

public class TransformData
{
    public string SessionId;
    public string PlayerId;
    public Vector3 Position;
    public Quaternion Rotation;
}