#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CameraReferenceChecks
{
    static CameraReferenceChecks() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("CameraChecks", true); EditorApplication.isPlaying = true; }
    static void Tick()
    {
        if (!SessionState.GetBool("CameraChecks", false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool("CameraChecks", false);
        new GameObject("CameraChecks").AddComponent<CameraCheckRunner>();
    }
}
public sealed class CameraCheckRunner : MonoBehaviour
{
    Camera camera;
    BattleStagePresenter stage;
    Dictionary<string, GameObject> views = new Dictionary<string, GameObject>();
    int checks;
    string output;
    IEnumerator Start() { yield return Guard(Run()); }
    IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception e) { Debug.LogError("CAMERA CHECK FAIL " + e); EditorApplication.Exit(1); yield break; }
            if (next is IEnumerator nested) yield return Guard(nested); else yield return next;
        }
    }
    void Require(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    void Capture(string name)
    {
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = camera.targetTexture;
        var image = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous; Destroy(image);
    }
    void InFrame(params string[] ids)
    {
        foreach (var id in ids)
        {
            var bounds = views[id].GetComponent<Renderer>().bounds;
            for (var x = -1; x <= 1; x += 2) for (var y = -1; y <= 1; y += 2) for (var z = -1; z <= 1; z += 2)
            {
                var p = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x,y,z)));
                Require(p.z > 0 && p.x > .02f && p.x < .98f && p.y > .06f && p.y < .94f, id + " clipped: " + p);
            }
        }
    }
    IEnumerator Continuous(IEnumerator action, string name)
    {
        bool done = false;
        var routine = StartCoroutine(Complete(action, () => done = true));
        var rotation = camera.transform.rotation;
        int frame = 0;
        while (!done)
        {
            yield return null;
            var angle = Quaternion.Angle(rotation, camera.transform.rotation);
            // A frame can span more than 1/60 s in batch rendering; reject a jump relative to elapsed time.
            Require(angle < Mathf.Max(35f, Time.unscaledDeltaTime * 650f), name + " abrupt rotation " + angle);
            rotation = camera.transform.rotation;
            if (++frame == 10) Capture(name + "-moving");
        }
        yield return routine;
    }
    IEnumerator Complete(IEnumerator action, Action finish) { yield return Guard(action); finish(); }
    IEnumerator Run()
    {
        output = Path.GetFullPath("Evidence"); Directory.CreateDirectory(output);
        camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f,.16f,.2f);
        camera.transform.SetPositionAndRotation(new Vector3(0,10,-16), Quaternion.Euler(25,0,0));
        camera.fieldOfView = 55f;
        var light = new GameObject("Light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45,30,0);
        RenderSettings.ambientLight = Color.gray;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * 5;
        foreach (var team in new[] { "p", "e" }) for (var i = 0; i < 3; i++)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule); obj.name = team + i;
            obj.transform.position = new Vector3((i - 1) * 3.2f, 1, team == "p" ? -4 : 4);
            obj.transform.rotation = Quaternion.Euler(0, team == "p" ? 0 : 180, 0);
            obj.GetComponent<Renderer>().material.color = team == "p" ? new Color(.12f,.5f,.9f) : new Color(.9f,.25f,.12f);
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube); face.transform.SetParent(obj.transform, false);
            face.transform.localPosition = new Vector3(0,.55f,.45f); face.transform.localScale = new Vector3(.4f,.2f,.12f);
            face.GetComponent<Renderer>().material.color = Color.white;
            views.Add(obj.name, obj);
        }
        new GameObject("CharHeroPosition2").transform.position = new Vector3(0,0,-4);
        new GameObject("EnemyHeroPosition2").transform.position = new Vector3(0,0,4);
        stage = new GameObject("Stage").AddComponent<BattleStagePresenter>(); stage.Initialize(views);
        foreach (var portrait in new[] { true, false })
        {
            camera.targetTexture = new RenderTexture(portrait ? 540 : 960, portrait ? 960 : 540, 24); camera.targetTexture.Create();
            camera.aspect = portrait ? 540f/960f : 960f/540f;
            var prefix = portrait ? "portrait" : "landscape";
            for (var rank=1; rank<=3; rank++)
            {
                yield return stage.ReturnToPlanning(TeamSide.Player, .05f);
                yield return stage.BeginExecution("p1", "e1", rank, false);
                Capture(prefix + "-rank" + rank + "-intro");
                if (rank > 1) Require(Vector3.Dot(camera.transform.position - views["p1"].transform.position, views["p1"].transform.forward) > 0, "portrait behind caster");
                yield return Continuous(stage.Attack("p1", "e1"), prefix + "-rank" + rank);
                InFrame("p1", "e1"); Capture(prefix + "-rank" + rank + "-attack");
                Require(Vector3.Dot(camera.transform.position - views["p1"].transform.position, Vector3.forward) < 0, "attack not behind caster");
                yield return stage.Impact("e1", true); InFrame("p1", "e1");
            }
            yield return stage.BeginExecution("e0", "p2", 3, false);
            yield return Continuous(stage.AttackArea("e0", new[] { "p0", "p1", "p2" }), prefix + "-enemy-area");
            InFrame("e0", "p0", "p1", "p2"); Capture(prefix + "-enemy-area");
            foreach (var category in new[] { CardCategory.Stance, CardCategory.Buff, CardCategory.Recovery, CardCategory.Debuff })
            {
                yield return stage.BeginExecution("p1", "p1", 3, false);
                var ids = category == CardCategory.Stance ? new[] { "p1" } : category == CardCategory.Debuff ? new[] { "e0", "e1", "e2" } : new[] { "p0", "p1", "p2" };
                yield return Continuous(stage.SupportAction("p1", ids, category), prefix + "-" + category);
                InFrame(ids); Capture(prefix + "-" + category);
            }
            foreach (var category in new[] { CardCategory.Stance, CardCategory.Buff, CardCategory.Recovery, CardCategory.Debuff })
            {
                yield return stage.ReturnToPlanning(TeamSide.Player, .05f);
                var beforeRank1 = camera.transform.position;
                yield return stage.BeginExecution("p1", "e1", 1, false);
                Require(Vector3.Distance(beforeRank1, camera.transform.position) < .001f, "Rank1 must not cut to portrait");
                var support = stage.SupportAction("p1", category == CardCategory.Debuff ? new[] { "e1" } : new[] { "p1" }, category);
                Require(support.MoveNext(), "Support should start its cast");
                Require(Vector3.Dot(camera.transform.position - views["p1"].transform.position, views["p1"].transform.forward) <= 0.05f ||
                    category == CardCategory.Buff || category == CardCategory.Recovery, "Rank1 utility must not enter portrait before action");
                if (category == CardCategory.Stance)
                    Require(Vector3.Dot(camera.transform.position - views["p1"].transform.position, views["p1"].transform.forward) < 0f, "Rank1 stance must not face front");
                // Resume the nested yield obtained above, then the remaining sequence.
                if (support.Current is IEnumerator first) yield return first;
                yield return support;
                Capture(prefix + "-rank1-" + category);
            }
            yield return stage.BeginExecution("p0", "p2", 1, false);
            yield return stage.SupportAction("p0", new[] { "p2" }, CardCategory.Recovery);
            InFrame("p2"); Capture(prefix + "-single-heal");
            yield return stage.BeginExecution("p2", "e0", 1, true); Capture(prefix + "-ultimate-reveal");
            yield return Continuous(stage.Attack("p2", "e0"), prefix + "-ultimate"); InFrame("p2", "e0");
            stage.SnapToPlanning(); var saved = camera.transform.position; yield return null; yield return null;
            Require(Vector3.Distance(saved,camera.transform.position) < .001f, "stale camera tracking after restore");
            camera.targetTexture.Release(); Destroy(camera.targetTexture); camera.targetTexture = null;
        }
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS " + checks + " camera assertions");
        Debug.Log("CAMERA CHECK PASS " + checks); EditorApplication.Exit(0);
    }
}

#endif
