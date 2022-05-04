using System.IO;
using Terraria;
using Terraria.ID;

namespace Pings.Netcode.Packets
{
	public class PingPacket : MPPacket
	{
		protected readonly Ping ping;

		//For reflection
		public PingPacket() { }

		public PingPacket(Ping ping)
		{
			this.ping = ping;
		}

		public override void Send(BinaryWriter writer)
		{
			ping.NetSend(writer);
		}

		public override void Receive(BinaryReader reader, int sender)
		{
			Ping ping = Ping.FromNet(reader);
			bool? success = PingsSystem.AddOrRemove(ping);

			//PingsMod.Log($"Recv ping {success}", true, true);

			if (Main.netMode == NetmodeID.Server)
			{
				new PingPacket(ping).Send(from: sender);
			}
		}
	}
}
