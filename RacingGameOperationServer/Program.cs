using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

namespace RacingGameOperationServer
{
    public class Program
    {
        private static void Main(string[] args)
        {
            /*OperationServer t = new OperationServer(9000, 512, 4);
            t.Start();

            Console.ReadLine();*/

            PacketHeader header;
            header.PacketType = PacketType.Connect;
            header.PacketId = 1;

            PacketHeaderSerializer s = new PacketHeaderSerializer();

            s.Serialize(header);
            var buf = s.GetBuffer();
            
            Console.WriteLine(header.PacketId + " " + header.PacketType);

            PacketHeader header2;
            header2.PacketType = 0;
            header2.PacketId = 0;

            s.Deserialize(buf, ref header2);
            Console.WriteLine(header2.PacketId + " " + header2.PacketType);
        }
    }
}
