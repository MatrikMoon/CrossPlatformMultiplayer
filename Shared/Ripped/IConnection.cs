using LiteNetLib;
using LiteNetLib.Utils;

public interface IConnection
{
	string userId { get; }

	string userName { get; }

	bool isConnectionOwner { get; }

	void Send(NetDataWriter writer, DeliveryMethod deliveryMethod);

	void Disconnect();
}
