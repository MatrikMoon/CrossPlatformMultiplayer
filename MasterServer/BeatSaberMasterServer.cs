using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Net;

namespace MasterServer
{
    class BeatSaberMasterServer : MessageHandler.IMessageSender
    {
        NetManager _netManager;
        SimpleListener _listener;
        PacketEncryptionLayer _encryptionLayer;
        MasterServerMessageHandler _messageHandler;

        public BeatSaberMasterServer()
        {
            _listener = new SimpleListener();
            _encryptionLayer = new PacketEncryptionLayer();
            _messageHandler = new MasterServerMessageHandler(this, _encryptionLayer);
            _netManager = new NetManager(_listener, _encryptionLayer)
            {
                UnconnectedMessagesEnabled = true
            };

            _listener.NetworkReceiveUnconnectedEvent += (IPEndPoint endpoint, NetPacketReader packetReader, UnconnectedMessageType messageType) =>
            {
                _messageHandler.ReceiveMessage(endpoint, packetReader);
            };
        }

        public bool Start(int port) => _netManager.Start(port);
        public void Stop() => _netManager.Stop();
        public void PollUpdate() {
            _netManager.PollEvents();
            _messageHandler.PollUpdate();
        }

        public void SendMessage(NetDataWriter writer, IPEndPoint endPoint)
        {
            _netManager.SendUnconnectedMessage(writer, endPoint);
        }
    }
}
