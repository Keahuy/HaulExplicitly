using RimWorld;
using Verse;

namespace HaulExplicitly;

public class Notice : MapComponent
{
    private bool _flag = false;

    public Notice(Map map) : base(map)
    {
    }

    public override void MapComponentTick()
    {
        if (_flag) return;
        string text = "HaulExplicitly.UpdateNotice.title".Translate() + "\n\n" + "HaulExplicitly.UpdateNotice.description".Translate() + "\n2025.8.28 22:00(CST)";
        ChoiceLetter choiceLetter = LetterMaker.MakeLetter("HaulExplicitly.UpdateNotice.LetterTitle".Translate(), text, LetterDefOf.PositiveEvent);
        Find.LetterStack.ReceiveLetter(choiceLetter);
        Find.Archive.Remove(choiceLetter);
        _flag = true;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _flag, "IsUpdateInfoHasShow", false);
    }
}