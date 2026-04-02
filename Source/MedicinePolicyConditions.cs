using System.Collections.Generic;
using Verse;
using RimWorld;

namespace AdvancedMedicinePolicies;

// This class isn't restrictive. A pawn can have multiple "conditions" at the same time.
public enum PawnCondition
{
    IMMUNITY_DISEASE,
    CHRONIC_DISEASE,
    BLEEDING_OUT,
    INCAPACITATED,
    LIFE_THREATENING,
    SELF_TENDING,

    DRAFTED_FIELD_TEND,
    PENDING_SURGERY,
    INFECTION_RISK,
    NON_BLEEDING_INJURIES,
}

public static class ConditionRegistry
{
    // Delegate signatures
    public delegate bool PawnConditionCheck(Pawn patient, Pawn healer);
    public delegate bool HediffConditionCheck(Pawn patient, Pawn healer, Hediff hediff);

    static bool CheckIncapacitated(Pawn patient, Pawn healer)
    {
        return patient.health.InPainShock;
    }

    static bool CheckBleedingOut(Pawn patient, Pawn healer)
    {
        return GetDaysUntilBleedOut(patient) <= 0.2f;
    }

    static bool CheckSelfTending(Pawn patient, Pawn healer)
    {
        return patient == healer;
    }

    static bool CheckDraftedFieldTend(Pawn patient, Pawn healer)
    {
        return patient.Drafted || (healer != null && healer.Drafted);
    }

    static bool CheckPendingSurgery(Pawn patient, Pawn healer)
    {
        return patient.health.surgeryBills.AnyShouldDoNow;
    }

    // The reason I check each hediff independently here is that I want this check to be
    // before all hediffs checks as if one of the others is true it will return true already and
    // fail us. (Non-bleeding checks that nothing is bleeding, not that one hediff is non-bleeding).
    static bool CheckNonBleedingInjuries(Pawn patient, Pawn healer)
    {
        bool hasAnyTendable = false;

        foreach (Hediff hediff in patient.health.hediffSet.hediffs)
        {
            // Only look at things that currently need a doctor's attention
            if (!hediff.TendableNow()) continue;
            
            hasAnyTendable = true;

            // REFACTOR: Use the hediff-specific logic. 
            // If even ONE tendable condition is NOT a simple injury, the whole pawn fails this rule.
            if (!CheckIsSimpleInjury(patient, healer, hediff))
            {
                return false;
            }
        }

        // True only if they have wounds AND all of them were simple
        return hasAnyTendable;
    }

    static bool CheckChronic(Pawn patient, Pawn healer, Hediff hediff)
    {
        return hediff.def.chronic;
    }

    static bool CheckImmunity(Pawn patient, Pawn healer, Hediff hediff)
    {
        HediffComp_Immunizable comp = hediff.TryGetComp<HediffComp_Immunizable>();
        return comp != null && !comp.FullyImmune;
    }

    static bool CheckLifeThreatening(Pawn patient, Pawn healer, Hediff hediff)
    {
        return hediff.CurStage != null && hediff.CurStage.lifeThreatening;
    }

    static bool CheckInfectionRisk(Pawn patient, Pawn healer, Hediff hediff)
    {
        // Must be actively tendable AND have the capacity to become infected
        return hediff.TendableNow() && hediff.def.CompProps<HediffCompProperties_Infecter>() != null;
    }

    static bool CheckIsSimpleInjury(Pawn patient, Pawn healer, Hediff hediff)
    {
        // If it's not a physical injury (e.g. it's a disease or infection), it's not "simple"
        if (hediff is not Hediff_Injury injury) return false;

        // Simple injuries don't bleed
        if (injury.Bleeding) return false;

        // Simple injuries don't have infection risks (no bites, no dirty cuts)
        if (injury.def.CompProps<HediffCompProperties_Infecter>() != null) return false;

        return true;
    }

    // --- DICTIONARY REGISTRIES ---

    // 1. Pawn-Level Checks (Run once)
    public static readonly Dictionary<PawnCondition, PawnConditionCheck> PawnChecks = new()
    {
        {PawnCondition.INCAPACITATED, CheckIncapacitated},
        {PawnCondition.BLEEDING_OUT, CheckBleedingOut},
        {PawnCondition.SELF_TENDING, CheckSelfTending},
        {PawnCondition.DRAFTED_FIELD_TEND, CheckDraftedFieldTend},
        {PawnCondition.PENDING_SURGERY, CheckPendingSurgery},
        {PawnCondition.NON_BLEEDING_INJURIES, CheckNonBleedingInjuries}
    };

    // 2. Hediff-Level Checks (Run inside the hediff loop)
    public static readonly Dictionary<PawnCondition, HediffConditionCheck> HediffChecks = new()
    {
        {PawnCondition.CHRONIC_DISEASE, CheckChronic},
        {PawnCondition.IMMUNITY_DISEASE, CheckImmunity},
        {PawnCondition.LIFE_THREATENING, CheckLifeThreatening},
        {PawnCondition.INFECTION_RISK, CheckInfectionRisk}
    };

    // --- HELPER METHODS ---

    static float GetDaysUntilBleedOut(Pawn pawn)
    {
        float bleedRate = pawn.health.hediffSet.BleedRateTotal;
        if (bleedRate <= 0.0001f) return float.MaxValue;

        // Get current bleed.
        Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        
        // Severity 1.0 is death. 
        float currentSeverity = bloodLoss?.Severity ?? 0f;
        float remainingSeverity = 1.0f - currentSeverity;

        return remainingSeverity / bleedRate;
    }
}
