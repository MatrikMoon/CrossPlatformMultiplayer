using LiteNetLib.Utils;

public interface INetworkPacketSerializer<TData>
{
	void ProcessAllPackets(NetDataReader reader, TData data);

	void SerializePacket(NetDataWriter writer, INetSerializable packet);
}