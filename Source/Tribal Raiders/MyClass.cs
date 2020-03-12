using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace TribalRaiders_Code {

	public class Controller : Mod {
		public static Settings Settings;
		public override string SettingsCategory() { return "TribalRaiders.Title".Translate(); }
		public override void DoSettingsWindowContents(Rect canvas) { Settings.DoWindowContents(canvas); }
		public Controller(ModContentPack content) : base(content) {
			Harmony harmony = new Harmony("net.rainbeau.rimworld.mod.tribalraiders");
			harmony.PatchAll( Assembly.GetExecutingAssembly() );
			Settings = GetSettings<Settings>();
		}
	}

	public class Settings : ModSettings {
		public bool tribalPlanet = false;
		public void DoWindowContents(Rect canvas) {
			Listing_Standard list = new Listing_Standard();
			list.ColumnWidth = canvas.width;
			list.Begin(canvas);
			list.Gap();
			list.CheckboxLabeled( "TribalRaiders.TribalPlanet".Translate(), ref tribalPlanet, "TribalRaiders.TribalPlanetTip".Translate() );
			list.End();
		}
		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref tribalPlanet, "tribalPlanet", false);
		}
	}
	
	[HarmonyPatch(typeof(FactionGenerator), "GenerateFactionsIntoWorld", null)]
	public static class FactionGenerator_GenerateFactionsIntoWorld {
		public static bool Prefix() {
			if (Controller.Settings.tribalPlanet.Equals(false)) {
				return true;
			}
			else {
				int num = 0;
				foreach (FactionDef allDef in DefDatabase<FactionDef>.AllDefs) {
					if (allDef.defName == "OutlanderCivil" || allDef.defName == "OutlanderRough" || allDef.defName == "Pirate") { }
					else {
						for (int i = 0; i < allDef.requiredCountAtGameStart; i++) {
							Faction faction = FactionGenerator.NewGeneratedFaction(allDef);
							Find.FactionManager.Add(faction);
							if (!allDef.hidden) {
								num++;
							}
						}
					}
				}
				while (num < 5) {
					FactionDef factionDef = (
					from fa in DefDatabase<FactionDef>.AllDefs
					where (!fa.canMakeRandomly ? false : Find.FactionManager.AllFactions.Count<Faction>((Faction f) => f.def == fa) < fa.maxCountAtGameStart)
					select fa).RandomElement<FactionDef>();
					if (factionDef.defName == "OutlanderCivil" || factionDef.defName == "OutlanderRough" || factionDef.defName == "Pirate") { }
					else {
						Faction faction1 = FactionGenerator.NewGeneratedFaction(factionDef);
						Find.World.factionManager.Add(faction1);
						num++;
					}
				}
				float tilesCount = (float)Find.WorldGrid.TilesCount / 100000f;
				FloatRange settlementsPer100kTiles = new FloatRange(75f, 85f);
				int count = GenMath.RoundRandom(tilesCount * settlementsPer100kTiles.RandomInRange);
				count -= Find.WorldObjects.Settlements.Count;
				for (int j = 0; j < count; j++) {
					Faction faction2 = (
					from x in Find.World.factionManager.AllFactionsListForReading
					where (x.def.isPlayer ? false : !x.def.hidden)
					select x).RandomElementByWeight<Faction>((Faction x) => x.def.settlementGenerationWeight);
					Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
					settlement.SetFaction(faction2);
					settlement.Tile = TileFinder.RandomSettlementTileFor(faction2, false, null);
					settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement, null);
					Find.WorldObjects.Add(settlement);
				}
				return false;
			}
		}
	}

}
