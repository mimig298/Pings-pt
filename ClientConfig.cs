using System.ComponentModel;
using System.Runtime.Serialization;
using Terraria;
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

		public const int MouseoverIconSize_Min = 4;
		public const int MouseoverIconSize_Max = 80;
		[DefaultValue(30)]
		[Slider]
		[Range(MouseoverIconSize_Min, MouseoverIconSize_Max)]
		public int MouseoverIconSize;

		[OnDeserialized]
		public void OnDeserializedMethod(StreamingContext context)
		{
			MouseoverIconSize = Utils.Clamp(MouseoverIconSize, MouseoverIconSize_Min, MouseoverIconSize_Max);
		}
	}
}
