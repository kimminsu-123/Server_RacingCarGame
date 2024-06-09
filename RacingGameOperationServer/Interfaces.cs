public interface IPacket<out T>
{
    byte[] Serialize();
    T Deserialize(byte[] bytes);
}

public interface ICompress
{
    byte[] Compress(byte[] bytes);
    byte[] Decompress(byte[] bytes);
}