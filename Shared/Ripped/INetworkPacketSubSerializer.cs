using System;
using LiteNetLib.Utils;

public interface INetworkPacketSubSerializer<TData>
{
	void Deserialize(NetDataReader reader, int length, TData data);

	void Serialize(NetDataWriter writer, INetSerializable packet);

	bool HandlesType(Type type);
}
