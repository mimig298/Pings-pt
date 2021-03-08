using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pings.Netcode;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Pings
{
	//Credit to hamstar for the map draw code
	//TODO light level check threshold (everything, configurable)
	public class PingsMod : Mod
	{
		public static ModHotKey PingHotKey { internal set; get; }

		public static PingsMod Mod { internal set; get; }

		public static HashSet<ushort> GemOres { internal set; get; }

		public static bool IsCluster(ushort type) => type <= TileLoader.TileCount && (TileID.Sets.Ore[type] || GemOres.Contains(type));

		public override void Load()
		{
			Mod = this;
			NetHandler.Load();
			Ping.Load();
			PingHotKey = RegisterHotKey("Ping Object at Cursor", "Mouse3");
			On.Terraria.IO.PlayerFileData.SetAsActive += PlayerFileData_SetAsActive;
		}

		public override void PostSetupContent()
		{
			GemOres = new HashSet<ushort>
			{
				TileID.Sapphire,
				TileID.Ruby,
				TileID.Emerald,
				TileID.Topaz,
				TileID.Amethyst,
				TileID.Diamond,
			};
		}

		private void PlayerFileData_SetAsActive(On.Terraria.IO.PlayerFileData.orig_SetAsActive orig, Terraria.IO.PlayerFileData self)
		{
			orig(self);

			PingsPlayer.CalculateUUIDForLocalPlayer();
		}

		public override void Unload()
		{
			Mod = null;
			NetHandler.Unload();
			Ping.Unload();
			PingsWorld.Pings = null;
			GemOres = null;
			PingHotKey = null;
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			NetHandler.HandlePackets(reader, whoAmI);
		}

		public override void PostUpdateEverything()
		{
			PingsWorld.UpdatePingDespawn();
			PingsWorld.UpdatePingDust();
		}

		public override void PostDrawFullscreenMap(ref string mouseText)
		{
			//TODO client config to not show pings on map?
			int maxDistSqr = 8 * 8;

			int mouseX = Main.mouseX - (Main.screenWidth / 2);
			mouseX = (int)((float)mouseX * Main.UIScale);
			mouseX = (Main.screenWidth / 2) + mouseX;
			int mouseY = Main.mouseY - (Main.screenHeight / 2);
			mouseY = (int)((float)mouseY * Main.UIScale);
			mouseY = (Main.screenHeight / 2) + mouseY;

			Point16 mouseTile;
			GetFullscreenMapTileOfScreenPosition(mouseX, mouseY, out mouseTile);

			float closestTileDistSqr = maxDistSqr;

			Ping targetPing = null;

			foreach (var ping in PingsWorld.Pings)
			{
				float distX = mouseTile.X - ping.TileCenter.X;
				float distY = mouseTile.Y - ping.TileCenter.Y;
				float distSqr = (distX * distX) + (distY * distY);

				if (distSqr < closestTileDistSqr)
				{
					closestTileDistSqr = distSqr;
					targetPing = ping;
				}
			}

			foreach (var ping in PingsWorld.Pings)
			{
				if (ping.IsVisible())
				{
					bool isTarget = ping == targetPing;

					DrawPingOnFullscreenMap(ping, isTarget, ref mouseText);
				}
			}
		}

		public void DrawPingOnFullscreenMap(Ping ping, bool isTarget, ref string mouseText)
		{
			float pulse = (float)Math.Sin((Main.GameUpdateCount % 120 / 120f) * MathHelper.TwoPi) * 0.1f + 0.9f;

			float myScale = isTarget ? 0.3f : 0.2f;
			float uiScale = 5f; //Main.mapFullscreenScale;
			float scale = uiScale * myScale * pulse;

			var wldPos = ping.TileCenter.ToWorldCoordinates(0, 0);

			var effects = SpriteEffects.None;

			Texture2D tex = Ping.DefaultTextures[ping.PingType];

			if (ping.PingType == PingType.MultiTile)
			{
				if (Main.tileContainer[ping.Type])
				{
					if (Ping.SpecialTextures.TryGetValue("MultiTile_Container", out Texture2D texture))
					{
						tex = texture;
					}
				}
			}
			else if (ping.PingType == PingType.NPC)
			{
				NPC npc = Main.npc[ping.WhoAmI];
				effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
				if (NPCID.Sets.TownCritter[npc.type] || (npc.friendly && npc.lifeMax < 5))
				{
					if (Ping.SpecialTextures.TryGetValue("NPC_Critter", out Texture2D texture))
					{
						tex = texture;
					}
				}
				else if (npc.GetBossHeadTextureIndex() > -1 || npc.townNPC)
				{
					if (Ping.SpecialTextures.TryGetValue("Special_Circle", out Texture2D texture))
					{
						tex = texture;
						scale *= 1.5f;
					}
				}
			}

			Tuple<Vector2, bool> overMapData = GetFullMapScreenPosition(wldPos);

			if (true/* || overMapData.Item2*/)
			{
				//Always draw, its quite janky with the on screen detection
				Vector2 scrPos = overMapData.Item1;

				Main.spriteBatch.Draw(
					texture: tex,
					position: scrPos,
					sourceRectangle: null,
					color: (isTarget ? Color.White : Color.White * 0.78f) * ping.Alpha,
					rotation: 0f,
					origin: tex.Size() / 2,
					scale: scale,
					effects: effects,
					layerDepth: 1f
				);

				//Bosses and TownNPCs already have text for them, we ignore it

				string timeText = string.Empty;

				string text = ping.Text;

				if (ping.PingType == PingType.SelfPlayer)
				{
					string uk = "Unknown";
					text = $"{ping.Player?.name ?? uk} Rendezvous";
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

		/// <summary>
		/// Returns a screen position of a given world position as if projected onto the fullscreen map.
		/// </summary>
		/// <param name="worldPosition"></param>
		/// <returns>A tuple indicating the screen-relative position and whether the point is within the screen
		/// boundaries.</returns>
		public Tuple<Vector2, bool> GetFullMapScreenPosition(Vector2 worldPosition)
		{
			//Main.mapFullscreen
			return GetFullMapScreenPosition(new Rectangle((int)worldPosition.X, (int)worldPosition.Y, 0, 0));
		}

		/// <summary>
		/// Gets the current screen size, factoring zoom.
		/// </summary>
		/// <returns></returns>
		public Tuple<int, int> GetScreenSize()
		{
			int screenWid = (int)((float)Main.screenWidth / Main.GameZoomTarget);
			int screenHei = (int)((float)Main.screenHeight / Main.GameZoomTarget);

			return Tuple.Create(screenWid, screenHei);
		}

		/// <summary>
		/// Returns a screen position of a given world position as if projected onto the fullscreen map.
		/// </summary>
		/// <param name="worldPosition"></param>
		/// <returns>A tuple indicating the screen-relative position and whether the point is within the screen
		/// boundaries.</returns>
		public Tuple<Vector2, bool> GetFullMapScreenPosition(Rectangle worldPosition)
		{
			//Main.mapFullscreen
			float mapScale = Main.mapFullscreenScale / Main.UIScale;
			var scrSize = GetScreenSize();

			//float offscrLitX = 10f * mapScale;
			//float offscrLitY = 10f * mapScale;

			float mapFullscrX = Main.mapFullscreenPos.X * mapScale;
			float mapFullscrY = Main.mapFullscreenPos.Y * mapScale;
			float mapX = -mapFullscrX + (float)(Main.screenWidth / 2);
			float mapY = -mapFullscrY + (float)(Main.screenHeight / 2);

			float originMidX = (worldPosition.X / 16f) * mapScale;
			float originMidY = (worldPosition.Y / 16f) * mapScale;

			originMidX += mapX;
			originMidY += mapY;

			var scrPos = new Vector2(originMidX, originMidY);
			bool isOnscreen = originMidX >= 0 &&
				originMidY >= 0 &&
				originMidX < scrSize.Item1 &&
				originMidY < scrSize.Item2;

			return Tuple.Create(scrPos, isOnscreen);
		}

		/// <summary>
		/// Returns a screen position of a given world position as if projected onto the overlay map.
		/// </summary>
		/// <param name="worldPosition"></param>
		/// <returns>A tuple indicating the screen-relative position and whether the point is within the screen
		/// boundaries.</returns>
		public Tuple<Vector2, bool> GetOverlayMapScreenPosition(Vector2 worldPosition)
		{
			//Main.mapStyle == 2
			return GetOverlayMapScreenPosition(new Rectangle((int)worldPosition.X, (int)worldPosition.Y, 0, 0));
		}

		/// <summary>
		/// Returns a screen position of a given world position as if projected onto the overlay map.
		/// </summary>
		/// <param name="worldPosition"></param>
		/// <returns>A tuple indicating the screen-relative position and whether the point is within the screen
		/// boundaries.</returns>
		public Tuple<Vector2, bool> GetOverlayMapScreenPosition(Rectangle worldPosition)
		{
			//Main.mapStyle == 2
			float mapScale = Main.mapOverlayScale;
			var scrSize = GetScreenSize();

			float offscrLitX = 10f * mapScale;
			float offscrLitY = 10f * mapScale;

			float scrWrldPosMidX = (Main.screenPosition.X + (float)(Main.screenWidth / 2)) / 16f;
			float scrWrldPosMidY = (Main.screenPosition.Y + (float)(Main.screenHeight / 2)) / 16f;
			scrWrldPosMidX *= mapScale;
			scrWrldPosMidY *= mapScale;
			float mapX = -scrWrldPosMidX + (float)(Main.screenWidth / 2);
			float mapY = -scrWrldPosMidY + (float)(Main.screenHeight / 2);

			float originMidX = (worldPosition.X / 16f) * mapScale;
			float originMidY = (worldPosition.Y / 16f) * mapScale;

			originMidX += mapX;
			originMidY += mapY;

			var scrPos = new Vector2(originMidX, originMidY);
			bool isOnscreen = originMidX >= 0 &&
				originMidY >= 0 &&
				originMidX < scrSize.Item1 &&
				originMidY < scrSize.Item2;

			return Tuple.Create(scrPos, isOnscreen);
		}

		/// <summary>
		/// Returns a screen position of a given world position as if projected onto the mini-map.
		/// </summary>
		/// <param name="worldPosition"></param>
		/// <returns>A tuple indicating the screen-relative position and whether the point is within the screen
		/// boundaries.</returns>
		public Tuple<Vector2, bool> GetMiniMapScreenPosition(Vector2 worldPosition)
		{
			//Main.mapStyle == 1
			return GetMiniMapScreenPosition(new Rectangle((int)worldPosition.X, (int)worldPosition.Y, 0, 0));
		}

		/// <summary>
		/// Returns a screen position of a given world position as if projected onto the mini-map.
		/// </summary>
		/// <param name="worldPosition"></param>
		/// <returns>A tuple indicating the screen-relative position and whether the point is within the screen
		/// boundaries.</returns>
		public Tuple<Vector2, bool> GetMiniMapScreenPosition(Rectangle worldPosition)
		{
			//Main.mapStyle == 1
			float mapScale = Main.mapMinimapScale;

			float wldScreenPosX = (Main.screenPosition.X + (float)(Main.screenWidth / 2)) / 16f;
			float wldScreenPosY = (Main.screenPosition.Y + (float)(Main.screenHeight / 2)) / 16f;
			float minimapWidScaled = (float)Main.miniMapWidth / mapScale;
			float minimapHeiScaled = (float)Main.miniMapHeight / mapScale;
			float minimapWorldX = (float)((int)wldScreenPosX) - minimapWidScaled * 0.5f;
			float minimapWorldY = (float)((int)wldScreenPosY) - minimapHeiScaled * 0.5f;
			float floatRemainderX = (wldScreenPosX - (float)((int)wldScreenPosX)) * mapScale;
			float floatRemainderY = (wldScreenPosY - (float)((int)wldScreenPosY)) * mapScale;

			float originX = worldPosition.X + (float)(worldPosition.Width * 0.5f);
			float originY = worldPosition.Y + (float)(worldPosition.Height * 0.5f);
			float originXRelativeToMap = ((originX / 16f) - minimapWorldX) * mapScale;
			float originYRelativeToMap = ((originY / 16f) - minimapWorldY) * mapScale;
			float originXScreenPos = originXRelativeToMap + (float)Main.miniMapX;
			float originYScreenPos = originYRelativeToMap + (float)Main.miniMapY;
			originYScreenPos -= 2f * mapScale / 5f;

			var scrPos = new Vector2(originXScreenPos - floatRemainderX, originYScreenPos - floatRemainderY);
			bool isOnscreen = originXScreenPos > (float)(Main.miniMapX + 12) &&
					originXScreenPos < (float)(Main.miniMapX + Main.miniMapWidth - 16) &&
					originYScreenPos > (float)(Main.miniMapY + 10) &&
					originYScreenPos < (float)(Main.miniMapY + Main.miniMapHeight - 14);

			return Tuple.Create(scrPos, isOnscreen);
		}

		/// <summary>
		/// Gets the scaled dimensions of a given width and height as if projectected onto the full screen map.
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <returns></returns>
		public Vector2 GetSizeOnFullMap(float width, float height)
		{
			//Main.mapFullscreen 
			float baseX = Main.screenPosition.X;
			float baseY = Main.screenPosition.Y;

			Vector2 mapBasePos = GetFullMapScreenPosition(new Rectangle((int)baseX, (int)baseY, 0, 0)).Item1;
			Vector2 mapNewPos = GetFullMapScreenPosition(new Rectangle((int)(baseX + width), (int)(baseY + height), 0, 0)).Item1;

			return mapNewPos - mapBasePos;
		}

		/// <summary>
		/// Gets the scaled dimensions of a given width and height as if projectected onto the overlay map.
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <returns></returns>
		public Vector2 GetSizeOnOverlayMap(float width, float height)
		{
			//Main.mapStyle == 2
			float baseX = Main.screenPosition.X;
			float baseY = Main.screenPosition.Y;

			Vector2 mapBasePos = GetOverlayMapScreenPosition(new Rectangle((int)baseX, (int)baseY, 0, 0)).Item1;
			Vector2 mapNewPos = GetOverlayMapScreenPosition(new Rectangle((int)(baseX + width), (int)(baseY + height), 0, 0)).Item1;

			return mapNewPos - mapBasePos;
		}

		/// <summary>
		/// Gets the scaled dimensions of a given width and height as if projectected onto the mini-map.
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <returns></returns>
		public Vector2 GetSizeOnMinimap(float width, float height)
		{
			//Main.mapStyle == 1
			float baseX = Main.screenPosition.X;
			float baseY = Main.screenPosition.Y;

			Vector2 mapBasePos = GetMiniMapScreenPosition(new Rectangle((int)baseX, (int)baseY, 0, 0)).Item1;
			Vector2 mapNewPos = GetMiniMapScreenPosition(new Rectangle((int)(baseX + width), (int)(baseY + height), 0, 0)).Item1;

			return mapNewPos - mapBasePos;
		}

		/// <summary>
		/// Outputs the world tile position of a given screen position in the fullscreen map.
		/// </summary>
		/// <param name="scrPosX"></param>
		/// <param name="scrPosY"></param>
		/// <param name="tilePos"></param>
		/// <returns>`true` if the tile is within world bounds.</returns>
		public bool GetFullscreenMapTileOfScreenPosition(int scrPosX, int scrPosY, out Point16 tilePos)
		{
			float mapScale = Main.mapFullscreenScale;
			float minX = 10f;
			float minY = 10f;
			float maxX = (float)(Main.maxTilesX - 10);
			float maxY = (float)(Main.maxTilesY - 10);

			float mapPosX = Main.mapFullscreenPos.X * mapScale;
			float mapPosY = Main.mapFullscreenPos.Y * mapScale;

			float scrOriginX = (float)(Main.screenWidth / 2) - mapPosX;
			scrOriginX += minX * mapScale;
			float scrOriginY = (float)(Main.screenHeight / 2) - mapPosY;
			scrOriginY += minX * mapScale;

			int tileX = (int)(((float)scrPosX - scrOriginX) / mapScale + minX);
			int tileY = (int)(((float)scrPosY - scrOriginY) / mapScale + minY);

			tilePos = new Point16(tileX, tileY);
			return tileX >= minX
				&& tileX < maxX
				&& tileY >= minY
				&& tileY < maxY;
		}

		/// <summary>
		/// Logs a message in the respective log (client or server). Optionally, displays it in the respective output (chat or console)
		/// </summary>
		/// <param name="message">The message</param>
		/// <param name="chat">If true, displays the message in the respective output</param>
		public static void Log(object message, bool chat = false, bool localTime = false)
		{
			string msg = message.ToString();

			if (localTime)
			{
				string time = (DateTime.Now.Ticks).ToString();
				msg = $"{time,16} {msg}";
			}

			PingsMod.Mod.Logger.Info(msg);

			if (!chat) return;

			if (Main.netMode == NetmodeID.Server)
			{
				Console.WriteLine(msg);
			}
			else
			{
				Main.NewText(msg);
			}
		}
	}
}
