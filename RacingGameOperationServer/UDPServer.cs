using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

public abstract class UDPServer
{
    protected readonly int Port;
    protected readonly int BufferSize;
    protected readonly int WorkerThreadCount;
        
    protected volatile Socket UdpSocket;
    protected volatile IPEndPoint ServerIpEndPoint;
    protected volatile bool IsRunning;
    protected volatile PacketHeaderSerializer HeaderSerializer;

    protected const int RECEIVE_INTERVAL_MS = 100;
    protected const int SEND_INTERVAL_MS = 100;

    protected Action OnStart;
    protected Action<PacketInfo> OnReceived;
    protected Action<SocketAsyncEventArgs> OnSent;

    private readonly ConcurrentQueue<PacketInfo> _sendQueue = new ConcurrentQueue<PacketInfo>();

    public UDPServer(int port, int bufferSize, int workerThreadCount)
    {
        Port = port;
        BufferSize = bufferSize;
        WorkerThreadCount = workerThreadCount;
        UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        ServerIpEndPoint = new IPEndPoint(IPAddress.Any, Port);
        IsRunning = false;
        HeaderSerializer = new PacketHeaderSerializer();
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            UdpSocket.Bind(ServerIpEndPoint);
            
            IsRunning = true;

            for (int i = 0; i < WorkerThreadCount; i++)
            {
                Thread receiveThread = new Thread(ProcessReceive);
                Thread sendThread = new Thread(ProcessSend);

                receiveThread.IsBackground = true;
                sendThread.IsBackground = true;
                
                receiveThread.Start();
                sendThread.Start();
            }
            
            OnStart?.Invoke();
        }
        catch (Exception err)
        {
            Console.WriteLine($"UDP Server Initialize Error : {err.Message}");
        }
    }

    private void ProcessReceive()
    {
        if (!IsRunning) return;
        
        try
        {
            Thread.Sleep(RECEIVE_INTERVAL_MS);
            
            SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.SetBuffer(new byte[BufferSize], 0, BufferSize);
            receiveArgs.RemoteEndPoint = new IPEndPoint(IPAddress.None, 0);
            receiveArgs.Completed += ReceiveCompleted;

            UdpSocket.ReceiveFromAsync(receiveArgs);   
        }
        catch (Exception err)
        {
            Console.WriteLine($"UDP Start Receive Failed : {err.Message}");
        }
    }

    private void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
    {
        PacketHeader header = default;
        byte[] received = new byte[e.BytesTransferred];
        Buffer.BlockCopy(e.Buffer, 0, received, 0, received.Length);
        bool ret = HeaderSerializer.Deserialize(received, ref header);

        if (!ret)
        {
            Console.WriteLine("Failed Packet Header Deserialize");
            return;
        }
        
        int headerSize = Marshal.SizeOf(typeof(PacketHeader));
        byte[] packetData = new byte[received.Length - headerSize];
        Buffer.BlockCopy(received, headerSize, packetData, 0, packetData.Length);
        
        PacketInfo packetInfo = new PacketInfo
        {
            Header = header,
            Buffer = packetData,
            ClientEndPoint = e.RemoteEndPoint
        };
        
        OnReceived?.Invoke(packetInfo);

        ProcessReceive();
    }

    private void ProcessSend()
    {
        while (IsRunning)
        {
            Thread.Sleep(SEND_INTERVAL_MS);
            
            if(_sendQueue.IsEmpty) continue;
            
            _sendQueue.TryDequeue(out PacketInfo packetInfo);

            if (packetInfo == null) continue;
            
            bool ret = HeaderSerializer.Serialize(packetInfo.Header);
            if (!ret)
            {
                Console.WriteLine("Failed Packet Header Serialize");
                return;
            }
            
            byte[] headerBytes = HeaderSerializer.GetBuffer();
            EndPoint clientEndPoint = packetInfo.ClientEndPoint;

            byte[] sendBuffer = new byte[headerBytes.Length + packetInfo.Buffer.Length];
            int headerSize = Marshal.SizeOf(typeof(PacketHeader));
            Buffer.BlockCopy(headerBytes, 0, sendBuffer, 0, headerSize);
            Buffer.BlockCopy(packetInfo.Buffer, 0, sendBuffer, headerSize, packetInfo.Buffer.Length);
            
            try
            {
                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
                receiveArgs.SetBuffer(sendBuffer, 0, sendBuffer.Length);
                receiveArgs.RemoteEndPoint = clientEndPoint;
                receiveArgs.Completed += SendCompleted;

                UdpSocket.SendToAsync(receiveArgs);   
            }
            catch (Exception err)
            {
                Console.WriteLine($"UDP Start Send Failed : {err.Message}");
            }
        }
    }

    public void EnqueueSendQueue(PacketInfo packetInfo)
    {
        _sendQueue.Enqueue(packetInfo);
    }
    
    private void SendCompleted(object sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            Console.WriteLine($"UDP Send Success : {e.RemoteEndPoint} -> {e.Buffer.Length}");
        }
        else
        {
            Console.WriteLine($"UDP Send Failed : {e.SocketError}");
        }
        
        OnSent?.Invoke(e);
    }
}

public class OperationServer : UDPServer
{
    private const int ConnectionTimeoutMs = 3000;
    private const int PingIntervalMs = 1000;

