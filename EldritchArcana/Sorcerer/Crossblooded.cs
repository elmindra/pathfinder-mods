// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.Utility;

namespace EldritchArcana
{
    static class CrossbloodedSorcerer
    {

        const String crossbloodedId = "3860be573aa848718fdb9d69f7ea821e";

        internal static BlueprintFeatureSelection bloodlineChoice1, bloodlineChoice2;

        internal static BlueprintProgression crossbloodProgression;

        internal static readonly Dictionary<BlueprintProgression, BlueprintProgression> crossbloodForBloodline = new Dictionary<BlueprintProgression, BlueprintProgression>();

        static LibraryScriptableObject library => Main.library;

        static BlueprintCharacterClass sorcererClass => Helpers.sorcererClass;
        static BlueprintCharacterClass dragonDiscipleClass => Helpers.dragonDiscipleClass;

        // Adds the Crossblooded Sorcerer archetype.
        internal static void Load()
        {
            var progressionClasses = new BlueprintCharacterClass[] { sorcererClass, dragonDiscipleClass };

            var crossblooded = Helpers.Create<BlueprintArchetype>(a =>
            {
                a.name = "CrossbloodedSorcererArchetype";
                a.LocalizedName = Helpers.CreateString("Crossblooded.Name", "Crossblooded Sorcerer");
                a.LocalizedDescription = Helpers.CreateString("Crossblooded.Description",
                    "A crossblooded sorcerer selects two different bloodlines. The sorcerer may gain access to the skills, feats, and some of the powers of both bloodlines they are descended from, but at the cost of reduced mental clarity and choice.");
            });
            library.AddAsset(crossblooded, crossbloodedId);
            crossblooded.SetIcon(sorcererClass.Icon);

            crossblooded.ReplaceSpellbook = CreateReplacementSpellbook();

            var bloodlineSelection = Helpers.bloodlineSelection;
            var bloodlines = bloodlineSelection.AllFeatures.Cast<BlueprintProgression>().ToList();

            var sorcererFeatSelection = library.Get<BlueprintFeatureSelection>("3a60f0c0442acfb419b0c03b584e1394");

            var powers = new SortedDictionary<int, SortedSet<BlueprintFeature>>();
            var spells = new SortedDictionary<int, SortedSet<BlueprintFeature>>();
            var powerLevels = new int[] { 1, 3, 9, 15, 20 };
            var spellLevels = new int[] { 3, 5, 7, 9, 11, 13, 15, 17, 19 };
            foreach (var level in powerLevels)
            {
                powers.Add(level, new SortedSet<BlueprintFeature>(BlueprintCompare.instance));
            }
            foreach (var level in spellLevels)
            {
                spells.Add(level, new SortedSet<BlueprintFeature>(BlueprintCompare.instance));
            }

            var crossbloodlines = new List<BlueprintProgression>();
            foreach (var bloodline in bloodlines)
            {
                var levelLogic = CrossBloodlineLogic.TryCreate(bloodline);
                if (levelLogic == null) continue;
                var crossbloodline = library.CopyAndAdd(bloodline, $"{bloodline.name}Cross", bloodline.AssetGuid, "933224357f8d48be837a3083e33a18a8");
                crossbloodline.SetName($"{bloodline.Name} (Crossblood)");
                crossbloodline.LevelEntries = new LevelEntry[] {
                    Helpers.LevelEntry(1, bloodline.GetLevelEntry(1).Features.Where(EldritchHeritage.IsArcanaOrClassSkill))
                };
                crossbloodline.Classes = progressionClasses;
                crossbloodline.Ranks = 1;
                crossbloodline.AddComponent(levelLogic);
                crossbloodlines.Add(crossbloodline);
                crossbloodForBloodline.Add(bloodline, crossbloodline);

                // Collect spells and abilities so we can build the choices for the main crossblooded progression.
                // (Note: we need to clone these, so we can change the prerequisites).
                foreach (var level in powerLevels)
                {
                    var power = CreateBloodlinePower(bloodline, level, crossbloodline);
                    powers[level].Add(power);
                }
                foreach (var level in spellLevels)
                {
                    var spell = CreateBloodlineSpell(bloodline, level, crossbloodline);
                    spells[level].Add(spell);
                }
            }

            FixBloodlinePrerequisites(crossblooded);

            crossbloodProgression = Helpers.CreateProgression("CrossbloodedProgression",
                crossblooded.Name, crossblooded.Description, "5f9f524c33ae4df780c54189ab9642b6",
                crossblooded.Icon, FeatureGroup.None);

            crossbloodProgression.Classes = progressionClasses;

            var entries = new List<LevelEntry>();
            var powerGroup = new List<BlueprintFeatureBase>();
            var spellGroup = new List<BlueprintFeatureBase>();

            var allPowerChoices = new List<BlueprintFeature>();
            var allSpellChoices = new List<BlueprintFeature>();
            foreach (var level in new SortedSet<int>(powerLevels.Concat(spellLevels)))
            {
                var entry = Helpers.LevelEntry(level);
                entries.Add(entry);

                SortedSet<BlueprintFeature> powerChoices;
                if (powers.TryGetValue(level, out powerChoices))
                {
                    var description = "At 1st, 3rd, 9th, 15th, and 20th levels, a crossblooded sorcerer gains one of the two new bloodline powers available to their at that level. They may instead select a lower-level bloodline power they did not choose in place of one of these higher-level powers.";
                    var powerSelection = Helpers.CreateFeatureSelection($"BloodlinePowerSelection{level}",
                        "Bloodline Power",
                        DescribeChoices(description, powerChoices),
                        Helpers.MergeIds(guidsByLevel[level], "15029e64baee4db6b09ca6a6ed2d70c0"),
                        null,
                        FeatureGroup.None);
                    //powerSelection.HideInUI = true;

                    allPowerChoices.AddRange(powerChoices);
                    powerSelection.SetFeatures(allPowerChoices);
                    powerGroup.Add(powerSelection);
                    entry.Features.Add(powerSelection);
                }
                SortedSet<BlueprintFeature> spellChoices;
                if (spells.TryGetValue(level, out spellChoices))
                {
                    var description = "A crossblooded sorcerer may select their bonus spells from either of their bloodlines. The sorcerer also has the choice to learn a lower-level bonus spell they did not choose in place of the higher-level bonus spell they would normally gain. Lower-level bonus spells learned this way always use the spell level that they would be if the sorcerer had learned them with the appropriate bonus spell.";
                    var spellSelection = Helpers.CreateFeatureSelection($"BloodlineSpellSelection{level}",
                        "Bloodline Spell",
                        DescribeChoices(description, spellChoices),
                        Helpers.MergeIds(guidsByLevel[level], "d333e2fb82ab4ab4af7d03d84aa5895c"),
                        null,
                        FeatureGroup.None);
                    //spellSelection.HideInUI = true;

                    allSpellChoices.AddRange(spellChoices);
                    spellSelection.SetFeatures(allSpellChoices);
                    spellGroup.Add(spellSelection);
                    entry.Features.Add(spellSelection);
                }
            }

            crossbloodProgression.LevelEntries = entries.ToArray();
            crossbloodProgression.UIGroups = new UIGroup[] { Helpers.CreateUIGroup(spellGroup), Helpers.CreateUIGroup(powerGroup) };

            bloodlineChoice1 = Helpers.CreateFeatureSelection($"{bloodlineSelection.name}Cross1",
                bloodlineSelection.Name, bloodlineSelection.Description,
                "08cbfcda615f49218d3b118fc9322b81",
                bloodlineSelection.Icon,
                FeatureGroup.BloodLine);
            bloodlineChoice1.SetFeatures(crossbloodlines);

            bloodlineChoice2 = Helpers.CreateFeatureSelection($"{bloodlineSelection.name}Cross2",
                bloodlineSelection.Name, bloodlineSelection.Description,
                "ef595c20be9745b698e25e5a33b7d443",
                bloodlineSelection.Icon,
                FeatureGroup.BloodLine);
            bloodlineChoice2.SetFeatures(crossbloodlines);

            var level1 = Helpers.LevelEntry(1,
                crossbloodProgression,
                Helpers.CreateFeature("CrossbloodedConflictingUrges", "Crossblooded (conflicting urges)",
                    "The conflicting urges created by the divergent nature of the crossblooded sorcerer’s dual heritage forces them to constantly take some mental effort just to remain focused on their current situation and needs. This leaves them with less mental resolve to deal with external threats. A crossblooded sorcerer always takes a –2 penalty on Will saves.",
                    "ebefc29906f74c0d9a8b914095cc05d6",
                    Helpers.GetIcon("983e8ad193160b44da80b38af4927e75"), // Diverse Training
                    FeatureGroup.None,
                    Helpers.CreateAddStatBonus(StatType.SaveWill, -2, ModifierDescriptor.Penalty)),
                bloodlineChoice1,
                bloodlineChoice2);

            crossblooded.RemoveFeatures = new LevelEntry[] { Helpers.LevelEntry(1, bloodlineSelection) };
            crossblooded.AddFeatures = new LevelEntry[] { level1 };

            crossblooded.BaseAttackBonus = sorcererClass.BaseAttackBonus;
            crossblooded.FortitudeSave = sorcererClass.FortitudeSave;
            crossblooded.ReflexSave = sorcererClass.ReflexSave;
            crossblooded.WillSave = sorcererClass.WillSave;

            Helpers.SetField(crossblooded, "m_ParentClass", sorcererClass);

            var archetypes = sorcererClass.Archetypes.ToList();
            archetypes.Insert(0, crossblooded);
            sorcererClass.Archetypes = archetypes.ToArray();

            // Fix prestige class spellbook selection
            // TODO: find these automatically, or it won't be compatible with new prestige classes.
            FixSpellbookSelection(crossblooded, "ae04b7cdeb88b024b9fd3882cc7d3d76", "9ff7ad30e5a074346a40f80efda277c8", FeatureGroup.ArcaneTricksterSpellbook);
            FixSpellbookSelection(crossblooded, "8c1ba14c0b6dcdb439c56341385ee474", "fa2a2469c9ba6d54b8fc2356f4fc0e9e", FeatureGroup.DragonDiscipleSpellbook);
            FixSpellbookSelection(crossblooded, "dc3ab8d0484467a4787979d93114ebc3", "89d2b9f096b54804c8350dd2a899f8a4", FeatureGroup.EldritchKnightSpellbook);
            FixSpellbookSelection(crossblooded, "97f510c6483523c49bc779e93e4c4568", "e8013bf2853590f4fba12b4b57366bcc", FeatureGroup.MysticTheurgeArcaneSpellbook);

            // Fix the SpellBookView icon support for archetypes.
            Main.ApplyPatch(typeof(UIUtilityUnit_CollectClassDeterminators_Patch), "Crossblooded bloodline icon in spellbook UI");
        }

