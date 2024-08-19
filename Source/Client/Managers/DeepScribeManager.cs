using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Shared;
using UnityEngine.Assertions.Must;
using Verse;

namespace GameClient
{
    //Class that handles transformation of humans

    public static class HumanScribeManager
    {
        //Functions

        public static Pawn[] GetHumansFromString(TransferData transferData)
        {
            List<Pawn> humans = new List<Pawn>();

            for (int i = 0; i < transferData.HumanDatas.Count(); i++) humans.Add(StringToHuman(transferData.HumanDatas[i]));

            return humans.ToArray();
        }

        public static HumanData HumanToString(Pawn pawn, bool passInventory = true)
        {
            HumanData humanData = new HumanData();

            GetPawnBioDetails(pawn, humanData);

            GetPawnKind(pawn, humanData);

            GetPawnFaction(pawn, humanData);

            GetPawnHediffs(pawn, humanData);

            if (ModsConfig.BiotechActive)
            {
                GetPawnChildState(pawn, humanData);

                GetPawnXenotype(pawn, humanData);

                GetPawnXenogenes(pawn, humanData);

                GetPawnEndogenes(pawn, humanData);
            }

            GetPawnStory(pawn, humanData);

            GetPawnSkills(pawn, humanData);

            GetPawnTraits(pawn, humanData);

            GetPawnApparel(pawn, humanData);

            GetPawnEquipment(pawn, humanData);

            if (passInventory) GetPawnInventory(pawn, humanData);

            GetPawnFavoriteColor(pawn, humanData);

            GetPawnPosition(pawn, humanData);

            GetPawnRotation(pawn, humanData);

            return humanData;
        }

        public static Pawn StringToHuman(HumanData humanData)
        {
            PawnKindDef kind = SetPawnKind(humanData);

            Faction faction = SetPawnFaction(humanData);

            Pawn pawn = SetPawn(kind, faction, humanData);

            SetPawnHediffs(pawn, humanData);

            if (ModsConfig.BiotechActive)
            {
                SetPawnChildState(pawn, humanData);

                SetPawnXenotype(pawn, humanData);

                SetPawnXenogenes(pawn, humanData);

                SetPawnEndogenes(pawn, humanData);
            }

            SetPawnBioDetails(pawn, humanData);

            SetPawnStory(pawn, humanData);

            SetPawnSkills(pawn, humanData);

            SetPawnTraits(pawn, humanData);

            SetPawnApparel(pawn, humanData);

            SetPawnEquipment(pawn, humanData);

            SetPawnInventory(pawn, humanData);

            SetPawnFavoriteColor(pawn, humanData);

            SetPawnPosition(pawn, humanData);

            SetPawnRotation(pawn, humanData);

            return pawn;
        }

        //Getters

        private static void GetPawnBioDetails(Pawn pawn, HumanData humanData)
        {
            try
            {
                humanData.DefName = pawn.def.defName;
                humanData.Name = pawn.LabelShortCap.ToString();
                humanData.BiologicalAge = pawn.ageTracker.AgeBiologicalTicks.ToString();
                humanData.ChronologicalAge = pawn.ageTracker.AgeChronologicalTicks.ToString();
                humanData.Gender = pawn.gender.ToString();
                
                humanData.HairDefName = pawn.story.hairDef.defName.ToString();
                humanData.HairColor = pawn.story.HairColor.ToString();
                humanData.HeadTypeDefName = pawn.story.headType.defName.ToString();
                humanData.SkinColor = pawn.story.SkinColor.ToString();
                humanData.BeardDefName = pawn.style.beardDef.defName.ToString();
                humanData.BodyTypeDefName = pawn.story.bodyType.defName.ToString();
                humanData.FaceTattooDefName = pawn.style.FaceTattoo.defName.ToString();
                humanData.BodyTattooDefName = pawn.style.BodyTattoo.defName.ToString();
            }
            catch { Logger.Warning($"Failed to get biological details from human {pawn.Label}"); }
        }

        private static void GetPawnKind(Pawn pawn, HumanData humanData)
        {
            try { humanData.KindDef = pawn.kindDef.defName; }
            catch { Logger.Warning($"Failed to get kind from human {pawn.Label}"); }
        }

        private static void GetPawnFaction(Pawn pawn, HumanData humanData)
        {
            if (pawn.Faction == null) return;

            try { humanData.FactionDef = pawn.Faction.def.defName; }
            catch { Logger.Warning($"Failed to get faction from human {pawn.Label}"); }
        }

        private static void GetPawnHediffs(Pawn pawn, HumanData humanData)
        {
            if (pawn.health.hediffSet.hediffs.Count() > 0)
            {
                foreach (Hediff hd in pawn.health.hediffSet.hediffs)
                {
                    try
                    {
                        humanData.HediffDefNames.Add(hd.def.defName);

                        if (hd.Part != null) humanData.HediffPartDefNames.Add(hd.Part.def.defName.ToString());
                        else humanData.HediffPartDefNames.Add("null");

                        humanData.HediffSeverities.Add(hd.Severity);
                        humanData.HediffPermanents.Add(hd.IsPermanent());
                    }
                    catch 
                    { 
                        Logger.Warning($"Failed to get heddif {hd} from human {pawn.Label}"); 
                    }
                }
            }
        }

        private static void GetPawnChildState(Pawn pawn, HumanData humanData)
        {
            try { humanData.GrowthPoints = pawn.ageTracker.growthPoints; }
            catch { Logger.Warning($"Failed to get child state from human {pawn.Label}"); }
        }

        private static void GetPawnXenotype(Pawn pawn, HumanData humanData)
        {
            try
            {
                if (pawn.genes.Xenotype != null) humanData.XenotypeDefName = pawn.genes.Xenotype.defName.ToString();
                else humanData.XenotypeDefName = "null";

                if (pawn.genes.CustomXenotype != null) humanData.CustomXenotypeName = pawn.genes.xenotypeName.ToString();
                else humanData.CustomXenotypeName = "null";
            }
            catch { Logger.Warning($"Failed to get xenotype from human {pawn.Label}"); }
        }

        private static void GetPawnXenogenes(Pawn pawn, HumanData humanData)
        {
            if (pawn.genes.Xenogenes.Count() > 0)
            {
                foreach (Gene gene in pawn.genes.Xenogenes)
                {
                    try { humanData.XenogeneDefNames.Add(gene.def.defName); }
                    catch { Logger.Warning($"Failed to get gene {gene} from human {pawn.Label}"); }
                }
            }
        }

        private static void GetPawnEndogenes(Pawn pawn, HumanData humanData)
        {
            if (pawn.genes.Endogenes.Count() > 0)
            {
                foreach (Gene gene in pawn.genes.Endogenes)
                {
                    try { humanData.EndogeneDefNames.Add(gene.def.defName.ToString()); }
                    catch { Logger.Warning($"Failed to get endogene {gene} from human {pawn.Label}"); }
                }
            }
        }

