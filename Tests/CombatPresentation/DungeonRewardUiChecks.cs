#if UNITY_EDITOR
using System;
using System.IO;
using FightingAllstar.EditorTools;
using UnityEditor;

[InitializeOnLoad]
public static class DungeonRewardUiChecks
{
    private static double deadline;
    static DungeonRewardUiChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("DungeonRewardUiChecks", true);
        deadline = EditorApplication.timeSinceStartup + 240;
        ScreenLayoutPreview.RunDungeonRewards();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool("DungeonRewardUiChecks", false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 240;
        var path = "Artifacts/DungeonRewards/Verification.txt";
        if (File.Exists(path))
        {
            SessionState.SetBool("DungeonRewardUiChecks", false);
            var result = File.ReadAllText(path); UnityEngine.Debug.Log(result);
            EditorApplication.Exit(result.Contains("FAIL") ? 1 : 0);
        }
        else if (EditorApplication.timeSinceStartup > deadline)
        {
            SessionState.SetBool("DungeonRewardUiChecks", false);
            UnityEngine.Debug.LogError("Dungeon reward UI checks timed out."); EditorApplication.Exit(1);
        }
    }
}
#endif