        static void FixBloodlinePrerequisites(BlueprintArchetype crossblooded)
        {
            // Fix sorcerer bonus feats.
            var sorcererFeatSelection = library.Get<BlueprintFeatureSelection>("3a60f0c0442acfb419b0c03b584e1394");
            sorcererFeatSelection.AllFeatures.ForEach(FixBloodlinePrerequisite);

            // Fix blood of dragon selection: add draconic crossbloodlines.
            var bloodOfDragonsSelection = library.Get<BlueprintFeatureSelection>("da48f9d7f697ae44ca891bfc50727988");
            var draconicBloodlines = bloodOfDragonsSelection.AllFeatures.ToArray();
            var draconicCrossbloods = draconicBloodlines.Select(f => crossbloodForBloodline[f as BlueprintProgression]).ToArray();
            bloodOfDragonsSelection.SetFeatures(draconicBloodlines.Concat(draconicCrossbloods));
            foreach (var draconic in draconicCrossbloods)
            {
                // Ensure the crossblood ones can only be chosen if this is a crossblooded sorcerer.
                draconic.AddComponent(Helpers.Create<PrerequisiteArchetypeLevel>(p =>
                {
                    p.CharacterClass = sorcererClass;
                    p.Archetype = crossblooded;
                    p.Level = 1;
                }));
            }

            // Fix dragon disciple features.
            var dragonDiscipleProgression = library.Get<BlueprintProgression>("69fc2bad2eb331346a6c777423e0d0f7");
            foreach (var entry in dragonDiscipleProgression.LevelEntries)
            {
                foreach (var feat in entry.Features.OfType<BlueprintFeature>())
                {
                    FixDragonDisciple(feat, draconicBloodlines);
                }
            }
        }

