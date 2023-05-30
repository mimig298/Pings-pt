using Microsoft.Xna.Framework;
using Pings.Netcode.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Pings
{
	public class PingsSystem : ModSystem
	{
		//No loading/saving by design. Reason: Pinged NPCs

		public static List<Ping> Pings { internal set; get; }

		public static LocalizedText PlayerNameUnknownText { get; private set; }
		public static LocalizedText PlayerRendezvousText { get; private set; }
		public static LocalizedText PingCDText { get; private set; }
		public static LocalizedText RemovedPingText { get; private set; }
		public static LocalizedText PingAnythingText { get; private set; }
		public static LocalizedText PingPlayerText { get; private set; }

		public override void Load()
		{
			string category = $"PingStatus.";
			PlayerNameUnknownText ??= Language.GetOrRegister(Mod.GetLocalizationKey($"{category}PlayerNameUnknown"));
			PlayerRendezvousText ??= Language.GetOrRegister(Mod.GetLocalizationKey($"{category}PlayerRendezvous"));

			category = $"PingFeedback.";
			PingCDText ??= Language.GetOrRegister(Mod.GetLocalizationKey($"{category}PingCD"));
			RemovedPingText ??= Language.GetOrRegister(Mod.GetLocalizationKey($"{category}RemovedPing"));
			PingAnythingText ??= Language.GetOrRegister(Mod.GetLocalizationKey($"{category}PingAnything"));
			PingPlayerText ??= Language.GetOrRegister(Mod.GetLocalizationKey($"{category}PingPlayer"));
		}

		public override void ClearWorld()
		{
			Pings = new List<Ping>();
		}

		public override void OnWorldUnload()
		{
			Pings = null;
		}

		public override void Unload()
		{
			Pings = null;
		}

		public override void NetSend(BinaryWriter writer)
		{
			//PingsMod.Log("NetSend", true, true);
			int count = Pings.Count;
			writer.Write7BitEncodedInt(count);
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
			int count = reader.Read7BitEncodedInt();
			for (int i = 0; i < count; i++)
			{
				Ping ping = Ping.FromNet(reader);
				Pings.Add(ping);
			}
		}

		public override void PostUpdateEverything()
		{
			UpdatePingDespawn();
			UpdatePingDust();
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
						Main.NewText(RemovedPingText.ToString(), Color.DeepPink);
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
					Main.NewText(PingAnythingText.Format(player.name, actualPing.Text), Color.DarkSeaGreen);
				}
				else
				{
					Main.NewText(PingPlayerText.Format(player.name), Color.Green);
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
					if (Main.netMode != NetmodeID.SinglePlayer)
					{
						new PingPacket(ping).Send();
					}
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
