using System;
using LiteNetLib;
using LiteNetLib.Utils;

public interface IConnectionManager : IDisposable
{
	public enum DisconnectedReason
	{
		Unknown = 1,
		UserInitiated,
		Timeout,
		Kicked,
		ServerAtCapacity,
		ServerShutDown,
		MasterServerUnreachable
	}

	public enum ConnectionFailedReason
	{
		Unknown = 1,
		ConnectionCanceled,
		ServerUnreachable,
		ServerAlreadyExists,
		ServerDoesNotExist,
		ServerAtCapacity,
		VersionMismatch,
		InvalidPassword,
		MasterServerUnreachable,
		MasterServerNotAuthenticated,
		NetworkNotConnected
	}

	event Action onConnectedEvent;
	event Action<DisconnectedReason> onDisconnectedEvent;
	event Action<ConnectionFailedReason> onConnectionFailedEvent;
	event Action<IConnection> onConnectionConnectedEvent;
	event Action<IConnection> onConnectionDisconnectedEvent;
	event Action<IConnection, float> onLatencyUpdatedEvent;
	event Action<IConnection, NetDataReader, DeliveryMethod> onReceivedDataEvent;

	string userId { get; }

	string userName { get; }

	bool isConnected { get; }

	bool isConnecting { get; }

	int connectionCount { get; }

	bool isConnectionOwner { get; }

	bool isDisposed { get; }

	void SendToAll(NetDataWriter writer, DeliveryMethod deliveryMethod);

	void SendToAll(NetDataWriter writer, DeliveryMethod deliveryMethod, IConnection excludingConnection);

	void PollUpdate();

	bool Init<T>(IConnectionInitParams<T> initParams) where T : IConnectionManager;

	void Disconnect(DisconnectedReason disconnectedReason = DisconnectedReason.UserInitiated);

	IConnection GetConnection(int index);
}
