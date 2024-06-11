using System.Net;

public class ClientInfo
{
    public EndPoint ClientEndPoint;
}

public enum PacketType
{
    Connect,
    Disconnect,
    
    SyncTransform,
    GoalLine,
}

public struct PacketHeader
{
    public PacketType PacketType;
    public int PacketId;
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