        private static void GetPawnFavoriteColor(Pawn pawn, HumanData humanData)
        {
            try { humanData.FavoriteColor = pawn.story.favoriteColor.ToString(); }
            catch { Logger.Warning($"Failed to get favorite color from human {pawn.Label}"); }
        }

        private static void GetPawnStory(Pawn pawn, HumanData humanData)
        {
            try
            {
                if (pawn.story.Childhood != null) humanData.ChildhoodStory = pawn.story.Childhood.defName.ToString();
                else humanData.ChildhoodStory = "null";

                if (pawn.story.Adulthood != null) humanData.AdulthoodStory = pawn.story.Adulthood.defName.ToString();
                else humanData.AdulthoodStory = "null";
            }
            catch { Logger.Warning($"Failed to get backstories from human {pawn.Label}"); }
        }

        private static void GetPawnSkills(Pawn pawn, HumanData humanData)
        {
            if (pawn.skills.skills.Count() > 0)
            {
                foreach (SkillRecord skill in pawn.skills.skills)
                {
                    try
                    {
                        humanData.SkillDefNames.Add(skill.def.defName);
                        humanData.SkillLevels.Add(skill.levelInt);  // Store as int directly
                        humanData.Passions.Add(skill.passion.ToString());
                    }
                    catch { Logger.Warning($"Failed to get skill {skill} from human {pawn.Label}"); }
                }
            }
        }

        private static void GetPawnTraits(Pawn pawn, HumanData humanData)
        {
            if (pawn.story.traits.allTraits.Count() > 0)
            {
                foreach (Trait trait in pawn.story.traits.allTraits)
                {
                    try
                    {
                        humanData.TraitDefNames.Add(trait.def.defName);
                        humanData.TraitDegrees.Add(trait.Degree);  // Store as int directly (had fucking issues with To.String so fuck it directly store int than)
                    }
                    catch { Logger.Warning($"Failed to get trait {trait} from human {pawn.Label}"); }
                }
            }
        }

        private static void GetPawnApparel(Pawn pawn, HumanData humanData)
        {
            if (pawn.apparel.WornApparel.Count() > 0)
            {
                foreach (Apparel ap in pawn.apparel.WornApparel)
                {
                    try
                    {
                        ThingData thingData = ThingScribeManager.ItemToString(ap, 1);
                        humanData.EquippedApparel.Add(thingData);
                        humanData.ApparelWornByCorpse.Add(ap.WornByCorpse);
                    }
                    catch { Logger.Warning($"Failed to get apparel {ap} from human {pawn.Label}"); }
                }
            }
        }

        private static void GetPawnEquipment(Pawn pawn, HumanData humanData)
        {
            if (pawn.equipment.Primary != null)
            {
                try
                {
                    ThingWithComps weapon = pawn.equipment.Primary;
                    ThingData thingData = ThingScribeManager.ItemToString(weapon, weapon.stackCount);
                    humanData.EquippedWeapon = thingData;
                }
                catch { Logger.Warning($"Failed to get weapon from human {pawn.Label}"); }
            }
        }

        private static void GetPawnInventory(Pawn pawn, HumanData humanData)
        {
            if (pawn.inventory.innerContainer.Count() != 0)
            {
                foreach (Thing thing in pawn.inventory.innerContainer)
                {
                    try
                    {
                        ThingData thingData = ThingScribeManager.ItemToString(thing, thing.stackCount);
                        humanData.InventoryItems.Add(thingData);
                    }
                    catch { Logger.Warning($"Failed to get item from human {pawn.Label}"); }
                }
            }
        }

        private static void GetPawnPosition(Pawn pawn, HumanData humanData)
        {
            try
            {
                humanData.Position = new string[] { pawn.Position.x.ToString(),
                    pawn.Position.y.ToString(), pawn.Position.z.ToString() };
            }
            catch { Logger.Message("Failed to get human position"); }
        }

        private static void GetPawnRotation(Pawn pawn, HumanData humanData)
        {
            try { humanData.Rotation = pawn.Rotation.AsInt; }
            catch { Logger.Message("Failed to get human rotation"); }
        }

        //Setters

        private static PawnKindDef SetPawnKind(HumanData humanData)
        {
            try { return DefDatabase<PawnKindDef>.AllDefs.First(fetch => fetch.defName == humanData.KindDef); }
            catch { Logger.Warning($"Failed to set kind in human {humanData.Name}"); }

            return null;
        }

        private static Faction SetPawnFaction(HumanData humanData)
        {
            if (humanData.FactionDef == null) return null;

            try { return Find.FactionManager.AllFactions.First(fetch => fetch.def.defName == humanData.FactionDef); }
            catch { Logger.Warning($"Failed to set faction in human {humanData.Name}"); }

            return null;
        }

        private static Pawn SetPawn(PawnKindDef kind, Faction faction, HumanData humanData)
        {
            try { return PawnGenerator.GeneratePawn(kind, faction); }
            catch { Logger.Warning($"Failed to set biological details in human {humanData.Name}"); }

            return null;
        }

        private static void SetPawnBioDetails(Pawn pawn, HumanData humanData)
        {
            try
            {
                pawn.Name = new NameSingle(humanData.Name);
                pawn.ageTracker.AgeBiologicalTicks = long.Parse(humanData.BiologicalAge);
                pawn.ageTracker.AgeChronologicalTicks = long.Parse(humanData.ChronologicalAge);

                Enum.TryParse(humanData.Gender, true, out Gender humanGender);
                pawn.gender = humanGender;

                pawn.story.hairDef = DefDatabase<HairDef>.AllDefs.ToList().Find(x => x.defName == humanData.HairDefName);
                pawn.story.headType = DefDatabase<HeadTypeDef>.AllDefs.ToList().Find(x => x.defName == humanData.HeadTypeDefName);
                pawn.style.beardDef = DefDatabase<BeardDef>.AllDefs.ToList().Find(x => x.defName == humanData.BeardDefName);
                pawn.story.bodyType = DefDatabase<BodyTypeDef>.AllDefs.ToList().Find(x => x.defName == humanData.BodyTypeDefName);
                pawn.style.FaceTattoo = DefDatabase<TattooDef>.AllDefs.ToList().Find(x => x.defName == humanData.FaceTattooDefName);
                pawn.style.BodyTattoo = DefDatabase<TattooDef>.AllDefs.ToList().Find(x => x.defName == humanData.BodyTattooDefName);

                string hairColor = humanData.HairColor.Replace("RGBA(", "").Replace(")", "");
                string[] isolatedHair = hairColor.Split(',');
                float r = float.Parse(isolatedHair[0]);
                float g = float.Parse(isolatedHair[1]);
                float b = float.Parse(isolatedHair[2]);
                float a = float.Parse(isolatedHair[3]);
                pawn.story.HairColor = new UnityEngine.Color(r, g, b, a);

                string skinColor = humanData.SkinColor.Replace("RGBA(", "").Replace(")", "");
                string[] isolatedSkin = skinColor.Split(',');
                r = float.Parse(isolatedSkin[0]);
                g = float.Parse(isolatedSkin[1]);
                b = float.Parse(isolatedSkin[2]);
                a = float.Parse(isolatedSkin[3]);
                pawn.story.SkinColorBase = new UnityEngine.Color(r, g, b, a);
            }
            catch { Logger.Warning($"Failed to set biological details in human {humanData.Name}"); }
        }

