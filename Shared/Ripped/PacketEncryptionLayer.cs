using LiteNetLib.Layers;
using LiteNetLib.Utils;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

public class PacketEncryptionLayer : PacketLayerBase
{
	private static void NoDomainReloadInit()
	{
		PacketEncryptionLayer._masterSecretSeed = Encoding.UTF8.GetBytes("master secret");
		PacketEncryptionLayer._keyExpansionSeed = Encoding.UTF8.GetBytes("key expansion");
		PacketEncryptionLayer._random = new RNGCryptoServiceProvider();
		PacketEncryptionLayer._tempByte = new byte[1];
		PacketEncryptionLayer._tempHash = new byte[10];
		PacketEncryptionLayer._tempIV = new byte[16];
	}

	private PacketEncryptionLayer(AesCryptoServiceProvider aes) : base(95)
	{
		this._aes = aes;
		this._aes.Mode = CipherMode.CBC;
		this._aes.Padding = PaddingMode.None;
	}

	public PacketEncryptionLayer() : this(new AesCryptoServiceProvider())
	{
	}

	public bool filterUnencryptedTraffic
	{
		get
		{
			return this._filterUnencryptedTraffic;
		}
		set
		{
			this._filterUnencryptedTraffic = value;
		}
	}

	public void SetUnencryptedTrafficFilter(byte[] unencryptedTrafficFilter)
	{
		this._unencryptedTrafficFilter = unencryptedTrafficFilter;
	}

	public void AddEncryptedEndpoint(uint protocolVersion, IPEndPoint endPoint, string userId, string userName, byte[] preMasterSecret, byte[] serverRandom, byte[] clientRandom, bool isClient)
	{
		PacketEncryptionLayer.EncryptionState encryptionState = new PacketEncryptionLayer.EncryptionState(protocolVersion, preMasterSecret, serverRandom, clientRandom, isClient);
		encryptionState.SetIdentity(userId, userName);
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		lock (encryptionStates)
		{
			this._encryptionStates[endPoint] = encryptionState;
		}
	}

	public PacketEncryptionLayer.IEncryptionState AddUnidentifiedEncryptedEndpoint(uint protocolVersion, IPEndPoint endPoint, byte[] preMasterSecret, byte[] serverRandom, byte[] clientRandom, bool isClient)
	{
		PacketEncryptionLayer.EncryptionState encryptionState = new PacketEncryptionLayer.EncryptionState(protocolVersion, preMasterSecret, serverRandom, clientRandom, isClient);
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		lock (encryptionStates)
		{
			this._encryptionStates[endPoint] = encryptionState;
		}
		return encryptionState;
	}

	public void RemoveUnidentifiedEncryptedEndpoint(IPEndPoint endPoint, PacketEncryptionLayer.IEncryptionState encryptionState)
	{
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		lock (encryptionStates)
		{
			PacketEncryptionLayer.EncryptionState encryptionState2;
			if (this._encryptionStates.TryGetValue(endPoint, out encryptionState2))
			{
				if (encryptionState2 == encryptionState)
				{
					this._encryptionStates.Remove(endPoint);
				}
			}
		}
	}

	public bool RemoveEncryptedEndpoint(IPEndPoint endPoint)
	{
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		bool result;
		lock (encryptionStates)
		{
			Dictionary<int, PacketEncryptionLayer.EncryptionState> dictionary;
			if (this._encryptionStates.Remove(endPoint))
			{
				result = true;
			}
			else if (!this._pendingEncryptionStates.TryGetValue(endPoint.Address, out dictionary))
			{
				result = false;
			}
			else
			{
				bool flag2 = dictionary.Remove(endPoint.Port);
				if (dictionary.Count == 0)
				{
					this._pendingEncryptionStates.Remove(endPoint.Address);
				}
				result = flag2;
			}
		}
		return result;
	}

