using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public abstract class UDPServer
{
    protected readonly int Port;
    protected readonly int BufferSize;
    protected readonly int WorkerThreadCount;
        
    protected volatile Socket UdpSocket;
    protected volatile IPEndPoint ServerIpEndPoint;
    protected volatile bool IsRunning;

    protected const int RECEIVE_INTERVAL_MS = 100;
    protected const int SEND_INTERVAL_MS = 100;

    protected Action OnStart;
    protected Action<PacketInfo> OnReceived;
    protected Action<SocketAsyncEventArgs> OnSent;

    protected readonly PacketHeaderSerializer HeaderSerializer = new PacketHeaderSerializer();

    private readonly ConcurrentQueue<PacketInfo> _sendQueue = new ConcurrentQueue<PacketInfo>();

    public UDPServer(int port, int bufferSize, int workerThreadCount)
    {
        Port = port;
        BufferSize = bufferSize;
        WorkerThreadCount = workerThreadCount;
        UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        ServerIpEndPoint = new IPEndPoint(IPAddress.Any, Port);
        IsRunning = false;
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
    
    private readonly ConcurrentDictionary<string, ClientInfo> _clientInfos = new ConcurrentDictionary<string, ClientInfo>();
    
    public OperationServer(int port, int bufferSize, int workerThreadCount) : base(port, bufferSize, workerThreadCount)
    {
        OnStart += StartPingToClient;
        OnReceived += OnReceivePacket;
    }
    
    private void StartPingToClient()
    {
        Thread pingThread = new Thread(PingToClient);
        pingThread.IsBackground = true;
        pingThread.Start();
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
        ClientInfo clientInfo = new ClientInfo();
        clientInfo.ClientEndPoint = packetInfo.ClientEndPoint;

        ConnectionPacket connection = new ConnectionPacket(packetInfo.Buffer);
        string playerId = connection.GetData().PlayerId;

        // 세션 매니저에 정보 전달하기
        // 세션 매니저에서 정보를 받으면 현재 세션을 생성하거나 플레이어를 업데이트 하는 용도로 사용
        
        _clientInfos.TryAdd(playerId, clientInfo);
    }
    
    private void HandleDisconnect(PacketInfo packetInfo)
    {
        ConnectionPacket connection = new ConnectionPacket(packetInfo.Buffer);
        string playerId = connection.GetData().PlayerId;

        // 세션 매니저에 정보 전달하기
        // 세션 매니저에서 정보를 받으면 현재 세션을 지우거나 플레이어를 업데이트 하는 용도로 사용
        
        _clientInfos.TryRemove(playerId, out _);
    }
    
    private void HandleSyncTransform(PacketInfo packetInfo)
    {
        
    }
    
    private void HandleGoalLine(PacketInfo packetInfo)
    {
        
    }
}