using LiteNetLib;
using System;
using System.Net;
using System.Net.Sockets;

namespace MasterServer
{
    public class DebugListener : INetEventListener
    {
        public void OnPeerConnected(NetPeer peer)
        {
            Console.WriteLine("[Server] Peer connected: " + peer.EndPoint);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Console.WriteLine("[Server] Peer disconnected: " + peer.EndPoint + ", reason: " + disconnectInfo.Reason);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
        {
            Console.WriteLine("[Server] error: " + socketErrorCode);
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            Console.WriteLine("[Server] Receive: {0}", reader.GetString(100));
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            Console.WriteLine("[Server] ReceiveUnconnected: {0}", reader.GetString(100));
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            Console.WriteLine($"[Server] LatencyUpdate: {peer.EndPoint} = {latency}");
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            var acceptedPeer = request.AcceptIfKey("key");
            Console.WriteLine("[Server] ConnectionRequest. Ep: {0}, Accepted: {1}",
                request.RemoteEndPoint,
                acceptedPeer != null);
        }
    }
}
