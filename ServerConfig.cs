using System.ComponentModel;
using System.Runtime.Serialization;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Pings
{
	public class ServerConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static ServerConfig Instance => ModContent.GetInstance<ServerConfig>();

		public const int PingsPerPlayerMin = 1;
		public const int PingsPerPlayerMax = 10;

		[Tooltip("Exceeding this limit will delete the oldest ping of that player")]
		[Label("Maximum amount of pings per player")]
		[Slider]
		[SliderColor(50, 50, 255)]
		[Range(PingsPerPlayerMin, PingsPerPlayerMax)]
		[DefaultValue(3)]
		public int PingsPerPlayer;

		public const int PingDurationMin = 10; //Seconds!
		public const int PingDurationMax = 30 * 60; //Half an hour = Infinite in code

		[Tooltip("In seconds. If max, infinite duration")]
		[Label("The lifetime/duration of a ping")]
		[Slider]
		[SliderColor(50, 50, 255)]
		[Range(PingDurationMin, PingDurationMax)]
		[DefaultValue(5 * 60)] //5 minutes
		public int PingDuration;

		public const int PingCooldownMin = 1; //Seconds!
		public const int PingCooldownMax = 60; //A minute

		[Tooltip("In seconds")]
		[Label("Cooldown between pings")]
		[Slider]
		[SliderColor(50, 50, 255)]
		[Range(PingCooldownMin, PingCooldownMax)]
		[DefaultValue(1)] //1 second
		public int PingCooldown;

		public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref string message)
		{
			message = "Only the host of this world can change the config! Do so in singleplayer.";
			return false;
		}

		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context)
		{
			PingsPerPlayer = Utils.Clamp(PingsPerPlayer, PingsPerPlayerMin, PingsPerPlayerMax);

			PingDuration = Utils.Clamp(PingDuration, PingDurationMin, PingDurationMax);

			PingCooldown = Utils.Clamp(PingCooldown, PingCooldownMin, PingCooldownMax);
		}
	}
}
