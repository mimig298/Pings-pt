using Microsoft.Xna.Framework;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace Pings
{
	public class PingsNPC : GlobalNPC
	{
		public override void DrawEffects(NPC npc, ref Color drawColor)
		{
			PingsWorld.HighlightNPC(npc, ref drawColor);
		}

		public override bool CheckActive(NPC npc)
		{
			bool anyMatchingPing = PingsWorld.Pings.Any(p => p.PingType == PingType.NPC && p.WhoAmI == npc.whoAmI && p.Type == npc.type);
			return !anyMatchingPing; //If there is a matching ping, return false
		}
	}
}
