#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MultiHitVisualChecks
{
    static MultiHitVisualChecks() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("MultiHitVisualChecks", true); EditorApplication.isPlaying = true; }
    private static void Tick()
    {
        if (!SessionState.GetBool("MultiHitVisualChecks", false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool("MultiHitVisualChecks", false);
        new GameObject("MultiHitVisualChecks").AddComponent<MultiHitVisualRunner>();
    }
}

public sealed class MultiHitVisualRunner : MonoBehaviour
{
    private Camera _camera;
    private BattleStagePresenter _stage;
    private readonly Dictionary<string, GameObject> _views = new Dictionary<string, GameObject>();
    private string _output;
    private IEnumerator Start() { yield return Guard(Run()); }
    private IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception error) { Debug.LogError("MULTIHIT VISUAL FAIL: " + error); EditorApplication.Exit(1); yield break; }
            if (next is IEnumerator nested) yield return Guard(nested); else yield return next;
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private void Fighter(string id, Vector3 position, Color color)
    {
        var root = new GameObject(id);
        root.transform.position = position;
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(root.transform, false); body.transform.localPosition = Vector3.up;
        var material = new Material(Shader.Find("Standard")); material.color = color;
        body.GetComponent<Renderer>().sharedMaterial = material;
        _views.Add(id, root);
    }
    private void Capture(string name)
    {
        _camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = _camera.targetTexture;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous; Destroy(image);
    }
    private IEnumerator Run()
    {
        _output = Path.Combine(Directory.GetCurrentDirectory(), "Evidence");
        Directory.CreateDirectory(_output);
        _camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        _camera.tag = "MainCamera";
        _camera.gameObject.AddComponent<AudioListener>();
        _camera.transform.SetPositionAndRotation(new Vector3(0, 9, -12), Quaternion.Euler(32, 0, 0));
        _camera.backgroundColor = new Color(.045f, .055f, .08f);
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.targetTexture = new RenderTexture(960, 540, 24);
        var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(45, -25, 0);
        RenderSettings.ambientLight = new Color(.5f, .5f, .55f);
        Fighter("attacker", new Vector3(0, 0, -4), new Color(.2f, .65f, 1));
        Fighter("target", new Vector3(0, 0, 3), new Color(1, .25f, .2f));
        Fighter("left", new Vector3(-2.6f, 0, 3), new Color(1, .4f, .25f));
        Fighter("right", new Vector3(2.6f, 0, 3), new Color(1, .4f, .25f));
        _stage = new GameObject("Stage").AddComponent<BattleStagePresenter>();
        _stage.Initialize(_views);
        var home = _views["attacker"].transform.position;
        for (var i = 0; i < DamageAnimationTemplates.All.Count; i++)
        {
            var preset = DamageAnimationTemplates.All[i];
            var targets = preset.Area ? new[] { "target", "left", "right" } : new[] { "target" };
            yield return _stage.BeginExecution("attacker", "target", 1, false);
            yield return _stage.BeginDamageAttack("attacker", targets, preset.Hits, preset.Range, preset.Area);
            Require((preset.Range == AttackRange.Long) == (Vector3.Distance(home, _views["attacker"].transform.position) < .01f), "Wrong range travel: " + preset.Name);
            for (var hit = 1; hit <= preset.Hits; hit++)
            {
                yield return _stage.DamageHit(hit, targets);
                foreach (var id in targets)
                {
                    var point = _camera.WorldToViewportPoint(_views[id].transform.position + Vector3.up);
                    Require(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1, "Target offscreen: " + preset.Name);
                }
                if (hit == preset.Hits) Capture(i.ToString("D2") + "-" + preset.Name.Replace(' ', '-'));
                var impacts = new List<(string, bool)>(); foreach (var id in targets) impacts.Add((id, hit == preset.Hits));
                yield return _stage.ImpactMultiple(impacts);
            }
            yield return _stage.WaitForLastHit();
            yield return _stage.RecoverAttacker();
            Require(Vector3.Distance(home, _views["attacker"].transform.position) < .001f, "Recovery failed: " + preset.Name);
            _stage.ClearAuras();
            Debug.Log("MULTIHIT VISUAL PASS: " + preset.Name);
        }
        yield return _stage.BeginExecution("attacker", "target", 1, false);
        yield return _stage.BeginDamageAttack("attacker", new[] { "target" }, 10, AttackRange.Close, false);
        _stage.SnapToPlanning(); _stage.ClearAuras();
        Require(Vector3.Distance(home, _views["attacker"].transform.position) < .001f, "Skip failed to restore the attacker.");
        Require(_views["attacker"].transform.localScale == Vector3.one, "Skip failed to restore scale.");
        Debug.Log("MULTIHIT VISUAL PASS: all 16 templates, camera framing, recovery and skip.");
        EditorApplication.Exit(0);
    }
}
#endif
