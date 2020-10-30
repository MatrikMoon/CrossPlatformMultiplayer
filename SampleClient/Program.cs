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
            var endpoint = NetUtils.MakeEndPoint("127.0.0.1", 2328);
            var encryption = new PacketEncryptionLayer();
            encryption.filterUnencryptedTraffic = true;

            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager client = new NetManager(listener, encryption)
            {
                UnconnectedMessagesEnabled = true
            };

            client.Start();
            listener.NetworkReceiveUnconnectedEvent += (fromPeer, dataReader, deliveryMethod) =>
            {
                Console.WriteLine($"Recieved: {dataReader.GetString(100)}");
            };

            client.SendUnconnectedMessage(NetDataWriter.FromString("test"), endpoint);

            Thread.Sleep(1000);

            encryption.AddEncryptedEndpoint(1u, endpoint, null, null, CreateSpecialByteArray(48, 0x1), CreateSpecialByteArray(32, 0x2), CreateSpecialByteArray(32, 0x3), true);

            client.SendUnconnectedMessage(NetDataWriter.FromString("test2"), endpoint);

            while (!Console.KeyAvailable)
            {
                client.PollEvents();
                Thread.Sleep(15);
            }

            client.Stop();
        }

        public static byte[] CreateSpecialByteArray(int length, byte fill)
        {
            var arr = new byte[length];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = fill;
            }
            return arr;
        }

        private static byte[] CreateHandshakeHeader()
        {
            byte[] array = new byte[5];
            array[0] = 8;
            FastBitConverter.GetBytes(array, 1, 3192347326u);
            return array;
        }
    }
}
