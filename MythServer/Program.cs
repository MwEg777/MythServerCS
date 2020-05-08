using MythServer;
using System;
using System.Threading;
using static MythServer.Server;

class Program
{

    [STAThread]
    static void Main(string[] args)
    {

        Thread t = new Thread(delegate ()
        {
            Server myserver = new Server("0.0.0.0", 4466);
        });

        t.Start();

        Console.WriteLine("Myth server is running..");

        //new Application().Run(new MythForm());

    }

}
