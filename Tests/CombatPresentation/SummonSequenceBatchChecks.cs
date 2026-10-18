#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using FightingAllstar.EditorTools;
using UnityEditor;
using UnityEngine;

// This file is copied only into the dedicated preview project, which excludes ScreenLayoutPreview.
namespace FightingAllstar.EditorTools
{
    internal static class UiPreviewSession
    {
        public static bool IsBusy { get; private set; }
        public static void Acquire() => IsBusy = true;
        public static void Release() => IsBusy = false;
    }
}

public static class SummonSequenceBatchChecks
{
    private static bool _active;
    private static double _deadline;
    private static System.Collections.Generic.IEnumerator<object> _flows;

    public static void Run()
    {
        _active = true;
        _deadline = EditorApplication.timeSinceStartup + 300;
        EditorApplication.update += Tick;
        if (Environment.GetCommandLineArgs().Contains("-summonMotion")) SummonSequencePreview.RunMotion();
        else SummonSequencePreview.Run();
    }

    private static void Tick()
    {
        if (!_active) return;
        if (SummonSequencePreview.IsRunning && EditorApplication.timeSinceStartup < _deadline) return;
        var passed = SummonSequencePreview.Status == "PASS";
        if (passed)
        {
            try
            {
                if (_flows == null) _flows = ObtainFlowIntegrationChecks.Run().GetEnumerator();
                if (_flows.MoveNext()) return;
                _flows.Dispose();
            }
            catch (Exception error)
            {
                _flows?.Dispose(); Debug.LogException(error); passed = false;
                File.WriteAllText("Artifacts/SummonSequence/ObtainFlows.txt", "FAIL " + error);
            }
        }
        _active = false;
        if (!passed) Debug.LogError("Summon/obtain batch validation failed. See visual or ObtainFlows report.");
        try
        {
            var args = Environment.GetCommandLineArgs();
            var option = Array.IndexOf(args, "-summonEvidencePath");
            if (option >= 0 && option + 1 < args.Length && Directory.Exists(SummonSequencePreview.OutputFolder))
                CopyEvidence(Path.GetFullPath(SummonSequencePreview.OutputFolder), Path.GetFullPath(args[option + 1]));
        }
        catch (Exception error) { Debug.LogException(error); passed = false; }
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void CopyEvidence(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var directory in Directory.GetDirectories(source)) CopyEvidence(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
#endif