        private static void SetPawnHediffs(Pawn pawn, HumanData humanData)
        {
            try
            {
                pawn.health.RemoveAllHediffs();
                pawn.health.Reset();
            }
            catch { Logger.Warning($"Failed to remove heddifs of human {humanData.Name}"); }

            if (humanData.HediffDefNames.Count() > 0)
            {
                for (int i = 0; i < humanData.HediffDefNames.Count(); i++)
                {
                    try
                    {
                        HediffDef hediffDef = DefDatabase<HediffDef>.AllDefs.ToList().Find(x => x.defName == humanData.HediffDefNames[i]);
                        BodyPartRecord bodyPart = null;

                        if (humanData.HediffPartDefNames[i] != "null")
                        {
                            bodyPart = pawn.RaceProps.body.AllParts.ToList().Find(x =>
                                x.def.defName == humanData.HediffPartDefNames[i]);
                        }

                        Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                        hediff.Severity = humanData.HediffSeverities[i];

                        if (humanData.HediffPermanents[i])
                        {
                            HediffComp_GetsPermanent hediffComp = hediff.TryGetComp<HediffComp_GetsPermanent>();
                            hediffComp.IsPermanent = true;
                        }

                        pawn.health.AddHediff(hediff, bodyPart);
                    }
                    catch { Logger.Warning($"Failed to set heddif in {humanData.HediffPartDefNames[i]} to human {humanData.Name}"); }
                }
            }
        }

        private static void SetPawnChildState(Pawn pawn, HumanData humanData)
        {
            try { pawn.ageTracker.growthPoints = humanData.GrowthPoints; }
            catch { Logger.Warning($"Failed to set child state in human {pawn.Label}"); }
        }

        private static void SetPawnXenotype(Pawn pawn, HumanData humanData)
        {
            try
            {
                if (humanData.XenotypeDefName != "null")
                {
                    pawn.genes.SetXenotype(DefDatabase<XenotypeDef>.AllDefs.ToList().Find(x => x.defName == humanData.XenotypeDefName));
                }

                if (humanData.CustomXenotypeName != "null")
                {
                    pawn.genes.xenotypeName = humanData.CustomXenotypeName;
                }
            }
            catch { Logger.Warning($"Failed to set xenotypes in human {humanData.Name}"); }
        }

        private static void SetPawnXenogenes(Pawn pawn, HumanData humanData)
        {
            try { pawn.genes.Xenogenes.Clear(); }
            catch { Logger.Warning($"Failed to clear xenogenes for human {humanData.Name}"); }

            if (humanData.XenogeneDefNames.Count() > 0)
            {
                foreach (string str in humanData.XenogeneDefNames)
                {
                    try
                    {
                        GeneDef def = DefDatabase<GeneDef>.AllDefs.First(fetch => fetch.defName == str);
                        pawn.genes.AddGene(def, true);
                    }
                    catch { Logger.Warning($"Failed to set xenogenes for human {humanData.Name}"); }
                }
            }
        }

        private static void SetPawnEndogenes(Pawn pawn, HumanData humanData)
        {
            try { pawn.genes.Endogenes.Clear(); }
            catch { Logger.Warning($"Failed to clear endogenes for human {humanData.Name}"); }

            if (humanData.EndogeneDefNames.Count() > 0)
            {
                foreach (string str in humanData.EndogeneDefNames)
                {
                    try
                    {
                        GeneDef def = DefDatabase<GeneDef>.AllDefs.First(fetch => fetch.defName == str);
                        pawn.genes.AddGene(def, false);
                    }
                    catch { Logger.Warning($"Failed to set endogenes for human {humanData.Name}"); }
                }
            }
        }

        private static void SetPawnFavoriteColor(Pawn pawn, HumanData humanData)
        {
            try
            {
                float r;
                float g;
                float b;
                float a;

                string favoriteColor = humanData.FavoriteColor.Replace("RGBA(", "").Replace(")", "");
                string[] isolatedFavoriteColor = favoriteColor.Split(',');
                r = float.Parse(isolatedFavoriteColor[0]);
                g = float.Parse(isolatedFavoriteColor[1]);
                b = float.Parse(isolatedFavoriteColor[2]);
                a = float.Parse(isolatedFavoriteColor[3]);
                pawn.story.favoriteColor = new UnityEngine.Color(r, g, b, a);
            }
            catch { Logger.Warning($"Failed to set colors in human {humanData.Name}"); }
        }

        private static void SetPawnStory(Pawn pawn, HumanData humanData)
        {
            try
            {
                if (humanData.ChildhoodStory != "null")
                {
                    pawn.story.Childhood = DefDatabase<BackstoryDef>.AllDefs.ToList().Find(x => x.defName == humanData.ChildhoodStory);
                }

                if (humanData.AdulthoodStory != "null")
                {
                    pawn.story.Adulthood = DefDatabase<BackstoryDef>.AllDefs.ToList().Find(x => x.defName == humanData.AdulthoodStory);
                }
            }
            catch { Logger.Warning($"Failed to set stories in human {humanData.Name}"); }
        }
        private static void SetPawnSkills(Pawn pawn, HumanData humanData)
        {
            if (humanData.SkillDefNames.Count() > 0)
            {
                for (int i = 0; i < humanData.SkillDefNames.Count(); i++)
                {
                    try
                    {
                        pawn.skills.skills[i].levelInt = humanData.SkillLevels[i];

                        Enum.TryParse(humanData.Passions[i], true, out Passion passion);
                        pawn.skills.skills[i].passion = passion;
                    }
                    catch { Logger.Warning($"Failed to set skill {humanData.SkillDefNames[i]} to human {humanData.Name}"); }
                }
            }
        }

