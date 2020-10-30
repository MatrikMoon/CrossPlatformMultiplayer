using LiteNetLib;
using LiteNetLib.Utils;
using Shared;
using System;
using System.Net;
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
            //SampleServer();
            BeatSaberServer();
        }

        static void BeatSaberServer()
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

        static async void SampleServer()
        {
            //Set logger so we don't crash to unity incompatibility
            BGNetDebug.SetLogger(new BGNetLogger());

            var _listener = new SimpleListener();
            var _encryptionLayer = new PacketEncryptionLayer();
            var _netManager = new NetManager(_listener, _encryptionLayer)
            {
                UnconnectedMessagesEnabled = true
            };
            //_netManager.Start("192.168.1.67", "::1", 2328);

            var layerAdded = false;

            _listener.NetworkReceiveUnconnectedEvent += (IPEndPoint endpoint, NetPacketReader packetReader, UnconnectedMessageType messageType) =>
            {
                Logger.Info($"Recieved: {packetReader.GetString(100)}");

                _netManager.SendUnconnectedMessage(NetDataWriter.FromString("response"), endpoint);

                if (!layerAdded)
                {
                    layerAdded = true;
                    _encryptionLayer.AddEncryptedEndpoint(1u, endpoint, null, null, CreateSpecialByteArray(48, 0x1), CreateSpecialByteArray(32, 0x2), CreateSpecialByteArray(32, 0x3), false);
                }
            };

            if (!_netManager.Start(2328))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }

            while (!Console.KeyAvailable)
            {
                _netManager.PollEvents();
                Thread.Sleep(15);
            }
            _netManager.Stop();
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
    }
}
