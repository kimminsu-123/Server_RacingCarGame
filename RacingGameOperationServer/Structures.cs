using System;
using System.Data;
using System.Diagnostics;
using System.Net;

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
        Console.WriteLine($@"
================================
[{DateTime.Now}] 
[{title}]
--------------------------------
{contents}
================================");
        Console.ForegroundColor = ConsoleColor.White;
    }
}

public class ClientInfo
{
    public string Id;
    public EndPoint ClientEndPoint;
}

public enum PacketType
{
    Connect,
    Disconnect,
    
    SyncTransform,
    GoalLine,
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

    public const int MaxLenSessionId = 16;
    public const int MaxLenPlayerId = 16;
}