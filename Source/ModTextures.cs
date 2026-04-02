using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
namespace AdvancedMedicinePolicies;

public static class ModTextureFinder
{
    public static Texture2D Get(bool warn, params string[] paths)
    {
        for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
        {
            // This function receives a list of fallbacks, so warn only on the LAST one.
            bool shouldWarn = warn && pathIndex == paths.Length - 1;
            Texture2D tex = ContentFinder<Texture2D>.Get(paths[pathIndex], shouldWarn);
            if (tex != null) return tex;
        }

        return null;
    }

    public static Texture2D Get(params string[] paths) => Get(true, paths);

    public static Texture2D GetMod(bool warn, params string[] paths) =>
        Get(warn, [.. paths.Select(x => $"AdvancedMedicinePolicies/{x}")]);

    public static Texture2D GetMod(params string[] paths) => GetMod(true, paths);
}

[StaticConstructorOnStartup]
public static class IconTextures
{
    public static readonly Texture2D IconBleeding = ModTextureFinder.Get("UI/Icons/Medical/Bleeding");
    public static readonly Texture2D IconLifeThreatening = ModTextureFinder.Get("UI/Designators/Slaughter");
    public static readonly Texture2D IconInfectionRisk = ModTextureFinder.GetMod("Conditions/Bandage");
    public static readonly Texture2D IconDraftedTend = ModTextureFinder.Get("UI/Commands/Draft");
    public static readonly Texture2D IconIncapacitated = ModTextureFinder.Get("UI/Designators/Cancel"); 
    public static readonly Texture2D IconSelfTend = ModTextureFinder.GetMod("Conditions/SelfTend"); 
    public static readonly Texture2D IconSurgery = ThingDefOf.MedicineIndustrial?.uiIcon;
    public static readonly Texture2D IconNonBleeding = GetNonBleedingIcon();
    public static readonly Texture2D IconChronic = DefDatabase<ThingDef>.GetNamedSilentFail("GoJuice")?.uiIcon;
    public static readonly Texture2D IconImmunity = ModTextureFinder.GetMod("Conditions/Biohazard");

    public static Texture2D GetNonBleedingIcon()
    {
        // Might not exist because it's part of the Biotech DLC.
        // Texture2D geneFist = DefDatabase<GeneDef>.GetNamedSilentFail("MeleeDamage_Strong")?.Icon;
        // if (geneFist != null) return geneFist;

        return DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_Club").uiIcon;
    }

    public static readonly Texture2D IconPrisoner = ModTextureFinder.Get("UI/Commands/ForPrisoners");

    public static readonly Dictionary<PawnCondition, Texture2D> ConditionIconMap = new()
    {
        { PawnCondition.BLEEDING_OUT, IconBleeding },
        { PawnCondition.LIFE_THREATENING, IconLifeThreatening },
        { PawnCondition.INCAPACITATED, IconIncapacitated },
        
        { PawnCondition.PENDING_SURGERY, IconSurgery },
        { PawnCondition.DRAFTED_FIELD_TEND, IconDraftedTend },
        { PawnCondition.SELF_TENDING, IconSelfTend },
        
        { PawnCondition.INFECTION_RISK, IconInfectionRisk },
        { PawnCondition.CHRONIC_DISEASE, IconChronic },
        { PawnCondition.IMMUNITY_DISEASE, IconImmunity },

        { PawnCondition.NON_BLEEDING_INJURIES, IconNonBleeding }
    };

    public static readonly Dictionary<PawnAllegiance, Texture2D> AllegianceIconMap = new()
    {
        { PawnAllegiance.PRISONER, IconPrisoner}
    };
}