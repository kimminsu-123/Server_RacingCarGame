using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Schema;

namespace RacingGameOperationServer
{
    public class Program
    {
        private static int sum = 0;

        private static Action sumAction = Sum;
        private static Action minusAction = Minus;
        
        private static void TestFunc1()
        {
            for (int i = 0; i < 10000000; i++)
            {
                sumAction.Invoke();
            }
        }
        
        private static void TestFunc2()
        {
            for (int i = 0; i < 10000000; i++)
            {
                minusAction.Invoke();
            }
        }

        public static void Sum()
        {
            sum += 1;
        }

        private static void Minus()
        {
            sum -= 1;
        }
        
        private static void Main(string[] args)
        {
            Thread t1 = new Thread(TestFunc1);
            Thread t2 = new Thread(TestFunc2);
            
            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();
            
            Console.WriteLine(sum);

            /*OperationServer t = new OperationServer(9000, 512, 4);
            t.Start();

            Console.ReadLine();*/

            /*PacketHeader header;
            header.PacketType = PacketType.Connect;
            header.PacketId = 1;

            PacketHeaderSerializer s = new PacketHeaderSerializer();

            s.Serialize(header);
            var buf = s.GetBuffer();
            
            Console.WriteLine(header.PacketId + " " + header.PacketType);

            PacketHeader header2 = default;
            s.Deserialize(buf, ref header2);
            Console.WriteLine(header2.PacketId + " " + header2.PacketType);
            
            Console.WriteLine(s.GetBuffer().Length);*/
        }
    }
}
