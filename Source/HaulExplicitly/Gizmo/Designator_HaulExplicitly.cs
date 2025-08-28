using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Designator_HaulExplicitly : Designator
{
    private static HaulExplicitlyPosting? _prospectiveJob;

    public static void ResetJob()
    {
        _prospectiveJob = null;
    }

    public static void UpdateJob()
    {
        List<object> objects = Find.Selector.SelectedObjects;
        // 获取所有需要搬运的物品
        _prospectiveJob = new HaulExplicitlyPosting(objects);
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

    // 选定目标地点时
    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        HaulExplicitlyPosting? posting = _prospectiveJob;
        return posting != null && posting.TryMakeDestinations(UI.MouseMapPosition());
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        HaulExplicitlyPosting posting = _prospectiveJob ?? throw new InvalidOperationException();
        posting.TryMakeDestinations(UI.MouseMapPosition(), false);
        HaulExplicitly.RegisterPosting(posting);
        ResetJob();
    }

    public override bool CanRemainSelected()
    {
        return _prospectiveJob != null;
    }

    public override void Selected()
    {
        ResetJob();
        UpdateJob();
    }

    public override void SelectedUpdate()
    {
        HaulExplicitlyPosting? posting = _prospectiveJob;
        if (posting == null)
            return;
        if (posting.TryMakeDestinations(UI.MouseMapPosition()) && posting.Destinations != null)
        {
            float alt = AltitudeLayer.MetaOverlays.AltitudeFor();
            foreach (IntVec3 d in posting.Destinations)
            {
                Vector3 drawPos = d.ToVector3ShiftedWithAltitude(alt);
                Graphics.DrawMesh(MeshPool.plane10, drawPos, Quaternion.identity,
                    DesignatorUtility.DragHighlightThingMat, 0);
            }
        }
    }

    private Vector2 _scrollPosition = Vector2.zero;
    private float _guiLastDrawnHeight = 0;

    public override void DoExtraGuiControls(float leftX, float bottomY)
    {
        HaulExplicitlyPosting posting = _prospectiveJob ?? throw new InvalidOperationException();
        var records = new List<HaulExplicitlyInventoryRecord>(
            posting.Inventory.OrderBy(r => r.Label));
        const float max_height = 450f;
        const float width = 268f;
        const float row_height = 28f;
        float height = Math.Min(_guiLastDrawnHeight + 20f, max_height);
        Rect winRect = new Rect(leftX, bottomY - height, width, height);
        Rect outerRect = new Rect(0f, 0f, width, height).ContractedBy(10f);
        Rect innerRect = new Rect(0f, 0f, outerRect.width - 16f, Math.Max(_guiLastDrawnHeight, outerRect.height));
        Find.WindowStack.ImmediateWindow(622372, winRect, WindowLayer.GameUI, delegate
        {
            Widgets.BeginScrollView(outerRect, ref _scrollPosition, innerRect, true);
            GUI.BeginGroup(innerRect);
            GUI.color = ITab_Pawn_Gear.ThingLabelColor;
            GameFont prev_font = Text.Font;
            Text.Font = GameFont.Small;
            float y = 0f;
            Widgets.ListSeparator(ref y, innerRect.width, "Items to haul");
            foreach (var rec in records)
            {
                Rect rowRect = new Rect(0f, y, innerRect.width - 24f, 28f);
                if (rec.SelectedQuantity > 1)
                {
                    Rect buttonRect = new Rect(rowRect.x + rowRect.width,
                        rowRect.y + (rowRect.height - 24f) / 2, 24f, 24f);
                    if (Widgets.ButtonImage(buttonRect,
                            RimWorld.Planet.CaravanThingsTabUtility.AbandonSpecificCountButtonTex))
                    {
                        string txt = "HaulExplicitly.ItemHaulSetQuantity".Translate(new NamedArgument((rec.ItemDef.label).CapitalizeFirst(), "ITEMTYPE"));
                        var dialog = new Dialog_Slider(txt, 1, rec.SelectedQuantity, delegate(int x) { rec.SetQuantity = x; }, rec.SetQuantity);
                        dialog.layer = WindowLayer.GameUI;
                        Find.WindowStack.Add(dialog);
                    }
                }

                if (Mouse.IsOver(rowRect))
                {
                    GUI.color = ITab_Pawn_Gear.HighlightColor;
                    GUI.DrawTexture(rowRect, TexUI.HighlightTex);
                }

                if (rec.ItemDef.DrawMatSingle?.mainTexture != null)
                {
                    Rect iconRect = new Rect(4f, y, 28f, 28f);
                    if (rec.MiniDef != null || rec.SelectedQuantity == 1)
                        Widgets.ThingIcon(iconRect, rec.Items[0]);
                    else
                        Widgets.ThingIcon(iconRect, rec.ItemDef);
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Rect textRect = new Rect(36f, y, rowRect.width - 36f, rowRect.height);
                string str = rec.Label;
                Widgets.Label(textRect, str.Truncate(textRect.width));

                y += row_height;
            }

            _guiLastDrawnHeight = y;
            Text.Font = prev_font;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            GUI.EndGroup();
            Widgets.EndScrollView();
        }, true, false, 1f);
    }
}