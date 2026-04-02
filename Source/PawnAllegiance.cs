using Verse;
using RimWorld;

namespace AdvancedMedicinePolicies;

public enum PawnAllegiance
{
    COLONIST,
    SLAVE,
    PRISONER,

    // I think can be relevant for crashed pawns and the likes.
    ALLY,
    ENEMY,
    NEUTRAL,

    COLONY_ANIMAL,
    WILD_ANIMAL,
    WILD_HUMAN,

    EMPIRE,
    INSECTOID,
    MECHANOID,
}

public static class AllegianceResolver
{
    public static PawnAllegiance GetPawnAllegiance(Pawn pawn)
    {
        if (pawn.Faction != null && pawn.Faction.IsPlayer)
        {
            if (pawn.IsSlave) return PawnAllegiance.SLAVE;
            if (pawn.IsColonist) return PawnAllegiance.COLONIST;
            if (pawn.RaceProps.Animal) return PawnAllegiance.COLONY_ANIMAL;
        }

        if (pawn.IsPrisonerOfColony) return PawnAllegiance.PRISONER;

        if (pawn.Faction == null)
        {
            return pawn.RaceProps.Animal ? PawnAllegiance.WILD_ANIMAL : PawnAllegiance.WILD_HUMAN;
        }

        if (pawn.Faction == Faction.OfEmpire) return PawnAllegiance.EMPIRE;
        if (pawn.Faction == Faction.OfInsects) return PawnAllegiance.INSECTOID;
        if (pawn.Faction == Faction.OfMechanoids) return PawnAllegiance.MECHANOID;

        FactionRelationKind relation = pawn.Faction.PlayerRelationKind;
        if (relation == FactionRelationKind.Hostile) return PawnAllegiance.ENEMY;
        if (relation == FactionRelationKind.Ally) return PawnAllegiance.ALLY;

        return PawnAllegiance.NEUTRAL;
    }
}
