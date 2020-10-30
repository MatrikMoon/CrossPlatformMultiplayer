using LiteNetLib;
using System;
using System.Net;
using System.Net.Sockets;

namespace MasterServer
{
	public class SimpleListener : INetEventListener, IDeliveryEventListener
	{
		public event Action<NetPeer> PeerConnectedEvent;
		public event Action<NetPeer, DisconnectInfo> PeerDisconnectedEvent;
		public event Action<IPEndPoint, SocketError> NetworkErrorEvent;
		public event Action<NetPeer, NetPacketReader, DeliveryMethod> NetworkReceiveEvent;
		public event Action<IPEndPoint, NetPacketReader, UnconnectedMessageType> NetworkReceiveUnconnectedEvent;
		public event Action<NetPeer, int> NetworkLatencyUpdateEvent;
		public event Action<ConnectionRequest> ConnectionRequestEvent;
		public event Action<NetPeer, object> DeliveryEvent;

		public void ClearPeerConnectedEvent()
		{
			PeerConnectedEvent = null;
		}

		public void ClearPeerDisconnectedEvent()
		{
			PeerDisconnectedEvent = null;
		}

		public void ClearNetworkErrorEvent()
		{
			NetworkErrorEvent = null;
		}

		public void ClearNetworkReceiveEvent()
		{
			NetworkReceiveEvent = null;
		}

		public void ClearNetworkReceiveUnconnectedEvent()
		{
			NetworkReceiveUnconnectedEvent = null;
		}

		public void ClearNetworkLatencyUpdateEvent()
		{
			NetworkLatencyUpdateEvent = null;
		}

		public void ClearConnectionRequestEvent()
		{
			ConnectionRequestEvent = null;
		}

		public void ClearDeliveryEvent()
		{
			DeliveryEvent = null;
		}

		void INetEventListener.OnPeerConnected(NetPeer peer)
		{
			if (PeerConnectedEvent != null)
			{
				PeerConnectedEvent(peer);
			}
		}

		void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
		{
			if (PeerDisconnectedEvent != null)
			{
				PeerDisconnectedEvent(peer, disconnectInfo);
			}
		}

		void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
		{
			if (NetworkErrorEvent != null)
			{
				NetworkErrorEvent(endPoint, socketErrorCode);
			}
		}

		void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
		{
			if (NetworkReceiveEvent != null)
			{
				NetworkReceiveEvent(peer, reader, deliveryMethod);
			}
		}

		void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
		{
			if (NetworkReceiveUnconnectedEvent != null)
			{
				NetworkReceiveUnconnectedEvent(remoteEndPoint, reader, messageType);
			}
		}

		void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
		{
			if (NetworkLatencyUpdateEvent != null)
			{
				NetworkLatencyUpdateEvent(peer, latency);
			}
		}

		void INetEventListener.OnConnectionRequest(ConnectionRequest request)
		{
			if (ConnectionRequestEvent != null)
			{
				ConnectionRequestEvent(request);
			}
		}

		void IDeliveryEventListener.OnMessageDelivered(NetPeer peer, object userData)
		{
			if (DeliveryEvent != null)
			{
				DeliveryEvent(peer, userData);
			}
		}
	}
}
