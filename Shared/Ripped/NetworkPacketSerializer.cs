using LiteNetLib.Utils;
using Shared;
using System;
using System.Collections.Generic;

public class NetworkPacketSerializer<TType, TData> : INetworkPacketSerializer<TData>, INetworkPacketSubSerializer<TData> where TType : struct, IConvertible
{
	public void RegisterCallback<TPacket>(TType packetType, Action<TPacket> callback) where TPacket : INetSerializable, new()
	{
		this.RegisterCallback<TPacket>(packetType, delegate(TPacket packet, TData data)
		{
			Action<TPacket> callback2 = callback;
			if (callback2 == null)
			{
				return;
			}
			callback2(packet);
		});
	}

	public void RegisterCallback<TPacket>(TType packetType, Action<TPacket> callback, Func<TPacket> constructor) where TPacket : INetSerializable
	{
		this.RegisterCallback<TPacket>(packetType, delegate(TPacket packet, TData data)
		{
			Action<TPacket> callback2 = callback;
			if (callback2 == null)
			{
				return;
			}
			callback2(packet);
		}, constructor);
	}

	public void RegisterCallback<TPacket>(TType packetType, Action<TPacket, TData> callback) where TPacket : INetSerializable, new()
	{
		this.RegisterCallback<TPacket>(packetType, callback, () => Activator.CreateInstance<TPacket>());
	}

	public void RegisterCallback<TPacket>(TType packetType, Action<TPacket, TData> callback, Func<TPacket> constructor) where TPacket : INetSerializable
	{
		byte b = (byte)Convert.ChangeType(packetType, typeof(byte));
		this._typeRegistry[typeof(TPacket)] = b;
		Func<NetDataReader, int, TPacket> deserialize = delegate(NetDataReader reader, int size)
		{
			TPacket tpacket = constructor();
			if (tpacket == null)
			{
				Logger.Error("Constructor for " + typeof(TPacket) + " returned null!");
				reader.SkipBytes(size);
			}
			else
			{
				tpacket.Deserialize(reader);
			}
			return tpacket;
		};
		this._messsageHandlers[b] = delegate(NetDataReader reader, int size, TData data)
		{
			callback(deserialize(reader, size), data);
		};
	}

	public void UnregisterCallback<TPacket>(TType packetType)
	{
		byte key = (byte)((object)packetType);
		this._typeRegistry.Remove(typeof(TPacket));
		this._messsageHandlers.Remove(key);
	}

	public void RegisterSubSerializer(TType packetType, INetworkPacketSubSerializer<TData> subSubSerializer)
	{
		byte b = (byte)((object)packetType);
		this._subSerializerRegistry[subSubSerializer] = b;
		this._messsageHandlers[b] = delegate(NetDataReader reader, int size, TData data)
		{
			subSubSerializer.Deserialize(reader, size, data);
		};
	}

	public void UnregisterSubSerializer(TType packetType, INetworkPacketSubSerializer<TData> subSubSerializer)
	{
		byte key = (byte)((object)packetType);
		this._subSerializerRegistry.Remove(subSubSerializer);
		this._messsageHandlers.Remove(key);
	}

	public void SerializePacket(NetDataWriter writer, INetSerializable packet)
	{
		this.SerializePacketInternal(writer, packet, true);
	}

	private void SerializePacketInternal(NetDataWriter externalWriter, INetSerializable packet, bool prependLength)
	{
		byte value;
		INetworkPacketSubSerializer<TData> networkPacketSubSerializer;
		if (!this.TryGetPacketType(packet.GetType(), out value, out networkPacketSubSerializer))
		{
			return;
		}
		NetDataWriter netDataWriter = prependLength ? this._internalWriter : externalWriter;
		netDataWriter.Put(value);
		if (networkPacketSubSerializer != null)
		{
			networkPacketSubSerializer.Serialize(netDataWriter, packet);
		}
		else
		{
			packet.Serialize(netDataWriter);
		}
		if (prependLength)
		{
			externalWriter.PutVarUInt((uint)this._internalWriter.Length);
			externalWriter.Put(this._internalWriter.Data, 0, this._internalWriter.Length);
			this._internalWriter.Reset();
		}
	}

	public void ProcessAllPackets(NetDataReader reader, TData data)
	{
		while (this.ProcessPacket(reader, data))
		{
		}
	}

	public bool ProcessPacket(NetDataReader reader, TData data)
	{
		if (reader.EndOfData)
		{
			return false;
		}
		int varUInt = (int)reader.GetVarUInt();
		this.ProcessPacketInternal(reader, varUInt, data);
		return true;
	}

	private void ProcessPacketInternal(NetDataReader reader, int length, TData data)
	{
		byte @byte = reader.GetByte();
		length--;
		Action<NetDataReader, int, TData> action;
		if (this._messsageHandlers.TryGetValue(@byte, out action))
		{
			if (action != null)
			{
				action(reader, length, data);
				return;
			}
		}
		else
		{
			reader.SkipBytes(length);
		}
	}

	private bool TryGetPacketType(Type type, out byte packetType, out INetworkPacketSubSerializer<TData> subSubSerializer)
	{
		subSubSerializer = null;
		if (this._typeRegistry.TryGetValue(type, out packetType))
		{
			return true;
		}
		foreach (KeyValuePair<INetworkPacketSubSerializer<TData>, byte> keyValuePair in this._subSerializerRegistry)
		{
			if (keyValuePair.Key.HandlesType(type))
			{
				subSubSerializer = keyValuePair.Key;
				packetType = keyValuePair.Value;
				return true;
			}
		}
		return false;
	}

	public bool HandlesType(Type type)
	{
		byte b;
		INetworkPacketSubSerializer<TData> networkPacketSubSerializer;
		return this.TryGetPacketType(type, out b, out networkPacketSubSerializer);
	}

	void INetworkPacketSubSerializer<TData>.Serialize(NetDataWriter writer, INetSerializable packet)
	{
		this.SerializePacketInternal(writer, packet, false);
	}

	void INetworkPacketSubSerializer<TData>.Deserialize(NetDataReader reader, int length, TData data)
	{
		this.ProcessPacketInternal(reader, length, data);
	}

	private void Log(string message)
	{
		Logger.Debug("[NetworkPacketSerializer] " + message);
	}

	private Dictionary<byte, Action<NetDataReader, int, TData>> _messsageHandlers = new Dictionary<byte, Action<NetDataReader, int, TData>>();

	private Dictionary<Type, byte> _typeRegistry = new Dictionary<Type, byte>();

	private Dictionary<INetworkPacketSubSerializer<TData>, byte> _subSerializerRegistry = new Dictionary<INetworkPacketSubSerializer<TData>, byte>();

	private readonly NetDataWriter _internalWriter = new NetDataWriter();
}
