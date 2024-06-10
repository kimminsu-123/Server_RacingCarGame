public interface IPacket<T>
{
    T Data { get; set; }
    byte[] Serialize();
    T Deserialize(byte[] bytes);
}

public interface ICompress
{
    byte[] Compress(byte[] bytes);
    byte[] Decompress(byte[] bytes);
}