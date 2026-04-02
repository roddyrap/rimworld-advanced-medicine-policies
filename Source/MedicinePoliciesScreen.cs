using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using HarmonyLib;
using IList = System.Collections.IList;

namespace AdvancedMedicinePolicies;

/// <summary>
/// A custom RimWorld window that allows the player to create, edit, and reorder global medicine
/// policies based on pawn allegiance and health conditions.
/// </summary>
/// <remarks>
/// This window acts as the primary UI controller for the <see cref="AdvancedMedicinePoliciesComponent"/>.
/// </remarks>
public class Dialog_AdvancedMedicinePolicies : Window
{
    private class FixedFloatMenu(List<FloatMenuOption> options, Vector2? initialPos) : FloatMenu(options)
    {
        public Vector2? Position { get; private set; } = initialPos;

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            
            if (Position.HasValue) windowRect.position = Position.Value;
            else Position = windowRect.position;
        }
    }

    private static class Layout
    {
        public const float DragWidth = 24f;
        public const float BtnWidth = 140f;
        public const float MedWidth = 140f;
        public const float DeleteWidth = 24f;
        public const float RowHeight = 35f;
        public const float RowContentHeight = 30f;
        public const float RowXPad = 4f;
        public const float ElementXPad = 10f;
    }

    private readonly ref struct RowBounds
    {
        public Rect Drag { get; }
        public Rect Allegiance { get; }
        public Rect Conditions { get; }
        public Rect Delete { get; }
        public Rect Medicine { get; }

        public RowBounds(Rect rowRect, float contentHeight)
        {
            float contentY = rowRect.y + (rowRect.height - contentHeight) / 2f;
            float leftX = rowRect.x + Layout.RowXPad;

            Drag = new(leftX, rowRect.y + (rowRect.height - Layout.DragWidth) / 2f, Layout.DragWidth, Layout.DragWidth);
            
            leftX += Layout.DragWidth + Layout.ElementXPad;
            Allegiance = new(leftX, contentY, Layout.BtnWidth, contentHeight);
            
            leftX += Layout.BtnWidth + Layout.ElementXPad;
            Conditions = new(leftX, contentY, Layout.BtnWidth, contentHeight);

            float rightX = rowRect.xMax - Layout.RowXPad;
            Delete = new(rightX - Layout.DeleteWidth, rowRect.y + (rowRect.height - Layout.DeleteWidth) / 2f, Layout.DeleteWidth, Layout.DeleteWidth);
            
            rightX -= Layout.DeleteWidth + Layout.ElementXPad;
            Medicine = new(rightX - Layout.MedWidth, contentY, Layout.MedWidth, contentHeight);
        }
    }

    // I need a unique ID for the window but I don't really know what other IDs are used, so
    // I hope a hash that contains my mod names will provide a random-enough base so that
    // adding indices will not make a collision.
    private static readonly int GhostWindowBaseId = "AdvancedMedicinePolicies.DragGhost".GetHashCode();

    private readonly AdvancedMedicinePoliciesComponent comp;
    private Vector2 scrollPosition;
    private int reorderableGroupID = -1;

    public override Vector2 InitialSize => new(850f, 600f);

    public Dialog_AdvancedMedicinePolicies()
    {
        forcePause = true;
        doCloseX = true;
        doCloseButton = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        onlyOneOfTypeAllowed = true;

        comp = Current.Game.GetComponent<AdvancedMedicinePoliciesComponent>();
    }

    public override void DoWindowContents(Rect windowRect)
    {
        Rect titleRect = new(windowRect.x, windowRect.y, windowRect.width, 35f);
        Rect addBtnRect = new(windowRect.x, windowRect.yMax - 30f, 150f, 30f);
        Rect headersRect = new(windowRect.x, titleRect.yMax + 10f, windowRect.width - 16f, 20f);

        float scrollY = headersRect.yMax + 5f;
        Rect outRect = new(windowRect.x, scrollY, windowRect.width, (addBtnRect.y - 15f) - scrollY);
        Rect viewRect = new(0f, 0f, outRect.width - 16f, comp.medicinePolicies.Count * Layout.RowHeight);

        DrawStaticUI(titleRect, addBtnRect);
        DrawHeaders(headersRect);
        DrawPolicyList(outRect, viewRect);
    }

    private static string GetPrettyName<T>(T enumValue) where T : Enum =>
        System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(enumValue.ToString().Replace("_", " ").ToLower());

    private void DrawStaticUI(Rect titleRect, Rect addBtnRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(titleRect, "Advanced Medicine Policies");
        Text.Font = GameFont.Small;

        if (Widgets.ButtonText(addBtnRect, "+ Add New Policy"))
        {
            comp.medicinePolicies.Add(new MedicinePolicy());
            scrollPosition.y = float.MaxValue;
        }
    }

    private void DrawHeaders(Rect rect)
    {
        RowBounds bounds = new(rect, rect.height);

        Text.Anchor = TextAnchor.LowerLeft;
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;

        Widgets.Label(bounds.Allegiance, "ALLEGIANCE");
        Widgets.Label(bounds.Conditions, "CONDITION");
        Widgets.Label(bounds.Medicine, "ALLOWED MEDICINE");

        Widgets.DrawLineHorizontal(rect.x, rect.yMax, rect.width);

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
    }

    private void DrawPolicyList(Rect outRect, Rect viewRect)
    {
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

        if (Event.current.type is EventType.Repaint)
        {
            SetupReorderableGroup(outRect, viewRect);
        }

        for (int i = 0; i < comp.medicinePolicies.Count; i++)
        {
            Rect rowRect = new(0f, i * Layout.RowHeight, viewRect.width, Layout.RowHeight);
            RowBounds bounds = new(rowRect, Layout.RowContentHeight);

            ReorderableWidget.Reorderable(reorderableGroupID, bounds.Drag);

            if (i % 2 == 0) Widgets.DrawHighlight(rowRect);

            if (DrawPolicyRow(bounds, comp.medicinePolicies[i], i)) break; 
        }

        Widgets.EndScrollView();
    }

    private void SetupReorderableGroup(Rect outRect, Rect viewRect)
    {
        reorderableGroupID = ReorderableWidget.NewGroup((from, to) =>
        {
            var item = comp.medicinePolicies[from];
            comp.medicinePolicies.RemoveAt(from);
            comp.medicinePolicies.Insert(from < to ? to - 1 : to, item);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }, ReorderableDirection.Vertical, outRect, -1f, (index, _) =>
        {
            var mousePos = UI.MousePositionOnUIInverted;
            Rect ghostRect = new(mousePos.x - 16f, mousePos.y - (Layout.RowHeight / 2f), viewRect.width, Layout.RowHeight);

            Find.WindowStack.ImmediateWindow(GhostWindowBaseId + index, ghostRect, WindowLayer.Super, () =>
            {
                Widgets.DrawWindowBackground(ghostRect.AtZero());
                DrawPolicyRow(new RowBounds(ghostRect.AtZero(), Layout.RowContentHeight), comp.medicinePolicies[index], index);
                Widgets.DrawBoxSolid(ghostRect.AtZero(), new Color(0f, 0f, 0f, 0.4f));
            }, doBackground: false, absorbInputAroundWindow: false, 0f);
        });
    }

    private bool DrawPolicyRow(RowBounds bounds, MedicinePolicy policy, int index)
    {
        GUI.DrawTexture(bounds.Drag, TexButton.DragHash);

        string allegianceLabel = policy.pawnClasses.Count == 0 ? "Allegiance: Any" : $"Allegiance ({policy.pawnClasses.Count})";
        if (Widgets.ButtonText(bounds.Allegiance, allegianceLabel))
        {
            OpenMultiSelectMenu(policy.pawnClasses, iconFunction: IconTextures.AllegianceIconMap.GetValueSafe);
        }

        DrawConditionSelector(policy, bounds.Conditions, bounds.Conditions.height / 2f);

        if (Widgets.ButtonImage(bounds.Delete, TexButton.CloseXSmall))
        {
            comp.medicinePolicies.RemoveAt(index);
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }

        MedicalCareUtility.MedicalCareSetter(bounds.Medicine, ref policy.allowedMedicine);

        return false;
    }

    private void DrawConditionSelector(MedicinePolicy policy, Rect rect, float iconSize)
    {
        // TODO: This floor to int is risky. If the result is slightly less because of floating
        //       point imprecision then it's fucked.
        int maxRows = Mathf.FloorToInt(rect.height / iconSize);
        int maxInRow = Mathf.FloorToInt(rect.width / iconSize);
        int maxDrawn = maxRows * maxInRow;

        int numDrawn = 0;
        foreach (PawnCondition condition in Enum.GetValues(typeof(PawnCondition)))
        {
            if (!IconTextures.ConditionIconMap.TryGetValue(condition, out var conditionTex)) continue;

            string niceName = GetPrettyName(condition);
            bool isSelected = policy.GetConditions().Contains(condition);

            float rowX = rect.x + (numDrawn % maxInRow) * iconSize;
            float rowY = rect.y + (numDrawn / maxInRow) * iconSize;
            Rect selectorRect = new(rowX, rowY, iconSize, iconSize);

            // Show the user if the condition's already selected.
            if (isSelected)
            {
                Widgets.DrawHighlight(selectorRect);
                Widgets.DrawBox(selectorRect);
            }
            else
            {
                Widgets.DrawHighlightIfMouseover(selectorRect);
            }

            TooltipHandler.TipRegion(selectorRect, niceName);
            if (Widgets.ButtonImage(selectorRect, conditionTex))
            {
                if (isSelected) policy.RemoveCondition(condition);
                else policy.AddCondition(condition);
            }

            if (++numDrawn >= maxDrawn) break;
        }
    }

    private void OpenMultiSelectMenu<T>(Func<T, bool> checkActive, Action<T, bool> onToggle, Vector2? fixedPosition = null, Func<T, Texture2D> iconFunction = null) where T : Enum
    {
        List<FloatMenuOption> options = [];
        FixedFloatMenu menu = null;

        foreach (T enumValue in Enum.GetValues(typeof(T)))
        {
            bool isActive = checkActive(enumValue);
            string niceName = GetPrettyName(enumValue);

            // TODO: Not great tbh. I explicitly hate how checkmark and two spaces don't have the same width.
            string label = isActive ? $"[✓] {niceName}" : $"[  ] {niceName}";

            options.Add(new FloatMenuOption(label, () =>
            {
                // Toggle the option inside the policy.
                onToggle?.Invoke(enumValue, !isActive);

                // Reopen the menu to show the updated state.
                OpenMultiSelectMenu(checkActive, onToggle, menu.Position, iconFunction);
            }, iconFunction?.Invoke(enumValue), Color.white));
        }

        // Invert button.
        options.Add(new FloatMenuOption("Invert Selection", () =>
        {
            foreach (T enumValue in Enum.GetValues(typeof(T)))
            {
                onToggle?.Invoke(enumValue, !checkActive(enumValue));
            }

            // Reopen the menu to show the new inverted state.
            OpenMultiSelectMenu(checkActive, onToggle, menu.Position, iconFunction);
        }));

        menu = new FixedFloatMenu(options, fixedPosition);
        Find.WindowStack.Add(menu);
    }

    private void OpenMultiSelectMenu<T>(HashSet<T> activeSet, Vector2? fixedPosition = null, Func<T, Texture2D> iconFunction = null) where T : Enum
    {
        OpenMultiSelectMenu<T>(
            checkActive: activeSet.Contains,
            onToggle: (val, isAdding) => 
            {
                if (isAdding) activeSet.Add(val);
                else activeSet.Remove(val);
            },
            fixedPosition,
            iconFunction
        );
    }
}

