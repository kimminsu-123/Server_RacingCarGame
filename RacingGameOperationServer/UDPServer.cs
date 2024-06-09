using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

public abstract class UDPServer
{
    protected readonly int Port;
    protected readonly int BufferSize;
    protected readonly int WorkerThreadCount;
        
    protected volatile Socket UdpSocket;
    protected volatile IPEndPoint ServerIpEndPoint;
    protected volatile bool IsRunning;

    protected const int ReceiveIntervalMs = 100;
    protected const int SendIntervalMs = 100;

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
            Thread.Sleep(ReceiveIntervalMs);
            
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
        PacketInfo packetInfo = new PacketInfo
        {
            Buffer = e.Buffer,
            Size = e.BytesTransferred,
            ClientEndPoint = e.RemoteEndPoint
        };
        
        OnReceived?.Invoke(packetInfo);

        ProcessReceive();
    }

    private void ProcessSend()
    {
        while (IsRunning)
        {
            Thread.Sleep(SendIntervalMs);
            
            if(_sendQueue.IsEmpty) continue;
            
            _sendQueue.TryDequeue(out PacketInfo packetInfo);

            if (packetInfo == null) continue;

            byte[] sendBuffer = packetInfo.Buffer;
            EndPoint clientEndPoint = packetInfo.ClientEndPoint;
            
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
                Console.WriteLine($"RUDP Ping Error : {err.Message}");
            }
        }
    }
    
    private void OnReceivePacket(PacketInfo packetInfo)
    {
        Console.WriteLine(packetInfo.Size);
    }
}