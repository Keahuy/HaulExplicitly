using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Designator_Unhaul : Designator_Haul
{
    protected override DesignationDef Designation => null;
    public Designator_Unhaul()
    {
        defaultLabel = "HaulExplicitly.SetUnhaulableLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/Unhaulable");
        defaultDesc = "HaulExplicitly.SetUnhaulableDesc".Translate();
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = null;
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        return t.IsAHaulableSetToHaulable();
    }
}