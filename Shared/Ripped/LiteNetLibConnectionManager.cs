using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using Shared;
using static IConnectionManager;

public class LiteNetLibConnectionManager : IConnectionManager, IDisposable, INetEventListener
{
	public event Action onConnectedEvent;

	public event Action<DisconnectedReason> onDisconnectedEvent;

	public event Action<ConnectionFailedReason> onConnectionFailedEvent;

	public event Action<IConnection> onConnectionConnectedEvent;

	public event Action<IConnection> onConnectionDisconnectedEvent;

	public event Action<IConnection, float> onLatencyUpdatedEvent;

	public event Action<IConnection, NetDataReader, DeliveryMethod> onReceivedDataEvent;

	public event Action<IPEndPoint, NetDataReader> onReceiveUnconnectedDataEvent;

	public string userId
	{
		get
		{
			return this._userId;
		}
	}

	public string userName
	{
		get
		{
			return this._userName;
		}
	}

	public bool isConnected
	{
		get
		{
			return this._connectionState == LiteNetLibConnectionManager.ConnectionState.Connected;
		}
	}

	public bool isConnecting
	{
		get
		{
			return this._connectionState == LiteNetLibConnectionManager.ConnectionState.Connecting;
		}
	}

	public bool isConnectionOwner
	{
		get
		{
			return this._mode == LiteNetLibConnectionManager.NetworkMode.Server;
		}
	}

	public bool hasConnectionOwner
	{
		get
		{
			return this._connections.Find((LiteNetLibConnectionManager.NetPeerConnection c) => c.isConnectionOwner) != null;
		}
	}

	public bool isRelay
	{
		get
		{
			return this._mode == LiteNetLibConnectionManager.NetworkMode.Relay;
		}
	}

	public bool isServer
	{
		get
		{
			return this._mode == LiteNetLibConnectionManager.NetworkMode.Server;
		}
	}

	public bool isClient
	{
		get
		{
			return this._mode == LiteNetLibConnectionManager.NetworkMode.Client;
		}
	}

	public bool isDisposed
	{
		get
		{
			return this._mode == LiteNetLibConnectionManager.NetworkMode.None;
		}
	}

	public int connectionCount
	{
		get
		{
			return this._connections.Count;
		}
	}

	public string secret
	{
		get
		{
			return this._secret;
		}
	}

	public int port
	{
		get
		{
			return this._netManager.LocalPort;
		}
	}

	public PacketEncryptionLayer encryptionLayer
	{
		get
		{
			return this._encryptionLayer;
		}
	}

	public LiteNetLibConnectionManager()
	{
		this._encryptionLayer = new PacketEncryptionLayer();
		this._netManager = new NetManager(this, this._encryptionLayer);
		this._netManager.ReconnectDelay = 200;
		this._netManager.MaxConnectAttempts = 10;
	}

	public void SendToAll(NetDataWriter writer, DeliveryMethod deliveryMethod)
	{
		this._netManager.SendToAll(writer, deliveryMethod);
	}

	public void SendToAll(NetDataWriter writer, DeliveryMethod deliveryMethod, IConnection excludingConnection)
	{
		this._netManager.SendToAll(writer, deliveryMethod, ((LiteNetLibConnectionManager.NetPeerConnection)excludingConnection).netPeer);
	}

	public void SendUnconnectedMessage(NetDataWriter writer, IPEndPoint endPoint)
	{
		this._netManager.SendUnconnectedMessage(writer, endPoint);
	}

	public void SendUnconnectedMessage(byte[] message, int offset, int length, IPEndPoint endPoint)
	{
		this._netManager.SendUnconnectedMessage(message, offset, length, endPoint);
	}

	public void PollUpdate()
	{
		this.CheckSentryState();
		this._lastPollUpdateTime = DateTime.UtcNow.Ticks;
		this._netManager.PollEvents();
	}