        static void FixDragonDisciple(BlueprintFeature feat, BlueprintFeature[] bloodlines)
        {
            feat.AddComponents(
                feat.GetComponents<AddFeatureIfHasFact>()
                .Where(a => bloodlines.Contains(a.CheckedFact))
                .Select(addFeat =>
            {
                var newAdd = UnityEngine.Object.Instantiate(addFeat);
                newAdd.CheckedFact = crossbloodForBloodline[(BlueprintProgression)addFeat.CheckedFact];
                return newAdd;
            }));

            foreach (var addOnClass in feat.GetComponents<AddFeatureOnClassLevel>())
            {
                FixDragonDisciple(addOnClass.Feature, bloodlines);
            }
        }

        static void FixBloodlinePrerequisite(BlueprintFeature feat)
        {
            var components = feat.ComponentsArray.Where(c => !(c is PrerequisiteFeature || c is PrerequisiteFeaturesFromList)).ToList();
            foreach (var p in feat.GetComponents<PrerequisiteFeature>())
            {
                BlueprintProgression crossbloodline;
                if (crossbloodForBloodline.TryGetValue(p.Feature as BlueprintProgression, out crossbloodline))
                {
                    components.Add(Helpers.PrerequisiteFeaturesFromList(p.Feature, crossbloodline));
                }
                else
                {
                    components.Add(p);
                }
            }
            foreach (var prereq in feat.GetComponents<PrerequisiteFeaturesFromList>().ToList())
            {
                var features = prereq.Features.ToList();
                foreach (var f in prereq.Features)
                {
                    BlueprintProgression crossbloodline;
                    if (crossbloodForBloodline.TryGetValue(f as BlueprintProgression, out crossbloodline))
                    {
                        features.Add(crossbloodline);
                    }
                }
                components.Add(Helpers.PrerequisiteFeaturesFromList(features));
            }
            feat.SetComponents(components);
        }

