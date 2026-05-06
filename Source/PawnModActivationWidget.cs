using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;
using HarmonyLib;

namespace AdvancedMedicinePolicies;

[HarmonyPatch(typeof(PawnColumnWorker_MedicalCare), nameof(PawnColumnWorker_MedicalCare.DoCell))]
public static class Patch_PawnColumnWorker_MedicalCare_DoCell
{

    const float PAWN_BUTTON_ICON_SIZE = 24f;
    const float PAWN_BUTTON_SIDE_PADDING = 2f;
    public const float PAWN_BUTTON_RESERVED_SPACE = PAWN_BUTTON_ICON_SIZE + 2 * PAWN_BUTTON_SIDE_PADDING;

    static readonly string PAWN_BUTTON_TOOLTIP = @"
Toggle Modded Medicine Policies

If enabled, the mod 'Advanced Medicine Policies' decides the medicine. If disabled, vanilla policy applies.

In any case, the mod will not allow for a medicine better than the pawn's restriction.
    ".Trim();
    public static void Prefix(ref Rect rect, out Rect __state)
    {
        /**
         * I want to enlarge the medical care space for each pawn in order to add my button to
         * it, but if I do that by just increasing the reported size then the medical care
         * button is just drawn in the middle of the box, which causes a collision.
         * 
         * In order to avoid this I make the rectangle larger _but_ reduce its size before every
         * call to the vanilla pawn column drawer, so that it would know it and keep drawing
         * the medical care button to the left of the new box.
         */
        __state = rect; 
        rect.width -= PAWN_BUTTON_RESERVED_SPACE;
    }

    public static void Postfix(Pawn pawn, Rect __state)
    {
        // The __state is enlarged and should contain both buttons, so I align my button to the
        // right of it (Because the vanilla button, after reducing the rect, should be to
        // the left).
        Rect reservedRect = new(
            __state.xMax - PAWN_BUTTON_RESERVED_SPACE,
            __state.y,
            PAWN_BUTTON_RESERVED_SPACE,
            __state.height
        );
        Rect modIconRect = new(
            reservedRect.center.x - PAWN_BUTTON_ICON_SIZE / 2f,
            reservedRect.center.y - PAWN_BUTTON_ICON_SIZE / 2f,
            PAWN_BUTTON_ICON_SIZE,
            PAWN_BUTTON_ICON_SIZE
        );

        var comp = Current.Game.GetComponent<AdvancedMedicinePoliciesComponent>();
        bool isManaged = comp.IsPawnManaged(pawn);

        Texture2D tex = isManaged ? Textures.modPoliciesActive : Textures.modPoliciesInactive;

        // Draw button like the medicine button.
        Widgets.DrawHighlightIfMouseover(reservedRect);
        Widgets.DrawTextureFitted(modIconRect, tex, 1f);
        if (Widgets.ButtonInvisible(reservedRect))
        {
            comp.SetPawnManaged(pawn, !isManaged);
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        TooltipHandler.TipRegion(modIconRect, PAWN_BUTTON_TOOLTIP);
    }
}

[HarmonyPatch(typeof(PawnColumnWorker_MedicalCare), nameof(PawnColumnWorker_MedicalCare.GetMinWidth))]
public static class Patch_PawnColumnWorker_MedicalCare_GetMinWidth
{
    public static void Postfix(ref int __result)
    {
        __result += (int)Patch_PawnColumnWorker_MedicalCare_DoCell.PAWN_BUTTON_RESERVED_SPACE;
    }
}