	public void AddPendingEncryptedEndpoint(uint protocolVersion, IPEndPoint endPoint, string userId, string userName, byte[] preMasterSecret, byte[] serverRandom, byte[] clientRandom, bool isClient)
	{
		PacketEncryptionLayer.EncryptionState encryptionState = new PacketEncryptionLayer.EncryptionState(protocolVersion, preMasterSecret, serverRandom, clientRandom, isClient);
		encryptionState.SetIdentity(userId, userName);
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		lock (encryptionStates)
		{
			Dictionary<int, PacketEncryptionLayer.EncryptionState> dictionary;
			if (!this._pendingEncryptionStates.TryGetValue(endPoint.Address, out dictionary))
			{
				dictionary = new Dictionary<int, PacketEncryptionLayer.EncryptionState>();
				this._pendingEncryptionStates[endPoint.Address] = dictionary;
			}
			dictionary[endPoint.Port] = encryptionState;
		}
	}

	public bool VerifyEndPoint(uint protocolVersion, IPEndPoint endPoint, string userId, string userName = null)
	{
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		bool result;
		lock (encryptionStates)
		{
			PacketEncryptionLayer.EncryptionState encryptionState;
			result = (this._encryptionStates.TryGetValue(endPoint, out encryptionState) && encryptionState.Verify(protocolVersion, userId, userName));
		}
		return result;
	}

	public void RemoveInactiveEndpoints()
	{
		List<IPEndPoint> list = new List<IPEndPoint>();
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		lock (encryptionStates)
		{
			foreach (KeyValuePair<IPEndPoint, PacketEncryptionLayer.EncryptionState> keyValuePair in this._encryptionStates)
			{
				if (keyValuePair.Value.HasTimedOut(-1294967296))
				{
					list.Add(keyValuePair.Key);
				}
			}
			foreach (IPEndPoint key in list)
			{
				this._encryptionStates.Remove(key);
			}
			list.Clear();
			foreach (KeyValuePair<IPAddress, Dictionary<int, PacketEncryptionLayer.EncryptionState>> keyValuePair2 in this._pendingEncryptionStates)
			{
				foreach (KeyValuePair<int, PacketEncryptionLayer.EncryptionState> keyValuePair3 in keyValuePair2.Value)
				{
					if (keyValuePair3.Value.HasTimedOut(100000000L))
					{
						list.Add(new IPEndPoint(keyValuePair2.Key, keyValuePair3.Key));
					}
				}
			}
			foreach (IPEndPoint ipendPoint in list)
			{
				Dictionary<int, PacketEncryptionLayer.EncryptionState> dictionary = this._pendingEncryptionStates[ipendPoint.Address];
				dictionary.Remove(ipendPoint.Port);
				if (dictionary.Count == 0)
				{
					this._pendingEncryptionStates.Remove(ipendPoint.Address);
				}
			}
		}
	}

	public void RemoveAllEndpoints()
	{
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		lock (encryptionStates)
		{
			this._encryptionStates.Clear();
			this._pendingEncryptionStates.Clear();
		}
	}

	private bool TryGetEncryptionState(IPEndPoint endPoint, out PacketEncryptionLayer.EncryptionState state, out bool statePending)
	{
		state = null;
		statePending = false;
		if (endPoint == null)
		{
			return false;
		}
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		bool result;
		lock (encryptionStates)
		{
			Dictionary<int, PacketEncryptionLayer.EncryptionState> dictionary;
			if (this._encryptionStates.TryGetValue(endPoint, out state))
			{
				result = true;
			}
			else if (!this._pendingEncryptionStates.TryGetValue(endPoint.Address, out dictionary))
			{
				result = false;
			}
			else
			{
				statePending = true;
				if (dictionary.TryGetValue(endPoint.Port, out state))
				{
					result = true;
				}
				else
				{
					int num = int.MaxValue;
					foreach (KeyValuePair<int, PacketEncryptionLayer.EncryptionState> keyValuePair in dictionary)
					{
						int num2 = Math.Abs(keyValuePair.Key - endPoint.Port);
						if (num2 < num)
						{
							num = num2;
							state = keyValuePair.Value;
						}
					}
					result = true;
				}
			}
		}
		return result;
	}