        private static void SetPawnTraits(Pawn pawn, HumanData humanData)
        {
            try { pawn.story.traits.allTraits.Clear(); }
            catch { Logger.Warning($"Failed to remove traits of human {humanData.Name}"); }

            if (humanData.TraitDefNames.Count() > 0)
            {
                for (int i = 0; i < humanData.TraitDefNames.Count(); i++)
                {
                    try
                    {
                        TraitDef traitDef = DefDatabase<TraitDef>.AllDefs.ToList().Find(x => x.defName == humanData.TraitDefNames[i]);
                        Trait trait = new Trait(traitDef, humanData.TraitDegrees[i]);
                        pawn.story.traits.GainTrait(trait);
                    }
                    catch { Logger.Warning($"Failed to set trait {humanData.TraitDefNames[i]} to human {humanData.Name}"); }
                }
            }
        }

        private static void SetPawnApparel(Pawn pawn, HumanData humanData)
        {
            try
            {
                pawn.apparel.DestroyAll();
                pawn.apparel.DropAllOrMoveAllToInventory();
            }
            catch { Logger.Warning($"Failed to destroy apparel in human {humanData.Name}"); }

            if (humanData.EquippedApparel.Count() > 0)
            {
                for (int i = 0; i < humanData.EquippedApparel.Count(); i++)
                {
                    try
                    {
                        Apparel apparel = (Apparel)ThingScribeManager.StringToItem(humanData.EquippedApparel[i]);
                        if (humanData.ApparelWornByCorpse[i]) apparel.WornByCorpse.MustBeTrue();
                        else apparel.WornByCorpse.MustBeFalse();

                        pawn.apparel.Wear(apparel);
                    }
                    catch { Logger.Warning($"Failed to set apparel in human {humanData.Name}"); }
                }
            }
        }

        private static void SetPawnEquipment(Pawn pawn, HumanData humanData)
        {
            try { pawn.equipment.DestroyAllEquipment(); }
            catch { Logger.Warning($"Failed to destroy equipment in human {humanData.Name}"); }

            if (humanData.EquippedWeapon != null)
            {
                try
                {
                    ThingWithComps thing = (ThingWithComps)ThingScribeManager.StringToItem(humanData.EquippedWeapon);
                    pawn.equipment.AddEquipment(thing);
                }
                catch { Logger.Warning($"Failed to set weapon in human {humanData.Name}"); }
            }
        }

        private static void SetPawnInventory(Pawn pawn, HumanData humanData)
        {
            if (humanData.InventoryItems.Count() > 0)
            {
                foreach (ThingData item in humanData.InventoryItems)
                {
                    try
                    {
                        Thing thing = ThingScribeManager.StringToItem(item);
                        pawn.inventory.TryAddAndUnforbid(thing);
                    }
                    catch { Logger.Warning($"Failed to add thing to pawn {pawn.Label}"); }
                }
            }
        }

        private static void SetPawnPosition(Pawn pawn, HumanData humanData)
        {
            if (humanData.Position != null)
            {
                try
                {
                    pawn.Position = new IntVec3(int.Parse(humanData.Position[0]), int.Parse(humanData.Position[1]),
                        int.Parse(humanData.Position[2]));
                }
                catch { Logger.Message($"Failed to set position in human {pawn.Label}"); }
            }
        }

        private static void SetPawnRotation(Pawn pawn, HumanData humanData)
        {
            try { pawn.Rotation = new Rot4(humanData.Rotation); }
            catch { Logger.Message($"Failed to set rotation in human {pawn.Label}"); }
        }
    }

    //Class that handles transformation of animals

    public static class AnimalScribeManager
    {
        //Functions

        public static Pawn[] GetAnimalsFromString(TransferData transferData)
        {
            List<Pawn> animals = new List<Pawn>();

            for (int i = 0; i < transferData.AnimalDatas.Count(); i++) animals.Add(StringToAnimal(transferData.AnimalDatas[i]));

            return animals.ToArray();
        }

        public static AnimalData AnimalToString(Pawn animal)
        {
            AnimalData animalData = new AnimalData();

            GetAnimalBioDetails(animal, animalData);

            GetAnimalKind(animal, animalData);

            GetAnimalFaction(animal, animalData);

            GetAnimalHediffs(animal, animalData);

            GetAnimalSkills(animal, animalData);

            GetAnimalPosition(animal, animalData);

            GetAnimalRotation(animal, animalData);

            return animalData;
        }

        public static Pawn StringToAnimal(AnimalData animalData)
        {
            PawnKindDef kind = SetAnimalKind(animalData);

            Faction faction = SetAnimalFaction(animalData);

            Pawn animal = SetAnimal(kind, faction, animalData);

            SetAnimalBioDetails(animal, animalData);

            SetAnimalHediffs(animal, animalData);

            SetAnimalSkills(animal, animalData);

            SetAnimalPosition(animal, animalData);

            SetAnimalRotation(animal, animalData);

            return animal;
        }

        //Getters

        private static void GetAnimalBioDetails(Pawn animal, AnimalData animalData)
        {
            try
            {
                animalData.DefName = animal.def.defName;
                animalData.Name = animal.LabelShortCap.ToString();
                animalData.BiologicalAge = animal.ageTracker.AgeBiologicalTicks.ToString();
                animalData.ChronologicalAge = animal.ageTracker.AgeChronologicalTicks.ToString();
                animalData.Gender = animal.gender.ToString();
            }
            catch { Logger.Warning($"Failed to get biodetails of animal {animal.def.defName}"); }
        }

        private static void GetAnimalKind(Pawn animal, AnimalData animalData)
        {
            try { animalData.KindDef = animal.kindDef.defName; }
            catch { Logger.Warning($"Failed to get kind from human {animal.Label}"); }
        }

        private static void GetAnimalFaction(Pawn animal, AnimalData animalData)
        {
            if (animal.Faction == null) return;

            try { animalData.FactionDef = animal.Faction.def.defName; }
            catch { Logger.Warning($"Failed to get faction from animal {animal.def.defName}"); }
        }

        private static void GetAnimalHediffs(Pawn animal, AnimalData animalData)
        {
            if (animal.health.hediffSet.hediffs.Count() > 0)
            {
                foreach (Hediff hd in animal.health.hediffSet.hediffs)
                {
                    try
                    {
                        animalData.HediffDefNames.Add(hd.def.defName);

                        if (hd.Part != null) animalData.HediffPartDefNames.Add(hd.Part.def.defName.ToString());
                        else animalData.HediffPartDefNames.Add("null");

                        animalData.HediffSeverities.Add(hd.Severity.ToString());
                        animalData.HediffPermanents.Add(hd.IsPermanent());
                    }
                    catch { Logger.Warning($"Failed to get headdifs from animal {animal.def.defName}"); }
                }
            }
        }

