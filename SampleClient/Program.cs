using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Threading;

/**
 * Created by Moon on 10/16/2020
 * Just to quickly test the server without spinning up Beat Saber
 */

namespace SampleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager client = new NetManager(listener, new PacketEncryptionLayer())
            {
                UnconnectedMessagesEnabled = true
            };

            client.Start();

            client.SendUnconnectedMessage(NetDataWriter.FromString("test"), NetUtils.MakeEndPoint("server1.networkauditor.org", 2328));

            listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                Console.WriteLine("We got: {0}", dataReader.GetString(100));
                dataReader.Recycle();
            };

            while (!Console.KeyAvailable)
            {
                client.PollEvents();
                Thread.Sleep(15);
            }

            client.Stop();
        }
    }
}