	private PacketEncryptionLayer.EncryptionState[] GetPotentialPendingEncryptionStates(IPEndPoint endPoint)
	{
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		PacketEncryptionLayer.EncryptionState[] result;
		lock (encryptionStates)
		{
			Dictionary<int, PacketEncryptionLayer.EncryptionState> source;
			if (!this._pendingEncryptionStates.TryGetValue(endPoint.Address, out source))
			{
				result = null;
			}
			else
			{
				result = (from kvp in source
						  orderby Math.Abs(kvp.Key - endPoint.Port)
						  select kvp.Value).ToArray<PacketEncryptionLayer.EncryptionState>();
			}
		}
		return result;
	}

	private void PromotePendingEncryptionState(IPEndPoint endPoint, PacketEncryptionLayer.EncryptionState state)
	{
		Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> encryptionStates = this._encryptionStates;
		lock (encryptionStates)
		{
			if (!this._encryptionStates.ContainsKey(endPoint))
			{
				this._encryptionStates[endPoint] = state;
				Dictionary<int, PacketEncryptionLayer.EncryptionState> dictionary;
				if (this._pendingEncryptionStates.TryGetValue(endPoint.Address, out dictionary))
				{
					if (!dictionary.Remove(endPoint.Port))
					{
						int num = -1;
						foreach (KeyValuePair<int, PacketEncryptionLayer.EncryptionState> keyValuePair in dictionary)
						{
							if (keyValuePair.Value == state)
							{
								num = keyValuePair.Key;
							}
						}
						if (num != -1)
						{
							dictionary.Remove(num);
						}
					}
					if (dictionary.Count == 0)
					{
						this._pendingEncryptionStates.Remove(endPoint.Address);
					}
				}
			}
		}
	}

	public static byte[] GeneratePreMasterSecret()
	{
		byte[] array = new byte[48];
		PacketEncryptionLayer._random.GetBytes(array);
		array[0] = 3;
		array[1] = 3;
		return array;
	}

	public static bool ValidatePreMasterSecret(byte[] preMasterSecret)
	{
		return preMasterSecret != null && preMasterSecret.Length == 48 && preMasterSecret[0] == 3 && preMasterSecret[1] == 3;
	}

	public static byte[] GenerateRandom(int length)
	{
		byte[] array = new byte[length];
		PacketEncryptionLayer._random.GetBytes(array);
		return array;
	}

	public static byte GetRandomByte()
	{
		if (PacketEncryptionLayer._tempByte == null)
		{
			PacketEncryptionLayer._tempByte = new byte[1];
		}
		PacketEncryptionLayer._random.GetBytes(PacketEncryptionLayer._tempByte);
		return PacketEncryptionLayer._tempByte[0];
	}

	private bool MatchesFilter(byte[] data, int offset, int length)
	{
		if (this._unencryptedTrafficFilter == null)
		{
			return false;
		}
		if (length < this._unencryptedTrafficFilter.Length)
		{
			return false;
		}
		for (int i = 0; i < this._unencryptedTrafficFilter.Length; i++)
		{
			if (data[offset + i] != this._unencryptedTrafficFilter[i])
			{
				return false;
			}
		}
		return true;
	}

	public override void ProcessInboundPacket(ref byte[] data, ref int length)
	{
		var offset = 0;
		ProcessInboundPacket(null, ref data, ref offset, ref length);
	}