        private static void GetAnimalSkills(Pawn animal, AnimalData animalData)
        {
            if (animal.training == null) return;

            foreach (TrainableDef trainable in DefDatabase<TrainableDef>.AllDefsListForReading)
            {
                try
                {
                    animalData.TrainableDefNames.Add(trainable.defName);
                    animalData.CanTrain.Add(animal.training.CanAssignToTrain(trainable).Accepted);
                    animalData.HasLearned.Add(animal.training.HasLearned(trainable));
                    animalData.IsDisabled.Add(animal.training.GetWanted(trainable));
                }
                catch { Logger.Warning($"Failed to get skills of animal {animal.def.defName}"); }
            }
        }

        private static void GetAnimalPosition(Pawn animal, AnimalData animalData)
        {
            try
            {
                animalData.Position = new string[] { animal.Position.x.ToString(),
                        animal.Position.y.ToString(), animal.Position.z.ToString() };
            }
            catch { Logger.Message($"Failed to get position of animal {animal.def.defName}"); }
        }

        private static void GetAnimalRotation(Pawn animal, AnimalData animalData)
        {
            try { animalData.Rotation = animal.Rotation.AsInt; }
            catch { Logger.Message($"Failed to get rotation of animal {animal.def.defName}"); }
        }

        //Setters

        private static PawnKindDef SetAnimalKind(AnimalData animalData)
        {
            try { return DefDatabase<PawnKindDef>.AllDefs.First(fetch => fetch.defName == animalData.DefName); }
            catch { Logger.Warning($"Failed to set kind in animal {animalData.Name}"); }

            return null;
        }

        private static Faction SetAnimalFaction(AnimalData animalData)
        {
            if (animalData.FactionDef == null) return null;

            try { return Find.FactionManager.AllFactions.First(fetch => fetch.def.defName == animalData.FactionDef); }
            catch { Logger.Warning($"Failed to set faction in animal {animalData.Name}"); }

            return null;
        }

        private static Pawn SetAnimal(PawnKindDef kind, Faction faction, AnimalData animalData)
        {
            try { return PawnGenerator.GeneratePawn(kind, faction); }
            catch { Logger.Warning($"Failed to set animal {animalData.Name}"); }

            return null;
        }

        private static void SetAnimalBioDetails(Pawn animal, AnimalData animalData)
        {
            try
            {
                animal.Name = new NameSingle(animalData.Name);
                animal.ageTracker.AgeBiologicalTicks = long.Parse(animalData.BiologicalAge);
                animal.ageTracker.AgeChronologicalTicks = long.Parse(animalData.ChronologicalAge);

                Enum.TryParse(animalData.Gender, true, out Gender animalGender);
                animal.gender = animalGender;
            }
            catch { Logger.Warning($"Failed to set biodetails of animal {animalData.Name}"); }
        }

        private static void SetAnimalHediffs(Pawn animal, AnimalData animalData)
        {
            try
            {
                animal.health.RemoveAllHediffs();
                animal.health.Reset();
            }
            catch { Logger.Warning($"Failed to remove heddifs of animal {animalData.Name}"); }

            if (animalData.HediffDefNames.Count() > 0)
            {
                for (int i = 0; i < animalData.HediffDefNames.Count(); i++)
                {
                    try
                    {
                        HediffDef hediffDef = DefDatabase<HediffDef>.AllDefs.ToList().Find(x => x.defName == animalData.HediffDefNames[i]);
                        BodyPartRecord bodyPart = null;

                        if (animalData.HediffPartDefNames[i] != "null")
                        {
                            bodyPart = animal.RaceProps.body.AllParts.ToList().Find(x =>
                                x.def.defName == animalData.HediffPartDefNames[i]);
                        }

                        Hediff hediff = HediffMaker.MakeHediff(hediffDef, animal, bodyPart);
                        hediff.Severity = float.Parse(animalData.HediffSeverities[i]);

                        if (animalData.HediffPermanents[i])
                        {
                            HediffComp_GetsPermanent hediffComp = hediff.TryGetComp<HediffComp_GetsPermanent>();
                            hediffComp.IsPermanent = true;
                        }

                        animal.health.AddHediff(hediff);
                    }
                    catch { Logger.Warning($"Failed to set headiffs in animal {animalData.DefName}"); }
                }
            }
        }

        private static void SetAnimalSkills(Pawn animal, AnimalData animalData)
        {
            if (animalData.TrainableDefNames.Count() > 0)
            {
                for (int i = 0; i < animalData.TrainableDefNames.Count(); i++)
                {
                    try
                    {
                        TrainableDef trainable = DefDatabase<TrainableDef>.AllDefs.ToList().Find(x => x.defName == animalData.TrainableDefNames[i]);
                        if (animalData.CanTrain[i]) animal.training.Train(trainable, null, complete: animalData.HasLearned[i]);
                    }
                    catch { Logger.Warning($"Failed to set skills of animal {animalData.Name}"); }
                }
            }
        }

        private static void SetAnimalPosition(Pawn animal, AnimalData animalData)
        {
            if (animal.Position != null)
            {
                try
                {
                    animal.Position = new IntVec3(int.Parse(animalData.Position[0]), int.Parse(animalData.Position[1]),
                        int.Parse(animalData.Position[2]));
                }
                catch { Logger.Warning($"Failed to set position of animal {animalData.Name}"); }
            }
        }

        private static void SetAnimalRotation(Pawn animal, AnimalData animalData)
        {
            try { animal.Rotation = new Rot4(animalData.Rotation); }
            catch { Logger.Message($"Failed to set rotation of animal {animalData.Name}"); }
        }
    }

    //Class that handles transformation of things

    public static class ThingScribeManager
    {
        //Functions

        public static Thing[] GetItemsFromString(TransferData transferData)
        {
            List<Thing> things = new List<Thing>();

            for (int i = 0; i < transferData.ItemDatas.Count(); i++)
            {
                Thing thingToAdd = StringToItem(transferData.ItemDatas[i]);
                if (thingToAdd != null) things.Add(thingToAdd);
            }

            return things.ToArray();
        }

        public static ThingData ItemToString(Thing thing, int thingCount)
        {
            ThingData thingData = new ThingData();

            Thing toUse = null;
            if (GetItemMinified(thing, thingData)) toUse = thing.GetInnerIfMinified();
            else toUse = thing;

            GetItemName(toUse, thingData);

            GetItemMaterial(toUse, thingData);

            GetItemQuantity(toUse, thingData, thingCount);

            GetItemQuality(toUse, thingData);

            GetItemHitpoints(toUse, thingData);

            GetItemPosition(toUse, thingData);

            GetItemRotation(toUse, thingData);

            return thingData;
        }

        public static Thing StringToItem(ThingData thingData)
        {
            Thing thing = SetItem(thingData);

            SetItemQuantity(thing, thingData);

            SetItemQuality(thing, thingData);

            SetItemHitpoints(thing, thingData);

            SetItemPosition(thing, thingData);

            SetItemRotation(thing, thingData);

            SetItemMinified(thing, thingData);

            return thing;
        }

