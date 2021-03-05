using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace Pings
{
	public class PingsTile : GlobalTile
	{
		public override void DrawEffects(int i, int j, int type, SpriteBatch spriteBatch, ref Color drawColor, ref int nextSpecialDrawIndex)
		{
			PingsWorld.HighlightTile(i, j, ref drawColor);
		}
	}
}
