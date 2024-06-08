using System.Net;
using System.Net.Sockets;

public struct ClientInfo
{
    public EndPoint ClientEndPoint;
}

public class PacketInfo
{
    public byte[] Buffer;
    public ClientInfo Client;
}