        //Getters

        private static void GetItemName(Thing thing, ThingData thingData)
        {
            try { thingData.DefName = thing.def.defName; }
            catch { Logger.Warning($"Failed to get name of thing {thing.def.defName}"); }
        }

        private static void GetItemMaterial(Thing thing, ThingData thingData)
        {
            try 
            {
                if (DeepScribeHelper.CheckIfThingHasMaterial(thing)) thingData.MaterialDefName = thing.Stuff.defName;
                else thingData.MaterialDefName = null;
            }
            catch { Logger.Warning($"Failed to get material of thing {thing.def.defName}"); }
        }

        private static void GetItemQuantity(Thing thing, ThingData thingData, int thingCount)
        {
            try { thingData.Quantity = thingCount; }
            catch { Logger.Warning($"Failed to get quantity of thing {thing.def.defName}"); }
        }

        private static void GetItemQuality(Thing thing, ThingData thingData)
        {
            try { thingData.Quality = DeepScribeHelper.GetThingQuality(thing); }
            catch { Logger.Warning($"Failed to get quality of thing {thing.def.defName}"); }
        }

        private static void GetItemHitpoints(Thing thing, ThingData thingData)
        {
            try { thingData.Hitpoints = thing.HitPoints; }
            catch { Logger.Warning($"Failed to get hitpoints of thing {thing.def.defName}"); }
        }

        private static void GetItemPosition(Thing thing, ThingData thingData)
        {
            try
            {
                thingData.Position = new string[] { thing.Position.x.ToString(),
                    thing.Position.y.ToString(), thing.Position.z.ToString() };
            }
            catch { Logger.Warning($"Failed to get position of thing {thing.def.defName}"); }
        }

        private static void GetItemRotation(Thing thing, ThingData thingData)
        {
            try { thingData.Rotation = thing.Rotation.AsInt; }
            catch { Logger.Warning($"Failed to get rotation of thing {thing.def.defName}"); }
        }

        private static bool GetItemMinified(Thing thing, ThingData thingData)
        {
            try 
            {
                thingData.IsMinified = DeepScribeHelper.CheckIfThingIsMinified(thing);
                return thingData.IsMinified;
            }
            catch { Logger.Warning($"Failed to get minified of thing {thing.def.defName}"); }

            return false;
        }

        //Setters

        private static Thing SetItem(ThingData thingData)
        {
            try
            {
                ThingDef thingDef = DefDatabase<ThingDef>.AllDefs.ToList().Find(x => x.defName == thingData.DefName);
                ThingDef defMaterial = DefDatabase<ThingDef>.AllDefs.ToList().Find(x => x.defName == thingData.MaterialDefName);
                return ThingMaker.MakeThing(thingDef, defMaterial);
            }
            catch { Logger.Warning($"Failed to set item for {thingData.DefName}"); }

            return null;
        }

        private static void SetItemQuantity(Thing thing, ThingData thingData)
        {
            try { thing.stackCount = thingData.Quantity; }
            catch { Logger.Warning($"Failed to set item quantity for {thingData.DefName}"); }
        }

        private static void SetItemQuality(Thing thing, ThingData thingData)
        {
            if (thingData.Quality != "null")
            {
                try
                {
                    CompQuality compQuality = thing.TryGetComp<CompQuality>();
                    if (compQuality != null)
                    {
                        QualityCategory iCategory = (QualityCategory)int.Parse(thingData.Quality);
                        compQuality.SetQuality(iCategory, ArtGenerationContext.Outsider);
                    }
                }
                catch { Logger.Warning($"Failed to set item quality for {thingData.DefName}"); }
            }
        }

        private static void SetItemHitpoints(Thing thing, ThingData thingData)
        {
            try { thing.HitPoints = thingData.Hitpoints; }
            catch { Logger.Warning($"Failed to set item hitpoints for {thingData.DefName}"); }
        }

        private static void SetItemPosition(Thing thing, ThingData thingData)
        {
            if (thingData.Position != null)
            {
                try
                {
                    thing.Position = new IntVec3(int.Parse(thingData.Position[0]), int.Parse(thingData.Position[1]),
                        int.Parse(thingData.Position[2]));
                }
                catch { Logger.Warning($"Failed to set position for item {thingData.DefName}"); }
            }
        }

        private static void SetItemRotation(Thing thing, ThingData thingData)
        {
            try { thing.Rotation = new Rot4(thingData.Rotation); }
            catch { Logger.Warning($"Failed to set rotation for item {thingData.DefName}"); }
        }

        private static void SetItemMinified(Thing thing, ThingData thingData)
        {
            if (thingData.IsMinified)
            {
                //INFO
                //This function is where you should transform the item back into a minified.
                //However, this isn't needed and is likely to cause issues with caravans if used
            }
        }
    }

    //Class that handles transformation of maps

    public static class MapScribeManager
    {
        //Functions

        public static MapData MapToString(Map map, bool factionThings, bool nonFactionThings, bool factionHumans, bool nonFactionHumans, bool factionAnimals, bool nonFactionAnimals)
        {
            MapData mapData = new MapData();

            GetMapTile(mapData, map);

            GetMapSize(mapData, map);

            GetMapTerrain(mapData, map);

            GetMapThings(mapData, map, factionThings, nonFactionThings);

            GetMapHumans(mapData, map, factionHumans, nonFactionHumans);

            GetMapAnimals(mapData, map, factionAnimals, nonFactionAnimals);

            GetMapWeather(mapData, map);

            return mapData;
        }

        public static Map StringToMap(MapData mapData, bool factionThings, bool nonFactionThings, bool factionHumans, bool nonFactionHumans, bool factionAnimals, bool nonFactionAnimals, bool lessLoot = false)
        {
            Map map = SetEmptyMap(mapData);

            SetMapTerrain(mapData, map);

            if (factionThings || nonFactionThings) SetMapThings(mapData, map, factionThings, nonFactionThings, lessLoot);

            if (factionHumans || nonFactionHumans) SetMapHumans(mapData, map, factionHumans, nonFactionHumans);

            if (factionAnimals || nonFactionAnimals) SetMapAnimals(mapData, map, factionAnimals, nonFactionAnimals);

            SetWeatherData(mapData, map);

            SetMapFog(map);

            SetMapRoofs(map);

            return map;
        }

        //Getters

        private static void GetMapTile(MapData mapData, Map map)
        {
            try { mapData.MapTile = map.Tile; }
            catch (Exception e) { Logger.Warning($"Failed to get map tile. Reason: {e}"); }
        }

