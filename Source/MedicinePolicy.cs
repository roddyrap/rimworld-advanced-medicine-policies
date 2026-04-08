using System.Collections.Generic;
using Verse;
using RimWorld;
using HarmonyLib;
using System;
using System.Linq;

namespace AdvancedMedicinePolicies;

public class MedicinePolicy : IExposable
{
    // TODO: I'd rather these not be publically modifiable.
    private HashSet<PawnCondition> pawnConditions;
    public HashSet<PawnAllegiance> pawnClasses;

    /// <summary>
    /// The highest quality medicine that is allowed to be used by this policy.
    /// </summary>
    /// <value>Defaults to Herbal Medicine if not explicitly set.</value>
    public MedicalCareCategory allowedMedicine;


    // Cached properties, not part of the actual policy and not saved.
    private readonly List<ConditionRegistry.PawnConditionCheck> activePawnChecks = [];
    private readonly List<ConditionRegistry.HediffConditionCheck> activeHediffChecks = [];

    public MedicinePolicy(
        HashSet<PawnCondition> pawnConditions,
        HashSet<PawnAllegiance> pawnClasses,
        MedicalCareCategory allowedMedicine
    )
    {
        this.pawnConditions = pawnConditions;
        this.pawnClasses = pawnClasses;
        this.allowedMedicine = allowedMedicine;

        RebuildCache();
    }

    public MedicinePolicy() : this([], [], MedicalCareCategory.Best) {}

    public void AddCondition(PawnCondition condition)
    {
        if (pawnConditions.Add(condition)) 
        {
            if (ConditionRegistry.PawnChecks.TryGetValue(condition, out var pawnFunc))
                activePawnChecks.Add(pawnFunc);
            else if (ConditionRegistry.HediffChecks.TryGetValue(condition, out var hediffFunc))
                activeHediffChecks.Add(hediffFunc);
        }
    }

    public void RemoveCondition(PawnCondition condition)
    {
        if (pawnConditions.Remove(condition)) 
        {
            if (ConditionRegistry.PawnChecks.TryGetValue(condition, out var pawnFunc))
                activePawnChecks.Remove(pawnFunc); 
            else if (ConditionRegistry.HediffChecks.TryGetValue(condition, out var hediffFunc))
                activeHediffChecks.Remove(hediffFunc);
        }
    }

    public IReadOnlyCollection<PawnCondition> GetConditions()
    {
        return pawnConditions;
    }

    public bool DoesPawnMatch(Pawn patient, Pawn healer)
    {
        if (!pawnClasses.Contains(AllegianceResolver.GetPawnAllegiance(patient))) return false;

        if (pawnConditions.Count > 0)
        {
            // Execute cached pawn-level checks
            for (int i = 0; i < activePawnChecks.Count; i++)
            {
                if (activePawnChecks[i](patient, healer)) return true;
            }

            if (activeHediffChecks.Count > 0)
            {
                foreach (Hediff hediff in patient.health.hediffSet.hediffs)
                {
                    for (int i = 0; i < activeHediffChecks.Count; i++)
                    {
                        if (activeHediffChecks[i](patient, healer, hediff)) return true;
                    }
                }
            }

            return false;
        }

        return true;
    }

    private void RebuildCache()
    {
        activePawnChecks.Clear();
        activeHediffChecks.Clear();

        foreach (var condition in pawnConditions.ToArray())
        {
            pawnConditions.Remove(condition); 
            AddCondition(condition);
        }
    }

    public void ExposeData()
    {
        Scribe_Collections.Look(ref pawnClasses, "pawnClasses", LookMode.Value);
        Scribe_Collections.Look(ref pawnConditions, "pawnConditions", LookMode.Value);
        Scribe_Values.Look(ref allowedMedicine, "allowedMedicine");

        RebuildCache();
    }
}


public class AdvancedMedicinePoliciesComponent : GameComponent
{
    public List<MedicinePolicy> medicinePolicies = [];
    private HashSet<Pawn> unmanagedPawns = [];

    // Rimworld forces this c'tor.
    public AdvancedMedicinePoliciesComponent(Game game)
    {
        _ = game; 
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref medicinePolicies, "medicinePolicies", LookMode.Deep);
        Scribe_Collections.Look(ref unmanagedPawns, "UnmanagedPawns", LookMode.Reference);

        // If variables aren't saved to savefile (First mod-load, for example) initialize default values.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            medicinePolicies ??= [];
            unmanagedPawns ??= [];

            // Clean up any pawns that died/were destroyed since the last save.
            unmanagedPawns.RemoveWhere(pawn => pawn == null || pawn.Destroyed);
        }
    }

    public MedicalCareCategory GetMedicalCareCategory(Pawn patient, Pawn healer)
    {
        foreach (MedicinePolicy policy in medicinePolicies)
        {
            if (policy.DoesPawnMatch(patient, healer))
            {
                return policy.allowedMedicine;
            }
        }

        // This is the least restrictive option I could output, which is correct when no policy
        // is matched because the current pawn medicine restriction is factored in later.
        return MedicalCareCategory.Best;
    }

    // TODO: this isn't really great. Currently there's no option to decide whether to apply
    //       the mod to prisoners and allies and the likes, so the medicine defaults tab
    //       is essentially being ignored.
    public bool IsPawnManaged(Pawn pawn)
    {
        return !unmanagedPawns.Contains(pawn);
    }

    public void SetPawnManaged(Pawn pawn, bool isManaged)
    {
        if (isManaged) unmanagedPawns.Remove(pawn);
        else unmanagedPawns.Add(pawn);
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Discard))]
public static class Patch_Pawn_Discard
{
    public static void Postfix(Pawn __instance)
    {
        var comp = Current.Game.GetComponent<AdvancedMedicinePoliciesComponent>();

        // I delete the pawn from the mod's list by setting it to the default, which is managed.
        comp?.SetPawnManaged(__instance, true);
    }
}

[HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.FindBestMedicine))]
public static class Patch_MedicineSelection
{
    public static bool Prefix(ref Pawn healer, ref Pawn patient, ref bool onlyUseInventory, ref MedicalCareCategory? __state)
    {
        __state = null;
        AdvancedMedicinePoliciesComponent medicinePoliciesComponent = Current.Game.GetComponent<AdvancedMedicinePoliciesComponent>();
        if (!medicinePoliciesComponent.IsPawnManaged(patient))
        {
            return true;
        }

        var medicalCareCategory = medicinePoliciesComponent.GetMedicalCareCategory(patient, healer);
        __state = patient.playerSettings?.medCare;

        // Take the highest care level that is compatible both with my mod and with the vanilla selector.
        if (__state is not null)
        {
            medicalCareCategory = (MedicalCareCategory)Math.Min((int)__state, (int)medicalCareCategory);
        }

        // Temporarily override and restrict the actual allowed medicine.
        patient.playerSettings.medCare = medicalCareCategory;
        return true;
    }

    public static void Finalizer(ref Pawn healer, ref Pawn patient, ref bool onlyUseInventory, ref MedicalCareCategory? __state)
    {
        if (__state is not null && patient?.playerSettings != null)
        {
            patient.playerSettings.medCare = __state.Value;
        }
    }
}
