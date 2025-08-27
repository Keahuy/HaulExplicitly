using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Designator_Rehaul : Designator_Haul
{
    protected override DesignationDef Designation => null;

    public Designator_Rehaul()
    {
        defaultLabel = "HaulExplicitly.SetHaulableLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/Haulable", true);
        defaultDesc = "HaulExplicitly.SetHaulableDesc".Translate();
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = null;
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        

        return t.IsAHaulableSetToUnhaulable();
    }
}