	public void ProcessInboundPacket(IPEndPoint remoteEndPoint, ref byte[] data, ref int offset, ref int length)
	{
		if (length == 0)
		{
			return;
		}
		if (remoteEndPoint == null)
		{
			return;
		}
		int num = offset;
		byte b = data[offset];
		offset++;
		length--;
		if (b == 0)
		{
			if (this._filterUnencryptedTraffic && !this.MatchesFilter(data, offset, length))
			{
				length = 0;
			}
			return;
		}
		if (b != 1)
		{
			length = 0;
			return;
		}
		if (length < 36)
		{
			length = 0;
			return;
		}
		if ((length - 4) % 16 != 0)
		{
			length = 0;
			return;
		}
		PacketEncryptionLayer.EncryptionState state;
		bool flag;
		if (!this.TryGetEncryptionState(remoteEndPoint, out state, out flag))
		{
			length = 0;
			return;
		}
		if (flag)
		{
			PacketEncryptionLayer.EncryptionState[] potentialPendingEncryptionStates = this.GetPotentialPendingEncryptionStates(remoteEndPoint);
			byte[] array = new byte[length];
			foreach (PacketEncryptionLayer.EncryptionState state2 in potentialPendingEncryptionStates)
			{
				Array.Copy(data, offset, array, 0, length);
				int sourceIndex = 0;
				int num2 = length;
				if (this.TryDecryptData(array, state2, 0, ref sourceIndex, ref num2))
				{
					Array.Copy(array, sourceIndex, data, num, num2);
					offset = num;
					length = num2;
					this.PromotePendingEncryptionState(remoteEndPoint, state2);
					return;
				}
			}
			length = 0;
			return;
		}
		if (!this.TryDecryptData(data, state, num, ref offset, ref length))
		{
			length = 0;
			return;
		}
	}

	private bool TryDecryptData(byte[] data, PacketEncryptionLayer.EncryptionState state, int startingOffset, ref int offset, ref int length)
	{
		object receiveMutex = state.receiveMutex;
		bool result;
		lock (receiveMutex)
		{
			uint num = BitConverter.ToUInt32(data, offset);
			offset += 4;
			length -= 4;
			if (!state.IsValidSequenceNum(num))
			{
				result = false;
			}
			else
			{
				if (PacketEncryptionLayer._tempIV == null)
				{
					PacketEncryptionLayer._tempIV = new byte[16];
				}
				if (PacketEncryptionLayer._tempHash == null)
				{
					PacketEncryptionLayer._tempHash = new byte[10];
				}
				PacketEncryptionLayer.FastCopyBlock(data, offset, PacketEncryptionLayer._tempIV, 0);
				offset += PacketEncryptionLayer._tempIV.Length;
				length -= PacketEncryptionLayer._tempIV.Length;
				using (ICryptoTransform cryptoTransform = this._aes.CreateDecryptor(state.receiveKey, PacketEncryptionLayer._tempIV))
				{
					int num2 = startingOffset;
					int num3;
					for (int i = length; i >= cryptoTransform.InputBlockSize; i -= num3)
					{
						int inputCount = cryptoTransform.CanTransformMultipleBlocks ? (i / cryptoTransform.InputBlockSize * cryptoTransform.InputBlockSize) : cryptoTransform.InputBlockSize;
						num3 = cryptoTransform.TransformBlock(data, offset, inputCount, data, num2);
						offset += num3;
						num2 += num3;
					}
					offset = startingOffset;
					length = num2 - offset;
				}
				int num4 = (int)data[offset + length - 1];
				bool flag2 = true;
				if (num4 + 10 + 1 > length)
				{
					num4 = 0;
					flag2 = false;
				}
				length -= num4 + 10 + 1;
				PacketEncryptionLayer.FastCopyMac(data, offset + length, PacketEncryptionLayer._tempHash, 0);
				FastBitConverter.GetBytes(data, offset + length, num);
				byte[] array = state.receiveMac.ComputeHash(data, offset, length + 4);
				if (!flag2)
				{
					result = false;
				}
				else
				{
					for (int j = 0; j < 10; j++)
					{
						if (PacketEncryptionLayer._tempHash[j] != array[j])
						{
							return false;
						}
					}
					if (!state.PutSequenceNum(num))
					{
						result = false;
					}
					else
					{
						result = true;
					}
				}
			}
		}
		return result;
	}