    private SessionManager _sessionManager;
    
    private readonly ConcurrentDictionary<string, ClientInfo> _clientInfos = new ConcurrentDictionary<string, ClientInfo>();
    
    public OperationServer(int port, int bufferSize, int workerThreadCount) : base(port, bufferSize, workerThreadCount)
    {
        _sessionManager = new SessionManager();

        OnStart += StartPingToClient;
        OnReceived += OnReceivePacket;
    }
    
    private void StartPingToClient()
    {
        Thread pingThread = new Thread(PingToClient);
        pingThread.IsBackground = true;
        pingThread.Start();

        Console.WriteLine($"* Boot Operation Server *");
    }

    private void PingToClient()
    {
        while (IsRunning)
        {
            try
            {
                foreach (KeyValuePair<string, ClientInfo> client in _clientInfos)
                {
                    IPEndPoint ipEndPoint = client.Value.ClientEndPoint as IPEndPoint;
                    if (ipEndPoint == null)
                    {
                        _clientInfos.TryRemove(client.Key, out ClientInfo info);
                        break;
                    }
                    
                    string ip = ipEndPoint.Address.ToString();

                    Ping ping = new Ping();
                    PingReply reply = ping.Send(ip, ConnectionTimeoutMs);

                    if (reply == null || reply.Status != IPStatus.Success)
                    {
                        Console.WriteLine($"Disconnect client why it's ping timeout : {ipEndPoint.Address}");

                        _clientInfos.TryRemove(client.Key, out ClientInfo info);
                        break;
                    }
                }
                
                Thread.Sleep(PingIntervalMs);
            }
            catch (Exception err)
            {
                Console.WriteLine($"UDP Ping Error : {err.Message}");
            }
        }
    }
    
    private void OnReceivePacket(PacketInfo packetInfo)
    {
        Console.WriteLine($@"
======================
   [Receive Packet]
PacketType : {packetInfo.Header.PacketType}
PacketID : {packetInfo.Header.PacketId}
BufferLength : {packetInfo.Buffer.Length}
======================
");
        
        switch (packetInfo.Header.PacketType)
        {
            case PacketType.Connect:
                HandleConnect(packetInfo);
                break;
            case PacketType.Disconnect:
                HandleDisconnect(packetInfo);
                break;
            case PacketType.SyncTransform:
                HandleSyncTransform(packetInfo);
                break;
            case PacketType.GoalLine:
                HandleGoalLine(packetInfo);
                break;
        }
    }

    private void HandleConnect(PacketInfo packetInfo)
    {
        string playerId = string.Empty;
        string sessionId = string.Empty;
        bool ret = true;

        try
        {
            ConnectionPacket connection = new ConnectionPacket(packetInfo.Buffer);
            playerId = connection.GetData().PlayerId;
            sessionId = connection.GetData().SessionId;

            if (_clientInfos.TryGetValue(playerId, out _))
            {
                Console.WriteLine($@"
======================
   [Failed Connect Client]
SessionId : {sessionId}
PlayerId : {playerId}
Msg : already connected player
======================
");

                return;
            }

            ClientInfo clientInfo = new ClientInfo();
            clientInfo.Id = playerId;
            clientInfo.ClientEndPoint = packetInfo.ClientEndPoint;

            ret &= _sessionManager.AddPlayerInSession(sessionId, clientInfo);
            ret &= _clientInfos.TryAdd(playerId, clientInfo);

            packetInfo.Header.ResultType = ret ? ResultType.Success : ResultType.Failed;

            Console.WriteLine($@"
======================
   [Complete Connected Client]
SessionId : {sessionId}
PlayerId : {playerId}
======================
");
        }
        catch (Exception err)
        {
            Console.WriteLine($@"
======================
   [Exception Connected Client]
SessionId : {sessionId}
PlayerId : {playerId}
Error : {err.Message}
======================
");

            packetInfo.Header.ResultType = ResultType.Failed;
        }

        EnqueueSendQueue(packetInfo);
    }

    private void HandleDisconnect(PacketInfo packetInfo)
    {
        string sessionId = string.Empty;
        string playerId = string.Empty;

        try
        {
            ConnectionPacket connection = new ConnectionPacket(packetInfo.Buffer);
            sessionId = connection.GetData().SessionId;
            playerId = connection.GetData().PlayerId;

            bool ret = true;
            ret &= _sessionManager.RemovePlayerInSession(sessionId, playerId);
            ret &= _clientInfos.TryRemove(playerId, out _);

            packetInfo.Header.ResultType = ret ? ResultType.Success : ResultType.Failed;

            Console.WriteLine($@"
======================
   [Complete Disconnected Client]
SessionId : {sessionId}
PlayerId : {playerId}
======================
");
        }
        catch(Exception err)
        {
            Console.WriteLine($@"
======================
   [Exception Connected Client]
SessionId : {sessionId}
PlayerId : {playerId}
Error : {err.Message}
======================
");

            packetInfo.Header.ResultType = ResultType.Failed;
        }

        EnqueueSendQueue(packetInfo);
    }
    
    private void HandleSyncTransform(PacketInfo packetInfo)
    {
        
    }
    
    private void HandleGoalLine(PacketInfo packetInfo)
    {
        
    }
}