using System;
using System.Collections.Generic;
using System.Text;

public interface IServer
{
    void Send<T>(IPacket<T> packet);
    void Receive(byte[] bytes);
}

// T = Packet 화 할 데이터 
public interface IPacket<T>
{
    byte[] Serialize();
    T Deserialize(byte[] bytes);
}

public interface ICompress
{
    byte[] Compress(byte[] bytes);
    byte[] Decompress(byte[] bytes);
}