        private static void GetMapSize(MapData mapData, Map map)
        {
            try { mapData.MapSize = ValueParser.IntVec3ToArray(map.Size); }
            catch (Exception e) { Logger.Warning($"Failed to get map size. Reason: {e}"); }
        }

        private static void GetMapTerrain(MapData mapData, Map map)
        {
            try 
            {
                List<string> tempTileDefNames = new List<string>();
                List<string> tempTileRoofDefNames = new List<string>();
                List<bool> tempTilePollutions = new List<bool>();

                for (int z = 0; z < map.Size.z; ++z)
                {
                    for (int x = 0; x < map.Size.x; ++x)
                    {
                        IntVec3 vectorToCheck = new IntVec3(x, map.Size.y, z);

                        tempTileDefNames.Add(map.terrainGrid.TerrainAt(vectorToCheck).defName.ToString());
                        tempTilePollutions.Add(map.pollutionGrid.IsPolluted(vectorToCheck));

                        if (map.roofGrid.RoofAt(vectorToCheck) == null) tempTileRoofDefNames.Add("null");
                        else tempTileRoofDefNames.Add(map.roofGrid.RoofAt(vectorToCheck).defName.ToString());
                    }
                }

                mapData.TileDefNames = tempTileDefNames.ToArray();
                mapData.TileRoofDefNames = tempTileRoofDefNames.ToArray();
                mapData.TilePollutions = tempTilePollutions.ToArray();
            }
            catch (Exception e) { Logger.Warning($"Failed to get map terrain. Reason: {e}"); }
        }

        private static void GetMapThings(MapData mapData, Map map, bool factionThings, bool nonFactionThings)
        {
            try 
            {
                List<ThingData> tempFactionThings = new List<ThingData>();
                List<ThingData> tempNonFactionThings = new List<ThingData>();

                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (!DeepScribeHelper.CheckIfThingIsHuman(thing) && !DeepScribeHelper.CheckIfThingIsAnimal(thing))
                    {
                        ThingData thingData = ThingScribeManager.ItemToString(thing, thing.stackCount);

                        if (thing.def.alwaysHaulable && factionThings) tempFactionThings.Add(thingData);
                        else if (!thing.def.alwaysHaulable && nonFactionThings) tempNonFactionThings.Add(thingData);

                        if (DeepScribeHelper.CheckIfThingCanGrow(thing))
                        {
                            try
                            {
                                Plant plant = thing as Plant;
                                thingData.GrowthTicks = plant.Growth;
                            }
                            catch { Logger.Warning($"Failed to parse plant {thing.def.defName}"); }
                        }
                    }
                }

                mapData.FactionThings = tempFactionThings.ToArray();
                mapData.NonFactionThings = tempNonFactionThings.ToArray();
            }
            catch (Exception e) { Logger.Warning($"Failed to get map things. Reason: {e}"); }
        }

