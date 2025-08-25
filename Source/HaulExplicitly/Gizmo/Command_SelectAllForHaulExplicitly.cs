using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Command_SelectAllForHaulExplicitly : Command
{
    public Command_SelectAllForHaulExplicitly()
    {
        defaultLabel = "HaulExplicitly.SelectAllHaulExplicitlyLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/SelectHaulExplicitlyJob");
        defaultDesc = "HaulExplicitly.SelectAllHaulExplicitlyDesc".Translate();
        hotKey = null;
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        Selector selector = Find.Selector;
        List<object> selection = selector.SelectedObjects;
        Thing example = (Thing)selection.First();
        HaulExplicitlyPosting posting = HaulExplicitly.GetManager(example).PostingWithItem(example);
        foreach (object o in posting.items)
        {
            Thing t = o as Thing;
            if (!selection.Contains(o) && t != null && t.SpawnedOrAnyParentSpawned)
                selector.Select(o);
        }
    }

    public static bool RelevantToThing(Thing t)
    {
        var mgr = HaulExplicitly.GetManager(t);
        HaulExplicitlyPosting posting = mgr.PostingWithItem(t);
        if (posting == null)
            return false;
        foreach (object o in Find.Selector.SelectedObjects)
        {
            Thing other = o as Thing;
            if (other == null || !posting.items.Contains(other))
                return false;
        }

        return Find.Selector.SelectedObjects.Count < Enumerable.Count(posting.items, i => i.SpawnedOrAnyParentSpawned);
    }
}