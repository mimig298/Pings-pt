using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Pings
{
	public class ClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static ClientConfig Instance => ModContent.GetInstance<ClientConfig>();

		[DefaultValue(true)]
		public bool CircularParticles;

		[DefaultValue(true)]
		public bool Highlight;
	}
}
