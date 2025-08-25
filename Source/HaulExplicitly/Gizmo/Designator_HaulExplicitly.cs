﻿using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Designator_HaulExplicitly : Designator
{
    private static HaulExplicitlyPosting prospective_job;

    public static void ResetJob()
    {
        prospective_job = null;
    }

    public static void UpdateJob()
    {
        List<object> objects = Find.Selector.SelectedObjects;
        prospective_job = new HaulExplicitlyPosting(objects);
    }

    public Designator_HaulExplicitly()
    {
        defaultLabel = "HaulExplicitly.HaulExplicitlyLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/HaulExplicitly");
        defaultDesc = "HaulExplicitly.HaulExplicitlyDesc".Translate();
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = null;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        HaulExplicitlyPosting posting = prospective_job;
        if (posting == null)
            return false;
        return posting.TryMakeDestinations(UI.MouseMapPosition());
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        HaulExplicitlyPosting posting = prospective_job;
        posting.TryMakeDestinations(UI.MouseMapPosition(), false);
        HaulExplicitly.RegisterPosting(posting);
        ResetJob();
    }

    public override bool CanRemainSelected()
    {
        return prospective_job != null;
    }

    public override void Selected()
    {
        ResetJob();
        UpdateJob();
    }

    public override void SelectedUpdate()
    {
        HaulExplicitlyPosting posting = prospective_job;
        if (posting == null)
            return;
        if (posting.TryMakeDestinations(UI.MouseMapPosition()) && posting.destinations != null)
        {
            float alt = AltitudeLayer.MetaOverlays.AltitudeFor();
            foreach (IntVec3 d in posting.destinations)
            {
                Vector3 drawPos = d.ToVector3ShiftedWithAltitude(alt);
                Graphics.DrawMesh(MeshPool.plane10, drawPos, Quaternion.identity,
                    DesignatorUtility.DragHighlightThingMat, 0);
            }
        }
    }

    private Vector2 scrollPosition = Vector2.zero;
    private float gui_last_drawn_height = 0;

    public override void DoExtraGuiControls(float leftX, float bottomY)
    {
        HaulExplicitlyPosting posting = prospective_job;
        var records = new List<HaulExplicitlyInventoryRecord>(
            posting.inventory.OrderBy(r => r.Label));
        const float max_height = 450f;
        const float width = 268f;
        const float row_height = 28f;
        float height = Math.Min(gui_last_drawn_height + 20f, max_height);
        Rect winRect = new Rect(leftX, bottomY - height, width, height);
        Rect outerRect = new Rect(0f, 0f, width, height).ContractedBy(10f);
        Rect innerRect = new Rect(0f, 0f, outerRect.width - 16f, Math.Max(gui_last_drawn_height, outerRect.height));
        Find.WindowStack.ImmediateWindow(622372, winRect, WindowLayer.GameUI, delegate
        {
            Widgets.BeginScrollView(outerRect, ref scrollPosition, innerRect, true);
            GUI.BeginGroup(innerRect);
            GUI.color = ITab_Pawn_Gear.ThingLabelColor;
            GameFont prev_font = Text.Font;
            Text.Font = GameFont.Small;
            float y = 0f;
            Widgets.ListSeparator(ref y, innerRect.width, "Items to haul");
            foreach (var rec in records)
            {
                Rect rowRect = new Rect(0f, y, innerRect.width - 24f, 28f);
                if (rec.selectedQuantity > 1)
                {
                    Rect buttonRect = new Rect(rowRect.x + rowRect.width,
                        rowRect.y + (rowRect.height - 24f) / 2, 24f, 24f);
                    if (Widgets.ButtonImage(buttonRect,
                            RimWorld.Planet.CaravanThingsTabUtility.AbandonSpecificCountButtonTex))
                    {
                        string txt = "HaulExplicitly.ItemHaulSetQuantity".Translate(new NamedArgument((rec.itemDef.label).CapitalizeFirst(), "ITEMTYPE"));
                        var dialog = new Dialog_Slider(txt, 1, rec.selectedQuantity, delegate(int x) { rec.setQuantity = x; }, rec.setQuantity);
                        dialog.layer = WindowLayer.GameUI;
                        Find.WindowStack.Add(dialog);
                    }
                }

                if (Mouse.IsOver(rowRect))
                {
                    GUI.color = ITab_Pawn_Gear.HighlightColor;
                    GUI.DrawTexture(rowRect, TexUI.HighlightTex);
                }

                if (rec.itemDef.DrawMatSingle?.mainTexture != null)
                {
                    Rect iconRect = new Rect(4f, y, 28f, 28f);
                    if (rec.miniDef != null || rec.selectedQuantity == 1)
                        Widgets.ThingIcon(iconRect, rec.items[0]);
                    else
                        Widgets.ThingIcon(iconRect, rec.itemDef);
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Rect textRect = new Rect(36f, y, rowRect.width - 36f, rowRect.height);
                string str = rec.Label;
                Widgets.Label(textRect, str.Truncate(textRect.width));

                y += row_height;
            }

            gui_last_drawn_height = y;
            Text.Font = prev_font;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            GUI.EndGroup();
            Widgets.EndScrollView();
        }, true, false, 1f);
    }
}