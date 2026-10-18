namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        void ShowAchievementBook()
        {
            var progress=session.RefugeActivities;
            string body="Achievements are saved for this browser profile.\n\n";
            for(int i=0;i<5;i++)body+=(progress!=null&&progress.IsUnlocked(i)?"EARNED · ":"LOCKED · ")+RefugeActivityDirector.AchievementName(i)+"\n\n";
            ShowLoreBook("Oracle achievements",body);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing pause-menu book modal; no world or combat mutation.
// Array Formats: Five fixed achievement entries, strings constructed only on an explicit menu click.
// Dependencies/Pointers: RefugeActivityDirector owns once-only persisted achievement bits and sound dispatch.
