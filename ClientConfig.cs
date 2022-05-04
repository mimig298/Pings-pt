using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Pings
{
	[Label("Client Config")]
	public class ClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static ClientConfig Instance => ModContent.GetInstance<ClientConfig>();
		[Tooltip("If particles should circulate around the pinged object")]
		[Label("Circular Particles on Ping Object")]
		[DefaultValue(true)]
		public bool CircularParticles;

		[Tooltip("If the object should appear highlighted (similar to the spelunker effect on ores)")]
		[Label("Highlight Pinged Object")]
		[DefaultValue(true)]
		public bool Highlight;
	}
}
