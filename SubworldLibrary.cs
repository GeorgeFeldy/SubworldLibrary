using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.NetModules;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.Net;
using Terraria.Net.Sockets;
using Terraria.UI.Chat;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using static Mono.Cecil.Cil.OpCodes;

namespace SubworldLibrary
{
	public class SubworldLibrary : Mod
	{
		private static ILHook tcpSocketHook;
		private static ILHook socialSocketHook;

		public override void Load()
		{
			FieldInfo current = typeof(SubworldSystem).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
			FieldInfo cache = typeof(SubworldSystem).GetField("cache", BindingFlags.NonPublic | BindingFlags.Static);
			MethodInfo normalUpdates = typeof(Subworld).GetMethod("get_NormalUpdates");
			MethodInfo shouldSave = typeof(Subworld).GetMethod("get_ShouldSave");

			IL_WorldGen.clearWorld += il =>
			{
				var c = new ILCursor(il);

				c.Emit(Ldsfld, typeof(WorldGen).GetField("lastMaxTilesX", BindingFlags.NonPublic | BindingFlags.Static));
				c.Emit(Ldsfld, typeof(WorldGen).GetField("lastMaxTilesY", BindingFlags.NonPublic | BindingFlags.Static));
				c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("ResizeSubworld", BindingFlags.NonPublic | BindingFlags.Static));
			};

