using Microsoft.Xna.Framework;
using Pings.Netcode.Packets;
using System;
using System.Linq;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Audio;

namespace Pings
{
	public class PingsPlayer : ModPlayer
	{
		public string UUID { internal set; get; }

		private int KeyHoldTimer = 0;

		public const int KeyHoldTimerMax = 30;

		private bool notifyAfterHeld = false;

		private int PingCooldownTimer = -1;

		private bool dataRequestedAfterJoin = false;

		private void HandleKeybind()
		{
			if (Main.mapFullscreen)
			{
				return;
			}

			bool? notify = null;
			ModKeybind hotkey = PingsMod.PingKeybind;

			if (hotkey.JustPressed)
			{
				SoundEngine.PlaySound(SoundID.MenuOpen);
			}

			if (hotkey.Current)
			{
				if (!notifyAfterHeld)
				{
					KeyHoldTimer++;
					if (KeyHoldTimer > KeyHoldTimerMax)
					{
						PingsSystem.SpawnDustGoingOutwards(Main.MouseWorld, 10f, DustID.Torch, 10, 0.5f);

						SoundEngine.PlaySound(SoundID.MaxMana);
						notifyAfterHeld = true;
					}
				}

				Vector2 size = new Vector2(32);
				float distance = 0.8f * Math.Min(KeyHoldTimer / (float)KeyHoldTimerMax, 1f);
				int type = 204;
				if (PingCooldownTimer > 0)
				{
					type = 60;
				}
				PingsSystem.SpawnRotatingDust(Main.MouseWorld - size / 2, distance, size, type);

			}
			else if (hotkey.JustReleased)
			{
				notify = notifyAfterHeld;
			}
			else
			{
				notifyAfterHeld = false;
			}

			if (notify.HasValue && notify.Value is bool notified)
			{
				KeyHoldTimer = 0;
				notifyAfterHeld = false;
				if (PingCooldownTimer > 0)
				{
					//+60 so it doesn't say "0s"
					CombatText.NewText(Player.getRect(), Color.Cyan, $"Ping CD: {(PingCooldownTimer + 60) / 60}s");
					return;
				}

				Ping ping = Ping.MakePing(Player, Main.MouseWorld, notified);
				if (ping != null)
				{
					bool? success = PingsSystem.AddOrRemove(ping);

					if (success.HasValue && success.Value is bool added)
					{
						//PingsMod.Log($"Send ping {added}", true, true);
						new PingPacket(ping).Send();

						if (added)
						{
							PingCooldownTimer = 60 * ServerConfig.Instance.PingCooldown + 30;
						}

						SoundEngine.PlaySound(added ? SoundID.Chat : SoundID.MenuClose);
					}
				}
			}
		}

		public override void UpdateDead()
		{
			if (Main.myPlayer == Player.whoAmI && Main.netMode != NetmodeID.Server)
			{
				HandleKeybind();
			}
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Player.dead)
			{
				return;
			}
			HandleKeybind();
		}

		public override void Initialize()
		{
			UUID = string.Empty;
		}

		public override void LoadData(TagCompound tag)
		{
			UUID = tag.GetString("uuid");
			if (UUID == null)
			{
				UUID = string.Empty;
			}
		}

		public override void SaveData(TagCompound tag)
		{
			if (UUID != string.Empty)
			{
				tag.Add("uuid", UUID);
			}
		}

		public override void OnEnterWorld()
		{
			CalculateUUIDForLocalPlayer();
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				new UUIDPacket(Player, UUID).Send();
			}
		}

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			//Don't think this is even necessary
			//if (Main.netMode == NetmodeID.MultiplayerClient && !newPlayer) //newPlayer is true for the client that joins the game. We send it in OnEnterWorld in this case
			//{
			//	new UUIDPacket(Player, UUID).Send();
			//}

			if (Main.netMode == NetmodeID.Server)
			{
				new UUIDPacket(Player, UUID).Send(toWho, fromWho);
			}
		}

		public override void PreUpdate()
		{
			if (Player.whoAmI == Main.myPlayer)
			{
				if (PingCooldownTimer >= 0)
				{
					PingCooldownTimer--;
				}

				if (!dataRequestedAfterJoin && Main.netMode == NetmodeID.MultiplayerClient)
				{
					//Funny workaround due to how vanilla orders data sent/received for this player, other players, and world
					//It makes sure each ping has a valid UUID
					dataRequestedAfterJoin = true;
					new PingsRequestPacket().Send();
				}
			}
		}

		public static Player GetPlayerFromUUID(string uuid)
		{
			//PingsMod.Log("Get UUID (" + uuid + ")", true, true);
			if (uuid == string.Empty)
			{
				//PingsMod.Log("Player null", true, true);
				return null;
			}

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player player = Main.player[i];
				if (player.active && player.GetModPlayer<PingsPlayer>().UUID == uuid)
				{
					//PingsMod.Log("Found player " + player, true, true);
					return player;
				}
			}
			//PingsMod.Log("Player null after searching", true, true);
			return null;
		}

		public static string GetUUIDFromPlayer(Player player)
		{
			return player?.GetModPlayer<PingsPlayer>().UUID ?? string.Empty;
		}

		public static void CalculateUUIDForLocalPlayer(bool shortHash = true)
		{
			PingsPlayer pingsPlayer = Main.LocalPlayer.GetModPlayer<PingsPlayer>();
			if (pingsPlayer.UUID == string.Empty)
			{
				int hash = Math.Abs(Main.ActivePlayerFileData.Path.GetHashCode() ^ Main.ActivePlayerFileData.IsCloudSave.GetHashCode());
				string uuid = Main.clientUUID + "_" + hash;

				int n = 4;
				if (shortHash && uuid.Length > n * 2)
				{
					var firstN = new string(uuid.Take(n).ToArray());
					var lastN = new string(uuid.Reverse().Take(n).Reverse().ToArray());

					uuid = string.Concat(firstN, lastN);
				}

				pingsPlayer.UUID = uuid;
			}
		}
	}
}