	public bool Init<T>(IConnectionInitParams<T> initParams) where T : IConnectionManager
	{
		if (this._mode != LiteNetLibConnectionManager.NetworkMode.None)
		{
			this.Disconnect(DisconnectedReason.UserInitiated);
		}
		LiteNetLibConnectionManager.LiteNetLibConnectionParamsBase liteNetLibConnectionParamsBase;
		if ((liteNetLibConnectionParamsBase = (initParams as LiteNetLibConnectionManager.LiteNetLibConnectionParamsBase)) != null)
		{
			this._userId = liteNetLibConnectionParamsBase.userId;
			this._userName = liteNetLibConnectionParamsBase.userName;
			this._secret = liteNetLibConnectionParamsBase.secret;
			this._encryptionLayer.filterUnencryptedTraffic = liteNetLibConnectionParamsBase.filterUnencryptedTraffic;
			this._netManager.UnconnectedMessagesEnabled = liteNetLibConnectionParamsBase.enableUnconnectedMessages;
			if (!this.TryStartNetManager(liteNetLibConnectionParamsBase.port, liteNetLibConnectionParamsBase.enableBackgroundSentry))
			{
				return false;
			}
		}
		if (initParams is LiteNetLibConnectionManager.StartServerParams)
		{
			this._mode = LiteNetLibConnectionManager.NetworkMode.Server;
			this._connectionState = LiteNetLibConnectionManager.ConnectionState.Connected;
			Action action = this.onConnectedEvent;
			if (action != null)
			{
				action();
			}
			return true;
		}
		if (initParams is LiteNetLibConnectionManager.StartRelayParams)
		{
			this._mode = LiteNetLibConnectionManager.NetworkMode.Relay;
			this._connectionState = LiteNetLibConnectionManager.ConnectionState.Connecting;
			return true;
		}
		LiteNetLibConnectionManager.ConnectToServerParams connectToServerParams;
		if ((connectToServerParams = (initParams as LiteNetLibConnectionManager.ConnectToServerParams)) != null)
		{
			this._mode = LiteNetLibConnectionManager.NetworkMode.Client;
			this.ConnectToEndPoint(connectToServerParams.endPoint, connectToServerParams.serverUserId, connectToServerParams.serverUserName, connectToServerParams.serverIsConnectionOwner);
			return true;
		}
		if (initParams is LiteNetLibConnectionManager.StartClientParams)
		{
			this._mode = LiteNetLibConnectionManager.NetworkMode.Client;
			return true;
		}
		return false;
	}

	public void SetSecret(string secret)
	{
		this._secret = secret;
	}

	public void ConnectToEndPoint(IPEndPoint remoteEndPoint, string remoteUserId, string remoteUserName, bool remoteUserIsConnectionOwner)
	{
		if (this._connectionState != LiteNetLibConnectionManager.ConnectionState.Connected)
		{
			this._connectionState = LiteNetLibConnectionManager.ConnectionState.Connecting;
		}
		this.CreatePendingConnection(this._netManager.Connect(remoteEndPoint, this.GetConnectionMessage()), remoteUserId, remoteUserName, remoteUserIsConnectionOwner);
	}

	public void Dispose()
	{
		this.Disconnect(DisconnectedReason.UserInitiated);
		this._mode = LiteNetLibConnectionManager.NetworkMode.None;
		CancellationTokenSource backgroundSentryShutdownCts = this._backgroundSentryShutdownCts;
		if (backgroundSentryShutdownCts != null)
		{
			backgroundSentryShutdownCts.Cancel();
		}
		this._netManager.Stop();
		this._encryptionLayer.RemoveAllEndpoints();
	}

	public void Disconnect(DisconnectedReason disconnectedReason = DisconnectedReason.UserInitiated)
	{
		this.DisconnectInternal(disconnectedReason, ConnectionFailedReason.ConnectionCanceled);
	}

	private void DisconnectInternal(DisconnectedReason disconnectedReason = DisconnectedReason.UserInitiated, ConnectionFailedReason connectionFailedReason = ConnectionFailedReason.Unknown)
	{
		if (this._connectionState == LiteNetLibConnectionManager.ConnectionState.Unconnected)
		{
			return;
		}
		bool flag = this._connectionState == LiteNetLibConnectionManager.ConnectionState.Connecting;
		this._connectionState = LiteNetLibConnectionManager.ConnectionState.Unconnected;
		CancellationTokenSource backgroundSentryDisconnectCts = this._backgroundSentryDisconnectCts;
		if (backgroundSentryDisconnectCts != null)
		{
			backgroundSentryDisconnectCts.Cancel();
		}
		this._netManager.DisconnectAll();
		this._netManager.PollEvents();
		if (flag)
		{
			Action<ConnectionFailedReason> action = this.onConnectionFailedEvent;
			if (action == null)
			{
				return;
			}
			action(connectionFailedReason);
			return;
		}
		else
		{
			Action<DisconnectedReason> action2 = this.onDisconnectedEvent;
			if (action2 == null)
			{
				return;
			}
			action2(disconnectedReason);
			return;
		}
	}

