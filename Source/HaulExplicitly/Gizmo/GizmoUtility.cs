using Verse;

namespace HaulExplicitly.Gizmo;

public class GizmoUtility
{
    public static IEnumerable<Verse.Gizmo> GetHaulExplicitlyGizmos(Thing t)
    {
        if (t.def.EverHaulable)
        {
            yield return new Designator_HaulExplicitly();
            if (Command_Cancel_HaulExplicitly.RelevantToThing(t))
                yield return new Command_Cancel_HaulExplicitly(t);
            if (Command_SelectAllForHaulExplicitly.RelevantToThing(t))
                yield return new Command_SelectAllForHaulExplicitly();
        }
    }
}