        static String DescribeChoices(String description, IEnumerable<BlueprintFeature> features)
        {
            var str = new StringBuilder(description);
            str.Append("\nChoices for each bloodline:");
            var seenDraconic = false;
            var seenElemental = false;
            foreach (var f in features)
            {
                foreach (var prereq in f.GetComponents<PrerequisiteFeature>())
                {
                    var bloodline = prereq.Feature;
                    var displayName = bloodline.Name;
                    if (bloodline.name.StartsWith("CrossBloodlineDraconic"))
                    {
                        if (seenDraconic) continue;

                        var i = displayName.IndexOf(" — ");
                        if (i >= 0) displayName = displayName.Substring(0, i);
                        seenDraconic = true;
                    }
                    else if (bloodline.name.StartsWith("CrossBloodlineElemental"))
                    {
                        if (seenElemental) continue;
                        var i = displayName.IndexOf(" — ");
                        if (i >= 0) displayName = displayName.Substring(0, i);
                        seenElemental = true;
                    }
                    str.Append($"\n• {displayName}: {f.Name}");
                }
            }
            return str.ToString();
        }


        static BlueprintSpellsTable CreateSpellsKnown(BlueprintSpellbook spellbook)
        {
            var baseSpellLevels = spellbook.SpellsKnown.Levels;
            var spellLevels = new SpellsLevelEntry[baseSpellLevels.Length];
            for (int i = 0; i < spellLevels.Length; i++)
            {
                var known = baseSpellLevels[i].Count.ToArray();
                for (int j = 1; j < known.Length; j++)
                {
                    // Reduce known spells by 1, but keep the minimum per level at 1.
                    //
                    // In PnP Crossblooded starts each spell level with zero spells known. But that seems to bug
                    // the game UI (it fails to allow any spells to be added later). Perhaps it computes the
                    // "max spell level" incorrectly for the spellbook. This might be fixable with some patching
                    // (e.g. ApplySpellbook.Apply).
                    //
                    // Another issue: in PnP a sorcerer can cast lower level spells in higher level slots without
                    // a feat, so they're able to use those slots. In PF:K that isn't possible (unless a no-op
                    // metamagic is added) so I'd rather they are able to cast something. Last but not least, the
                    // penalty is considered rather harsh in PnP, so it seems reasonable to skip it for balance
                    // reasons.
                    known[j] = Math.Max(known[j] - 1, 1);
                }
                spellLevels[i] = new SpellsLevelEntry() { Count = known };
            }
            return Helpers.Create<BlueprintSpellsTable>(s => s.Levels = spellLevels);
        }

