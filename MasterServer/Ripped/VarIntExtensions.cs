using LiteNetLib.Utils;

namespace MasterServer.Ripped
{
    public static class VarIntExtensions
	{
		public static void PutVarInt(this NetDataWriter writer, int val)
		{
			writer.PutVarLong((long)val);
		}

		public static int GetVarInt(this NetDataReader reader)
		{
			return (int)reader.GetVarLong();
		}

		public static void PutVarUInt(this NetDataWriter writer, uint val)
		{
			writer.PutVarULong((ulong)val);
		}

		public static uint GetVarUInt(this NetDataReader reader)
		{
			return (uint)reader.GetVarULong();
		}

		public static void PutVarLong(this NetDataWriter writer, long val)
		{
			writer.PutVarULong((ulong)((val < 0L) ? ((-val << 1) - 1L) : (val << 1)));
		}

		public static long GetVarLong(this NetDataReader reader)
		{
			long varULong = (long)reader.GetVarULong();
			if ((varULong & 1L) != 1L)
			{
				return varULong >> 1;
			}
			return -(varULong >> 1) + 1L;
		}

		public static void PutVarULong(this NetDataWriter writer, ulong val)
		{
			do
			{
				byte b = (byte)(val & 127UL);
				val >>= 7;
				if (val != 0UL)
				{
					b |= 128;
				}
				writer.Put(b);
			}
			while (val != 0UL);
		}

		public static ulong GetVarULong(this NetDataReader reader)
		{
			ulong num = 0UL;
			int num2 = 0;
			ulong num3;
			while (((num3 = (ulong)reader.GetByte()) & 128UL) != 0UL)
			{
				num |= (num3 & 127UL) << num2;
				num2 += 7;
			}
			return num | num3 << num2;
		}

		public static bool TryGetVarUInt(this NetDataReader reader, out uint value)
		{
			ulong num;
			if (reader.TryGetVarULong(out num) && num >> 32 == 0UL)
			{
				value = (uint)num;
				return true;
			}
			value = 0u;
			return false;
		}

		public static bool TryGetVarULong(this NetDataReader reader, out ulong value)
		{
			value = 0UL;
			int num = 0;
			byte b;
			while (num <= 63 && reader.TryGetByte(out b))
			{
				value |= (ulong)((ulong)((long)(b & 127)) << num);
				num += 7;
				if ((b & 128) == 0)
				{
					return true;
				}
			}
			value = 0UL;
			return false;
		}
	}

}
