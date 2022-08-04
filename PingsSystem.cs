using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pings.Netcode.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Pings
{
	public class PingsSystem : ModSystem
	{
		//No loading/saving by design. Reason: Pinged NPCs

		public static List<Ping> Pings { internal set; get; }

		private void Initialize()
		{
			Pings = new List<Ping>();
		}

		public override void PreWorldGen()
		{
			Initialize();
		}

		public override void OnWorldLoad()
		{
			Initialize();
		}

		public override void OnWorldUnload()
		{
			Pings = null;
		}

		public override void NetSend(BinaryWriter writer)
		{
			//PingsMod.Log("NetSend", true, true);
			int count = Pings.Count;
			writer.Write(count);
			for (int i = 0; i < count; i++)
			{
				Ping ping = Pings[i];
				ping.NetSend(writer);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			Pings = new List<Ping>();

			//PingsMod.Log("NetReceive", true, true);
			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++)
			{
				Ping ping = Ping.FromNet(reader);
				Pings.Add(ping);
			}
		}



		public override void PostUpdateEverything()
		{
			PingsSystem.UpdatePingDespawn();
			PingsSystem.UpdatePingDust();
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

			foreach (var ping in PingsSystem.Pings)
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

			foreach (var ping in PingsSystem.Pings)
			{
				if (ping.IsVisible())
				{
					bool isTarget = ping == targetPing;

					DrawPingOnFullscreenMap(ping, isTarget, ref mouseText);
				}
			}
		}

		//TODO rework this to ModMapLayer
		public void DrawPingOnFullscreenMap(Ping ping, bool isTarget, ref string mouseText)
		{
			float pulse = (float)Math.Sin((Main.GameUpdateCount % 120 / 120f) * MathHelper.TwoPi) * 0.1f + 0.9f;

			float myScale = isTarget ? 0.3f : 0.2f;
			float uiScale = 5f; //Main.mapFullscreenScale;
			float scale = uiScale * myScale * pulse;

			var wldPos = ping.TileCenter.ToWorldCoordinates(0, 0);

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
				if (NPCID.Sets.TownCritter[npc.type] || (npc.friendly && npc.lifeMax < 5))
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

				string text = PingsMod.SplitCapitalString(ping.Text);

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
		/// Adds or removes the ping
		/// </summary>
		/// <param name="ping">The ping</param>
		/// <returns>Returns false if ping was not was removed, true if added, null if invalid</returns>
		public static bool? AddOrRemove(Ping ping)
		{
			Player player = ping.Player;
			if (player == null)
			{
				return null;
			}

			Ping actualPing = ping;

			Ping same = Pings.FirstOrDefault(p => p.Equals(ping));
			if (same != null)
			{
				//Existing ping:

				//Remove it if the player owns it
				if (same.PlayerUUID == ping.PlayerUUID)
				{
					Pings.Remove(same);

					if (player.whoAmI == Main.myPlayer)
					{
						Main.NewText("Removed ping.", Color.DeepPink);
					}

					return false;
				}
				else
				{
					//Do not do anything if another player owns the ping
					return null;
				}
			}
			//New ping:
			//If current player limit full, remove oldest
			//Then, Add it

			var playerPings = Pings.FindAll(p => p.PlayerUUID == ping.PlayerUUID);

			if (playerPings.Count >= ServerConfig.Instance.PingsPerPlayer)
			{
				Pings.Remove(playerPings[0]);
			}

			Pings.Add(ping);

			if (ping.Notify && Main.netMode != NetmodeID.Server)
			{
				if (ping.PingType != PingType.SelfPlayer)
				{
					Main.NewText($"{player.name} found '{actualPing.Text}'!", Color.DarkSeaGreen);
				}
				else
				{
					Main.NewText($"{player.name} marked a rendezvous location!", Color.Green);
				}
			}

			return true;
		}

		internal static void UpdatePingDespawn()
		{
			List<Ping> toRemove = new List<Ping>();
			List<Ping> toRemoveAndSync = new List<Ping>();
			foreach (var ping in Pings)
			{
				bool decayed = ping.DoDecay();

				if (decayed)
				{
					toRemove.Add(ping);
					continue;
				}

				bool remove = false;
				bool removeAndSync = false;
				switch (ping.PingType)
				{
					//Tiles might be problematic to check in MP like that because they can be null or not active
					//Here check that they can only be despawned if not null, and not active (Framing.GetTileSafely always returns not null so its not applicable)
					case PingType.SingleTile:
					case PingType.MultiTile:
						Tile tile = Main.tile[ping.TileLocation.X, ping.TileLocation.Y];
						remove = tile != null && !tile.HasTile;
						break;

					case PingType.ClusterTile:
						if (Main.netMode != NetmodeID.MultiplayerClient)
						{
							//Only server knows all tile locations so it can decide about its state properly
							Point16 topLeft = ping.TileLocation;
							Point16 size = ping.TileSize;
							int totalCount = ping.TileCount;
							int threshold = (int)(totalCount * 0.5f);
							int leftCount = 0;

							bool b = false;
							for (int i = topLeft.X; i < topLeft.X + size.X; i++)
							{
								if (b) break;

								for (int j = topLeft.Y; j < topLeft.Y + size.Y; j++)
								{
									if (b) break;

									if (!WorldGen.InWorld(i, j)) continue;

									Tile checkTile = Framing.GetTileSafely(i, j);
									if (checkTile.HasTile && checkTile.TileType == ping.Type)
									{
										leftCount++;
										if (leftCount >= threshold)
										{
											//No point checking further
											b = true;
										}
									}
								}
							}

							if (b) break;

							//If not breaked, remove
							removeAndSync = true;
						}
						break;

					case PingType.SelfPlayer:
						remove = !Main.player[ping.WhoAmI].active;
						break;

					case PingType.NPC:
						remove = !Main.npc[ping.WhoAmI].active || Main.npc[ping.WhoAmI].type != ping.Type;
						break;

					case PingType.Item:
						remove = !Main.item[ping.WhoAmI].active || Main.item[ping.WhoAmI].type != ping.Type;
						break;

					default:
						break;
				}

				if (remove)
				{
					toRemove.Add(ping);
				}

				if (removeAndSync)
				{
					toRemoveAndSync.Add(ping);
				}
			}

			foreach (var ping in toRemove)
			{
				Pings.Remove(ping);
			}

			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				foreach (var ping in toRemoveAndSync)
				{
					new PingPacket(ping).Send();
					Pings.Remove(ping);
				}
			}
		}

		internal static void UpdatePingDust()
		{
			if (Main.netMode == NetmodeID.Server)
			{
				return;
			}

			if (!ClientConfig.Instance.CircularParticles)
			{
				return;
			}

			Vector2 add = new Vector2(300);
			Rectangle screenRect = new Rectangle(
				(int)(Main.screenPosition.X - add.X / 2),
				(int)(Main.screenPosition.Y - add.Y / 2),
				(int)(Main.screenWidth + add.X),
				(int)(Main.screenHeight + add.Y));

			foreach (var ping in Pings)
			{
				if (!ping.IsVisible())
				{
					continue;
				}

				int dustType = 204;
				float distanceFromCenter = ping.DecayTimer <= Ping.AlphaTimerStart ? (ping.DecayTimer / (float)Ping.AlphaTimerStart) : 1f;
				if (Main.netMode == NetmodeID.MultiplayerClient && ping.Player?.whoAmI != Main.myPlayer)
				{
					dustType = DustID.Torch;
				}

				if (ping.IsTile)
				{
					Vector2 worldPos = ping.TileLocation.ToWorldCoordinates(0, 0);
					Vector2 size = ping.TileSize.ToVector2() * 16;
					if (screenRect.Intersects(new Rectangle((int)worldPos.X, (int)worldPos.Y, (int)size.X, (int)size.Y)))
					{
						SpawnRotatingDust(worldPos, distanceFromCenter, size, dustType);
					}
				}
				else if (ping.PingType == PingType.NPC)
				{
					NPC npc = Main.npc[ping.WhoAmI];
					if (screenRect.Intersects(npc.Hitbox))
					{
						SpawnRotatingDust(npc.position + new Vector2(0f, npc.gfxOffY), distanceFromCenter, npc.Size, dustType);
					}
				}
				else if (ping.PingType == PingType.Item)
				{
					Item item = Main.item[ping.WhoAmI];
					if (screenRect.Intersects(item.Hitbox))
					{
						SpawnRotatingDust(item.position, distanceFromCenter, item.Size, dustType);
					}
				}
				else if (ping.PingType == PingType.SelfPlayer)
				{
					Vector2 worldPos = ping.TileLocation.ToWorldCoordinates(8, 8);
					Vector2 size = new Vector2(32);
					worldPos -= size / 2;
					if (screenRect.Contains(worldPos.ToPoint()))
					{
						SpawnRotatingDust(worldPos, distanceFromCenter, size, dustType);
					}
				}
			}
		}

		internal static void HighlightTile(int i, int j, ref TileDrawInfo drawData)
		{
			if (!ClientConfig.Instance.Highlight)
			{
				return;
			}

			Point point = new Point(i, j);
			foreach (var ping in Pings)
			{
				if (ping.IsVisible() && ping.IsTile)
				{
					Tile tile = Framing.GetTileSafely(point);
					Rectangle rect = ping.TileDimensions;
					if (tile.HasTile && tile.TileType == ping.Type && rect.Contains(point))
					{
						var color = drawData.tileLight;
						ModifyColor(ping, ref color);
						drawData.tileLight = color;
						break;
					}
				}
			}
		}

		internal static void HighlightNPC(NPC npc, ref Color drawColor)
		{
			if (!ClientConfig.Instance.Highlight)
			{
				return;
			}

			foreach (var ping in Pings)
			{
				if (ping.IsVisible() && ping.HasWhoAmI && ping.PingType == PingType.NPC && npc.whoAmI == ping.WhoAmI && npc.type == ping.Type)
				{
					ModifyColor(ping, ref drawColor);
					break;
				}
			}
		}

		internal static void SpawnRotatingDust(Vector2 pos, float distanceFromCenter, Vector2? size = null, int type = 204)
		{
			Dust dust;

			if (size.HasValue && size.Value is Vector2 value)
			{
				float amount = 16f;

				int i = (int)(amount * (Main.GameUpdateCount % 60 / 60f));

				int loops = 1 + (int)(value.LengthSquared() / (40 * 40));

				loops = Math.Min(8, loops);

				amount *= loops;

				float clockOffset = MathHelper.TwoPi / loops;

				Vector2 center = pos + value * 0.5f;
				for (int j = 0; j < loops; j++)
				{
					Vector2 vector = -Vector2.UnitY.RotatedBy(j * clockOffset + i * (MathHelper.TwoPi / amount)) * value * 0.5f;

					dust = Dust.NewDustDirect(center, 0, 0, type);
					dust.noGravity = true;
					dust.noLight = true;
					dust.position = center + vector * distanceFromCenter;
					dust.velocity *= 0f;
				}
			}
			else
			{
				dust = Dust.NewDustPerfect(pos, type, Alpha: 150);
				dust.noGravity = true;
				dust.noLight = true;
				dust.velocity *= 0.1f;
			}
		}

		public static void SpawnDustGoingOutwards(Vector2 center, float distanceFromCenter, int type, int amount, float speed)
		{
			for (int i = 0; i < amount; i++)
			{
				Vector2 vector = -Vector2.UnitY.RotatedBy(i * (MathHelper.TwoPi / amount)) * distanceFromCenter;

				Dust dust = Dust.NewDustDirect(center, 0, 0, type);
				dust.noGravity = true;
				dust.noLight = true;
				dust.position = center + vector;
				dust.velocity = vector.SafeNormalize(Vector2.UnitY) * speed;
			}
		}

		private static void ModifyColor(Ping ping, ref Color drawColor)
		{
			//(-0.2 to 0.2) + 0.8 = -0.6 to 1.0
			float pulse = (float)Math.Sin((Main.GameUpdateCount % 60 / 60f) * MathHelper.TwoPi) * 0.2f + 0.8f;

			byte r = (byte)(200 * pulse);
			byte g = (byte)(170 * pulse);
			if (drawColor.R < r)
				drawColor.R = r;

			if (drawColor.G < g)
				drawColor.G = g;

			drawColor.A = Main.mouseTextColor;
		}
	}
}
