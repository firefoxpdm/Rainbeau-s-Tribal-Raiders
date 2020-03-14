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


    public class BackstoryDef : Def
    {
        #region XML Data
        public string baseDesc;
        public string bodyTypeGlobal;
        public string bodyTypeMale;
        public string bodyTypeFemale;
        public string title;
        public string titleShort;
        public BackstorySlot slot = BackstorySlot.Adulthood;
        public bool shuffleable = true;
        public bool addToDatabase = true;
        public List<WorkTags> workAllows = new List<WorkTags>();
        public List<WorkTags> workDisables = new List<WorkTags>();
        public List<WorkTags> requiredWorkTags = new List<WorkTags>();
        public List<BackstoryDefSkillListItem> skillGains = new List<BackstoryDefSkillListItem>();
        public List<string> spawnCategories = new List<string>();
        public List<BackstoryDefTraitListItem> forcedTraits = new List<BackstoryDefTraitListItem>();
        public List<BackstoryDefTraitListItem> disallowedTraits = new List<BackstoryDefTraitListItem>();
        #endregion
        public static BackstoryDef Named(string defName)
        {
            return DefDatabase<BackstoryDef>.GetNamed(defName);
        }
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (!this.addToDatabase) return;
            if (BackstoryDatabase.allBackstories.ContainsKey(this.UniqueSaveKey()))
            {
                Log.Error("Backstory Error (" + this.defName + "): Duplicate defName.");
                return;
            }
            Backstory b = new Backstory();
            if (!this.title.NullOrEmpty())
                b.SetTitle(this.title, this.title);
            else
            {
                return;
            }
            if (!titleShort.NullOrEmpty())
                b.SetTitleShort(titleShort, titleShort);
            else
                b.SetTitleShort(b.title, b.title);
            if (!baseDesc.NullOrEmpty())
                b.baseDesc = baseDesc;
            else
            {
                b.baseDesc = "Empty.";
            }
            bool bodyTypeSet = false;
            if (!string.IsNullOrEmpty(bodyTypeGlobal))
            {
                bodyTypeSet = SetGlobalBodyType(b, bodyTypeGlobal);
            }
            if (!bodyTypeSet)
            {
                if (!SetMaleBodyType(b, bodyTypeMale))
                {
                    SetMaleBodyType(b, "Male");
                }
                if (!SetFemaleBodyType(b, bodyTypeFemale))
                {
                    SetFemaleBodyType(b, "Female");
                }
            }
            b.slot = slot;
            b.shuffleable = shuffleable;
            if (spawnCategories.NullOrEmpty())
            {
                return;
            }
            else
                b.spawnCategories = spawnCategories;
            if (workAllows.Count > 0)
            {
                foreach (WorkTags current in Enum.GetValues(typeof(WorkTags)))
                {
                    if (!workAllows.Contains(current))
                    {
                        b.workDisables |= current;
                    }
                }
            }
            else if (workDisables.Count > 0)
            {
                foreach (var tag in workDisables)
                {
                    b.workDisables |= tag;
                }
            }
            else
            {
                b.workDisables = WorkTags.None;
            }
            if (requiredWorkTags.Count > 0)
            {
                foreach (var tag in requiredWorkTags)
                {
                    b.requiredWorkTags |= tag;
                }
            }
            else
            {
                b.requiredWorkTags = WorkTags.None;
            }
            Dictionary<SkillDef, int> skillDefs = new Dictionary<SkillDef, int>();
            foreach (BackstoryDefSkillListItem skillGain in this.skillGains)
            {
                SkillDef named = DefDatabase<SkillDef>.GetNamed(skillGain.key, false);
                if (named == null)
                {
                    Log.Error(string.Concat(new string[] { "Tribal Raiders: Unable to find SkillDef of [", skillGain.key, "] for Backstory.Title [", b.title, "]" }), false);
                    continue;
                }
                skillDefs.Add(named, skillGain.@value);
            }
            b.skillGainsResolved = skillDefs;
            Dictionary<string, int> fTraitList = forcedTraits.ToDictionary(i => i.key, i => i.value);
            if (fTraitList.Count > 0)
            {
                b.forcedTraits = new List<TraitEntry>();
                foreach (KeyValuePair<string, int> trait in fTraitList)
                {
                    b.forcedTraits.Add(new TraitEntry(TraitDef.Named(trait.Key), trait.Value));
                }
            }
            Dictionary<string, int> dTraitList = disallowedTraits.ToDictionary(i => i.key, i => i.value);
            if (dTraitList.Count > 0)
            {
                b.disallowedTraits = new List<TraitEntry>();
                foreach (KeyValuePair<string, int> trait in dTraitList)
                {
                    b.disallowedTraits.Add(new TraitEntry(TraitDef.Named(trait.Key), trait.Value));
                }
            }
            b.ResolveReferences();
            b.PostLoad();
            b.identifier = this.UniqueSaveKey();
            bool flag2 = false;
            foreach (var s in b.ConfigErrors(false))
            {
                Log.Error("Backstory Error (" + b.identifier + "): " + s);
                if (!flag2)
                {
                    flag2 = true;
                }
            }
            if (!flag2)
            {
                BackstoryDatabase.allBackstories.Add(b.identifier, b);
            }
        }
        private static bool SetGlobalBodyType(Backstory b, string s)
        {
            BodyTypeDef def;
            if (TryGetBodyTypeDef(s, out def))
            {
                typeof(Backstory).GetField("bodyTypeGlobal", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(b, def.ToString());
                typeof(Backstory).GetField("bodyTypeGlobalResolved", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(b, def);
                return true;
            }
            return false;
        }
        private static bool SetMaleBodyType(Backstory b, string s)
        {
            BodyTypeDef def;
            if (TryGetBodyTypeDef(s, out def))
            {
                typeof(Backstory).GetField("bodyTypeMale", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(b, def.ToString());
                typeof(Backstory).GetField("bodyTypeMaleResolved", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(b, def);
                return true;
            }
            return false;
        }
        private static bool SetFemaleBodyType(Backstory b, string s)
        {
            BodyTypeDef def;
            if (TryGetBodyTypeDef(s, out def))
            {
                typeof(Backstory).GetField("bodyTypeFemale", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(b, def.ToString());
                typeof(Backstory).GetField("bodyTypeFemaleResolved", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(b, def);
                return true;
            }
            return false;
        }
        private static bool TryGetBodyTypeDef(string s, out BodyTypeDef def)
        {
            if (string.IsNullOrEmpty(s))
            {
                def = null;
                return false;
            }
            def = DefDatabase<BodyTypeDef>.GetNamed(s, false);
            if (def == null)
                return false;
            return true;
        }
    }

    public static class BackstoryDefExt
    {
        public static string UniqueSaveKey(this BackstoryDef def)
        {
            return "TR_" + def.defName;
        }
    }

    public struct BackstoryDefSkillListItem
    {
        public string key;
        public int value;
    }

    public struct BackstoryDefTraitListItem
    {
        public string key;
        public int value;
    }
}
