public interface IPacket<T>
{
    byte[] GetBytes();
    T GetData();
}

public interface ICompress
{
    byte[] Compress(byte[] bytes);
    byte[] Decompress(byte[] bytes);
}