	private bool TryStartNetManager(int port, bool enableBackgroundSentry)
	{
		if (this._netManager.IsRunning && (this._netManager.LocalPort == port || port == 0))
		{
			if (enableBackgroundSentry)
			{
				this.StartBackgroundSentry();
			}
			return true;
		}
		this._netManager.Stop();
		if (!this._netManager.Start(port))
		{
			return false;
		}
		if (port != 0)
		{
			this._netManager.SendBroadcast(new byte[1], port);
		}
		if (enableBackgroundSentry)
		{
			this.StartBackgroundSentry();
		}
		return true;
	}

	private void StartBackgroundSentry()
	{
		this._lastPollUpdateTime = DateTime.UtcNow.Ticks;
		this._sentryDisconnected = false;
		CancellationTokenSource backgroundSentryDisconnectCts = this._backgroundSentryDisconnectCts;
		if (backgroundSentryDisconnectCts != null)
		{
			backgroundSentryDisconnectCts.Cancel();
		}
		CancellationTokenSource backgroundSentryShutdownCts = this._backgroundSentryShutdownCts;
		if (backgroundSentryShutdownCts != null)
		{
			backgroundSentryShutdownCts.Cancel();
		}
		this._backgroundSentryDisconnectCts = new CancellationTokenSource();
		this._backgroundSentryShutdownCts = new CancellationTokenSource();
		Task.Run(new Func<Task>(this.BackgroundDisconnectSentry));
		Task.Run(new Func<Task>(this.BackgroundShutdownSentry));
	}

	private void CheckSentryState()
	{
		if (this._sentryDisconnected)
		{
			this.DisconnectInternal(DisconnectedReason.Timeout, ConnectionFailedReason.ServerUnreachable);
			this._sentryDisconnected = false;
		}
		if (this._sentryShutdown)
		{
			this._mode = LiteNetLibConnectionManager.NetworkMode.None;
			this._encryptionLayer.RemoveAllEndpoints();
			this._sentryShutdown = false;
		}
	}

	public IConnection GetConnection(int index)
	{
		return this._connections[index];
	}

	public bool IsConnectedToUser(string userId)
	{
		for (int i = 0; i < this._connections.Count; i++)
		{
			if (this._connections[i].userId == userId)
			{
				return true;
			}
		}
		return false;
	}

	public bool HasConnectionToEndPoint(IPEndPoint endPoint)
	{
		for (int i = 0; i < this._connections.Count; i++)
		{
			if (object.Equals(this._connections[i].netPeer.EndPoint, endPoint))
			{
				return true;
			}
		}
		return false;
	}

	private bool HasPendingConnectionToEndPoint(IPEndPoint endPoint)
	{
		for (int i = 0; i < this._pendingConnections.Count; i++)
		{
			if (object.Equals(this._pendingConnections[i].netPeer.EndPoint, endPoint))
			{
				return true;
			}
		}
		return false;
	}

	void INetEventListener.OnPeerConnected(NetPeer peer)
	{
		int i = 0;
		while (i < this._pendingConnections.Count)
		{
			if (this._pendingConnections[i].netPeer == peer)
			{
				LiteNetLibConnectionManager.NetPeerConnection netPeerConnection = this._pendingConnections[i];
				this._pendingConnections.RemoveAt(i);
				for (int j = 0; j < this._connections.Count; j++)
				{
					if (this._connections[j].userId == netPeerConnection.userId)
					{
						netPeerConnection.Disconnect();
						return;
					}
				}
				this._connections.Add(netPeerConnection);
				if (this._connectionState == LiteNetLibConnectionManager.ConnectionState.Connecting)
				{
					this._connectionState = LiteNetLibConnectionManager.ConnectionState.Connected;
					Action action = this.onConnectedEvent;
					if (action != null)
					{
						action();
					}
				}
				if (this.isRelay && netPeerConnection.isConnectionOwner)
				{
					this.AcceptAllPendingRequests();
				}
				Action<IConnection> action2 = this.onConnectionConnectedEvent;
				if (action2 == null)
				{
					return;
				}
				action2(netPeerConnection);
				return;
			}
			else
			{
				i++;
			}
		}
		peer.Disconnect();
	}

