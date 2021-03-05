using System.IO;
using Terraria;

namespace Pings.Netcode.Packets
{
	public class UUIDPacket : PlayerPacket
	{
		protected readonly string uuid;

		//For reflection
		public UUIDPacket() { }

		public UUIDPacket(Player player, string uuid) : base(player)
		{
			this.uuid = uuid;
		}

		protected override void PostSend(BinaryWriter writer, Player player)
		{
			//PingsMod.Log("UUIDPacket Send: " + uuid, true, true);
			writer.Write(uuid);
		}

		protected override void PostReceive(BinaryReader reader, int sender, Player player)
		{
			string uuid = reader.ReadString();
			//PingsMod.Log("UUIDPacket Recv: " + uuid, true, true);

			player.GetModPlayer<PingsPlayer>().UUID = uuid;
		}
	}
}