	public override void ProcessOutBoundPacket(ref byte[] data, ref int offset, ref int length)
	{
		ProcessOutBoundPacket(null, ref data, ref offset, ref length);
	}

	public void ProcessOutBoundPacket(IPEndPoint remoteEndPoint, ref byte[] data, ref int offset, ref int length)
	{
		int num = offset;
		Array.Copy(data, offset, data, offset + 5 + 16, length);
		offset += 21;
		bool flag = this.MatchesFilter(data, offset, length);
		PacketEncryptionLayer.EncryptionState encryptionState;
		bool flag2;
		if (!flag && this.TryGetEncryptionState(remoteEndPoint, out encryptionState, out flag2))
		{
			object sendMutex = encryptionState.sendMutex;
			lock (sendMutex)
			{
				uint nextSentSequenceNum = encryptionState.GetNextSentSequenceNum();
				FastBitConverter.GetBytes(data, offset + length, nextSentSequenceNum);
				PacketEncryptionLayer._tempHash = encryptionState.sendMac.ComputeHash(data, offset, length + 4);
				PacketEncryptionLayer.FastCopyMac(PacketEncryptionLayer._tempHash, 0, data, offset + length);
				length += 10;
				if (PacketEncryptionLayer._tempIV == null)
				{
					PacketEncryptionLayer._tempIV = new byte[16];
				}
				PacketEncryptionLayer._random.GetBytes(PacketEncryptionLayer._tempIV);
				int num2 = (int)(PacketEncryptionLayer.GetRandomByte() % 64);
				int num3 = (length + num2 + 1) % 16;
				num2 -= num3;
				if (num2 < 0)
				{
					num2 += 16;
				}
				if (offset + length + num2 >= data.Length)
				{
					num2 -= 16;
				}
				for (int i = 0; i <= num2; i++)
				{
					data[offset + length + i] = (byte)num2;
				}
				length += num2 + 1;
				using (ICryptoTransform cryptoTransform = this._aes.CreateEncryptor(encryptionState.sendKey, PacketEncryptionLayer._tempIV))
				{
					int num4 = num + 5 + 16;
					int num5;
					for (int j = length; j >= cryptoTransform.InputBlockSize; j -= num5)
					{
						int inputCount = cryptoTransform.CanTransformMultipleBlocks ? (j / cryptoTransform.InputBlockSize * cryptoTransform.InputBlockSize) : cryptoTransform.InputBlockSize;
						num5 = cryptoTransform.TransformBlock(data, offset, inputCount, data, num4);
						offset += num5;
						num4 += num5;
					}
					offset = num;
					length = num4 - num;
				}
				data[offset] = 1;
				FastBitConverter.GetBytes(data, offset + 1, nextSentSequenceNum);
				PacketEncryptionLayer.FastCopyBlock(PacketEncryptionLayer._tempIV, 0, data, offset + 5);
			}
			return;
		}
		if (this._filterUnencryptedTraffic && !flag)
		{
			length = 0;
			return;
		}
		offset--;
		length++;
		data[offset] = 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void FastCopyBlock(byte[] inAr, int inOff, byte[] outArr, int outOff)
	{
		for (int i = 0; i < 16; i++)
		{
			outArr[outOff + i] = inAr[inOff + i];
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void FastCopyMac(byte[] inAr, int inOff, byte[] outArr, int outOff)
	{
		for (int i = 0; i < 10; i++)
		{
			outArr[outOff + i] = inAr[inOff + i];
		}
	}

	public static void Log(string message)
	{
		Logger.Debug("[Encryption] " + message);
	}

	public static void LogV(string message)
	{
		Logger.Debug("[Encryption] " + message);
	}

    private static byte[] _masterSecretSeed = Encoding.UTF8.GetBytes("master secret");

	private static byte[] _keyExpansionSeed = Encoding.UTF8.GetBytes("key expansion");

	private static RNGCryptoServiceProvider _random = new RNGCryptoServiceProvider();

	[ThreadStatic]
	private static byte[] _tempByte;

	[ThreadStatic]
	private static byte[] _tempIV;

	[ThreadStatic]
	private static byte[] _tempHash;

	private const int kMacHashSize = 10;

	private const int kHeaderSize = 5;

	private const int kMaxPadding = 64;

	private const int kMacKeySize = 64;

	private const int kKeySize = 32;

	private const int kBlockSize = 16;

	private const int kMasterKeySize = 48;

	private const byte kEncryptedPacketType = 1;

	private const byte kPlaintextPacketType = 0;

	public const int preMasterSecretSize = 48;

	public const int randomNonceSize = 32;

	private const long kEncryptionStateTimeout = 3000000000L;

	private const long kPendingEncryptionStateTimeout = 100000000L;

	private readonly Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState> _encryptionStates = new Dictionary<IPEndPoint, PacketEncryptionLayer.EncryptionState>();

	private readonly Dictionary<IPAddress, Dictionary<int, PacketEncryptionLayer.EncryptionState>> _pendingEncryptionStates = new Dictionary<IPAddress, Dictionary<int, PacketEncryptionLayer.EncryptionState>>();

	private readonly AesCryptoServiceProvider _aes;

	private bool _filterUnencryptedTraffic;

	private byte[] _unencryptedTrafficFilter;

	public interface IEncryptionState
	{
		bool SetIdentity(string userId, string userName = null);
	}

	private class EncryptionState : PacketEncryptionLayer.IEncryptionState, IDisposable
	{
		public bool Verify(uint protocolVersion, string userId, string userName)
		{
			return this._isVerified && this._protocolVersion == protocolVersion && this._userId == userId && this._userName == userName;
		}

		public bool SetIdentity(string userId, string userNmae)
		{
			if (this._isVerified)
			{
				return false;
			}
			this._userId = userId;
			this._userName = userNmae;
			this._isVerified = true;
			return true;
		}

		public bool HasTimedOut(long timeout)
		{
			return this._lastUsedTime < DateTime.UtcNow.Ticks - timeout;
		}

		public bool IsValidSequenceNum(uint sequenceNum)
		{
			if (this._receivedSequenceCount == 0)
			{
				return true;
			}
			uint num = this._receivedSequenceIds[this._lastReceivedSequenceIdIndex];
			if (sequenceNum < num && (ulong)sequenceNum > (ulong)num - (ulong)((long)this._receivedSequenceIds.Length))
			{
				for (int i = 0; i < this._receivedSequenceCount; i++)
				{
					if (this._receivedSequenceIds[(this._lastReceivedSequenceIdIndex + this._receivedSequenceIds.Length - i) % this._receivedSequenceIds.Length] == sequenceNum)
					{
						return false;
					}
				}
				return true;
			}
			return sequenceNum > num;
		}

		public bool PutSequenceNum(uint sequenceNum)
		{
			if (this.IsValidSequenceNum(sequenceNum))
			{
				int num = this._lastReceivedSequenceIdIndex;
				this._lastReceivedSequenceIdIndex = (this._lastReceivedSequenceIdIndex + 1) % this._receivedSequenceIds.Length;
				int num2 = this._lastReceivedSequenceIdIndex;
				int num3 = 0;
				while (num3 < this._receivedSequenceCount && this._receivedSequenceIds[num] > sequenceNum)
				{
					num2 = num;
					num = (num + this._receivedSequenceIds.Length - 1) % this._receivedSequenceIds.Length;
					num3++;
				}
				this._receivedSequenceIds[num2] = sequenceNum;
				if (this._receivedSequenceCount < this._receivedSequenceIds.Length)
				{
					this._receivedSequenceCount++;
				}
				this._lastUsedTime = DateTime.UtcNow.Ticks;
				return true;
			}
			return false;
		}

		public uint GetNextSentSequenceNum()
		{
			uint num = this._lastSentSequenceId + 1u;
			this._lastSentSequenceId = num;
			return num;
		}

		public EncryptionState(uint protocolVersion, byte[] preMasterSecret, byte[] serverSeed, byte[] clientSeed, bool isClient)
		{
			this._protocolVersion = protocolVersion;
			byte[] sourceArray = PacketEncryptionLayer.EncryptionState.PRF(PacketEncryptionLayer.EncryptionState.PRF(preMasterSecret, this.MakeSeed(PacketEncryptionLayer._masterSecretSeed, serverSeed, clientSeed), 48), this.MakeSeed(PacketEncryptionLayer._keyExpansionSeed, serverSeed, clientSeed), 192);
			this.sendKey = new byte[32];
			this.receiveKey = new byte[32];
			byte[] array = new byte[64];
			byte[] array2 = new byte[64];
			Array.Copy(sourceArray, 0, isClient ? this.receiveKey : this.sendKey, 0, 32);
			Array.Copy(sourceArray, 32, isClient ? this.sendKey : this.receiveKey, 0, 32);
			Array.Copy(sourceArray, 64, isClient ? array2 : array, 0, 64);
			Array.Copy(sourceArray, 128, isClient ? array : array2, 0, 64);
			this.sendMac = new HMACSHA256(array);
			this.receiveMac = new HMACSHA256(array2);
			this._lastUsedTime = DateTime.UtcNow.Ticks;
		}

		private byte[] MakeSeed(byte[] baseSeed, byte[] serverSeed, byte[] clientSeed)
		{
			byte[] array = new byte[baseSeed.Length + serverSeed.Length + clientSeed.Length];
			Array.Copy(baseSeed, 0, array, 0, baseSeed.Length);
			Array.Copy(serverSeed, 0, array, baseSeed.Length, serverSeed.Length);
			Array.Copy(clientSeed, 0, array, baseSeed.Length + serverSeed.Length, clientSeed.Length);
			return array;
		}

		private static byte[] PRF(byte[] key, byte[] seed, int length)
		{
			int i = 0;
			byte[] array = new byte[length + seed.Length];
			while (i < length)
			{
				Array.Copy(seed, 0, array, i, seed.Length);
				PacketEncryptionLayer.EncryptionState.PRF_Hash(key, array, ref i);
			}
			byte[] array2 = new byte[length];
			Array.Copy(array, 0, array2, 0, length);
			return array2;
		}

		private static void PRF_Hash(byte[] key, byte[] seed, ref int length)
		{
			using (HMACSHA256 hmacsha = new HMACSHA256(key))
			{
				byte[] array = hmacsha.ComputeHash(seed, 0, length);
				int num = Math.Min(length + array.Length, seed.Length);
				Array.Copy(array, 0, seed, length, num - length);
				length = num;
			}
		}

		public void Dispose()
		{
			this.sendMac.Dispose();
			this.receiveMac.Dispose();
		}

		private readonly uint _protocolVersion;

		private bool _isVerified;

		private string _userId;

		private string _userName;

		private long _lastUsedTime;

		private uint _lastSentSequenceId = uint.MaxValue;

		private int _lastReceivedSequenceIdIndex = -1;

		private int _receivedSequenceCount;

		public readonly object sendMutex = new object();

		public readonly object receiveMutex = new object();

		private readonly uint[] _receivedSequenceIds = new uint[64];

		public readonly byte[] sendKey;

		public readonly byte[] receiveKey;

		public readonly HMAC receiveMac;

		public readonly HMAC sendMac;
	}
}