			IL_Main.DoUpdateInWorld += il =>
			{
				ILCursor c, cc;
				if (!(c = new ILCursor(il)).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(SystemLoader), "PreUpdateTime"))
				|| !(cc = c.Clone()).TryGotoNext(i => i.MatchCall(typeof(SystemLoader), "PostUpdateTime")))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				cc.MarkLabel(label);
			};

			IL_WorldGen.UpdateWorld += il =>
			{
				var c = new ILCursor(il);
				if (!c.TryGotoNext(i => i.MatchCall(typeof(WorldGen), "UpdateWorld_Inner")))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, typeof(Subworld).GetMethod("Update"));

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				c.Index++;
				c.MarkLabel(label);
			};

			IL_Player.Update += il =>
			{
				ILCursor c, cc;
				if (!(c = new ILCursor(il)).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(out _), i => i.MatchConvR4(), i => i.MatchLdcR4(4200))
				|| !(cc = c.Clone()).TryGotoNext(MoveType.After, i => i.MatchLdarg(0), i => i.MatchLdarg(0), i => i.MatchLdfld(typeof(Player), "gravity"))
				|| !cc.Instrs[cc.Index].MatchLdloc(out int index))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brtrue, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Ldarg_0);
				c.Emit(Callvirt, typeof(Subworld).GetMethod("GetGravity"));
				c.Emit(Stloc, index);
				c.Emit(Br, label);

				c.MarkLabel(skip);

				cc.Index -= 3;
				cc.MarkLabel(label);
			};

			IL_NPC.UpdateNPC_UpdateGravity += il =>
			{
				ILCursor c, cc;
				if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchLdsfld(out _), i => i.MatchConvR4(), i => i.MatchLdcR4(4200))
				|| !(cc = c.Clone()).TryGotoNext(MoveType.After, i => i.MatchLdarg(0), i => i.MatchLdarg(0), i => i.MatchCall(typeof(NPC), "get_gravity"))
				|| !cc.Instrs[cc.Index].MatchLdloc(out int index))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				var label = c.DefineLabel();
				c.Emit(Brtrue, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Ldarg_0);
				c.Emit(Callvirt, typeof(Subworld).GetMethod("GetGravity"));
				c.Emit(Stloc, index);
				c.Emit(Br, label);

				c.MarkLabel(skip);

				cc.Index -= 3;
				cc.MarkLabel(label);
			};

			IL_Liquid.Update += il =>
			{
				var c = new ILCursor(il);
				if (!c.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchLdfld(typeof(Liquid), "y"), i => i.MatchCall(typeof(Main), "get_UnderworldLayer")))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Emit(Ldsfld, current);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, current);
				c.Emit(Callvirt, normalUpdates);
				c.Emit(Brfalse, (ILLabel)c.Instrs[c.Index + 3].Operand);

				c.MarkLabel(skip);
			};

			IL_Player.SavePlayer += il =>
			{
				ILCursor c, cc, ccc;
				if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchCall(typeof(Player), "InternalSaveMap"))
				|| !(cc = c.Clone()).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(typeof(Main), "ServerSideCharacter"))
				|| !(ccc = cc.Clone()).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(FileUtilities), "ProtectedInvoke")))
				{
					Logger.Error("FAILED:");
					return;
				}

				c.Index -= 3;

				c.Emit(Ldsfld, cache);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);

				c.Emit(Ldsfld, cache);
				c.Emit(Callvirt, shouldSave);
				var label = c.DefineLabel();
				c.Emit(Brfalse, label);

				c.MarkLabel(skip);

				cc.MarkLabel(label);

				cc.Emit(Ldsfld, cache);
				skip = cc.DefineLabel();
				cc.Emit(Brfalse, skip);

				cc.Emit(Ldsfld, cache);
				cc.Emit(Callvirt, typeof(Subworld).GetMethod("get_NoPlayerSaving"));
				label = cc.DefineLabel();
				cc.Emit(Brtrue, label);

				cc.MarkLabel(skip);

				ccc.MarkLabel(label);
			};

			IL_WorldFile.SaveWorld_bool_bool += il =>
			{
				var c = new ILCursor(il);

				c.Emit(Ldsfld, cache);
				var skip = c.DefineLabel();
				c.Emit(Brfalse, skip);
				c.Emit(Ldsfld, cache);
				c.Emit(Callvirt, shouldSave);
				c.Emit(Brtrue, skip);
				c.Emit(Ret);

				c.MarkLabel(skip);
			};

			if (Main.dedServ)
			{
				bool subserver = Program.LaunchParameters.ContainsKey("-subworld");

				IL_Netplay.UpdateServerInMainThread += il =>
				{
					var c = new ILCursor(il);
					if (!c.TryGotoNext(MoveType.AfterLabel, i => i.MatchRet()))
					{
						Logger.Error("FAILED:");
						return;
					}
					c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("CheckBytes", BindingFlags.NonPublic | BindingFlags.Static));
				};

				// these are effectively not called on subservers, no need to patch them
				if (!subserver)
				{
					socialSocketHook = new ILHook(typeof(SocialSocket).GetMethod("Terraria.Net.Sockets.ISocket.AsyncSend", BindingFlags.NonPublic | BindingFlags.Instance), AsyncSend);
					tcpSocketHook = new ILHook(typeof(TcpSocket).GetMethod("Terraria.Net.Sockets.ISocket.AsyncSend", BindingFlags.NonPublic | BindingFlags.Instance), AsyncSend);

					void AsyncSend(ILContext il)
					{
						var c = new ILCursor(il);
						if (!c.TryGotoNext(MoveType.After, i => i.MatchRet()))
						{
							Logger.Error("FAILED:");
							return;
						}
						c.MoveAfterLabels();

						c.Emit(Ldarg_0);
						c.Emit(Ldarg_1);
						c.Emit(Ldarg_2);
						c.Emit(Ldarg_3);
						c.Emit(Ldarga, 5);
						c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("DenySend", BindingFlags.NonPublic | BindingFlags.Static));
						var label = c.DefineLabel();
						c.Emit(Brfalse, label);
						c.Emit(Ret);
						c.MarkLabel(label);
					}

					IL_NetMessage.CheckBytes += il =>
					{
						ILCursor c, cc;
						if (!(c = new ILCursor(il)).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(BitConverter), "ToUInt16"))
						|| !c.Instrs[c.Index].MatchStloc(out int index)
						|| !c.TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(Stream), "get_Position"), i => i.MatchStloc(out _))
						|| !(cc = c.Clone()).TryGotoNext(i => i.MatchLdsfld(typeof(NetMessage), "buffer"), i => i.MatchLdarg(0), i => i.MatchLdelemRef(), i => i.MatchLdfld(typeof(MessageBuffer), "reader")))
						{
							Logger.Error("FAILED:");
							return;
						}

						c.Emit(Ldsfld, typeof(NetMessage).GetField("buffer"));
						c.Emit(Ldarg_0);
						c.Emit(Ldelem_Ref);
						c.Emit(Ldloc_2);
						c.Emit(Ldloc, index);
						c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("DenyRead", BindingFlags.NonPublic | BindingFlags.Static));

						var label = c.DefineLabel();
						c.Emit(Brtrue, label);

						cc.MarkLabel(label);

						cc.Index = c.Instrs.Count - 1;
						cc.Emit(Ldarg_0);
						cc.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("AllowAutoShutdown", BindingFlags.NonPublic | BindingFlags.Static));
					};

					IL_Netplay.UpdateConnectedClients += il =>
					{
						var c = new ILCursor(il);
						if (!c.TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(RemoteClient), "Reset"))
						|| !c.Instrs[c.Index].MatchLdloc(out int index))
						{
							Logger.Error("FAILED:");
							return;
						}

						c.Emit(Ldloc, index);
						c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("SyncDisconnect", BindingFlags.NonPublic | BindingFlags.Static));
					};

					return;
				}

				IL_Main.DedServ_PostModLoad += il =>
				{
					var c = new ILCursor(il);
					if (!c.TryGotoNext(i => i.MatchBr(out _)))
					{
						Logger.Fatal("FAILED - subserver cannot run without this injection!");
						Main.instance.Exit();
						return;
					}

					ConstructorInfo gameTime = typeof(GameTime).GetConstructor(Type.EmptyTypes);
					MethodInfo update = typeof(Main).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
					FieldInfo saveTime = typeof(Main).GetField("saveTime", BindingFlags.NonPublic | BindingFlags.Static);

					// DedServ must not run, so this abomination replicates the update loop (lots of private methods and reflection is slow)

					c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("LoadIntoSubworld", BindingFlags.NonPublic | BindingFlags.Static));

					c.Emit(Ldarg_0);
					c.Emit(Newobj, gameTime);
					c.Emit(Callvirt, update);

					c.Emit(Newobj, typeof(Stopwatch).GetConstructor(Type.EmptyTypes));
					c.Emit(Stloc_1);
					c.Emit(Ldloc_1);
					c.Emit(Callvirt, typeof(Stopwatch).GetMethod("Start"));

					c.Emit(Ldc_I4_0);
					c.Emit(Stsfld, typeof(Main).GetField("gameMenu"));

					// vanilla magic number, not sure why it's 1 higher than 60 / 1000
					c.Emit(Ldc_R8, 16.666666666666668);
					c.Emit(Stloc_2);
					c.Emit(Ldloc_2);
					c.Emit(Stloc_3);

					var loopStart = c.DefineLabel();
					c.Emit(Br, loopStart);

					// {

					var loop = c.DefineLabel();
					c.MarkLabel(loop);

					c.Emit(OpCodes.Call, typeof(Main).Assembly.GetType("Terraria.ModLoader.Engine.ServerHangWatchdog").GetMethod("Checkin", BindingFlags.NonPublic | BindingFlags.Static));

					c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("CheckClients", BindingFlags.NonPublic | BindingFlags.Static));

					c.Emit(Ldsfld, typeof(Netplay).GetField("HasClients"));
					var label = c.DefineLabel();
					c.Emit(Brfalse, label);

					c.Emit(Ldarg_0);
					c.Emit(Newobj, gameTime);
					c.Emit(Callvirt, update);
					var label2 = c.DefineLabel();
					c.Emit(Br, label2);

					c.MarkLabel(label);

					c.Emit(Ldsfld, saveTime);
					c.Emit(Callvirt, typeof(Stopwatch).GetMethod("get_IsRunning"));
					c.Emit(Brfalse, label2);

					c.Emit(Ldsfld, saveTime);
					c.Emit(Callvirt, typeof(Stopwatch).GetMethod("Stop"));
					c.Emit(Br, label2);

					c.MarkLabel(label2);

					c.Emit(Ldloc_1);
					c.Emit(Ldloc_2);
					c.Emit(Ldloca, 3);
					c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("Sleep", BindingFlags.NonPublic | BindingFlags.Static));

					c.MarkLabel(loopStart);

					c.Emit(Ldsfld, typeof(Netplay).GetField("Disconnect"));
					c.Emit(Brfalse, loop);

					// }

					c.Emit(Ldsfld, current);
					c.Emit(Callvirt, shouldSave);
					label = c.DefineLabel();
					c.Emit(Brfalse, label);
					c.Emit(OpCodes.Call, typeof(WorldFile).GetMethod("SaveWorld", Type.EmptyTypes));
					c.MarkLabel(label);

					c.Emit(OpCodes.Call, typeof(SystemLoader).GetMethod("OnWorldUnload"));

					c.Emit(Ret);
				};
			}
			else
			{
				FieldInfo hideUnderworld = typeof(SubworldSystem).GetField("hideUnderworld");

				IL_Main.DoDraw += il =>
				{
					var c = new ILCursor(il);
					if (!c.TryGotoNext(MoveType.After, i => i.MatchStsfld(typeof(Main), "HoverItem")))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(Ldsfld, typeof(Main).GetField("gameMenu"));
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldsfld, current);
					var label = c.DefineLabel();
					c.Emit(Brfalse, label);

					c.Emit(Ldsfld, current);
					c.Emit(Ldarg_1);
					c.Emit(Callvirt, typeof(Subworld).GetMethod("DrawSetup"));
					c.Emit(Ret);

					c.MarkLabel(label);

					c.Emit(Ldsfld, cache);
					c.Emit(Brfalse, skip);

					c.Emit(Ldsfld, cache);
					c.Emit(Ldarg_1);
					c.Emit(Callvirt, typeof(Subworld).GetMethod("DrawSetup"));
					c.Emit(Ret);

					c.MarkLabel(skip);
				};

				IL_Main.DrawBackground += il =>
				{
					ILCursor c, cc;
					if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchLdcI4(330))
					|| !(cc = c.Clone()).TryGotoNext(i => i.MatchStloc(out _), i => i.MatchLdcR4(255)))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Conv_R8);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					cc.MarkLabel(label);
				};

				IL_Main.OldDrawBackground += il =>
				{
					ILCursor c, cc;
					if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchLdcI4(230))
					|| !(cc = c.Clone()).TryGotoNext(i => i.MatchStloc(out _), i => i.MatchLdcI4(0)))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Conv_R8);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					cc.MarkLabel(label);
				};

				IL_Main.UpdateAudio += il =>
				{
					ILCursor c, cc;
					if (!(c = new ILCursor(il)).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdsfld(typeof(Main), "swapMusic"))
					|| !(cc = c.Clone()).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(Main), "UpdateAudio_DecideOnNewMusic"))
					|| !cc.Instrs[cc.Index].MatchBr(out ILLabel label))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("ChangeAudio", BindingFlags.NonPublic | BindingFlags.Static));
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("ManualAudioUpdates", BindingFlags.NonPublic | BindingFlags.Static));
					c.Emit(Brfalse, label);

					var ret = c.DefineLabel();
					ret.Target = c.Instrs[c.Instrs.Count - 1];
					c.Emit(Leave, ret);

					c.MarkLabel(skip);
				};

				IL_IngameOptions.Draw += il =>
				{
					ILCursor c, cc, ccc, cccc;
					if (!(c = new ILCursor(il)).TryGotoNext(i => i.MatchLdsfld(typeof(Lang), "inter"), i => i.MatchLdcI4(35))
					|| !(cc = c.Clone()).TryGotoNext(MoveType.After, i => i.MatchCallvirt(typeof(LocalizedText), "get_Value"))
					|| !(ccc = cc.Clone()).TryGotoNext(i => i.MatchLdnull(), i => i.MatchCall(typeof(WorldGen), "SaveAndQuit"))
					|| !(cccc = ccc.Clone()).TryGotoPrev(MoveType.AfterLabel, i => i.MatchLdloc(out _), i => i.MatchLdcI4(1), i => i.MatchAdd(), i => i.MatchStloc(out _)))
					{
						Logger.Error("FAILED:");
						return;
					}

					ccc.Index -= 4;

					ccc.Emit(Ldsfld, current);
					var skip = ccc.DefineLabel();
					ccc.Emit(Brfalse, skip);

					ccc.Emit(OpCodes.Call, typeof(SubworldSystem).GetMethod("Exit"));
					var label = ccc.DefineLabel();
					ccc.Emit(Br, label);

					ccc.MarkLabel(skip);

					ccc.Index += 6;
					ccc.MarkLabel(label);

					cccc.Emit(Ldsfld, typeof(SubworldSystem).GetField("noReturn"));
					cccc.Emit(Brtrue, label);

					c.Emit(Ldsfld, current);
					skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldstr, "Mods.SubworldLibrary.Return");
					c.Emit(OpCodes.Call, typeof(Language).GetMethod("GetTextValue", new Type[] { typeof(string) }));
					label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					cc.MarkLabel(label);
				};

				IL_TileLightScanner.GetTileLight += il =>
				{
					ILCursor c, cc, ccc;
					if (!(c = new ILCursor(il)).TryGotoNext(MoveType.After, i => i.MatchStloc(1))
					|| !(cc = c.Clone()).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdarg(2), i => i.MatchCall(typeof(Main), "get_UnderworldLayer"))
					|| !(ccc = cc.Clone()).TryGotoNext(MoveType.After, i => i.MatchCall(typeof(TileLightScanner), "ApplyHellLight")))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(Ldsfld, current);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldsfld, current);
					c.Emit(Ldloc_0);
					c.Emit(Ldarg_1);
					c.Emit(Ldarg_2);
					c.Emit(Ldloca, 1);
					c.Emit(Ldarg_3);
					c.Emit(Callvirt, typeof(Subworld).GetMethod("GetLight"));

					c.Emit(Brfalse, skip);
					c.Emit(Ret);

					c.MarkLabel(skip);

					cc.Emit(Ldsfld, hideUnderworld);
					skip = cc.DefineLabel();
					cc.Emit(Brtrue, skip);

					ccc.MarkLabel(skip);
				};

				IL_Player.UpdateBiomes += il =>
				{
					ILCursor c, cc;
					if (!(c = new ILCursor(il)).TryGotoNext(MoveType.AfterLabel, i => i.MatchLdloc(out _), i => i.MatchLdfld(typeof(Point), "Y"), i => i.MatchLdsfld(typeof(Main), "maxTilesY"))
					|| !(cc = c.Clone()).TryGotoNext(i => i.MatchStloc(out _)))
					{
						Logger.Error("FAILED:");
						return;
					}

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brfalse, skip);

					c.Emit(Ldc_I4_0);
					var label = c.DefineLabel();
					c.Emit(Br, label);

					c.MarkLabel(skip);

					cc.MarkLabel(label);
				};

				IL_Main.DrawUnderworldBackground += il =>
				{
					var c = new ILCursor(il);

					c.Emit(Ldsfld, hideUnderworld);
					var skip = c.DefineLabel();
					c.Emit(Brtrue, skip);
					c.Index = c.Instrs.Count - 1;
					c.MarkLabel(skip);
				};

				IL_Netplay.AddCurrentServerToRecentList += il =>
				{
					var c = new ILCursor(il);

					c.Emit(Ldsfld, current);
					var skip = c.DefineLabel();
					c.Emit(Brtrue, skip);
					c.Index = c.Instrs.Count - 1;
					c.MarkLabel(skip);
				};
			}

			IL_Main.EraseWorld += il =>
			{
				var c = new ILCursor(il);

				c.Emit(Ldarg_0);
				c.Emit(OpCodes.Call, typeof(SubworldLibrary).GetMethod("EraseSubworlds", BindingFlags.NonPublic | BindingFlags.Static));
			};
		}

		private static void SendBestiary(byte[] buffer, int start)
		{
			byte type = buffer[start + 5];
			short id = BitConverter.ToInt16(buffer, start + 6);

			MemoryStream stream = new MemoryStream(ModNet.NetModCount < 256 ? (type == 0 ? 10 : 8) : (type == 0 ? 11 : 9));
			using BinaryWriter writer = new BinaryWriter(stream);

			writer.Write((byte)255);
			if (ModNet.NetModCount < 256)
			{
				writer.Write(type == 0 ? (ushort)9 : (ushort)7);
				writer.Write((byte)255);
				writer.Write((byte)ModContent.GetInstance<SubworldLibrary>().NetID);
			}
			else
			{
				writer.Write(type == 0 ? (ushort)10 : (ushort)8);
				writer.Write((byte)255);
				writer.Write((ushort)ModContent.GetInstance<SubworldLibrary>().NetID);
			}
			writer.Write(type);
			writer.Write(id);
			if (type == 0)
			{
				writer.Write(BitConverter.ToUInt16(buffer, start + 8));
			}

			byte[] data = stream.GetBuffer();

			lock (NetMessage.buffer[256])
			{
				Buffer.BlockCopy(data, 1, NetMessage.buffer[256].readBuffer, NetMessage.buffer[256].totalData, data.Length - 1);
				NetMessage.buffer[256].totalData += data.Length - 1;
				NetMessage.buffer[256].checkBytes = true;
			}

			for (int i = 0; i < SubworldSystem.subworlds.Count; i++)
			{
				SubworldSystem.subworlds[i].link?.Send(data);
			}
		}

		private static void SendText(MessageBuffer buffer, int start, int length)
		{
			// reader position is reset by vanilla
			buffer.reader.BaseStream.Position = start + 5;

			string command;
			string worldName;

			ChatMessage message = ChatMessage.Deserialize(buffer.reader);
			buffer.reader.BaseStream.Position = start + 5;

			int sentFrom = SubworldSystem.playerLocations[buffer.whoAmI];
			if (sentFrom >= 0)
			{
				worldName = SubworldSystem.subworlds[sentFrom].DisplayName.Value;

				command = buffer.reader.ReadString();

				// only read commands where they were sent from
				if (command != "Say")
				{
					byte[] original = new byte[length + 1];
					original[0] = (byte)buffer.whoAmI;
					Buffer.BlockCopy(buffer.readBuffer, start, original, 1, length);
					SubworldSystem.subworlds[sentFrom].link?.Send(original);
					return;
				}
			}
			else
			{
				worldName = Main.worldName;

				command = buffer.reader.ReadString();

				// only read commands where they were sent from
				if (command != "Say")
				{
					ChatManager.Commands.ProcessIncomingMessage(message, buffer.whoAmI);
					return;
				}
			}

			string prepend =
				"[" +
				worldName +
				"] <" +
				Main.player[buffer.whoAmI].name +
				"> " +
				buffer.reader.ReadString();

			int len = Encoding.UTF8.GetByteCount(command) + Encoding.UTF8.GetByteCount(prepend);

			MemoryStream stream = new MemoryStream(len + (ModNet.NetModCount < 256 ? 9 : 10));
			using BinaryWriter writer = new BinaryWriter(stream);

			writer.Write((byte)255);
			if (ModNet.NetModCount < 256)
			{
				writer.Write((ushort)(len + 8));
				writer.Write((byte)255);
				writer.Write((byte)ModContent.GetInstance<SubworldLibrary>().NetID);
			}
			else
			{
				writer.Write((ushort)(len + 9));
				writer.Write((byte)255);
				writer.Write((ushort)ModContent.GetInstance<SubworldLibrary>().NetID);
			}
			writer.Write((byte)3);
			writer.Write((byte)255);
			writer.Write(command);
			writer.Write(prepend);

			// the stream's length is exact, so GetBuffer can be used instead of ToArray
			byte[] data = stream.GetBuffer();

			if (sentFrom < 0)
			{
				ChatManager.Commands.ProcessIncomingMessage(message, buffer.whoAmI);

				for (int i = 0; i < SubworldSystem.subworlds.Count; i++)
				{
					SubworldSystem.subworlds[i].link?.Send(data);
				}
				return;
			}

			// other clients may not know the name of this client, so pretend this is a server message
			message.Text = prepend;
			ChatManager.Commands.ProcessIncomingMessage(message, 255);

			for (int i = 0; i < SubworldSystem.subworlds.Count; i++)
			{
				if (i != sentFrom)
				{
					SubworldSystem.subworlds[i].link?.Send(data);
					continue;
				}

				byte[] original = new byte[length + 1];
				original[0] = (byte)buffer.whoAmI;
				Buffer.BlockCopy(buffer.readBuffer, start, original, 1, length);
				SubworldSystem.subworlds[i].link?.Send(original);
			}
		}

		private static bool DenyRead(MessageBuffer buffer, int start, int length)
		{
			byte[] buf = buffer.readBuffer;

			// always read sublib packets on the main server, MovePlayerToSubserver and SyncDisconnect will send them to subservers directly
			if (buf[start + 2] == 250 && (ModNet.NetModCount < 256 ? buf[start + 3] : BitConverter.ToUInt16(buf, start + 3)) == ModContent.GetInstance<SubworldLibrary>().NetID)
			{
				return false;
			}

			if (buf[start + 2] == 82)
			{
				ushort packetId = BitConverter.ToUInt16(buf, start + 3);

				// propagate chat messages
				if (packetId == NetManager.Instance.GetId<NetTextModule>())
				{
					Netplay.Clients[buffer.whoAmI].TimeOutTimer = 0;

					SendText(buffer, start, length);

					// the packet was read, don't read it again
					return true;
				}
			}

			int id = SubworldSystem.playerLocations[buffer.whoAmI];
			if (id < 0)
			{
				return false;
			}

			Netplay.Clients[buffer.whoAmI].TimeOutTimer = 0;

			byte[] packet = new byte[length + 1];
			packet[0] = (byte)buffer.whoAmI;
			Buffer.BlockCopy(buf, start, packet, 1, length);
			SubworldSystem.subworlds[id].link?.Send(packet);

			return true;
		}

		private static bool DenySend(ISocket socket, byte[] data, int start, int length, ref object state)
		{
			// always send sublib packets
			if (data[start + 2] == 250 && (ModNet.NetModCount < 256 ? data[start + 3] : BitConverter.ToUInt16(data, start + 3)) == ModContent.GetInstance<SubworldLibrary>().NetID)
			{
				return false;
			}

			if (Thread.CurrentThread.Name == "Subserver Packets")
			{
				if (data[start + 2] == 82)
				{
					ushort packetId = BitConverter.ToUInt16(data, start + 3);

					// propagate bestiary updates
					if (packetId == NetManager.Instance.GetId<NetBestiaryModule>())
					{
						SendBestiary(data, start);

						// the main server should always send this packet
						return false;
					}
				}
				return false;
			}

			return SubworldSystem.deniedSockets.Contains(socket);
		}

		private static void Sleep(Stopwatch stopwatch, double delta, ref double target)
		{
			double now = stopwatch.ElapsedMilliseconds;
			double remaining = target - now;
			target += delta;
			if (target < now)
			{
				target = now + delta;
			}
			if (remaining <= 0)
			{
				Thread.Sleep(0);
				return;
			}
			Thread.Sleep((int)remaining);
		}

		private static void CheckClients()
		{
			bool active = false;
			for (int i = 0; i < 256; i++)
			{
				RemoteClient client = Netplay.Clients[i];

				// the other checks vanilla does aren't needed
				if (client.PendingTerminationApproved)
				{
					client.Reset();

					NetMessage.SendData(14, -1, i, null, i, 0);
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Lang.mp[20].Key, client.Name), new Color(255, 240, 20), i);
					Player.Hooks.PlayerDisconnect(i);

					continue;
				}

				if (client.State > 0)
				{
					active = true;
				}
			}

			if (active)
			{
				Netplay.HasClients = true;
				return;
			}

			if (Netplay.HasClients)
			{
				Netplay.HasClients = false;
				Netplay.Disconnect = true;
			}
		}

		public override object Call(params object[] args)
		{
			try
			{
				string message = args[0] as string;
				switch (message)
				{
					case "Register":
						Mod mod = args[1] as Mod;

						int i = 6;
						CrossModSubworld subworld = new CrossModSubworld(
							args[2] as string,
							Convert.ToInt32(args[3]),
							Convert.ToInt32(args[4]),
							args[5] as List<GenPass>,
							args.Length > i ? args[i] as WorldGenConfiguration : null,
							args.Length > ++i ? Convert.ToInt32(args[i]) : -1,
							args.Length > ++i ? Convert.ToBoolean(args[i]) : false,
							args.Length > ++i ? Convert.ToBoolean(args[i]) : false,
							args.Length > ++i ? Convert.ToBoolean(args[i]) : false,
							args.Length > ++i ? Convert.ToBoolean(args[i]) : false,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action : null,
							args.Length > ++i ? args[i] as Action<GameTime> : null,
							args.Length > ++i ? args[i] as Func<bool> : null,
							args.Length > ++i ? args[i] as Func<Entity, float> : null);

						mod.AddContent(subworld);

						return subworld.Name;
					case "Enter":
						return SubworldSystem.Enter(args[1] as string);
					case "Exit":
						SubworldSystem.Exit();
						return true;
					case "Current":
						return SubworldSystem.Current.FullName;
					case "IsActive":
						return SubworldSystem.IsActive(args[1] as string);
					case "AnyActive":
						return SubworldSystem.AnyActive(args[1] as Mod);
				}
			}
			catch (Exception e)
			{
				Logger.Error("Call error: " + e.StackTrace + e.Message);
			}
			return false;
		}

		// HOW SUBWORLD LIBRARY HANDLES PACKETS
		// when a client is in a subworld, all packets sent to and from them are relayed to the subserver belonging to that subworld (see DenyRead)
		// sublib packets are never relayed to subservers automatically, but may be sent to them by sublib directly
		// subservers send packets to the main server via SubserverSocket
		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			if (Main.netMode == 2)
			{
				// packet came from a sub/server
				if (whoAmI == 256)
				{
					switch (reader.ReadByte())
					{
						case 0: // mirror NetBestiaryModule
							Main.BestiaryTracker.Kills.SetKillCountDirectly(ContentSamples.NpcsByNetId[reader.ReadInt16()].GetBestiaryCreditId(), reader.ReadUInt16());
							return;

						case 1: // mirror NetBestiaryModule
							Main.BestiaryTracker.Sights.SetWasSeenDirectly(ContentSamples.NpcsByNetId[reader.ReadInt16()].GetBestiaryCreditId());
							return;

						case 2: // mirror NetBestiaryModule
							Main.BestiaryTracker.Chats.SetWasChatWithDirectly(ContentSamples.NpcsByNetId[reader.ReadInt16()].GetBestiaryCreditId());
							return;

						case 3: // mirror NetTextModule
							int sender = reader.ReadByte(); // this may be set to 255 by the main server, since the actual sender may be unknown on the subserver
							ChatManager.Commands.ProcessIncomingMessage(ChatMessage.Deserialize(reader), sender);
							return;

						default:
							return;
					}
				}

				if (SubworldSystem.current != null)
				{
					int mod = ModNet.NetModCount < 256 ? reader.ReadByte() : reader.ReadUInt16();
					if (mod != NetID)
					{
						ModNet.GetMod(mod).HandlePacket(reader, 256);
						return;
					}

					Netplay.Clients[whoAmI].PendingTerminationApproved = true;
					return;
				}

				// always read an id in case a request is sent multiple times
				ushort id = reader.ReadUInt16();
				if (SubworldSystem.pendingMoves[whoAmI] >= 0)
				{
					SubworldSystem.FinishMove(whoAmI);
				}
				else if (!SubworldSystem.noReturn)
				{
					SubworldSystem.MovePlayerToSubserver(whoAmI, id);
				}
			}
			else
			{
				ushort id = reader.ReadUInt16();

				// it may be best to set this at the end of the update cycle
				SubworldSystem.current = id < ushort.MaxValue ? SubworldSystem.subworlds[id] : null;

				Main.menuMode = 10;
				Main.gameMenu = true;

				ModPacket packet = GetPacket();
				packet.Write(id);
				packet.Send();

				Task.Factory.StartNew(SubworldSystem.ExitWorldCallBack, id < ushort.MaxValue ? id : -1);
			}
		}

		private static void ResizeSubworld(int lastWidth, int lastHeight)
		{
			if ((Main.maxTilesX <= 8400 || Main.maxTilesX <= lastWidth) && (Main.maxTilesY <= 2400 || Main.maxTilesY <= lastHeight))
			{
				return;
			}

			ushort newWidth = (ushort)Math.Clamp(Main.maxTilesX + 1, 8401, 65535);
			ushort newHeight = (ushort)Math.Clamp(Main.maxTilesY + 1, 2401, 65535);
			if (newWidth == ushort.MaxValue)
			{
				Main.maxTilesX = 65534;
			}
			if (newHeight == ushort.MaxValue)
			{
				Main.maxTilesY = 65534;
			}

			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { newWidth, newHeight }, null);
			Main.Map = new WorldMap(newWidth, newHeight);

			// try to match vanilla dimensions
			Main.mapTargetX = (newWidth + 1678) / 1680;
			Main.mapTargetY = (newHeight + 1198) / 1200;
			Main.instance.mapTarget = new RenderTarget2D[Main.mapTargetX, Main.mapTargetY];

			Main.initMap = new bool[Main.mapTargetX, Main.mapTargetY];
			Main.mapWasContentLost = new bool[Main.mapTargetX, Main.mapTargetY];
		}

		private static void EraseSubworlds(int index)
		{
			WorldFileData world = Main.WorldList[index];
			string path = Path.Combine(world.IsCloudSave ? Main.CloudWorldPath : Main.WorldPath, world.UniqueId.ToString());
			if (FileUtilities.Exists(path, world.IsCloudSave))
			{
				FileUtilities.Delete(path, world.IsCloudSave);
			}
		}
	}
}