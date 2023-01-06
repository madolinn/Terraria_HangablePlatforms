using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.Enums;
using Terraria.ObjectData;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;
using OnTileObject = On.Terraria.TileObject;
using OnWorldGen = On.Terraria.WorldGen;
using OnTileDrawing = On.Terraria.GameContent.Drawing.TileDrawing;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace HangablePlatforms
{
	public class HangablePlatforms : Mod
	{
		internal List<int> hangableItems = new List<int>();
		internal List<int> bannerIDs = new List<int>();
		
		public override void Load() {
			/*Type delegateType = typeof(On.Terraria.GameContent.Drawing.TileDrawing.hook_AddSpecialPoint);
			MethodInfo method = delegateType.GetMethod("Invoke");
			foreach (ParameterInfo param in method.GetParameters()) { 
				Logger.Warn(param.ParameterType.Name + " | " + param.Name);
			}*/
			
			On.Terraria.TileObject.CanPlace += CanPlaceBanner;
			On.Terraria.WorldGen.CheckBanner += CheckBannerPatch;
			On.Terraria.WorldGen.Check1x2Top += Check1x2TopPatch;
			
			On.Terraria.GameContent.Drawing.TileDrawing.DrawMultiTileVinesInWind += OnDrawMultiTileVinesInWindPatch;
		}
		
		public override void PostSetupContent() {
			SetupBannerIDs();
			IL.Terraria.GameContent.Drawing.TileDrawing.Draw += ILTileDrawingPatch;
			IL.Terraria.GameContent.Drawing.TileDrawing.DrawMultiTileVines += ILHangingDrawPatch;
			OverwriteBannerObjectData();
		}
		
		private void SetupBannerIDs() {
			
			IEnumerable<ModTile> modContent = ModContent.GetContent<ModTile>();
			
			foreach (ModTile tile in modContent) {
				String className = tile.GetType().Name;
				int modNameEnd = tile.GetType().Name.IndexOf(".");
				if (className.ToLower().IndexOf("banner") > modNameEnd) {
					if (tile.Type < 0) { continue; }
					if (TileObjectData.GetTileData(tile.Type, 0).AnchorTop.type == (AnchorType)(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom)) {
						bannerIDs.Add(tile.Type);
					}
				}
			}
		}
		private void OnDrawMultiTileVinesInWindPatch(OnTileDrawing.orig_DrawMultiTileVinesInWind orig, TileDrawing self, Vector2 screenPosition, Vector2 offSet, int topLeftX, int topLeftY, int sizeX, int sizeY) {
			if (sizeX == 1 && topLeftY > 0) {
				Tile tile = Main.tile[topLeftX, topLeftY - 1];
				if (tile != null && tile.HasTile && TileID.Sets.Platforms[tile.TileType] && !tile.IsHalfBlock && tile.Slope == 0) {
					orig(self, screenPosition, new Vector2(offSet.X, offSet.Y - 8), topLeftX, topLeftY, sizeX, sizeY);
					return;
				}
			}
			orig(self, screenPosition, offSet, topLeftX, topLeftY, sizeX, sizeY);
		}
		
		private void ILHangingDrawPatch(ILContext il) {
			ILCursor c = new(il);
			
			if (!c.TryGotoNext(MoveType.After,
				i => i.MatchLdloc(10),
				i => i.MatchLdcI4(91),
				i => i.MatchBeq(out _))) {
					Logger.Fatal("Failed Patch ILHangingDraw");
					return;
				}
			
			c.Index--;
			
			Instruction jumpTo = il.Instrs[c.Index];
			
			if (!c.TryGotoNext(MoveType.After,
				i => i.MatchLdloc(10),
				i => i.MatchLdcI4(572),
				i => i.MatchBeq(out _))) {
					Logger.Fatal("Failed Patch ILHangingDraw");
					return;
				}
			
			foreach (int id in bannerIDs) {
				if (id != 91) {
					c.Emit(OpCodes.Ldloc_S, (byte)10);
					c.Emit(OpCodes.Ldc_I4, id);
					c.Emit(OpCodes.Beq, jumpTo.Operand);
				}
			}
			
			Logger.Warn("Patched ILHangingDraw");
		}
		
		private void ILTileDrawingPatch(ILContext il) {
			ILCursor c = new(il);
			
			if (!c.TryGotoNext(MoveType.After,
				i => i.MatchLdloc(15),
				i => i.MatchLdcI4(91),
				i => i.MatchBeq(out _))) {
					Logger.Fatal("Failed Patch ILTileDrawing");
					return;
				}
			
			c.Index--;
		
			Instruction jumpTo = il.Instrs[c.Index];
			
			if (!c.TryGotoNext(MoveType.After,
				i => i.MatchLdloc(15),
				i => i.MatchLdcI4(597),
				i => i.MatchBeq(out _))) {
					Logger.Fatal("Failed Patch ILTileDrawing");
					return;
				}
			
			foreach (int id in bannerIDs) {
				if (id != 91) {
					c.Emit(OpCodes.Ldloc_S, (byte)15);
					c.Emit(OpCodes.Ldc_I4, id);
					c.Emit(OpCodes.Beq, jumpTo.Operand);
				}
			}
			
			Logger.Warn("Patched ILTileDrawing");
		}
		
		private void Check1x2TopPatch(OnWorldGen.orig_Check1x2Top orig, int x, int j, ushort type) {
			if (Terraria.WorldGen.destroyObject) {
				return;
			}
			
			int num = Main.tile[x, j].TileFrameY / 18;
			int num2 = 0;
			while (num >= 2) {
				num -= 2;
				num2++;
			}
			
			num = j - num;
			
			Tile tile = Main.tile[x, num - 1];
			//Logger.Warn("Top block: " + tile.TileType + " | " + num);
			if (tile != null && tile.HasUnactuatedTile && (TileID.Sets.Platforms[tile.TileType] || tile.TileType == 380)) {
				int tileFrameX = Main.tile[x, j].TileFrameX;
				bool flag = false;
				for (int i = 0; i < 2; i++)	{
					if (!Main.tile[x, num + i].HasUnactuatedTile) {
						flag = true;
					} else if (Main.tile[x, num + i].TileType != type) {
						flag = true;
					} else if (Main.tile[x, num + i].TileFrameY != i * 18 + num2 * 18 * 2) {
						flag = true;
					} else if (Main.tile[x, num + i].TileFrameX != tileFrameX) {
						flag = true;
					}
				}
				if (!flag) { return; }
			}
			
			orig(x, j, type);
		}
		
		private void CheckBannerPatch(OnWorldGen.orig_CheckBanner orig, int x, int j, byte type) {
			if (Terraria.WorldGen.destroyObject) {
				return;
			}
			
			int num = Main.tile[x, j].TileFrameY / 18;
			int num2 = 0;
			while (num >= 3) {
				num -= 3;
				num2++;
			}
			
			num = j - num;
			
			Tile tile = Main.tile[x, num - 1];
			if (tile != null && tile.HasUnactuatedTile && (TileID.Sets.Platforms[tile.TileType] || tile.TileType == 380)) {
				int tileFrameX = Main.tile[x, j].TileFrameX;
				bool flag = false;
				for (int i = 0; i < 3; i++)	{
					if (!Main.tile[x, num + i].HasUnactuatedTile) {
						flag = true;
					} else if (Main.tile[x, num + i].TileType != type) {
						flag = true;
					} else if (Main.tile[x, num + i].TileFrameY != i * 18 + num2 * 18 * 3) {
						flag = true;
					} else if (Main.tile[x, num + i].TileFrameX != tileFrameX) {
						flag = true;
					}
				}
				if (!flag) { return; }
			}
			
			orig(x, j, type);
		}
		
		private bool CanPlaceBanner(OnTileObject.orig_CanPlace orig, int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck = false, bool checkStay = false) {
			
			if (!hangableItems.Contains(type)) {
				return orig(x, y, type, style, dir, out objectData, onlyCheck, checkStay);
			}
			
			TileObjectData tileData = TileObjectData.GetTileData(type, style, 0);
			objectData = TileObject.Empty;
			bool flag = tileData.RandomStyleRange > 0;
			if (TileObjectPreviewData.placementCache == null)
			{
				TileObjectPreviewData.placementCache = new TileObjectPreviewData();
			}
			TileObjectPreviewData.placementCache.Reset();
			int num3 = 0;
			
			if (tileData.AlternatesCount != 0) {
				num3 = tileData.AlternatesCount;
			}
			
			float num4 = -1f;
			float num5 = -1f;
			int num6 = 0;
			TileObjectData tileObjectData = null;
			int i = -1;
			
			while (i < num3) {
				i++;
				TileObjectData tileData2 = TileObjectData.GetTileData(type, style, i);
				
				if (tileData2.Direction != 0 && ((tileData2.Direction == TileObjectDirection.PlaceLeft && dir == 1) || (tileData2.Direction == TileObjectDirection.PlaceRight && dir == -1))) {
					continue;
				}
				
				int num7 = x - (int)tileData2.Origin.X;
				int num8 = y - (int)tileData2.Origin.Y;
				
				if (num7 < 5 || num7 + tileData2.Width > Main.maxTilesX - 5 || num8 < 5 || num8 + tileData2.Height > Main.maxTilesY - 5) {
					return false;
				}
				
				Rectangle rectangle = new Rectangle(0, 0, tileData2.Width, tileData2.Height);
				int num9 = 0;
				int num10 = 0;
				if (tileData2.AnchorTop.tileCount != 0) {
					if (rectangle.Y == 0) {
						rectangle.Y = -1;
						rectangle.Height++;
						num10++;
					}
					int checkStart = tileData2.AnchorTop.checkStart;
					if (checkStart < rectangle.X) {
						rectangle.Width += rectangle.X - checkStart;
						num9 += rectangle.X - checkStart;
						rectangle.X = checkStart;
					}
					int num11 = checkStart + tileData2.AnchorTop.tileCount - 1;
					int num12 = rectangle.X + rectangle.Width - 1;
					if (num11 > num12) {
						rectangle.Width += num11 - num12;
					}
				}
				
				if (onlyCheck) {
					TileObject.objectPreview.Reset();
					TileObject.objectPreview.Active = true;
					TileObject.objectPreview.Type = (ushort)type;
					TileObject.objectPreview.Style = (short)style;
					TileObject.objectPreview.Alternate = i;
					TileObject.objectPreview.Size = new Point16(rectangle.Width, rectangle.Height);
					TileObject.objectPreview.ObjectStart = new Point16(num9, num10);
					TileObject.objectPreview.Coordinates = new Point16(num7 - num9, num8 - num10);
				}
				
				float num21 = 0f;
				float num22 = (float)(tileData2.Width * tileData2.Height);
				float num23 = 0f;
				float num24 = 0f;
				for (int j = 0; j < tileData2.Width; j++) {
					for (int k = 0; k < tileData2.Height; k++) {
						Tile tileSafely = Framing.GetTileSafely(num7 + j, num8 + k);
						bool flag2 = !tileData2.LiquidPlace(tileSafely, checkStay);
						bool flag3 = false;
						if (tileData2.AnchorWall) {
							num24 += 1f;
							if (!tileData2.isValidWallAnchor(tileSafely.WallType)) {
								flag3 = true;
							} else {
								num23 += 1f;
							}
						}
						bool flag4 = false;
						if (tileSafely.HasTile && (!Main.tileCut[tileSafely.TileType] || tileSafely.TileType == 484) && !TileID.Sets.BreakableWhenPlacing[tileSafely.TileType] && !checkStay) {
							flag4 = true;
						}
						if (flag4 | flag2 | flag3) {
							if (onlyCheck) {
								TileObject.objectPreview[j + num9, k + num10] = 2;
							}
						} else {
							if (onlyCheck) {
								TileObject.objectPreview[j + num9, k + num10] = 1;
							}
							num21 += 1f;
						}
					}
				}
				
				AnchorData anchorData = tileData2.AnchorTop;
				if (anchorData.tileCount != 0) {
					num24 += (float)anchorData.tileCount;
					int num26 = -1;
					for (int m = 0; m < anchorData.tileCount; m++) {
						int num27 = anchorData.checkStart + m;
						Tile tileSafely3 = Framing.GetTileSafely(num7 + num27, num8 + num26);
						bool flag6 = false;
						if (tileSafely3.HasUnactuatedTile) {
							if (!flag6 && (anchorData.type & (AnchorType)256) == (AnchorType)256 && TileID.Sets.Platforms[tileSafely3.TileType]) {
								flag6 = tileData2.isValidTileAnchor(tileSafely3.TileType);
							}
							if (!flag6 && (anchorData.type & (AnchorType)512) == (AnchorType)512 && tileSafely3.TileType == 380) {
								flag6 = tileData2.isValidTileAnchor(tileSafely3.TileType);
							}
							if (!flag6 && (anchorData.type & AnchorType.AlternateTile) == AnchorType.AlternateTile && tileData2.isValidAlternateAnchor(tileSafely3.TileType)) {
								flag6 = true;
							}
						} else if (!flag6 && (anchorData.type & AnchorType.EmptyTile) == AnchorType.EmptyTile) {
							flag6 = true;
						}
						if (!flag6) {
							if (onlyCheck) {
								TileObject.objectPreview[num27 + num9, num26 + num10] = 2;
							}
						} else {
							if (onlyCheck) {
								TileObject.objectPreview[num27 + num9, num26 + num10] = 1;
							}
							num23 += 1f;
						}
					}
				}
				
				float num34 = num23 / num24;
				float num35 = num21 / num22;
				
				if (num35 == 1f && num24 == 0f) {
					num34 = 1f;
					num35 = 1f;
				}
				
				if (num34 == 1f && num35 == 1f) {
					num4 = 1f;
					num5 = 1f;
					num6 = i;
					tileObjectData = tileData2;
					break;
				}
				
				if (num34 > num4 || (num34 == num4 && num35 > num5)) {
					TileObjectPreviewData.placementCache.CopyFrom(TileObject.objectPreview);
					num4 = num34;
					num5 = num35;
					tileObjectData = tileData2;
					num6 = i;
				}
			}
				
			int num36 = -1;
			if (flag) {
				if (TileObjectPreviewData.randomCache == null) {
					TileObjectPreviewData.randomCache = new TileObjectPreviewData();
				}
				bool flag9 = false;
				if ((int)TileObjectPreviewData.randomCache.Type == type) {
					Point16 arg_11C3_0 = TileObjectPreviewData.randomCache.Coordinates;
					Point16 objectStart = TileObjectPreviewData.randomCache.ObjectStart;
					int num37 = (int)(arg_11C3_0.X + objectStart.X);
					int num38 = (int)(arg_11C3_0.Y + objectStart.Y);
					int num39 = x - (int)tileData.Origin.X;
					int num40 = y - (int)tileData.Origin.Y;
					if (num37 != num39 || num38 != num40) {
						flag9 = true;
					}
				} else {
					flag9 = true;
				}
				num36 = ((!flag9) ? TileObjectPreviewData.randomCache.Random : Main.rand.Next(tileData.RandomStyleRange));
			}
			if (onlyCheck) {
				if (num4 != 1f || num5 != 1f) {
					TileObject.objectPreview.CopyFrom(TileObjectPreviewData.placementCache);
					i = num6;
				}
				TileObject.objectPreview.Random = num36;
				if (tileData.RandomStyleRange > 0) {
					TileObjectPreviewData.randomCache.CopyFrom(TileObject.objectPreview);
				}
			}
			if (!onlyCheck)	{
				objectData.xCoord = x - (int)tileObjectData.Origin.X;
				objectData.yCoord = y - (int)tileObjectData.Origin.Y;
				objectData.type = type;
				objectData.style = style;
				objectData.alternate = i;
				objectData.random = num36;
			}
			
			if (num4 == 1f && num5 == 1f) {
				return true;
			}
			
			return orig(x, y, type, style, dir, out objectData, onlyCheck, checkStay);
			
		}
		
		private void OverwriteBannerObjectData() {
			
			bannerIDs.AddRange(new List<int>() { 91, 42, 270, 271, 572, 581 });
			
			for (int i = 0; i < bannerIDs.Count; i++) {
				TileObjectData.newTile.CopyFrom(TileObjectData.GetTileData(bannerIDs[i], 0));
				TileObjectData.newAlternate.CopyFrom(TileObjectData.GetTileData(bannerIDs[i], 0, 1));
				
				TileObjectData.newTile.AnchorTop = new AnchorData(TileObjectData.newTile.AnchorTop.type | (AnchorType)512, TileObjectData.newTile.AnchorTop.tileCount, TileObjectData.newTile.AnchorTop.checkStart);
				
				TileObjectData.newAlternate.AnchorTop = new AnchorData(TileObjectData.newAlternate.AnchorTop.type | (AnchorType)256, TileObjectData.newAlternate.AnchorTop.tileCount, TileObjectData.newAlternate.AnchorTop.checkStart);
				
				TileObjectData.newAlternate.DrawYOffset = -10;
				TileObjectData.addAlternate(0);
				TileObjectData.addTile(bannerIDs[i]);
			}
			
			hangableItems.AddRange(bannerIDs);
		}	
		
	}
}