        private static void GetMapHumans(MapData mapData, Map map, bool factionHumans, bool nonFactionHumans)
        {
            try 
            {
                List<HumanData> tempFactionHumans = new List<HumanData>();
                List<HumanData> tempNonFactionHumans = new List<HumanData>();

                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (DeepScribeHelper.CheckIfThingIsHuman(thing))
                    {
                        HumanData humanData = HumanScribeManager.HumanToString(thing as Pawn);

                        if (thing.Faction == Faction.OfPlayer && factionHumans) tempFactionHumans.Add(humanData);
                        else if (thing.Faction != Faction.OfPlayer && nonFactionHumans) tempNonFactionHumans.Add(humanData);
                    }
                }

                mapData.FactionHumans = tempFactionHumans.ToArray();
                mapData.NonFactionHumans = tempNonFactionHumans.ToArray();
            }
            catch (Exception e) { Logger.Warning($"Failed to get map humans. Reason: {e}"); }
        }

        private static void GetMapAnimals(MapData mapData, Map map, bool factionAnimals, bool nonFactionAnimals)
        {
            try 
            {
                List<AnimalData> tempFactionAnimals = new List<AnimalData>();
                List<AnimalData> tempNonFactionAnimals = new List<AnimalData>();

                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (DeepScribeHelper.CheckIfThingIsAnimal(thing))
                    {
                        AnimalData animalData = AnimalScribeManager.AnimalToString(thing as Pawn);

                        if (thing.Faction == Faction.OfPlayer && factionAnimals) tempFactionAnimals.Add(animalData);
                        else if (thing.Faction != Faction.OfPlayer && nonFactionAnimals) tempNonFactionAnimals.Add(animalData);
                    }
                }

                mapData.FactionAnimals = tempFactionAnimals.ToArray();
                mapData.NonFactionAnimals = tempNonFactionAnimals.ToArray();
            }
            catch (Exception e) { Logger.Warning($"Failed to get map animals. Reason: {e}"); }
        }

        private static void GetMapWeather(MapData mapData, Map map)
        {
            try { mapData.CurWeatherDefName = map.weatherManager.curWeather.defName; }
            catch (Exception e) { Logger.Warning($"Failed to get map weather. Reason: {e}"); }
        }

        //Setters

        private static Map SetEmptyMap(MapData mapData)
        {
            IntVec3 mapSize = ValueParser.ArrayToIntVec3(mapData.MapSize);

            PlanetManagerHelper.SetOverrideGenerators();
            Map toReturn = GetOrGenerateMapUtility.GetOrGenerateMap(ClientValues.chosenSettlement.Tile, mapSize, null);
            PlanetManagerHelper.SetDefaultGenerators();

            return toReturn;
        }

        private static void SetMapTerrain(MapData mapData, Map map)
        {
            try
            {
                int index = 0;

                for (int z = 0; z < map.Size.z; ++z)
                {
                    for (int x = 0; x < map.Size.x; ++x)
                    {
                        IntVec3 vectorToCheck = new IntVec3(x, map.Size.y, z);

                        try
                        {
                            TerrainDef terrainToUse = DefDatabase<TerrainDef>.AllDefs.ToList().Find(fetch => fetch.defName ==
                                mapData.TileDefNames[index]);

                            map.terrainGrid.SetTerrain(vectorToCheck, terrainToUse);
                            map.pollutionGrid.SetPolluted(vectorToCheck, mapData.TilePollutions[index]);

                        }
                        catch { Logger.Warning($"Failed to set terrain at {vectorToCheck}"); }

                        try
                        {
                            RoofDef roofToUse = DefDatabase<RoofDef>.AllDefs.ToList().Find(fetch => fetch.defName ==
                                        mapData.TileRoofDefNames[index]);

                            map.roofGrid.SetRoof(vectorToCheck, roofToUse);
                        }
                        catch { Logger.Warning($"Failed to set roof at {vectorToCheck}"); }

                        index++;
                    }
                }
            }
            catch (Exception e) { Logger.Warning($"Failed to set map terrain. Reason: {e}"); }
        }

        private static void SetMapThings(MapData mapData, Map map, bool factionThings, bool nonFactionThings, bool lessLoot)
        {
            try
            {
                List<Thing> thingsToGetInThisTile = new List<Thing>();

                if (factionThings)
                {
                    Random rnd = new Random();

                    foreach (ThingData item in mapData.FactionThings)
                    {
                        try
                        {
                            Thing toGet = ThingScribeManager.StringToItem(item);

                            if (lessLoot)
                            {
                                if (rnd.Next(1, 100) > 70) thingsToGetInThisTile.Add(toGet);
                                else continue;
                            }
                            else thingsToGetInThisTile.Add(toGet);

                            if (DeepScribeHelper.CheckIfThingCanGrow(toGet))
                            {
                                Plant plant = toGet as Plant;
                                plant.Growth = item.GrowthTicks;
                            }
                        }
                        catch { Logger.Warning($"Failed to parse thing {item.DefName}"); }
                    }
                }

                if (nonFactionThings)
                {
                    foreach (ThingData item in mapData.NonFactionThings)
                    {
                        try
                        {
                            Thing toGet = ThingScribeManager.StringToItem(item);
                            thingsToGetInThisTile.Add(toGet);

                            if (DeepScribeHelper.CheckIfThingCanGrow(toGet))
                            {
                                Plant plant = toGet as Plant;
                                plant.Growth = item.GrowthTicks;
                            }
                        }
                        catch { Logger.Warning($"Failed to parse thing {item.DefName}"); }
                    }
                }

                foreach (Thing thing in thingsToGetInThisTile)
                {
                    try { GenPlace.TryPlaceThing(thing, thing.Position, map, ThingPlaceMode.Direct, rot: thing.Rotation); }
                    catch { Logger.Warning($"Failed to place thing {thing.def.defName} at {thing.Position}"); }
                }
            }
            catch (Exception e) { Logger.Warning($"Failed to set map things. Reason: {e}"); }
        }

        private static void SetMapHumans(MapData mapData, Map map, bool factionHumans, bool nonFactionHumans)
        {
            try
            {
                if (factionHumans)
                {
                    foreach (HumanData pawn in mapData.FactionHumans)
                    {
                        try
                        {
                            Pawn human = HumanScribeManager.StringToHuman(pawn);
                            human.SetFaction(FactionValues.neutralPlayer);

                            GenSpawn.Spawn(human, human.Position, map, human.Rotation);
                        }
                        catch { Logger.Warning($"Failed to spawn human {pawn.Name}"); }
                    }
                }

                if (nonFactionHumans)
                {
                    foreach (HumanData pawn in mapData.NonFactionHumans)
                    {
                        try
                        {
                            Pawn human = HumanScribeManager.StringToHuman(pawn);
                            GenSpawn.Spawn(human, human.Position, map, human.Rotation);
                        }
                        catch { Logger.Warning($"Failed to spawn human {pawn.Name}"); }
                    }
                }
            }
            catch (Exception e) { Logger.Warning($"Failed to set map humans. Reason: {e}"); }
        }

        private static void SetMapAnimals(MapData mapData, Map map, bool factionAnimals, bool nonFactionAnimals)
        {
            try
            {
                if (factionAnimals)
                {
                    foreach (AnimalData pawn in mapData.FactionAnimals)
                    {
                        try
                        {
                            Pawn animal = AnimalScribeManager.StringToAnimal(pawn);
                            animal.SetFaction(FactionValues.neutralPlayer);

                            GenSpawn.Spawn(animal, animal.Position, map, animal.Rotation);
                        }
                        catch { Logger.Warning($"Failed to spawn animal {pawn.Name}"); }
                    }
                }

                if (nonFactionAnimals)
                {
                    foreach (AnimalData pawn in mapData.NonFactionAnimals)
                    {
                        try
                        {
                            Pawn animal = AnimalScribeManager.StringToAnimal(pawn);
                            GenSpawn.Spawn(animal, animal.Position, map, animal.Rotation);
                        }
                        catch { Logger.Warning($"Failed to spawn animal {pawn.Name}"); }
                    }
                }
            }
            catch (Exception e) { Logger.Warning($"Failed to set map animals. Reason: {e}"); }
        }

        private static void SetWeatherData(MapData mapData, Map map)
        {
            try
            {
                WeatherDef weatherDef = DefDatabase<WeatherDef>.AllDefs.First(fetch => fetch.defName == mapData.CurWeatherDefName);
                map.weatherManager.TransitionTo(weatherDef);
            }
            catch (Exception e) { Logger.Warning($"Failed to set map weather. Reason: {e}"); }
        }

        private static void SetMapFog(Map map)
        {
            try { FloodFillerFog.FloodUnfog(MapGenerator.PlayerStartSpot, map); }
            catch (Exception e) { Logger.Warning($"Failed to set map fog. Reason: {e}"); }
        }

        private static void SetMapRoofs(Map map)
        {
            try
            {
                map.roofCollapseBuffer.Clear();
                map.roofGrid.Drawer.SetDirty();
            }
            catch (Exception e) { Logger.Warning($"Failed to set map roofs. Reason: {e}"); }            
        }
    }

    //Class that contains helping functions for the deep scriber

    public static class DeepScribeHelper
    {
        //Checks if transferable thing is a human

        public static bool CheckIfThingIsHuman(Thing thing)
        {
            if (thing.def.defName == "Human") return true;
            else return false;
        }

        //Checks if transferable thing is an animal

        public static bool CheckIfThingIsAnimal(Thing thing)
        {
            PawnKindDef animal = DefDatabase<PawnKindDef>.AllDefs.ToList().Find(fetch => fetch.defName == thing.def.defName);
            if (animal != null) return true;
            else return false;
        }

        //Checks if transferable thing is an item that can have a growth state

        public static bool CheckIfThingCanGrow(Thing thing)
        {
            try
            {
                Plant plant = thing as Plant;
                _ = plant.Growth;
                return true;
            }
            catch { return false; }
        }

        //Checks if transferable thing has a material

        public static bool CheckIfThingHasMaterial(Thing thing)
        {
            if (thing.Stuff != null) return true;
            else return false;
        }

        //Gets the quality of a transferable thing

        public static string GetThingQuality(Thing thing)
        {
            QualityCategory qc = QualityCategory.Normal;
            thing.TryGetQuality(out qc);

            return ((int)qc).ToString();
        }

        //Checks if transferable thing is minified

        public static bool CheckIfThingIsMinified(Thing thing)
        {
            if (thing.def == ThingDefOf.MinifiedThing || thing.def == ThingDefOf.MinifiedTree) return true;
            else return false;
        }
    }
}
