using System;

namespace RacingGameOperationServer
{
    public class Program
    {
        private static void Main(string[] args)
        {
            OperationServer t = new OperationServer(9000, 512, 4);
            t.Start();

            Console.ReadLine();
        }
    }
}
