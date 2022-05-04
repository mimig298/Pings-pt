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
			PingsSystem.HighlightNPC(npc, ref drawColor);
		}

		public override bool CheckActive(NPC npc)
		{
			var dims = new Rectangle(16 * 40, 16 * 40, 16 * (Main.maxTilesX - 40), 16 * (Main.maxTilesY - 40));

			//"Inflate" the hitbox in all directions by its dimensions for safety,
			//Then check if its outside the "world" dimensions (excluding 40 tiles outside)
			if (npc.Left.X - npc.width    < dims.X ||
				npc.Right.X + npc.width   > dims.Width ||
				npc.Top.Y - npc.height    < dims.Y ||
				npc.Bottom.Y + npc.height > dims.Height)
			{
				return base.CheckActive(npc);
			}

			bool anyMatchingPing = PingsSystem.Pings.Any(p => p.PingType == PingType.NPC && p.WhoAmI == npc.whoAmI && p.Type == npc.type);
			return !anyMatchingPing; //If there is a matching ping, return false
		}
	}
}
