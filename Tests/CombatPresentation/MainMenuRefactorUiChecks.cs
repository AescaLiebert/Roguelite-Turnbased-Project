#if UNITY_EDITOR
using System;
using System.IO;
using FightingAllstar.EditorTools;
using UnityEditor;

[InitializeOnLoad]
public static class MainMenuRefactorUiChecks
{
    private static double deadline;
    static MainMenuRefactorUiChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("MainMenuRefactorUiChecks", true);
        deadline = EditorApplication.timeSinceStartup + 240;
        ScreenLayoutPreview.RunMenuRefactor();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool("MainMenuRefactorUiChecks", false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 240;
        var path = "Artifacts/MainMenuRefactor/Verification.txt";
        if (File.Exists(path))
        {
            SessionState.SetBool("MainMenuRefactorUiChecks", false);
            var result = File.ReadAllText(path); UnityEngine.Debug.Log(result);
            EditorApplication.Exit(result.Contains("FAIL") ? 1 : 0);
        }
        else if (EditorApplication.timeSinceStartup > deadline)
        {
            SessionState.SetBool("MainMenuRefactorUiChecks", false);
            UnityEngine.Debug.LogError("Main menu UI checks timed out."); EditorApplication.Exit(1);
        }
    }
}
#endif

