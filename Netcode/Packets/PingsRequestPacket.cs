using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Pings.Netcode.Packets
{
	public class PingsRequestPacket : MPPacket
	{
		public PingsRequestPacket() { }

		public override void Send(BinaryWriter writer)
		{
			if (Main.netMode == NetmodeID.Server)
			{
				//PingsMod.Log("send request", true, true);
				ModContent.GetInstance<PingsSystem>().NetSend(writer);
			}
		}

		public override void Receive(BinaryReader reader, int sender)
		{
			if (Main.netMode == NetmodeID.Server)
			{
				new PingsRequestPacket().Send(to: sender);
			}
			else
			{
				//PingsMod.Log("recv request", true, true);
				ModContent.GetInstance<PingsSystem>().NetReceive(reader);
			}
		}
	}
}
