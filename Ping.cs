using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Pings
{
	public sealed class Ping //: IComparable<Ping>
	{
		public static Dictionary<PingType, Texture2D> DefaultTextures { internal set; get; }

		public static Dictionary<string, Texture2D> SpecialTextures { internal set; get; }

		public PingType PingType { internal set; get; }

		public string PlayerUUID { internal set; get; } //not important to compare for equality

		private byte playerWhoAmI = Main.maxPlayers;

		public Player Player
		{
			//This will return null for newly joined clients when they try to access this on initially received pings.
			//Pings will get requested again on join so it might take a second or two for this to return a proper value, but after that it's settled
			get
			{
				if (playerWhoAmI == Main.maxPlayers)
				{
					Player player = PingsPlayer.GetPlayerFromUUID(PlayerUUID);
					if (player == null)
					{
						//PingsMod.Log("Get Player (" + PlayerUUID + "): null", true, true);
						return null;
					}
					//PingsMod.Log("Get Player (" + PlayerUUID + "): success", true, true);
					playerWhoAmI = (byte)player.whoAmI;
				}
				return Main.player[playerWhoAmI];
			}
		}

		public int DecayTimer { private set; get; } //not important to compare for equality

		public int DecayDuration { internal set; get; } //Set to what the current config is  //not important to compare for equality

		public const int AlphaTimerStart = 90;

		public const int PlayerDespawnStart = 60 * 60; //One minute

		public float Alpha
		{
			get
			{
				if (DecayTimer > AlphaTimerStart)
				{
					return 1f;
				}

				//(-0.3 to 0.3) + 0.7 = -0.4 to 1.0
				float pulse = (float)Math.Cos((DecayTimer % 30 / 30f) * MathHelper.TwoPi) * 0.3f + 0.7f;
				return pulse;
			}
		}

		public string Text { internal set; get; }

		public bool Notify { private set; get; }

		public bool IsTile => PingType == PingType.SingleTile || PingType == PingType.ClusterTile || PingType == PingType.MultiTile;

		public bool HasLocation => IsTile || PingType == PingType.SelfPlayer;

		public bool HasSize => PingType == PingType.ClusterTile || PingType == PingType.MultiTile;

		public bool HasTileCount => PingType == PingType.ClusterTile;

		public bool HasWhoAmI => PingType == PingType.SelfPlayer || PingType == PingType.NPC || PingType == PingType.Item;

		public bool HasType => (PingType != PingType.SelfPlayer && HasLocation) || HasWhoAmI;

		public bool HasText => PingType != PingType.SelfPlayer;

		public Vector2 TileCenter
		{
			get
			{
				if (HasLocation)
				{
					return TileDimensions.Center.ToVector2();
				}
				else if (HasWhoAmI)
				{
					if (PingType == PingType.Item)
					{
						return Main.item[WhoAmI].Center / 16f;
					}
					else if (PingType == PingType.NPC)
					{
						NPC npc = Main.npc[WhoAmI];
						return (npc.Center + new Vector2(0f, npc.gfxOffY)) / 16f;
					}
				}
				return Vector2.Zero;
			}
		}

		public int Type { private set; get; }

		#region Tiles and Player
		public Point16 TileLocation { private set; get; }
		public Point16 TileSize { private set; get; } //Defaults to 1,1 which is single tile
		public const short TileCountMax = short.MaxValue;
		public short TileCount { private set; get; }
		public Rectangle TileDimensions => new Rectangle(TileLocation.X, TileLocation.Y, TileSize.X, TileSize.Y);

		#endregion

		#region NPC and Item
		public short WhoAmI { private set; get; }
		#endregion

		public Ping(Player player, PingType pingType, bool notify)
		{
			PlayerUUID = PingsPlayer.GetUUIDFromPlayer(player);
			PingType = pingType;

			DecayTimer = DecayDuration = 60 * ServerConfig.Instance.PingDuration; //TODO config

			Notify = notify;

			TileSize = new Point16(1, 1);

			Text = string.Empty;
		}

		public Ping(string uuid, PingType pingType, bool notify) : this(PingsPlayer.GetPlayerFromUUID(uuid), pingType, notify)
		{
			//This gets called on netreceive
			//Joining client actually receives pings before he receives info from other players about their UUIDs
			//Bandaid fix is simply requesting world info again once the player actually fully gets ingame
		}

		public static Ping MakePing(Player player, Vector2 worldPos, bool notify)
		{
			//TODO support clicking from map to add/remove pings (maybe only remove as pinging a tile won't work in multi due to chunks etc)
			if (Main.netMode == NetmodeID.Server || Main.myPlayer != player.whoAmI)
			{
				return null;
			}

			Point worldPosPoint = worldPos.ToPoint();

			//Check small, then big entities first, then player, then tiles
			int foundWhoAmI = 0;
			int foundType;
			string text = null;

			int value = 0;

			Item foundItem = null;
			for (int i = 0; i < Main.maxItems; i++)
			{
				Item item = Main.item[i];
				Rectangle hitbox = item.Hitbox;
				hitbox.Inflate(6, 6);
				if (item.active && hitbox.Contains(worldPosPoint))
				{
					int strength = 1 + item.value * item.stack;
					if (strength > value)
					{
						value = strength;
						foundItem = item;
						foundWhoAmI = i;
					}
				}
			}

			if (foundItem != null && !foundItem.instanced) //Prevent boss bags etc. from being marked since they are clientside
			{
				foundType = foundItem.type;
				text = foundItem.Name;

				Ping ping = new Ping(player, PingType.Item, notify)
				{
					Type = foundType,
					WhoAmI = (short)foundWhoAmI,
					Text = text
				};

				return ping;
			}

			value = 0;
			NPC foundNPC = null;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				Rectangle hitbox = npc.Hitbox;
				hitbox.Inflate(6, 6);
				if (npc.active && npc.type != NPCID.TargetDummy && hitbox.Contains(worldPos.ToPoint()))
				{
					int strength = npc.damage * 10 + npc.lifeMax;
					if (strength > value)
					{
						value = strength;
						foundNPC = npc;
						foundWhoAmI = i;
					}
				}
			}

			if (foundNPC != null)
			{
				foundType = foundNPC.type;
				//FullName includes their NPC name + individual name
				text = foundNPC.townNPC ? foundNPC.FullName : foundNPC.GivenOrTypeName;

				Ping ping = new Ping(player, PingType.NPC, notify)
				{
					Type = foundType,
					WhoAmI = (short)foundWhoAmI,
					Text = text
				};

				return ping;
			}

			//Only use the player who made the ping (local player)
			Rectangle playerHitbox = player.Hitbox;
			playerHitbox.Inflate(2, 2);
			if (playerHitbox.Contains(worldPos.ToPoint()))
			{
				Ping ping = new Ping(player, PingType.SelfPlayer, notify)
				{
					WhoAmI = (short)player.whoAmI,
					TileLocation = player.getRect().Center.ToVector2().ToTileCoordinates16()
				};

				return ping;
			}

			Point point = worldPos.ToTileCoordinates();
			Tile tile = Framing.GetTileSafely(point);

			if (tile.active())
			{
				if (tile.type < TileID.Count)
				{
					text = GetTileName(text, point, tile);
					text = PingsMod.SplitCapitalString(text);
				}
				if (string.IsNullOrEmpty(text))
				{
					if (tile.type < TileID.Count)
					{
						text = TileID.Search.GetName(tile.type);
						text = PingsMod.SplitCapitalString(text);
					}
					else
					{
						text = TileLoader.GetTile(tile.type).Name;
					}
				}

				Rectangle rect;
				short tileCount = 0;
				bool isCluster = PingsMod.IsCluster(tile.type);
				if (isCluster)
				{
					rect = GetCluster(point, tile, out tileCount);
				}
				else
				{
					rect = GetMultiTileDimensions(point, tile);
				}

				var tileLocation = rect.TopLeft().ToPoint16();
				var tileSize = rect.Size().ToPoint16();

				var pingType = PingType.SingleTile;

				if (isCluster)
				{
					pingType = PingType.ClusterTile;
				}
				else if (tileSize != new Point16(1, 1))
				{
					pingType = PingType.MultiTile;
				}

				Ping ping = new Ping(player, pingType, notify)
				{
					Type = tile.type,
					TileLocation = tileLocation,
					TileSize = tileSize,
					TileCount = tileCount,
					Text = text
				};

				return ping;
			}

			return null;
		}

		private static Rectangle GetCluster(Point point, Tile tile, out short count)
		{
			//Start by going clockwise at 12 am and add tiles that match the same type to a queue and are not already in the queue
			//After that, peek the last element of the queue and perform the same

			ushort type = tile.type;
			Queue<Point> queue = new Queue<Point>();
			Queue<Point> filled = new Queue<Point>();
			queue.Enqueue(point);

			int[] xx = new int[] { 0, 1, 0, -1 };
			int[] yy = new int[] { -1, 0, 1, 0 };

			while (queue.Count > 0 && filled.Count < TileCountMax)
			{
				Point currentPoint = queue.Dequeue();
				if (filled.Contains(currentPoint))
				{
					continue;
				}
				else
				{
					filled.Enqueue(currentPoint);
				}

				for (int i = 0; i < 4; i++)
				{
					int offX = xx[i];
					int offY = yy[i];

					Point checkPoint = new Point(currentPoint.X + offX, currentPoint.Y + offY);

					int x = checkPoint.X;
					int y = checkPoint.Y;

					if (WorldGen.InWorld(x, y) && !queue.Contains(checkPoint) && !filled.Contains(checkPoint) && Framing.GetTileSafely(checkPoint) is Tile checkTile && checkTile.active() && checkTile.type == type)
					{
						queue.Enqueue(checkPoint);
					}
				}
			}

			queue.Clear();

			int left = filled.Min(p => p.X);
			int top = filled.Min(p => p.Y);
			int right = filled.Max(p => p.X);
			int bottom = filled.Max(p => p.Y);

			int width = 1 + right - left;
			int height = 1 + bottom - top;

			count = (short)filled.Count;
			return new Rectangle(left, top, width, height);
		}

		private static Rectangle GetMultiTileDimensions(Point point, Tile tile)
		{
			var data = TileObjectData.GetTileData(tile.type, 0);
			if (data != null)
			{
				//TODO doesnt work with tiles that have wrapped styles (banners)
				int width = data.Width;
				int height = data.Height;

				int tilesToLeft = tile.frameX / (data.CoordinateWidth + data.CoordinatePadding) % width;

				int frameY = tile.frameY;
				int[] accumulatedHeight = new int[height];

				for (int i = 1; i < height; i++)
				{
					accumulatedHeight[i] = accumulatedHeight[i - 1] + data.CoordinateHeights[i] + data.CoordinatePadding;
				}

				int accumulatedHeightIndex = 0;

				for (int i = 1; i < height; i++)
				{
					if (frameY > accumulatedHeight[i - 1] && frameY <= accumulatedHeight[i])
					{
						accumulatedHeightIndex = i;
						break;
					}
				}

				return new Rectangle(point.X - tilesToLeft, point.Y - accumulatedHeightIndex, width, height);
			}
			return new Rectangle(point.X, point.Y, 1, 1);
		}

		private static string GetTileName(string text, Point point, Tile tile)
		{
			int mapX = point.X;
			int mapY = point.Y;
			int type = Main.Map[mapX, mapY].Type;
			int containersLU = MapHelper.tileLookup[TileID.Containers];
			int fakeContainersLU = MapHelper.tileLookup[TileID.FakeContainers];
			int containersCounts = MapHelper.tileOptionCounts[TileID.Containers];
			int containers2LU = MapHelper.tileLookup[TileID.Containers2];
			int fakeContainers2LU = MapHelper.tileLookup[TileID.FakeContainers2];
			int containers2Counts = MapHelper.tileOptionCounts[TileID.Containers2];
			int dressersLU = MapHelper.tileLookup[TileID.Dressers];
			int dressersCounts = MapHelper.tileOptionCounts[TileID.Dressers];
			LocalizedText[] chestType = Lang.chestType;
			LocalizedText[] chestType2 = Lang.chestType2;
			if (type >= containersLU && type < containersLU + containersCounts)
			{
				int num101 = mapX;
				int num102 = mapY;
				if (tile.frameX % 36 != 0)
					num101--;

				if (tile.frameY % 36 != 0)
					num102--;

				text = DrawMap_FindChestName(chestType, tile, num101, num102);
			}
			else if (type >= containers2LU && type < containers2LU + containers2Counts)
			{
				int num103 = mapX;
				int num104 = mapY;
				if (tile.frameX % 36 != 0)
					num103--;

				if (tile.frameY % 36 != 0)
					num104--;

				text = DrawMap_FindChestName(chestType2, tile, num103, num104);
			}
			else if (type >= fakeContainersLU && type < fakeContainersLU + containersCounts)
			{
				int num105 = mapX;
				int num106 = mapY;
				if (tile.frameX % 36 != 0)
					num105--;

				if (tile.frameY % 36 != 0)
					num106--;

				text = chestType[tile.frameX / 36].Value;
			}
			else if (type >= fakeContainers2LU && type < fakeContainers2LU + containers2Counts)
			{
				int num107 = mapX;
				int num108 = mapY;
				if (tile.frameX % 36 != 0)
					num107--;

				if (tile.frameY % 36 != 0)
					num108--;

				text = chestType2[tile.frameX / 36].Value;
			}
			else if (type >= dressersLU && type < dressersLU + dressersCounts)
			{
				//patch file: num91, num92
				Tile tile5 = Main.tile[mapX, mapY];
				if (tile5 != null)
				{
					int num109 = mapY;
					int x2 = mapX - tile5.frameX % 54 / 18;
					if (tile5.frameY % 36 != 0)
						num109--;

					int num110 = Chest.FindChest(x2, num109);
					text = ((num110 < 0) ? Lang.dresserType[0].Value : ((!(Main.chest[num110].name != "")) ? Lang.dresserType[tile5.frameX / 54].Value : (Lang.dresserType[tile5.frameX / 54].Value + ": " + Main.chest[num110].name)));
				}
			}
			else
			{
				text = Lang._mapLegendCache.FromTile(Main.Map[mapX, mapY], mapX, mapY);
			}

			return text;
		}

		private static string DrawMap_FindChestName(LocalizedText[] chestNames, Tile chestTile, int x, int y, int fullTileWidth = 36)
		{
			int num = Chest.FindChestByGuessing(x, y);
			if (num < 0)
				return chestNames[0].Value;

			if (Main.chest[num].name != "")
				return string.Concat(chestNames[chestTile.frameX / fullTileWidth], ": ", Main.chest[num].name);

			return chestNames[chestTile.frameX / fullTileWidth].Value;
		}

		internal const string pre = "Pings/Textures/";

		private static Texture2D GetTexture(string name) => ModContent.GetTexture($"{pre}{name}");

		internal static void Load()
		{
			if (!Main.dedServ)
			{
				DefaultTextures = new Dictionary<PingType, Texture2D>();
				SpecialTextures = new Dictionary<string, Texture2D>();

				DefaultTextures[PingType.None] = GetTexture("Empty");

				DefaultTextures[PingType.SingleTile] = GetTexture("SingleTile");
				DefaultTextures[PingType.MultiTile] = DefaultTextures[PingType.SingleTile];
				DefaultTextures[PingType.ClusterTile] = GetTexture("ClusterTile");

				DefaultTextures[PingType.SelfPlayer] = GetTexture("SelfPlayer");

				DefaultTextures[PingType.NPC] = GetTexture("NPC");
				DefaultTextures[PingType.Item] = GetTexture("Item");

				string[] names = new string[] { "MultiTile_Container", "Special_Circle", "NPC_Critter" };
				foreach (var name in names)
				{
					SpecialTextures[name] = GetTexture(name);
				}

				foreach (PingType pingType in Enum.GetValues(typeof(PingType)))
				{
					if (!DefaultTextures.ContainsKey(pingType))
					{
						throw new Exception($"Default texture for {pingType} missing!");
					}
				}
			}
		}

		internal static void Unload()
		{
			DefaultTextures = null;
			SpecialTextures = null;
		}

		/// <summary>
		/// Counts down the decay timer, returns true if it reaches 0
		/// </summary>
		internal bool DoDecay()
		{
			if (DecayTimer > PlayerDespawnStart && Player != null && (!Player.active || Player.GetModPlayer<PingsPlayer>().UUID != PlayerUUID))
			{
				//Start despawning pings when player who owns it leaves.
				DecayTimer = PlayerDespawnStart;
			}
			else if (DecayTimer == ServerConfig.PingDurationMax * 60)
			{
				//Infinite duration
				return false;
			}
			//This should run on all clients, so visuals properly update too
			return --DecayTimer <= 0;
		}

		/// <summary>
		/// Refreshes the decay timer
		/// </summary>
		internal void RefreshDuration()
		{
			DecayTimer = DecayDuration;
		}

		/// <summary>
		/// Checks if the local player can see this ping
		/// </summary>
		/// <returns>Returns true if the pings owner is teamless or on the same team as the local player</returns>
		public bool IsVisible() => Player != null && (Player.team == 0 || Main.LocalPlayer.team == Player.team);

		public void NetSend(BinaryWriter writer)
		{
			//COULD use indexing here based on a flag (required because this is also done in world stuff and indexing might not be complete yet)
			writer.Write((string)PlayerUUID);

			writer.Write((byte)PingType);

			BitsByte flags = new BitsByte();

			bool notSpawned = DecayDuration != DecayTimer;

			flags[0] = Notify;
			flags[1] = notSpawned;

			writer.Write(flags);

			writer.Write((int)DecayDuration);
			if (notSpawned)
			{
				writer.Write((int)DecayTimer);
			}

			if (HasLocation)
			{
				writer.Write((short)TileLocation.X);
				writer.Write((short)TileLocation.Y);
			}

			if (HasSize)
			{
				writer.Write((short)TileSize.X);
				writer.Write((short)TileSize.Y);
			}

			if (HasTileCount)
			{
				writer.Write((short)TileCount);
			}

			if (HasWhoAmI)
			{
				writer.Write((short)WhoAmI);
			}

			if (HasType)
			{
				writer.Write((int)Type);
			}

			if (HasText)
			{
				writer.Write((string)Text);
			}
		}

		public static Ping FromNet(BinaryReader reader)
		{
			string uuid = reader.ReadString();

			PingType pingType = (PingType)reader.ReadByte();

			BitsByte flags = reader.ReadByte();

			bool notify = flags[0];
			bool notSpawned = flags[1];

			Ping ping = new Ping(uuid, pingType, notify);

			ping.DecayDuration = reader.ReadInt32();
			ping.RefreshDuration();
			if (notSpawned)
			{
				ping.DecayTimer = reader.ReadInt32();
			}

			if (ping.HasLocation)
			{
				ping.TileLocation = new Point16(reader.ReadInt16(), reader.ReadInt16());
			}

			if (ping.HasSize)
			{
				ping.TileSize = new Point16(reader.ReadInt16(), reader.ReadInt16());
			}

			if (ping.HasTileCount)
			{
				ping.TileCount = reader.ReadInt16();
			}

			if (ping.HasWhoAmI)
			{
				ping.WhoAmI = reader.ReadInt16();
			}

			if (ping.HasType)
			{
				ping.Type = reader.ReadInt32();
			}

			if (ping.HasText)
			{
				ping.Text = reader.ReadString();
			}

			return ping;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Ping ping)) return false;

			PingType pingType = ping.PingType;

			if (PingType != ping.PingType) return false;

			switch (pingType)
			{
				case PingType.SingleTile:
				case PingType.MultiTile:
				case PingType.ClusterTile:
					bool check = ping.Type == Type;
					if (pingType != PingType.ClusterTile)
					{
						check &= ping.TileLocation == TileLocation && ping.TileSize == TileSize;
					}
					else
					{
						//Since clusters can be expanded after pinging, tile size and location varies, all that matters here is overlap
						Rectangle thisSize = TileDimensions;
						Rectangle otherSize = ping.TileDimensions;
						check &= thisSize.Intersects(otherSize);
					}
					return check;

				case PingType.SelfPlayer:
					return ping.WhoAmI == WhoAmI;

				case PingType.NPC:
				case PingType.Item:
					return ping.WhoAmI == WhoAmI && ping.Type == Type;

				default:
					return false;
			}
		}

		public override int GetHashCode()
		{
			return Tuple.Create(Type, TileLocation, TileSize, TileCount, Type, WhoAmI).GetHashCode();
		}
	}
}
