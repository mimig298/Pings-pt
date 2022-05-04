using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Pings
{
	public class PingsTile : GlobalTile
	{
		public override void DrawEffects(int i, int j, int type, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
		{
			PingsSystem.HighlightTile(i, j, ref drawData);
		}
	}
}