        static BlueprintSpellbook CreateReplacementSpellbook()
        {
            var sorcererSpellbook = sorcererClass.Spellbook;
            var spellbook = library.CopyAndAdd(sorcererClass.Spellbook, "CrossbloodedSorcererSpellbook", "c6e6bf377e694afbb854b7388c5b53dd");
            spellbook.SpellsKnown = CreateSpellsKnown(sorcererSpellbook);
            return spellbook;
        }

        static BlueprintFeature CreateBloodlinePower(BlueprintProgression bloodline, int level, BlueprintProgression crossbloodline)
        {
            var power = EldritchHeritage.GetBloodlinePower(bloodline, level);
            var entries = EldritchHeritage.CollectLevelEntries(level, power, bloodline);
            if (entries.Count == 1)
            {
                return CreateCrossbloodedFeature(entries[0].Item2, crossbloodline);
            }

            var name = power.name;
            if (name.EndsWith("1")) name = name.Substring(0, power.name.Length - 1);

            var feature = Helpers.CreateFeature($"{name}Cross", power.Name, power.Description,
                Helpers.MergeIds(power.AssetGuid, "3b983f0653914618844275e20d9fe561"),
                power.Icon, FeatureGroup.None);

            var components = new List<BlueprintComponent> { crossbloodline.PrerequisiteFeature() };
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                components.Add(AddFactOnBloodlineLevel.Create(e.Item2, $"${i}", e.Item1));
            }
            feature.SetComponents(components);
            return feature;
        }

        static BlueprintFeature CreateBloodlineSpell(BlueprintProgression bloodline, int level, BlueprintProgression crossbloodline)
        {
            foreach (var feat in bloodline.GetLevelEntry(level).Features)
            {
                var addSpell = feat.GetComponents<AddKnownSpell>().FirstOrDefault(s => s.CharacterClass == sorcererClass);
                if (addSpell == null) continue;

                var result = CreateCrossbloodedFeature((BlueprintFeature)feat, crossbloodline);
                var spell = addSpell.Spell;
                if (!result.GetComponents<PrerequisiteSpellKnown>().Any(s => s.Spell == spell && s.Not))
                {
                    result.AddComponent(PrerequisiteSpellKnown.Create(spell, true));
                }
                spell.AddRecommendNoFeature(crossbloodline);
                return result;
            }
            throw Main.Error($"could not find level {level} spell for ${bloodline.name}");
        }

        static BlueprintFeature CreateCrossbloodedFeature(BlueprintFeature f, BlueprintFeature bloodline)
        {
            // Copies abilities so we can alter the prereqs.
            var featureId = Helpers.MergeIds(f.AssetGuid, "fd07f7d6fc6c420ab7e1054822d62636");
            var feature = library.TryGet<BlueprintFeature>(featureId);
            if (feature != null)
            {
                if (feature.GetComponents<PrerequisiteFeature>().Any(p => p.Feature == bloodline))
                {
                    feature.Ranks++;
                }
                else
                {
                    feature.AddComponent(Helpers.PrerequisiteFeature(bloodline, any: true));
                }
                return feature;
            }

            var selection = f as BlueprintFeatureSelection;
            if (selection != null)
            {
                var components = f.ComponentsArray.ToList();
                var noFeature = Helpers.PrerequisiteNoFeature(null);
                components.Add(noFeature);
                components.Add(Helpers.PrerequisiteFeature(bloodline, any: true));
                var newSelection = library.CopyAndAdd(selection, $"{f.name}Cross", featureId);
                newSelection.AddComponents(components);
                noFeature.Feature = newSelection;
                newSelection.SetFeatures(selection.AllFeatures.AddToArray(UndoSelection.Feature.Value));
                feature = newSelection;
            }
            else
            {
                feature = Helpers.CreateFeature($"{f.name}Cross", f.Name, f.Description,
                    featureId,
                    f.Icon,
                    FeatureGroup.None,
                    Helpers.CreateAddFacts(f),
                    Helpers.PrerequisiteFeature(bloodline, any: true));
            }
            //feature.HideInUI = true;
            feature.Ranks = 1;
            feature.Groups = f.Groups;
            return feature;
        }

