/*using System;
using System.Collections.Generic;
using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Utils;

namespace MasterServer.Ripped
{
	public class ConnectedPlayerManager : IDisposable
	{
		private byte GetNextConnectionId()
		{
			do
			{
				this._lastConnectionId += 1;
				if (this._lastConnectionId == 255)
				{
					this._lastConnectionId = 1;
				}
			}
			while (this.GetPlayer(this._lastConnectionId) != null);
			return this._lastConnectionId;
		}

		private void RemoveAllPlayers()
		{
			while (this._players.Count > 0)
			{
				this.RemovePlayer(this._players[0]);
			}
		}

		private void RemovePlayer(ConnectedPlayerManager.ConnectedPlayer player)
		{
			if (this._players.Remove(player))
			{
				player.Disconnect();
				Action<IConnectedPlayer> action = this.playerDisconnectedEvent;
				if (action != null)
				{
					action(player);
				}
				if (player.isConnectionOwner && this.isConnected)
				{
					this.Disconnect(DisconnectedReason.ServerShutDown);
				}
			}
		}

		private void AddPlayer(ConnectedPlayerManager.ConnectedPlayer player)
		{
			if (this.isConnectionOwner)
			{
				this.SendImmediately(ConnectedPlayerManager.SyncTimePacket.pool.Obtain().Init(this.syncTime), false);
			}
			this.SendImmediatelyExcludingPlayer(player.GetPlayerConnectedPacket(), player, true);
			if (player.isDirectConnection)
			{
				for (int i = 0; i < this._players.Count; i++)
				{
					this.SendImmediatelyToPlayer(this._players[i].GetPlayerConnectedPacket(), player);
					if (this._players[i].sortIndex != -1)
					{
						this.SendImmediatelyToPlayer(this._players[i].GetPlayerSortOrderPacket(), player);
					}
					this.SendImmediatelyFromPlayerToPlayer(this._players[i].GetPlayerStatePacket(), this._players[i], player);
				}
				if (this._localPlayer.sortIndex != -1)
				{
					this.SendImmediatelyToPlayer(this._localPlayer.GetPlayerSortOrderPacket(), player);
				}
				this.SendImmediatelyToPlayer(this._localPlayer.GetPlayerStatePacket(), player);
			}
			this._players.Add(player);
			Action<IConnectedPlayer> action = this.playerConnectedEvent;
			if (action == null)
			{
				return;
			}
			action(player);
		}

		private ConnectedPlayerManager.ConnectedPlayer GetPlayer(byte connectionId)
		{
			for (int i = 0; i < this._players.Count; i++)
			{
				if (this._players[i].connectionId == connectionId)
				{
					return this._players[i];
				}
			}
			return null;
		}

		private ConnectedPlayerManager.ConnectedPlayer GetPlayer(IConnection connection, byte remoteConnectionId)
		{
			for (int i = 0; i < this._players.Count; i++)
			{
				if (object.Equals(this._players[i].connection, connection) && this._players[i].remoteConnectionId == remoteConnectionId)
				{
					return this._players[i];
				}
			}
			return null;
		}

		private ConnectedPlayerManager.ConnectedPlayer GetPlayer(string userId)
		{
			for (int i = 0; i < this._players.Count; i++)
			{
				if (object.Equals(this._players[i].userId, userId))
				{
					return this._players[i];
				}
			}
			return null;
		}

		public IConnectedPlayer GetConnectedPlayer(int index)
		{
			return this._players[index];
		}

		public event Action connectedEvent;

		public event Action reinitializedEvent;

		public event Action<DisconnectedReason> disconnectedEvent;

		public event Action<ConnectionFailedReason> connectionFailedEvent;

		public event Action<IConnectedPlayer> playerConnectedEvent;

		public event Action<IConnectedPlayer> playerDisconnectedEvent;

		public event Action<IConnectedPlayer> playerStateChangedEvent;

		public event Action<IConnectedPlayer> playerOrderChangedEvent;

		public event Action syncTimeInitializedEvent;

		public bool isConnectionOwner
		{
			get
			{
				return this._localPlayer.isConnectionOwner;
			}
		}

		public bool isConnectedOrConnecting
		{
			get
			{
				return this.isConnected || this.isConnecting;
			}
		}

		public bool isConnected
		{
			get
			{
				return this._connectionManager.isConnected;
			}
		}

		public bool isConnecting
		{
			get
			{
				return this._connectionManager.isConnecting;
			}
		}

		public IConnectedPlayer localPlayer
		{
			get
			{
				return this._localPlayer;
			}
		}

		public int connectedPlayerCount
		{
			get
			{
				return this._players.Count;
			}
		}

		public float syncTime
		{
			get
			{
				return this.runTime + this._syncTimeOffset.currentAverage - this._syncTimeDelay;
			}
		}

		public float syncTimeDelay
		{
			get
			{
				return this._syncTimeDelay;
			}
			set
			{
				this._syncTimeDelay = value;
			}
		}

		public bool syncTimeInitialized
		{
			get
			{
				return this.isConnectionOwner || this._syncTimeOffset.hasValue;
			}
		}

		private float runTime
		{
			get
			{
				return (float)((double)(DateTime.UtcNow.Ticks - this._startTicks) / 10000000.0);
			}
		}

		public static ConnectedPlayerManager TryCreate<T>(IConnectionInitParams<T> initParams) where T : IConnectionManager, new()
		{
			T t = Activator.CreateInstance<T>();
			if (!t.Init<T>(initParams))
			{
				return null;
			}
			return new ConnectedPlayerManager(t);
		}

		private ConnectedPlayerManager(IConnectionManager connectionManager)
		{
			this._messageSerializer.RegisterCallback<ConnectedPlayerManager.PlayerConnectedPacket>(ConnectedPlayerManager.InternalMessageType.PlayerConnected, new Action<ConnectedPlayerManager.PlayerConnectedPacket, IConnectedPlayer>(this.HandleServerPlayerConnected), new Func<ConnectedPlayerManager.PlayerConnectedPacket>(ConnectedPlayerManager.PlayerConnectedPacket.pool.Obtain));
			this._messageSerializer.RegisterCallback<ConnectedPlayerManager.PlayerStatePacket>(ConnectedPlayerManager.InternalMessageType.PlayerStateUpdate, new Action<ConnectedPlayerManager.PlayerStatePacket, IConnectedPlayer>(this.HandlePlayerStateUpdate), new Func<ConnectedPlayerManager.PlayerStatePacket>(ConnectedPlayerManager.PlayerStatePacket.pool.Obtain));
			this._messageSerializer.RegisterCallback<ConnectedPlayerManager.PlayerLatencyPacket>(ConnectedPlayerManager.InternalMessageType.PlayerLatencyUpdate, new Action<ConnectedPlayerManager.PlayerLatencyPacket, IConnectedPlayer>(this.HandlePlayerLatencyUpdate), new Func<ConnectedPlayerManager.PlayerLatencyPacket>(ConnectedPlayerManager.PlayerLatencyPacket.pool.Obtain));
			this._messageSerializer.RegisterCallback<ConnectedPlayerManager.PlayerDisconnectedPacket>(ConnectedPlayerManager.InternalMessageType.PlayerDisconnected, new Action<ConnectedPlayerManager.PlayerDisconnectedPacket, IConnectedPlayer>(this.HandleServerPlayerDisconnected), new Func<ConnectedPlayerManager.PlayerDisconnectedPacket>(ConnectedPlayerManager.PlayerDisconnectedPacket.pool.Obtain));
			this._messageSerializer.RegisterCallback<ConnectedPlayerManager.PlayerSortOrderPacket>(ConnectedPlayerManager.InternalMessageType.PlayerSortOrderUpdate, new Action<ConnectedPlayerManager.PlayerSortOrderPacket, IConnectedPlayer>(this.HandlePlayerSortOrderUpdate), new Func<ConnectedPlayerManager.PlayerSortOrderPacket>(ConnectedPlayerManager.PlayerSortOrderPacket.pool.Obtain));
			this._messageSerializer.RegisterCallback<ConnectedPlayerManager.SyncTimePacket>(ConnectedPlayerManager.InternalMessageType.SyncTime, new Action<ConnectedPlayerManager.SyncTimePacket, IConnectedPlayer>(this.HandleSyncTimePacket), new Func<ConnectedPlayerManager.SyncTimePacket>(ConnectedPlayerManager.SyncTimePacket.pool.Obtain));
			this._messageSerializer.RegisterCallback<ConnectedPlayerManager.KickPlayerPacket>(ConnectedPlayerManager.InternalMessageType.KickPlayer, new Action<ConnectedPlayerManager.KickPlayerPacket, IConnectedPlayer>(this.HandleKickPlayerPacket), new Func<ConnectedPlayerManager.KickPlayerPacket>(ConnectedPlayerManager.KickPlayerPacket.pool.Obtain));
			this._connectionManager = connectionManager;
			this._connectionManager.onConnectedEvent += this.HandleConnected;
			this._connectionManager.onDisconnectedEvent += this.HandleDisconnected;
			this._connectionManager.onConnectionFailedEvent += this.HandleConnectionFailed;
			this._connectionManager.onConnectionConnectedEvent += this.HandleConnectionConnected;
			this._connectionManager.onConnectionDisconnectedEvent += this.HandleConnectionDisconnected;
			this._connectionManager.onLatencyUpdatedEvent += this.OnNetworkLatencyUpdate;
			this._connectionManager.onReceivedDataEvent += this.OnNetworkReceive;
			this.ResetLocalState();
			for (int i = 0; i < this._connectionManager.connectionCount; i++)
			{
				this.HandleConnectionConnected(this._connectionManager.GetConnection(i));
			}
		}

		private void ResetLocalState()
		{
			this._localPlayer = ConnectedPlayerManager.ConnectedPlayer.CreateLocalPlayer(this, this._connectionManager.userId, this._connectionManager.userName, this._connectionManager.isConnectionOwner);
			this._localPlayer.SetPlayerState(this._localPlayerState.ToBloomFilter());
			this._localPlayer.SetPlayerAvatar(this._localPlayerAvatar);
			this._lastConnectionId = 0;
			this._syncTimeOffset.Reset();
		}

		public bool TryReinitialize<T>(IConnectionInitParams<T> initParams) where T : IConnectionManager, new()
		{
			if (!this._connectionManager.Init<T>(initParams))
			{
				return false;
			}
			this.ResetLocalState();
			Action action = this.reinitializedEvent;
			if (action != null)
			{
				action();
			}
			return true;
		}

		public void PollUpdate()
		{
			this._connectionManager.PollUpdate();
			this._lastPollTime = this.runTime;
			if (!this.isConnected)
			{
				return;
			}
			if (this._connectionManager.isConnectionOwner && this._lastSyncTimeUpdate < this.syncTime - 5f)
			{
				this.Send<ConnectedPlayerManager.SyncTimePacket>(ConnectedPlayerManager.SyncTimePacket.pool.Obtain().Init(this.syncTime));
				this._lastSyncTimeUpdate = this.syncTime;
			}
			this.FlushReliableQueue();
			this.FlushUnreliableQueue();
		}

		public void RegisterSerializer(ConnectedPlayerManager.MessageType serializerType, INetworkPacketSubSerializer<IConnectedPlayer> subSerializer)
		{
			this._messageSerializer.RegisterSubSerializer((ConnectedPlayerManager.InternalMessageType)serializerType, subSerializer);
		}

		public void UnregisterSerializer(ConnectedPlayerManager.MessageType serializerType, INetworkPacketSubSerializer<IConnectedPlayer> subSerializer)
		{
			this._messageSerializer.UnregisterSubSerializer((ConnectedPlayerManager.InternalMessageType)serializerType, subSerializer);
		}

		public T GetConnectionManager<T>() where T : class, IConnectionManager
		{
			return this._connectionManager as T;
		}

		public void Dispose()
		{
			this.Disconnect(DisconnectedReason.UserInitiated);
			this._connectionManager.onConnectedEvent -= this.HandleConnected;
			this._connectionManager.onDisconnectedEvent -= this.HandleDisconnected;
			this._connectionManager.onConnectionFailedEvent -= this.HandleConnectionFailed;
			this._connectionManager.onConnectionConnectedEvent -= this.HandleConnectionConnected;
			this._connectionManager.onConnectionDisconnectedEvent -= this.HandleConnectionDisconnected;
			this._connectionManager.onLatencyUpdatedEvent -= this.OnNetworkLatencyUpdate;
			this._connectionManager.onReceivedDataEvent -= this.OnNetworkReceive;
			this._connectionManager.Dispose();
		}

		public void Disconnect(DisconnectedReason disconnectedReason = DisconnectedReason.UserInitiated)
		{
			this._connectionManager.Disconnect(disconnectedReason);
		}

		public void KickPlayer(string userId, DisconnectedReason disconnectedReason = DisconnectedReason.Kicked)
		{
			ConnectedPlayerManager.ConnectedPlayer player = this.GetPlayer(userId);
			if (player == null)
			{
				return;
			}
			if (this.isConnectionOwner)
			{
				this.SendImmediatelyToPlayer(ConnectedPlayerManager.KickPlayerPacket.pool.Obtain().Init(disconnectedReason), player);
				player.SetKicked();
				Action<IConnectedPlayer> action = this.playerStateChangedEvent;
				if (action == null)
				{
					return;
				}
				action(player);
			}
		}

		public void SetLocalPlayerState(string state, bool setState)
		{
			bool flag;
			if (setState)
			{
				flag = this._localPlayerState.Add(state);
			}
			else
			{
				flag = this._localPlayerState.Remove(state);
			}
			if (flag)
			{
				this._localPlayer.SetPlayerState(this._localPlayerState.ToBloomFilter());
				this.SendImmediately(this._localPlayer.GetPlayerStatePacket(), true);
			}
		}

		public void SetLocalPlayerAvatar(MultiplayerAvatarData multiplayerAvatarData)
		{
			if (!this._localPlayerAvatar.Equals(multiplayerAvatarData))
			{
				this._localPlayerAvatar = multiplayerAvatarData;
				this._localPlayer.SetPlayerAvatar(multiplayerAvatarData);
				this.SendImmediately(this._localPlayer.GetPlayerStatePacket(), true);
			}
		}

		public void SetLocalPlayerSortIndex(int sortIndex)
		{
			this.SetPlayerSortIndex(this._localPlayer, sortIndex);
		}

		public void SetPlayerSortIndex(IConnectedPlayer player, int sortIndex)
		{
			ConnectedPlayerManager.ConnectedPlayer connectedPlayer;
			if (!this.isConnectionOwner || (connectedPlayer = (player as ConnectedPlayerManager.ConnectedPlayer)) == null)
			{
				return;
			}
			if (connectedPlayer.UpdateSortIndex(sortIndex) && this.isConnected)
			{
				this.SendImmediately(connectedPlayer.GetPlayerSortOrderPacket(), true);
			}
		}

		private void HandleConnected()
		{
			Action action = this.connectedEvent;
			if (action == null)
			{
				return;
			}
			action();
		}

		private void HandleDisconnected(DisconnectedReason disconnectedReason)
		{
			this.RemoveAllPlayers();
			Action<DisconnectedReason> action = this.disconnectedEvent;
			if (action == null)
			{
				return;
			}
			action(disconnectedReason);
		}

		private void HandleConnectionFailed(ConnectionFailedReason reason)
		{
			this.RemoveAllPlayers();
			Action<ConnectionFailedReason> action = this.connectionFailedEvent;
			if (action == null)
			{
				return;
			}
			action(reason);
		}

		private void HandleConnectionConnected(IConnection connection)
		{
			this.AddPlayer(ConnectedPlayerManager.ConnectedPlayer.CreateDirectlyConnectedPlayer(this, this.GetNextConnectionId(), connection));
		}

		private void OnNetworkLatencyUpdate(IConnection connection, float latency)
		{
			ConnectedPlayerManager.ConnectedPlayer player = this.GetPlayer(connection, 0);
			if (player != null)
			{
				player.UpdateLatency(latency);
				this.SendImmediatelyFromPlayer(ConnectedPlayerManager.PlayerLatencyPacket.pool.Obtain().Init(player.currentLatency), player, true);
			}
		}

		private void HandleConnectionDisconnected(IConnection connection)
		{
			for (int i = this._players.Count - 1; i >= 0; i--)
			{
				ConnectedPlayerManager.ConnectedPlayer connectedPlayer = this._players[i];
				if (object.Equals(connectedPlayer.connection, connection))
				{
					this.SendImmediatelyFromPlayer(ConnectedPlayerManager.PlayerDisconnectedPacket.pool.Obtain(), connectedPlayer, false);
					this.RemovePlayer(connectedPlayer);
					if (i > this._players.Count)
					{
						i = this._players.Count;
					}
				}
			}
		}

		private void OnNetworkReceive(IConnection connection, NetDataReader reader, DeliveryMethod deliveryMethod)
		{
			byte remoteConnectionId;
			byte b;
			if (!reader.TryGetByte(out remoteConnectionId) || !reader.TryGetByte(out b) || reader.AvailableBytes == 0)
			{
				return;
			}
			ConnectedPlayerManager.ConnectedPlayer player = this.GetPlayer(connection, remoteConnectionId);
			if (player == null)
			{
				return;
			}
			if (b != 0 && this._connectionManager.connectionCount > 1)
			{
				if (b == 255)
				{
					this._temporaryDataWriter.Reset();
					this._temporaryDataWriter.Put(player.connectionId);
					this._temporaryDataWriter.Put(byte.MaxValue);
					this._temporaryDataWriter.Put(reader.RawData, reader.Position, reader.AvailableBytes);
					this._connectionManager.SendToAll(this._temporaryDataWriter, deliveryMethod, connection);
				}
				else
				{
					ConnectedPlayerManager.ConnectedPlayer player2 = this.GetPlayer(b);
					if (player2 != null && player2.connection != connection)
					{
						this._temporaryDataWriter.Reset();
						this._temporaryDataWriter.Put(player.connectionId);
						this._temporaryDataWriter.Put(player2.remoteConnectionId);
						this._temporaryDataWriter.Put(reader.RawData, reader.Position, reader.AvailableBytes);
						player2.connection.Send(this._temporaryDataWriter, deliveryMethod);
					}
				}
			}
			if (b == 0 || b == 255)
			{
				this._messageSerializer.ProcessAllPackets(reader, player);
			}
		}

		public void Send<T>(T message) where T : INetSerializable
		{
			if (!this.isConnected)
			{
				IPoolablePacket poolablePacket;
				if ((poolablePacket = (message as IPoolablePacket)) != null)
				{
					poolablePacket.Release();
				}
				return;
			}
			if (this._reliableDataWriter.Length == 0)
			{
				this._reliableDataWriter.Put(0);
				this._reliableDataWriter.Put(byte.MaxValue);
			}
			this.Write(this._reliableDataWriter, message);
		}

		public void SendUnreliable<T>(T message) where T : INetSerializable
		{
			if (!this.isConnected)
			{
				IPoolablePacket poolablePacket;
				if ((poolablePacket = (message as IPoolablePacket)) != null)
				{
					poolablePacket.Release();
				}
				return;
			}
			this._temporaryDataWriter.Reset();
			this.Write(this._temporaryDataWriter, message);
			if (this._temporaryDataWriter.Length + 2 > 412)
			{
				return;
			}
			if (this._unreliableDataWriter.Length + this._temporaryDataWriter.Length > 412)
			{
				this.FlushUnreliableQueue();
			}
			if (this._unreliableDataWriter.Length == 0)
			{
				this._unreliableDataWriter.Put(0);
				this._unreliableDataWriter.Put(byte.MaxValue);
			}
			this._unreliableDataWriter.Put(this._temporaryDataWriter.Data, 0, this._temporaryDataWriter.Length);
		}

		private void SendImmediately(INetSerializable message, bool onlyFirstDegree = false)
		{
			this._connectionManager.SendToAll(this.WriteOne(this._localPlayer.connectionId, onlyFirstDegree ? 0 : byte.MaxValue, message), DeliveryMethod.ReliableOrdered);
		}

		private void SendImmediatelyExcludingPlayer(INetSerializable message, ConnectedPlayerManager.ConnectedPlayer excludingPlayer, bool onlyFirstDegree = false)
		{
			this._connectionManager.SendToAll(this.WriteOne(this._localPlayer.connectionId, onlyFirstDegree ? 0 : byte.MaxValue, message), DeliveryMethod.ReliableOrdered, excludingPlayer.connection);
		}

		private void SendImmediatelyToPlayer(INetSerializable message, ConnectedPlayerManager.ConnectedPlayer toPlayer)
		{
			toPlayer.connection.Send(this.WriteOne(this._localPlayer.connectionId, toPlayer.remoteConnectionId, message), DeliveryMethod.ReliableOrdered);
		}

		private void SendImmediatelyFromPlayer(INetSerializable message, ConnectedPlayerManager.ConnectedPlayer fromPlayer, bool onlyFirstDegree = false)
		{
			this._connectionManager.SendToAll(this.WriteOne(fromPlayer.connectionId, onlyFirstDegree ? 0 : byte.MaxValue, message), DeliveryMethod.ReliableOrdered, fromPlayer.connection);
		}

		private void SendImmediatelyFromPlayerToPlayer(INetSerializable message, ConnectedPlayerManager.ConnectedPlayer fromPlayer, ConnectedPlayerManager.ConnectedPlayer toPlayer)
		{
			toPlayer.connection.Send(this.WriteOne(fromPlayer.connectionId, toPlayer.remoteConnectionId, message), DeliveryMethod.ReliableOrdered);
		}

		private void FlushReliableQueue()
		{
			if (this._reliableDataWriter.Length <= 0)
			{
				return;
			}
			this._connectionManager.SendToAll(this._reliableDataWriter, DeliveryMethod.ReliableOrdered);
			this._reliableDataWriter.Reset();
		}

		private void FlushUnreliableQueue()
		{
			if (this._unreliableDataWriter.Length <= 0)
			{
				return;
			}
			this._connectionManager.SendToAll(this._unreliableDataWriter, DeliveryMethod.Unreliable);
			this._unreliableDataWriter.Reset();
		}

		private NetDataWriter WriteOne(byte senderId, byte receiverId, INetSerializable message)
		{
			this._temporaryDataWriter.Reset();
			this._temporaryDataWriter.Put(senderId);
			this._temporaryDataWriter.Put(receiverId);
			this.Write(this._temporaryDataWriter, message);
			return this._temporaryDataWriter;
		}

		private void Write(NetDataWriter writer, INetSerializable packet)
		{
			this._messageSerializer.SerializePacket(writer, packet);
			IPoolablePacket poolablePacket;
			if ((poolablePacket = (packet as IPoolablePacket)) != null)
			{
				poolablePacket.Release();
			}
		}

		private void HandleServerPlayerConnected(ConnectedPlayerManager.PlayerConnectedPacket packet, IConnectedPlayer iPlayer)
		{
			ConnectedPlayerManager.ConnectedPlayer parent = (ConnectedPlayerManager.ConnectedPlayer)iPlayer;
			if (this.GetPlayer(packet.userId) == null)
			{
				this.AddPlayer(ConnectedPlayerManager.ConnectedPlayer.CreateRemoteConnectedPlayer(this, this.GetNextConnectionId(), packet, parent));
			}
			packet.Release();
		}

		private void HandlePlayerStateUpdate(ConnectedPlayerManager.PlayerStatePacket packet, IConnectedPlayer iPlayer)
		{
			ConnectedPlayerManager.ConnectedPlayer connectedPlayer = (ConnectedPlayerManager.ConnectedPlayer)iPlayer;
			connectedPlayer.UpdateState(packet);
			this.SendImmediatelyFromPlayer(packet, connectedPlayer, true);
			Action<IConnectedPlayer> action = this.playerStateChangedEvent;
			if (action == null)
			{
				return;
			}
			action(connectedPlayer);
		}

		private void HandlePlayerLatencyUpdate(ConnectedPlayerManager.PlayerLatencyPacket packet, IConnectedPlayer iPlayer)
		{
			ConnectedPlayerManager.ConnectedPlayer connectedPlayer = (ConnectedPlayerManager.ConnectedPlayer)iPlayer;
			connectedPlayer.UpdateLatency(packet.latency);
			packet.Release();
			this.SendImmediatelyFromPlayer(ConnectedPlayerManager.PlayerLatencyPacket.pool.Obtain().Init(connectedPlayer.currentLatency), connectedPlayer, true);
		}

		private void HandleServerPlayerDisconnected(ConnectedPlayerManager.PlayerDisconnectedPacket packet, IConnectedPlayer iPlayer)
		{
			packet.Release();
			ConnectedPlayerManager.ConnectedPlayer player = (ConnectedPlayerManager.ConnectedPlayer)iPlayer;
			this.RemovePlayer(player);
		}

		private void HandleKickPlayerPacket(ConnectedPlayerManager.KickPlayerPacket packet, IConnectedPlayer iPlayer)
		{
			DisconnectedReason disconnectedReason = packet.disconnectedReason;
			packet.Release();
			if (!iPlayer.isConnectionOwner)
			{
				return;
			}
			this.Disconnect(disconnectedReason);
		}

		private void HandlePlayerSortOrderUpdate(ConnectedPlayerManager.PlayerSortOrderPacket packet, IConnectedPlayer iPlayer)
		{
			ConnectedPlayerManager.ConnectedPlayer connectedPlayer = (packet.userId == this._localPlayer.userId) ? this._localPlayer : this.GetPlayer(packet.userId);
			int sortIndex = packet.sortIndex;
			packet.Release();
			if (connectedPlayer == null)
			{
				return;
			}
			if (connectedPlayer.UpdateSortIndex(sortIndex))
			{
				Action<IConnectedPlayer> action = this.playerOrderChangedEvent;
				if (action != null)
				{
					action(connectedPlayer);
				}
				this.SendImmediatelyExcludingPlayer(connectedPlayer.GetPlayerSortOrderPacket(), (ConnectedPlayerManager.ConnectedPlayer)iPlayer, true);
			}
		}

		private void HandleSyncTimePacket(ConnectedPlayerManager.SyncTimePacket packet, IConnectedPlayer player)
		{
			float syncTime = packet.syncTime;
			packet.Release();
			if (this.runTime - this._lastPollTime > 0.03f)
			{
				return;
			}
			bool flag = !this._syncTimeOffset.hasValue;
			this._syncTimeOffset.Update(syncTime + player.currentLatency - this.runTime);
			if (flag)
			{
				Action action = this.syncTimeInitializedEvent;
				if (action == null)
				{
					return;
				}
				action();
			}
		}

		[Conditional("VERBOSE_LOGGING")]
		private void Log(string message)
		{
			BGNetDebug.Log("[ConnectedPlayerManager]" + message);
		}

		public const int invalidSortIndex = -1;

		private const byte kAllConnectionsId = 255;

		private const byte kLocalConnectionId = 0;

		private const float kSyncTimeUpdateFrequency = 5f;

		private const float kSyncTimeAllowedReceiveWindow = 0.03f;

		private const int kMaxUnreliableMessageLength = 412;

		private readonly long _startTicks = DateTime.UtcNow.Ticks;

		private readonly RollingAverage _syncTimeOffset = new RollingAverage(30);

		private float _syncTimeDelay;

		private readonly IConnectionManager _connectionManager;

		private readonly NetDataWriter _temporaryDataWriter = new NetDataWriter();

		private readonly NetDataWriter _reliableDataWriter = new NetDataWriter();

		private readonly NetDataWriter _unreliableDataWriter = new NetDataWriter();

		private readonly List<ConnectedPlayerManager.ConnectedPlayer> _players = new List<ConnectedPlayerManager.ConnectedPlayer>();

		private readonly HashSet<string> _localPlayerState = new HashSet<string>();

		private MultiplayerAvatarData _localPlayerAvatar;

		private ConnectedPlayerManager.ConnectedPlayer _localPlayer;

		private byte _lastConnectionId;

		private float _lastSyncTimeUpdate;

		private float _lastPollTime;

		private readonly NetworkPacketSerializer<ConnectedPlayerManager.InternalMessageType, IConnectedPlayer> _messageSerializer = new NetworkPacketSerializer<ConnectedPlayerManager.InternalMessageType, IConnectedPlayer>();

		private class ConnectedPlayer : IConnectedPlayer
		{
			public IConnection connection
			{
				get
				{
					return this._connection;
				}
			}

			public byte connectionId
			{
				get
				{
					return this._connectionId;
				}
			}

			public byte remoteConnectionId
			{
				get
				{
					return this._remoteConnectionId;
				}
			}

			public bool isConnected
			{
				get
				{
					return this._isConnected;
				}
			}

			public bool isConnectionOwner
			{
				get
				{
					return this._isConnectionOwner;
				}
			}

			public bool isKicked
			{
				get
				{
					return this._isKicked;
				}
			}

			public int sortIndex
			{
				get
				{
					return this._sortIndex;
				}
			}

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

			public bool isMe
			{
				get
				{
					return this._isMe;
				}
			}

			public float currentLatency
			{
				get
				{
					float currentAverage = this._latency.currentAverage;
					ConnectedPlayerManager.ConnectedPlayer parent = this._parent;
					return currentAverage + ((parent != null) ? parent.currentLatency : 0f);
				}
			}

			public float offsetSyncTime
			{
				get
				{
					return Mathf.Min(this._manager.syncTime + this._manager.syncTimeDelay - this.currentLatency - (this.isMe ? 0f : 0.06f), this._manager.syncTime);
				}
			}

			public MultiplayerAvatarData multiplayerAvatarData
			{
				get
				{
					return this._playerAvatar;
				}
			}

			public bool isDirectConnection
			{
				get
				{
					return this._parent == null;
				}
			}

			private ConnectedPlayer(ConnectedPlayerManager manager, byte connectionId, byte remoteConnectionId, IConnection connection, ConnectedPlayerManager.ConnectedPlayer parent, string userId, string userName, bool isConnectionOwner, bool isMe)
			{
				this._manager = manager;
				this._connectionId = connectionId;
				this._remoteConnectionId = remoteConnectionId;
				this._parent = parent;
				this._connection = connection;
				this._userId = userId;
				this._userName = userName;
				this._isConnectionOwner = isConnectionOwner;
				this._isMe = isMe;
				this._sortIndex = -1;
			}

			public static ConnectedPlayerManager.ConnectedPlayer CreateLocalPlayer(ConnectedPlayerManager manager, string userId, string userName, bool isConnectionOwner)
			{
				return new ConnectedPlayerManager.ConnectedPlayer(manager, 0, 0, null, null, userId, userName, isConnectionOwner, true);
			}

			public static ConnectedPlayerManager.ConnectedPlayer CreateDirectlyConnectedPlayer(ConnectedPlayerManager manager, byte connectionId, IConnection connection)
			{
				return new ConnectedPlayerManager.ConnectedPlayer(manager, connectionId, 0, connection, null, connection.userId, connection.userName, connection.isConnectionOwner, false);
			}

			public static ConnectedPlayerManager.ConnectedPlayer CreateRemoteConnectedPlayer(ConnectedPlayerManager manager, byte connectionId, ConnectedPlayerManager.PlayerConnectedPacket packet, ConnectedPlayerManager.ConnectedPlayer parent)
			{
				return new ConnectedPlayerManager.ConnectedPlayer(manager, connectionId, packet.remoteConnectionId, parent.connection, parent, packet.userId, packet.userName, packet.isConnectionOwner, false);
			}

			public ConnectedPlayerManager.PlayerConnectedPacket GetPlayerConnectedPacket()
			{
				return ConnectedPlayerManager.PlayerConnectedPacket.pool.Obtain().Init(this.connectionId, this.userId, this.userName, this.isConnectionOwner);
			}

			public ConnectedPlayerManager.PlayerStatePacket GetPlayerStatePacket()
			{
				return ConnectedPlayerManager.PlayerStatePacket.pool.Obtain().Init(this._playerState, this._playerAvatar);
			}

			public ConnectedPlayerManager.PlayerSortOrderPacket GetPlayerSortOrderPacket()
			{
				return ConnectedPlayerManager.PlayerSortOrderPacket.pool.Obtain().Init(this._userId, this._sortIndex);
			}

			public void Disconnect()
			{
				if (this._isConnected)
				{
					this._isConnected = false;
					if (this.isDirectConnection)
					{
						IConnection connection = this.connection;
						if (connection == null)
						{
							return;
						}
						connection.Disconnect();
					}
				}
			}

			public void UpdateLatency(float latency)
			{
				this._latency.Update(latency);
			}

			public bool UpdateSortIndex(int index)
			{
				if (this._sortIndex == index)
				{
					return false;
				}
				this._sortIndex = index;
				return true;
			}

			public void SetKicked()
			{
				this._isKicked = true;
			}

			public void UpdateState(ConnectedPlayerManager.PlayerStatePacket packet)
			{
				this._playerState = packet.playerState;
				this._playerAvatar = packet.playerAvatar;
			}

			public bool HasState(string state)
			{
				return this._playerState.Contains(state);
			}

			public void SetPlayerState(BloomFilter bloomFilter)
			{
				this._playerState = bloomFilter;
			}

			public void SetPlayerAvatar(MultiplayerAvatarData avatarData)
			{
				this._playerAvatar = avatarData;
			}

			public override string ToString()
			{
				return string.Format("[ConnectedPlayer {0}({1}) isMe:{2} isConnectionOwner:{3}]", new object[]
				{
				this.userName,
				this.userId,
				this.isMe,
				this.isConnectionOwner
				});
			}

			private const float kFixedOffset = 0.06f;

			private readonly string _userId;

			private readonly string _userName;

			private readonly bool _isMe;

			private readonly bool _isConnectionOwner;

			private readonly ConnectedPlayerManager _manager;

			private readonly IConnection _connection;

			private readonly ConnectedPlayerManager.ConnectedPlayer _parent;

			private readonly byte _connectionId;

			private readonly byte _remoteConnectionId;

			private int _sortIndex;

			private bool _isConnected = true;

			private bool _isKicked;

			private BloomFilter _playerState;

			private MultiplayerAvatarData _playerAvatar;

			private readonly RollingAverage _latency = new RollingAverage(30);
		}

		private enum InternalMessageType : byte
		{
			SyncTime,
			PlayerConnected,
			PlayerStateUpdate,
			PlayerLatencyUpdate,
			PlayerDisconnected,
			PlayerSortOrderUpdate,
			Party,
			MultiplayerSession,
			KickPlayer
		}

		public enum MessageType : byte
		{
			Party = 6,
			MultiplayerSession
		}

		private class PlayerConnectedPacket : INetSerializable, IPoolablePacket
		{
			public static PacketPool<ConnectedPlayerManager.PlayerConnectedPacket> pool
			{
				get
				{
					return ThreadStaticPacketPool<ConnectedPlayerManager.PlayerConnectedPacket>.pool;
				}
			}

			public void Serialize(NetDataWriter writer)
			{
				writer.Put(this.remoteConnectionId);
				writer.Put(this.userId);
				writer.Put(this.userName);
				writer.Put(this.isConnectionOwner);
			}

			public void Deserialize(NetDataReader reader)
			{
				this.remoteConnectionId = reader.GetByte();
				this.userId = reader.GetString();
				this.userName = reader.GetString();
				this.isConnectionOwner = reader.GetBool();
			}

			public void Release()
			{
				ConnectedPlayerManager.PlayerConnectedPacket.pool.Release(this);
			}

			public ConnectedPlayerManager.PlayerConnectedPacket Init(byte connectionId, string userId, string userName, bool isConnectionOwner)
			{
				this.remoteConnectionId = connectionId;
				this.userId = userId;
				this.userName = userName;
				this.isConnectionOwner = isConnectionOwner;
				return this;
			}

			public byte remoteConnectionId;

			public string userId;

			public string userName;

			public bool isConnectionOwner;
		}

		private class PlayerStatePacket : INetSerializable, IPoolablePacket
		{
			public static PacketPool<ConnectedPlayerManager.PlayerStatePacket> pool
			{
				get
				{
					return ThreadStaticPacketPool<ConnectedPlayerManager.PlayerStatePacket>.pool;
				}
			}

			public void Serialize(NetDataWriter writer)
			{
				this.playerState.Serialize(writer);
				this.playerAvatar.Serialize(writer);
			}

			public void Deserialize(NetDataReader reader)
			{
				this.playerState = BloomFilter.Deserialize(reader);
				this.playerAvatar = MultiplayerAvatarData.Deserialize(reader);
			}

			public void Release()
			{
				ConnectedPlayerManager.PlayerStatePacket.pool.Release(this);
			}

			public ConnectedPlayerManager.PlayerStatePacket Init(BloomFilter states, MultiplayerAvatarData avatar)
			{
				this.playerState = states;
				this.playerAvatar = avatar;
				return this;
			}

			public BloomFilter playerState;

			public MultiplayerAvatarData playerAvatar;
		}

		private class PlayerSortOrderPacket : INetSerializable, IPoolablePacket
		{
			public static PacketPool<ConnectedPlayerManager.PlayerSortOrderPacket> pool
			{
				get
				{
					return ThreadStaticPacketPool<ConnectedPlayerManager.PlayerSortOrderPacket>.pool;
				}
			}

			public void Serialize(NetDataWriter writer)
			{
				writer.Put(this.userId);
				writer.PutVarInt(this.sortIndex);
			}

			public void Deserialize(NetDataReader reader)
			{
				this.userId = reader.GetString();
				this.sortIndex = reader.GetVarInt();
			}

			public void Release()
			{
				ConnectedPlayerManager.PlayerSortOrderPacket.pool.Release(this);
			}

			public ConnectedPlayerManager.PlayerSortOrderPacket Init(string userId, int sortIndex)
			{
				this.userId = userId;
				this.sortIndex = sortIndex;
				return this;
			}

			public string userId;

			public int sortIndex;
		}

		private class PlayerDisconnectedPacket : INetSerializable, IPoolablePacket
		{
			public static PacketPool<ConnectedPlayerManager.PlayerDisconnectedPacket> pool
			{
				get
				{
					return ThreadStaticPacketPool<ConnectedPlayerManager.PlayerDisconnectedPacket>.pool;
				}
			}

			public void Serialize(NetDataWriter writer)
			{
			}

			public void Deserialize(NetDataReader reader)
			{
			}

			public void Release()
			{
				ConnectedPlayerManager.PlayerDisconnectedPacket.pool.Release(this);
			}
		}

		private class KickPlayerPacket : INetSerializable, IPoolablePacket
		{
			public static PacketPool<ConnectedPlayerManager.KickPlayerPacket> pool
			{
				get
				{
					return ThreadStaticPacketPool<ConnectedPlayerManager.KickPlayerPacket>.pool;
				}
			}

			public ConnectedPlayerManager.KickPlayerPacket Init(DisconnectedReason disconnectedReason)
			{
				this.disconnectedReason = disconnectedReason;
				return this;
			}

			public void Serialize(NetDataWriter writer)
			{
				writer.PutVarInt((int)this.disconnectedReason);
			}

			public void Deserialize(NetDataReader reader)
			{
				this.disconnectedReason = (DisconnectedReason)reader.GetVarInt();
			}

			public void Release()
			{
				ConnectedPlayerManager.KickPlayerPacket.pool.Release(this);
			}

			public DisconnectedReason disconnectedReason;
		}

		private class SyncTimePacket : INetSerializable, IPoolablePacket
		{
			public static PacketPool<ConnectedPlayerManager.SyncTimePacket> pool
			{
				get
				{
					return ThreadStaticPacketPool<ConnectedPlayerManager.SyncTimePacket>.pool;
				}
			}

			public void Serialize(NetDataWriter writer)
			{
				writer.Put(this.syncTime);
			}

			public void Deserialize(NetDataReader reader)
			{
				this.syncTime = reader.GetFloat();
			}

			public ConnectedPlayerManager.SyncTimePacket Init(float syncTime)
			{
				this.syncTime = syncTime;
				return this;
			}

			public void Release()
			{
				ConnectedPlayerManager.SyncTimePacket.pool.Release(this);
			}

			public float syncTime;
		}

		private class PlayerLatencyPacket : INetSerializable, IPoolablePacket
		{
			public static PacketPool<ConnectedPlayerManager.PlayerLatencyPacket> pool
			{
				get
				{
					return ThreadStaticPacketPool<ConnectedPlayerManager.PlayerLatencyPacket>.pool;
				}
			}

			public void Serialize(NetDataWriter writer)
			{
				writer.Put(this.latency);
			}

			public void Deserialize(NetDataReader reader)
			{
				this.latency = reader.GetFloat();
			}

			public ConnectedPlayerManager.PlayerLatencyPacket Init(float latency)
			{
				this.latency = latency;
				return this;
			}

			public void Release()
			{
				ConnectedPlayerManager.PlayerLatencyPacket.pool.Release(this);
			}

			public float latency;
		}
	}
}*/