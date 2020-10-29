using System;
using System.Threading;

/**
* Created by Moon on 10/15/2020
* POC Mock Master Server for Beat Saber multiplayer
*/

namespace MasterServer
{
    class Program
    {
        static void Main(string[] args)
        {
            //Set logger so we don't crash to unity incompatibility
            BGNetDebug.SetLogger(new BGNetLogger());

            //server.Start("192.168.1.67", "::1", 2328);
            var server = new BeatSaberMasterServer();

            if (!server.Start(2328))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }

            while (!Console.KeyAvailable)
            {
                server.PollUpdate();
                Thread.Sleep(15);
            }
            server.Stop();
        }
    }
}
