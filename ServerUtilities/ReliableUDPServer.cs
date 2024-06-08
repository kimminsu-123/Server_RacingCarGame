using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public abstract class ReliableUDPServer
{
    private int _port;
    private int _bufferSize;
    private int _workerThreadCount;
    
    private volatile Socket _udpSocket;
    private volatile IPEndPoint _serverIpEndPoint;
    private volatile bool _isRunning;

    private readonly ConcurrentQueue<PacketInfo> _sendQueue = new ConcurrentQueue<PacketInfo>();

    public ReliableUDPServer(int port, int bufferSize, int workerThreadCount)
    {
        _port = port;
        _bufferSize = bufferSize;
        _workerThreadCount = workerThreadCount;
        _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _serverIpEndPoint = new IPEndPoint(IPAddress.Any, _port);
        _isRunning = false;
    }

    public void Start()
    {
        /*
         * 1. Receive 받을 때 Client IP 확인해서 없으면 클라이언트 정보 등록
         * 2. 패킷을 처리할 때 ACK 값 확인해서 현재 처리한 번호랑 비교, 처리 되지 않았으면 처리, 처리 됐으면 처리 안함.
         * 3. Send 할 때는 해당 클라이언트에게 전송할 번호 이전 3개까지 보내기 
         */

        if (_isRunning) return;

        try
        {
            _udpSocket.Bind(_serverIpEndPoint);

            for (int i = 0; i < _workerThreadCount; i++)
            {
                Thread receiveThread = new Thread(ProcessReceive);
                Thread sendThread = new Thread(ProcessSend);

                receiveThread.IsBackground = true;
                sendThread.IsBackground = true;
                
                receiveThread.Start();
                sendThread.Start();
            }

            _isRunning = true;
        }
        catch (Exception err)
        {
            Console.WriteLine($"RUDP Server Initialize Error : {err.Message}");
        }
    }

    private void ProcessReceive()
    {
        if (!_isRunning) return;
        
        try
        {
            SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.SetBuffer(new byte[_bufferSize], 0, _bufferSize);
            receiveArgs.RemoteEndPoint = new IPEndPoint(IPAddress.None, 0);
            receiveArgs.Completed += ReceiveCompleted;

            _udpSocket.ReceiveFromAsync(receiveArgs);   
        }
        catch (Exception err)
        {
            Console.WriteLine($"RUDP Start Receive Failed : {err.Message}");
        }
    }

    private void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
    {
        ClientInfo clientInfo;
        clientInfo.ClientEndPoint = e.RemoteEndPoint;
        
        PacketInfo packetInfo = new PacketInfo
        {
            Buffer = e.Buffer,
            Client = clientInfo
        };

        OnReceive(packetInfo);

        ProcessReceive();
    }

    private void ProcessSend()
    {
        while (_isRunning)
        {
            if(_sendQueue.IsEmpty) continue;
            
            _sendQueue.TryDequeue(out PacketInfo packetInfo);

            if (packetInfo == null) continue;

            byte[] sendBuffer = packetInfo.Buffer;
            EndPoint clientEndPoint = packetInfo.Client.ClientEndPoint;
            
            try
            {
                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
                receiveArgs.SetBuffer(sendBuffer, 0, sendBuffer.Length);
                receiveArgs.RemoteEndPoint = clientEndPoint;
                receiveArgs.Completed += SendCompleted;

                _udpSocket.SendToAsync(receiveArgs);   
            }
            catch (Exception err)
            {
                Console.WriteLine($"RUDP Start Send Failed : {err.Message}");
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
            Console.WriteLine($"RUDP Send Success : {e.RemoteEndPoint} -> {e.Buffer.Length}");
        }
        else
        {
            Console.WriteLine($"RUDP Send Failed : {e.SocketError}");
        }
    }
    
    protected abstract void OnReceive(PacketInfo packetInfo);
}