        static void FixSpellbookSelection(BlueprintArchetype archetype, String spellSelectionId, String sorcererSelectionId, FeatureGroup group)
        {
            var spellSelection = (BlueprintFeatureSelection)library.BlueprintsByAssetId[spellSelectionId];

            var sorcererClass = archetype.GetParentClass();
            var sorcererFeature = spellSelection.AllFeatures.Cast<BlueprintFeatureReplaceSpellbook>()
                .First(f => f.AssetGuid == sorcererSelectionId);

            // Restrict sorcerer feat so it can't be selected with this archetype
            var sorcererPrereqs = sorcererFeature.ComponentsArray.ToList();
            sorcererPrereqs.Add(Helpers.Create<PrerequisiteNoArchetype>(p => { p.CharacterClass = sorcererClass; p.Archetype = archetype; }));
            sorcererFeature.SetComponents(sorcererPrereqs);

            // Create a new feature for this archetype's spellbook
            var spellbookFeature = library.CopyAndAdd(sorcererFeature, group.ToString().Replace("Spellbook", archetype.name),
                sorcererFeature.AssetGuid, "7d400c2c080947ecb0a1052b453bc107");
            spellbookFeature.Spellbook = archetype.ReplaceSpellbook;
            spellbookFeature.SetName(archetype.LocalizedName);

            // Update the prerequisites.
            spellbookFeature.SetComponents(
                Helpers.Create<PrerequisiteArchetypeLevel>(p =>
                {
                    p.CharacterClass = sorcererClass;
                    p.Archetype = archetype;
                }),
                Helpers.Create<PrerequisiteClassSpellLevel>(p =>
                {
                    p.CharacterClass = sorcererClass;
                    p.RequiredSpellLevel = 1;
                }));

            // Add to the list of all features for this selector.
            var allFeatures = spellSelection.AllFeatures.ToList();
            allFeatures.Add(spellbookFeature);
            spellSelection.AllFeatures = allFeatures.ToArray();
        }

