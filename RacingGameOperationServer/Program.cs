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
        private static void Main(string[] args)
        {
            OperationServer t = new OperationServer(9000, 512, 4);
            t.Start();

            Console.ReadLine();
        }
    }
}
