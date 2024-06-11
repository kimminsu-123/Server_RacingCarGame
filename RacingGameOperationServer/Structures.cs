using System.Net;

public class ClientInfo
{
    public EndPoint ClientEndPoint;
}

public class PacketInfo
{
    public PacketHeader Header;
    public byte[] Buffer;
    public EndPoint ClientEndPoint;
}

public struct PacketHeader
{
    public PacketType PacketType;
    public int PacketId;
}

public enum PacketType
{
    Connect,
    Disconnect,
    
    SyncTransform,
    GoalLine,
}