        internal static readonly String[] guidsByLevel = new String[] {
            null, // level 0 unused
            "b61c6a369b3e47f4949787858e341423",
            "f1c1ad606b6a4624a0f59df697dd0bdd",
            "39c8d5fdc3de4c1e9b63d7b7c9815253",
            "f1897b53e7914316b3168ba3e9d1288e",
            "b64b1377964c4efc911b0e17d27ab824",
            "c0802049a699454e801532469202e82a",
            "b2a6b09f30234dbeae691bab08c10037",
            "a4501ff2f75444f39ebfd52951590f7f",
            "2a5c10befb2c4334bff02bccd40d8feb",
            "691cfac8f78841e887940ce113c8e66c",
            "d5dec997cdd94410b4191b274b2eb96f",
            "82e64363ff8843888e873b32ae4e7184",
            "d763a60ac10f4d17b1c4fce59aef6c37",
            "75e424118c0c4042acf9fcd8568b6ac9",
            "6e37ef2f670c4cb387d7772c2baa63c8",
            "b5cd8527ad124be2a519b7446790c7d7",
            "2522486946304644bd05e537ca9a04aa",
            "0e86fb7b76764416bc9d0def50105651",
            "c9b887972ce546aaa5f662ff3b227b6b",
            "bc7ecebc22834ab4b5b4320fbaee7145",
        };
    }


    class BlueprintCompare : IComparer<BlueprintScriptableObject>
    {
        public static BlueprintCompare instance = new BlueprintCompare();
        public int Compare(BlueprintScriptableObject x, BlueprintScriptableObject y) => x.name.CompareTo(y.name);
    }

    class CrossbloodSpellSelection : CustomParamSelection
    {
        internal static int[] spellLevels = new int[] { 3, 5, 7, 9, 11, 13, 15, 17, 19 };

        protected override IEnumerable<BlueprintScriptableObject> GetItems(UnitDescriptor beforeLevelUpUnit, UnitDescriptor previewUnit)
        {
            var unit = previewUnit;
            var bloodlines = unit.Progression.GetSelectionData(CrossbloodedSorcerer.bloodlineChoice1).GetSelections(1).Concat(
                unit.Progression.GetSelectionData(CrossbloodedSorcerer.bloodlineChoice2).GetSelections(1)).ToList();

            var crossbloodLevel = unit.Progression.SureProgressionData(CrossbloodedSorcerer.crossbloodProgression).Level;
            var spellbook = unit.DemandSpellbook(Helpers.sorcererClass);

            var result = new List<BlueprintScriptableObject>();
            foreach (var bloodline in bloodlines.Cast<BlueprintProgression>())
            {
                foreach (var level in spellLevels)
                {
                    if (level > crossbloodLevel) break;
                    foreach (var feature in bloodline.GetLevelEntry(crossbloodLevel).Features)
                    {
                        var addSpell = feature.GetComponents<AddKnownSpell>().FirstOrDefault(s => s.CharacterClass == Helpers.sorcererClass);
                        if (addSpell != null && !spellbook.IsKnown(addSpell.Spell))
                        {
                            result.Add(feature);
                        }
                    }
                }
            }
            return result;
        }

        protected override IEnumerable<BlueprintScriptableObject> GetAllItems() => Helpers.allSpells;
    }

    [AllowMultipleComponents]
    public class PrerequisiteSpellKnown : CustomPrerequisite
    {
        public BlueprintAbility Spell;
        public bool Not;

        public static PrerequisiteSpellKnown Create(BlueprintAbility spell, bool not)
        {
            var p = Helpers.Create<PrerequisiteSpellKnown>();
            p.Spell = spell;
            p.Not = not;
            return p;
        }

        public override bool Check(FeatureSelectionState selectionState, UnitDescriptor unit, LevelUpState state)
        {
            foreach (var spellbook in unit.Spellbooks)
            {
                if (spellbook.IsKnown(Spell)) return !Not;
            }
            return Not;
        }

        public override String GetCaption() => Not ? $"Doesn't know spell: {Spell.Name}" : $"Knows spell: {Spell.Name}";
    }

    [Harmony12.HarmonyPatch(typeof(UIUtilityUnit), "CollectClassDeterminator", typeof(BlueprintProgression), typeof(UnitProgressionData))]
    static class UIUtilityUnit_CollectClassDeterminators_Patch
    {
        // This method is only used by `SpellBookView.SetupHeader()` to find information such
        // as spellbook bloodlines, cleric deity, etc.
        //
        // Unfortunately, it doesn't understand how to find data from the archetype.
        // So this patch addresses it.
        static void Postfix(BlueprintProgression progression, UnitProgressionData progressionData, ref List<BlueprintFeatureBase> __result)
        {
            try
            {
                var @class = Helpers.classes.FirstOrDefault(c => c.Progression == progression);
                if (@class == null) return;

                var list = __result ?? new List<BlueprintFeatureBase>();
                var archetype = @class.Archetypes.FirstOrDefault(progressionData.IsArchetype);
                if (archetype == null) return;

                foreach (var entry in archetype.RemoveFeatures)
                {
                    foreach (var feat in entry.Features.OfType<BlueprintFeatureSelection>().Where(f => !f.HideInUI))
                    {
                        var selections = progressionData.GetSelections(feat, entry.Level);
                        list.RemoveAll(f => f == feat || selections.Contains(f));
                    }
                }
                foreach (var entry in archetype.AddFeatures)
                {
                    foreach (var feat in entry.Features.OfType<BlueprintFeatureSelection>().Where(f => !f.HideInUI))
                    {
                        var selections = progressionData.GetSelections(feat, entry.Level);
                        foreach (var s in selections)
                        {
                            if (IsDeterminator(s) && !list.Contains(s)) list.Add(s);
                        }
                        if (selections.Count == 0 && IsDeterminator(feat)) list.Add(feat);
                    }
                }
                __result = list.Count == 0 ? null : list;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        static bool IsDeterminator(BlueprintFeature feat)
        {
            switch (UIUtilityUnit.GetFeatureGroup(feat))
            {
                case FeatureGroup.Domain:
                case FeatureGroup.SpecialistSchool:
                case FeatureGroup.OppositionSchool:
                case FeatureGroup.BloodLine:
                case FeatureGroup.MagusArcana:
                case FeatureGroup.EldritchScionArcana:
                case FeatureGroup.DraconicBloodlineSelection:
                case FeatureGroup.BlightDruidDomain:
                case FeatureGroup.ClericSecondaryDomain:
                    return true;
                default:
                    return false;
            }
        }
    }

    public class CrossBloodlineLogic : BloodlineLevelLogic
    {
        public static CrossBloodlineLogic TryCreate(BlueprintProgression bloodline)
        {
            var h = Helpers.Create<CrossBloodlineLogic>();
            return h.SetBloodlineName(bloodline) ? h : null;
        }

        public override int CalcLevel()
        {
            return Owner.Progression.GetClassLevel(Helpers.sorcererClass) +
                Owner.Progression.GetClassLevel(Helpers.dragonDiscipleClass);
        }
    }
}