/// <summary>
/// Injects a custom button into the Assign tab to open the advanced medicine policies window.
/// </summary>
/// <remarks>
/// This patch takes the calculated widths of the assign tab data table to place the advanced
/// medicine policies window directly above the medical care column, which is an area that doesn't
/// contain a button in the vanilla game. The additional per-pawn mod toggle in the medicine column
/// makes it appear around twice as large as in vanilla, which is enough for a decently sized
/// button. Unfortunately there's not enough space to display the text "Manage Medicine Policies" on
/// the button so there's just an image of the mod on it.
/// </remarks>
/// <seealso cref="Dialog_AdvancedMedicinePolicies"/>
[HarmonyPatch(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.DoWindowContents))]
public static class Patch_MainTabWindow_Assign_DoWindowContents
{
    public static void Postfix(MainTabWindow_PawnTable __instance, Rect rect)
    {
        if (__instance is not MainTabWindow_Assign) return;

        var table = Traverse.Create(__instance).Field("table").GetValue();
        if (table is null) return;

        var tableProps = Traverse.Create(table);
        var cachedWidths = tableProps.Field<List<float>>("cachedColumnWidths").Value;
        
        var columns = tableProps.Field("def").Field<IList>("columns").Value;

        if (cachedWidths is null || columns is null || columns.Count != cachedWidths.Count) return;

        float exactX = rect.x;
        float exactWidth = 0f;

        for (int i = 0; i < columns.Count; i++)
        {
            if (Traverse.Create(columns[i]).Field<string>("defName").Value is "MedicalCare")
            {
                exactWidth = cachedWidths[i];
                break;
            }
            exactX += cachedWidths[i];
        }

        // If I didn't find the MedicalCare column then don't draw the button.
        if (exactWidth == 0f) return;

        Rect buttonRect = new(exactX, 0f, exactWidth, 33f);
        if (Widgets.ButtonImageWithBG(buttonRect, Textures.modPoliciesActive, Vector2.one * 24))
        {
            Find.WindowStack.Add(new Dialog_AdvancedMedicinePolicies());
        }
    }
}