	void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
	{
		this.LogError("error " + socketErrorCode);
	}

	void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latencyMs)
	{
		LiteNetLibConnectionManager.NetPeerConnection connection = this.GetConnection(peer);
		if (connection != null)
		{
			Action<IConnection, float> action = this.onLatencyUpdatedEvent;
			if (action == null)
			{
				return;
			}
			action(connection, 0.001f * (float)latencyMs);
		}
	}

	void INetEventListener.OnConnectionRequest(ConnectionRequest request)
	{
		this._pendingReconnections.Remove(request.RemoteEndPoint);
		string userName = null;
		string text = null;
		string a;
		bool isConnectionOwner;
		if (this._connectionState != LiteNetLibConnectionManager.ConnectionState.Unconnected && this.ParseConnectionMessage(request.Data, out a, out text, out userName, out isConnectionOwner) && a == this._secret && text != this._userId)
		{
			this.TryAccept(request, text, userName, isConnectionOwner);
			return;
		}
		request.Reject();
		this.TryDisconnect(DisconnectReason.ConnectionRejected);
	}

	void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
	{
		if (disconnectInfo.Reason != DisconnectReason.Reconnect && disconnectInfo.Reason != DisconnectReason.PeerToPeerConnection)
		{
			this._encryptionLayer.RemoveEncryptedEndpoint(peer.EndPoint);
		}
		this.RemoveConnection(peer, disconnectInfo.Reason);
	}

	void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
	{
		if (this._connectionState == LiteNetLibConnectionManager.ConnectionState.Unconnected)
		{
			reader.Recycle();
			return;
		}
		LiteNetLibConnectionManager.NetPeerConnection connection = this.GetConnection(peer);
		if (connection != null)
		{
			Action<IConnection, NetDataReader, DeliveryMethod> action = this.onReceivedDataEvent;
			if (action != null)
			{
				action(connection, reader, deliveryMethod);
			}
		}
		reader.Recycle();
	}

	void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
	{
		Action<IPEndPoint, NetDataReader> action = this.onReceiveUnconnectedDataEvent;
		if (action != null)
		{
			action(remoteEndPoint, reader);
		}
		reader.Recycle();
	}

	private LiteNetLibConnectionManager.NetPeerConnection GetConnection(NetPeer peer)
	{
		for (int i = 0; i < this._connections.Count; i++)
		{
			if (this._connections[i].netPeer == peer)
			{
				return this._connections[i];
			}
		}
		return null;
	}

	private void AcceptAllPendingRequests()
	{
		for (int i = 0; i < this._pendingRequests.Count; i++)
		{
			LiteNetLibConnectionManager.NetPeerConnectionRequest netPeerConnectionRequest = this._pendingRequests[i];
			this.CreatePendingConnection(netPeerConnectionRequest.Accept(), netPeerConnectionRequest.userId, netPeerConnectionRequest.userName, netPeerConnectionRequest.isConnectionOwner);
		}
		this._pendingRequests.Clear();
	}

	private void TryAccept(ConnectionRequest request, string userId, string userName, bool isConnectionOwner)
	{
		if (this.isRelay && !this.hasConnectionOwner && !isConnectionOwner)
		{
			this._pendingRequests.Add(new LiteNetLibConnectionManager.NetPeerConnectionRequest(request, userId, userName, isConnectionOwner));
			return;
		}
		this.CreatePendingConnection(request.Accept(), userId, userName, isConnectionOwner);
	}

	private void CreatePendingConnection(NetPeer peer, string userId, string userName, bool isConnectionOwner)
	{
		if (peer != null && !this.HasConnectionToEndPoint(peer.EndPoint) && !this.HasPendingConnectionToEndPoint(peer.EndPoint))
		{
			this._pendingConnections.Add(new LiteNetLibConnectionManager.NetPeerConnection(peer, userId, userName, isConnectionOwner));
		}
	}

	private void RemoveConnection(NetPeer netPeer, DisconnectReason reason)
	{
		if (reason == DisconnectReason.Reconnect || reason == DisconnectReason.PeerToPeerConnection)
		{
			this._pendingReconnections.Add(netPeer.EndPoint);
		}
		for (int i = 0; i < this._pendingConnections.Count; i++)
		{
			if (this._pendingConnections[i].netPeer == netPeer)
			{
				this._pendingConnections.RemoveAt(i);
				this.TryDisconnect(reason);
				return;
			}
		}
		for (int j = 0; j < this._connections.Count; j++)
		{
			if (this._connections[j].netPeer == netPeer)
			{
				LiteNetLibConnectionManager.NetPeerConnection obj = this._connections[j];
				this._connections.RemoveAt(j);
				Action<IConnection> action = this.onConnectionDisconnectedEvent;
				if (action != null)
				{
					action(obj);
				}
				this.TryDisconnect(reason);
				return;
			}
		}
	}

	private void TryDisconnect(DisconnectReason reason)
	{
		if (this.isClient && this._pendingConnections.Count == 0 && this._connections.Count == 0 && this._pendingReconnections.Count == 0)
		{
			this.DisconnectInternal((reason == DisconnectReason.Timeout) ? DisconnectedReason.Timeout : ((reason == DisconnectReason.RemoteConnectionClose) ? DisconnectedReason.ServerShutDown : DisconnectedReason.Unknown), (reason == DisconnectReason.DisconnectPeerCalled) ? ConnectionFailedReason.ServerAtCapacity : ConnectionFailedReason.ServerUnreachable);
		}
	}

	private NetDataWriter GetConnectionMessage()
	{
		NetDataWriter netDataWriter = new NetDataWriter();
		netDataWriter.Put(this._secret);
		netDataWriter.Put(this._userId);
		netDataWriter.Put(this._userName);
		netDataWriter.Put(this.isConnectionOwner);
		return netDataWriter;
	}

	private bool ParseConnectionMessage(NetDataReader reader, out string secret, out string userId, out string userName, out bool isConnectionOwner)
	{
		userId = null;
		userName = null;
		isConnectionOwner = false;
		return reader.TryGetString(out secret) && reader.TryGetString(out userId) && reader.TryGetString(out userName) && reader.TryGetBool(out isConnectionOwner);
	}

	private async Task BackgroundDisconnectSentry()
	{
		CancellationToken cancellationToken = this._backgroundSentryDisconnectCts.Token;
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				long num = this._lastPollUpdateTime + 1200000000L - DateTime.UtcNow.Ticks;
				if (num <= 0L)
				{
					this._netManager.DisconnectAll();
					this._sentryDisconnected = true;
					break;
				}
				await Task.Delay(TimeSpan.FromTicks(num + 10000L), cancellationToken);
			}
		}
		catch (Exception)
		{
		}
	}

	private async Task BackgroundShutdownSentry()
	{
		CancellationToken cancellationToken = this._backgroundSentryShutdownCts.Token;
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				long num = this._lastPollUpdateTime + 9000000000L - DateTime.UtcNow.Ticks;
				if (num <= 0L)
				{
					this._netManager.Stop();
					this._sentryShutdown = true;
					break;
				}
				await Task.Delay(TimeSpan.FromTicks(num + 10000L), cancellationToken);
			}
		}
		catch (Exception)
		{
		}
	}

	private void Log(string msg)
	{
		Logger.Debug("[LNLCM] " + msg);
	}

	private void LogError(string msg)
	{
		Logger.Error("[LNLCM] " + msg);
	}

	private const long kBackgroundDisconnectTimeout = 1200000000L;

	private const long kBackgroundShutdownTimeout = 9000000000L;

	private readonly NetManager _netManager;

	private readonly PacketEncryptionLayer _encryptionLayer;

	private readonly List<LiteNetLibConnectionManager.NetPeerConnection> _connections = new List<LiteNetLibConnectionManager.NetPeerConnection>();

	private readonly List<LiteNetLibConnectionManager.NetPeerConnection> _pendingConnections = new List<LiteNetLibConnectionManager.NetPeerConnection>();

	private readonly List<LiteNetLibConnectionManager.NetPeerConnectionRequest> _pendingRequests = new List<LiteNetLibConnectionManager.NetPeerConnectionRequest>();

	private readonly HashSet<IPEndPoint> _pendingReconnections = new HashSet<IPEndPoint>();

	private string _userId;

	private string _userName;

	private string _secret;

	private LiteNetLibConnectionManager.NetworkMode _mode;

	private LiteNetLibConnectionManager.ConnectionState _connectionState;

	private CancellationTokenSource _backgroundSentryDisconnectCts;

	private CancellationTokenSource _backgroundSentryShutdownCts;

	private bool _sentryDisconnected;

	private bool _sentryShutdown;

	private long _lastPollUpdateTime;

	private enum NetworkMode
	{
		None,
		Client,
		Server,
		Relay
	}

	private enum ConnectionState
	{
		Unconnected,
		Connecting,
		Connected
	}

	public abstract class LiteNetLibConnectionParamsBase : IConnectionInitParams<LiteNetLibConnectionManager>
	{
		public string userId;

		public string userName;

		public string secret;

		public int port;

		public bool filterUnencryptedTraffic;

		public bool enableUnconnectedMessages;

		public bool enableBackgroundSentry;
	}

	public class StartServerParams : LiteNetLibConnectionManager.LiteNetLibConnectionParamsBase
	{
	}

	public class StartRelayParams : LiteNetLibConnectionManager.LiteNetLibConnectionParamsBase
	{
	}

	public class StartClientParams : LiteNetLibConnectionManager.LiteNetLibConnectionParamsBase
	{
	}

	public class ConnectToServerParams : LiteNetLibConnectionManager.LiteNetLibConnectionParamsBase
	{
		public IPEndPoint endPoint;

		public string serverUserId;

		public string serverUserName;

		public bool serverIsConnectionOwner = true;
	}

	private class NetPeerConnectionRequest
	{
		public string userId
		{
			get
			{
				return this._userId;
			}
		}

		public string userName
		{
			get
			{
				return this._userName;
			}
		}

		public bool isConnectionOwner
		{
			get
			{
				return this._isConnectionOwner;
			}
		}

		public IPEndPoint endPoint
		{
			get
			{
				return this._request.RemoteEndPoint;
			}
		}

		public NetPeerConnectionRequest(ConnectionRequest request, string userId, string userName, bool isConnectionOwner)
		{
			this._request = request;
			this._userId = userId;
			this._userName = userName;
			this._isConnectionOwner = isConnectionOwner;
		}

		public NetPeer Accept()
		{
			return this._request.Accept();
		}

		private readonly string _userId;

		private readonly string _userName;

		private readonly bool _isConnectionOwner;

		private readonly ConnectionRequest _request;
	}

	private class NetPeerConnection : IConnection, IEquatable<LiteNetLibConnectionManager.NetPeerConnection>
	{
		public string userId
		{
			get
			{
				return this._userId;
			}
		}

		public string userName
		{
			get
			{
				return this._userName;
			}
		}

		public bool isConnectionOwner
		{
			get
			{
				return this._isConnectionOwner;
			}
		}

		public NetPeerConnection(NetPeer netPeer, string userId, string userName, bool isConnectionOwner)
		{
			this.netPeer = netPeer;
			this._userId = userId;
			this._userName = userName;
			this._isConnectionOwner = isConnectionOwner;
		}

		public void Send(NetDataWriter writer, DeliveryMethod deliveryMethod)
		{
			this.netPeer.Send(writer, deliveryMethod);
		}

		public bool Equals(LiteNetLibConnectionManager.NetPeerConnection other)
		{
			return other != null && (this == other || object.Equals(this.netPeer, other.netPeer));
		}

		public override bool Equals(object obj)
		{
			return obj != null && (this == obj || (!(obj.GetType() != base.GetType()) && this.Equals((LiteNetLibConnectionManager.NetPeerConnection)obj)));
		}

		public override int GetHashCode()
		{
			return this.netPeer.GetHashCode();
		}

		public void Disconnect()
		{
			this.netPeer.Disconnect();
		}

		private readonly string _userId;

		private readonly string _userName;

		private readonly bool _isConnectionOwner;

		public readonly NetPeer netPeer;
	}
}
