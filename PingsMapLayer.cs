using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace Pings
{
	public class PingsMapLayer : ModMapLayer
	{
		private static FieldInfo mapPositionField { get; set; }
		private static FieldInfo mapOffsetField { get; set; }
		private static FieldInfo mapScaleField { get; set; }

		public override void Load()
		{
			/*
			private readonly Vector2 _mapPosition;
			private readonly Vector2 _mapOffset;
			private readonly Rectangle? _clippingRect;
			private readonly float _mapScale;
			private readonly float _drawScale;
			 */
			Type type = typeof(MapOverlayDrawContext);
			mapPositionField = type.GetField("_mapPosition", BindingFlags.Instance | BindingFlags.NonPublic);
			mapOffsetField = type.GetField("_mapOffset", BindingFlags.Instance | BindingFlags.NonPublic);
			mapScaleField = type.GetField("_mapScale", BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Unload()
		{
			mapPositionField = null;
			mapOffsetField = null;
			mapScaleField = null;
		}

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			if (PingsSystem.Pings.Count == 0)
			{
				return;
			}

			Vector2 _mapPosition = (Vector2)mapPositionField.GetValue(context);
			Vector2 _mapOffset = (Vector2)mapOffsetField.GetValue(context);
			float _mapScale = (float)mapScaleField.GetValue(context);

			int size = ClientConfig.Instance.MouseoverIconSize;
			int maxDistSqr = (int)(size * size * _mapScale); //Consistent distance regardless of scale
			float closestTileDistSqr = maxDistSqr;
			Ping targetPing = null;

			Point mousePos = Main.MouseScreen.ToPoint();

			foreach (var ping in PingsSystem.Pings)
			{
				Vector2 position = ping.TileCenter;
				position = (position - _mapPosition) * _mapScale + _mapOffset;

				float distX = mousePos.X - position.X;
				float distY = mousePos.Y - position.Y;
				float distSqr = (distX * distX) + (distY * distY);

				if (distSqr < closestTileDistSqr)
				{
					closestTileDistSqr = distSqr;
					targetPing = ping;
				}
			}

			foreach (var ping in PingsSystem.Pings)
			{
				if (ping.IsVisible())
				{
					bool isTarget = ping == targetPing;

					DrawPingOnFullscreenMap(ping, isTarget, ref context, ref text);
				}
			}
		}

		private void DrawPingOnFullscreenMap(Ping ping, bool isTarget, ref MapOverlayDrawContext context, ref string mouseText)
		{
			float pulse = (float)Math.Sin(Main.GameUpdateCount % 120 / 120f * MathHelper.TwoPi) * 0.1f + 0.9f;

			float scale = (isTarget ? 1.5f : 1f) * pulse;

			//Unused because context.Draw doesn't use it
			var effects = SpriteEffects.None;

			Texture2D tex = Ping.DefaultTextures[ping.PingType].Value;

			if (ping.PingType == PingType.MultiTile)
			{
				if (Main.tileContainer[ping.Type])
				{
					if (Ping.SpecialTextures.TryGetValue("MultiTile_Container", out var texture))
					{
						tex = texture.Value;
					}
				}
			}
			else if (ping.PingType == PingType.NPC)
			{
				NPC npc = Main.npc[ping.WhoAmI];
				effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
				if (NPCID.Sets.TownCritter[npc.type] || NPCID.Sets.CountsAsCritter[npc.type] || (npc.friendly && npc.lifeMax < 5))
				{
					if (Ping.SpecialTextures.TryGetValue("NPC_Critter", out var texture))
					{
						tex = texture.Value;
					}
				}
				else if (npc.GetBossHeadTextureIndex() > -1 || npc.townNPC)
				{
					if (Ping.SpecialTextures.TryGetValue("Special_Circle", out var texture))
					{
						tex = texture.Value;
						scale *= 1.5f;
					}
				}
			}

			//Use same scale for both as we do custom mouseover determination
			context.Draw(tex, ping.TileCenter, (isTarget ? Color.White : Color.White * 0.78f) * ping.Alpha, new SpriteFrame(1, 1, 0, 0), scale, scale, Alignment.Center);

			//Bosses and TownNPCs already have text for them, we ignore it

			string timeText = string.Empty;

			//Commented because it causes stuff like "Katethe Nurse"
			//string text = PingsMod.SplitCapitalString(ping.Text);
			string text = ping.Text;

			if (ping.PingType == PingType.SelfPlayer)
			{
				string uk = PingsSystem.PlayerNameUnknownText.Value;
				text = PingsSystem.PlayerRendezvousText.Format(ping.Player?.name ?? uk);
			}

			if (isTarget && !string.IsNullOrEmpty(text))
			{
				mouseText = text;

				timeText = $" ({Lang.LocalizedDuration(new TimeSpan(0, 0, ping.DecayTimer / 60), abbreviated: true, showAllAvailableUnits: true)})";

				////Main.spriteBatch.DrawString(Main.fontItemStack, ratio, new Vector2(xPosition, yPosition), color, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
				//string s = ping.Text;
				//Vector2 stringLength = Main.fontMouseText.MeasureString(s);
				//Main.spriteBatch.DrawString(Main.fontItemStack, s, scrPos + new Vector2(-stringLength.X * 0.5f, 0), Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
			}

			if (timeText != string.Empty)
			{
				mouseText += timeText;
			}
		}
	}
}
