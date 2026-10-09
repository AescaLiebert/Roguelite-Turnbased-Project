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
public static class HitReactionVisualChecks
{
    static HitReactionVisualChecks() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("HitReactionVisualChecks", true); EditorApplication.isPlaying = true; }
    private static void Tick()
    {
        if (!SessionState.GetBool("HitReactionVisualChecks", false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool("HitReactionVisualChecks", false);
        new GameObject("HitReactionChecks").AddComponent<HitReactionVisualRunner>();
    }
}

public sealed class HitReactionVisualRunner : MonoBehaviour
{
    private BattleStagePresenter _stage;
    private Camera _camera;
    private readonly Dictionary<string, GameObject> _views = new Dictionary<string, GameObject>();
    private int _checks;
    private string _output;
    private bool _checkCamera;
    private Pose _heldCamera;
    private float _heldFov;
    private void LateUpdate()
    {
        if (!_checkCamera) return;
        if (Vector3.Distance(_camera.transform.position, _heldCamera.position) > .0001f ||
            Quaternion.Angle(_camera.transform.rotation, _heldCamera.rotation) > .01f ||
            Mathf.Abs(_camera.fieldOfView - _heldFov) > .001f)
        {
            Debug.LogError("REACTION VISUAL FAIL: Attack camera followed movement, shook or reframed.");
            EditorApplication.Exit(1);
        }
        _checks++;
    }
    private void HoldCamera()
    {
        _heldCamera = new Pose(_camera.transform.position, _camera.transform.rotation);
        _heldFov = _camera.fieldOfView; _checkCamera = true;
    }
    private IEnumerator Start() { yield return Guard(Run()); }
    private IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception error) { Debug.LogError("REACTION VISUAL FAIL: " + error); EditorApplication.Exit(1); yield break; }
            if (next is IEnumerator nested) yield return Guard(nested); else yield return next;
        }
    }
    private void Require(bool condition, string message) { _checks++; if (!condition) throw new Exception(message); }
    private void Fighter(string id, Vector3 position, float yaw, Color color, bool centerPivot = false)
    {
        var root = new GameObject(id); root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(root.transform, false); body.transform.localPosition = centerPivot ? Vector3.zero : Vector3.up;
        var material = new Material(Shader.Find("Standard")); material.color = color;
        body.GetComponent<Renderer>().sharedMaterial = material;
        // A front marker makes face-down versus face-up orientation visible.
        var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.transform.SetParent(root.transform, false); face.transform.localPosition = new Vector3(0, centerPivot ? .55f : 1.55f, .43f);
        face.transform.localScale = new Vector3(.4f, .16f, .1f);
        _views.Add(id, root);
    }
    private BattleEvent Hit(string id, HitReaction reaction) => new BattleEvent {
        SourceId = "source", TargetId = id, HasHitReaction = true, Reaction = reaction, HitIndex = 3, HitCount = 3 };
    private void Capture(string name)
    {
        _camera.Render(); var previous = RenderTexture.active; RenderTexture.active = _camera.targetTexture;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous; Destroy(image);
    }
    private void Restored(string id, Vector3 position, Quaternion rotation)
    {
        var target = _views[id].transform;
        Require(Vector3.Distance(target.position, position) < .001f, id + " position did not recover.");
        Require(Quaternion.Angle(target.rotation, rotation) < .01f, id + " facing did not recover.");
        Require(target.localScale == Vector3.one, id + " scale did not recover.");
    }
    private void SampleReactionPhase(string id, float phase)
    {
        // Screenshot rendering can consume the short get-up window on a busy Editor.
        // Sample that geometry deterministically; normal coroutine playback is tested below.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var poses = (IDictionary)typeof(BattleStagePresenter).GetField("_reactionPoses", flags).GetValue(_stage);
        var entry = poses[_views[id].transform];
        Require(entry != null, "No active reaction to sample.");
        var type = entry.GetType();
        var duration = (float)type.GetField("Duration").GetValue(entry);
        type.GetField("Elapsed").SetValue(entry, duration * phase);
        typeof(BattleStagePresenter).GetMethod("AnimateReaction", flags).Invoke(_stage, new[] { entry, (object)phase });
    }
    private IEnumerator Run()
    {
        _output = Path.Combine(Directory.GetCurrentDirectory(), "Evidence"); Directory.CreateDirectory(_output);
        _camera = new GameObject("Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        _camera.tag = "MainCamera"; _camera.targetTexture = new RenderTexture(960, 540, 24);
        _camera.transform.SetPositionAndRotation(new Vector3(7, 6, -8), Quaternion.LookRotation(new Vector3(0, 1, 1) - new Vector3(7, 6, -8)));
        _camera.backgroundColor = new Color(.07f, .08f, .1f); _camera.clearFlags = CameraClearFlags.SolidColor;
        var light = new GameObject("Light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45, -30, 0); light.intensity = 1.5f;
        RenderSettings.ambientLight = Color.gray;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.localScale = Vector3.one * 2;
        Fighter("source", new Vector3(0, 0, -3), 0, new Color(.2f, .6f, 1));
        Fighter("target", new Vector3(0, 0, 2), 180, new Color(1, .35f, .15f));
        Fighter("stance", new Vector3(-2.5f, 0, 2), 180, new Color(.2f, 1, .5f));
        Fighter("center-pivot", new Vector3(2.5f, 1, 2), 180, new Color(.85f, .4f, 1), true);
        var productionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/In-Game-CharacterPrefab.prefab");
        Require(productionPrefab != null, "Production centre-pivot prefab missing from fixture.");
        var production = BattleFighterView.Create(productionPrefab, new Vector3(0, 1.5f, 6), Quaternion.Euler(0, 180, 0), 0);
        _views.Add("production", production);
        _stage = new GameObject("Stage").AddComponent<BattleStagePresenter>(); _stage.Initialize(_views);
        var target = _views["target"].transform;
        var home = target.position; var rotation = target.rotation;
        var ultimate = ScriptableObject.CreateInstance<UltimateCardSO>();
        Require(ultimate.levels.Count == 1 && ultimate.levels[0].skillType == SkillType.Ultimate,
            "New ultimate SO did not provide an initial tier.");
        var defaultAttack = ultimate.levels[0].runtimeEffect.Attack;
        Require(defaultAttack.Reaction == HitReaction.KnockUp && defaultAttack.ReactionTiming == HitReactionTiming.LastHit,
            "New ultimate SO must default to last-hit knockup.");
        Require(new UltimateLevelData().runtimeEffect.Attack.Reaction == HitReaction.KnockUp,
            "New ultimate tier did not inherit knockup defaults.");
        defaultAttack.Reaction = HitReaction.KnockDown;
        defaultAttack.ReactionTiming = HitReactionTiming.EveryHit;
        var authored = EditorJsonUtility.ToJson(ultimate);
        var reloaded = ScriptableObject.CreateInstance<UltimateCardSO>();
        EditorJsonUtility.FromJsonOverwrite(authored, reloaded);
        Require(reloaded.levels[0].runtimeEffect.Attack.Reaction == HitReaction.KnockDown &&
            reloaded.levels[0].runtimeEffect.Attack.ReactionTiming == HitReactionTiming.EveryHit,
            "Loading an authored ultimate replaced its override.");
        Destroy(ultimate); Destroy(reloaded);
        foreach (var kind in new[] { HitReaction.Hit, HitReaction.None, HitReaction.KnockBack, HitReaction.KnockDown, HitReaction.KnockUp })
        {
            var routine = StartCoroutine(_stage.React(Hit("target", kind)));
            yield return new WaitForSecondsRealtime(kind == HitReaction.KnockDown ? .29f : .19f);
            if (kind == HitReaction.None) Restored("target", home, rotation);
            if (kind == HitReaction.KnockBack) Require(target.position.z > home.z + .5f, "Knockback did not move away from source.");
            if (kind == HitReaction.KnockDown)
            {
                Require(Vector3.Dot(target.forward, Vector3.down) > .95f, "Trip was not face down.");
                var center = target.GetComponentInChildren<Renderer>().bounds.center;
                Require(Mathf.Abs(center.x - home.x) < .01f && Mathf.Abs(center.z - home.z) < .01f, "Trip moved to another place.");
            }
            if (kind == HitReaction.KnockUp)
            {
                SampleReactionPhase("target", .25f);
                Require(target.position.y > home.y + 1, "Knockup did not launch upward.");
                Require(target.position.z > home.z + .5f, "Knockup did not carry attack force away from source.");
                Require(Vector3.Dot(target.up, Vector3.up) > .96f, "Target lay down during launch instead of flying upright.");
            }
            Capture(kind.ToString());
            if (kind == HitReaction.KnockUp)
            {
                SampleReactionPhase("target", .35f);
                Require(target.position.y > home.y + 2.2f, "Knockup apex was not high enough.");
                var apexHeight = target.GetComponentInChildren<Renderer>().bounds.min.y;
                SampleReactionPhase("target", .39f);
                Require(Mathf.Abs(target.GetComponentInChildren<Renderer>().bounds.min.y - apexHeight) < .05f,
                    "Knockup did not retain a readable apex before falling.");
                SampleReactionPhase("target", .55f);
                Require(target.GetComponentInChildren<Renderer>().bounds.min.y < apexHeight - .5f,
                    "Knockup did not accelerate down from the apex.");
                Capture("KnockUp-Drop");
                SampleReactionPhase("target", .74f);
                Require(Vector3.Dot(target.forward, Vector3.up) > .8f, "Knockup did not transition into a back fall.");
                Require(Mathf.Abs(target.GetComponentInChildren<Renderer>().bounds.min.y) < .01f,
                    "Fallen actor was hovering or clipping the floor.");
                Require(target.position.z > home.z + 1.5f, "Fall snapped home instead of landing away from source.");
                Capture("KnockFall");
                SampleReactionPhase("target", .885f);
                Require(Vector3.Dot(target.up, Vector3.up) > .9f && target.position.z > home.z + 1.5f,
                    "Actor slid home before getting up at its landing point.");
                Capture("Displaced-GetUp");
            }
            if (kind == HitReaction.KnockBack)
            {
                SampleReactionPhase("target", .2f);
                var pushed = target.position;
                SampleReactionPhase("target", .65f);
                Require(Vector3.Distance(target.position, pushed) < .001f,
                    "Knockback returned before its one-second posture hold ended.");
                Capture("KnockBack-Hold");
                SampleReactionPhase("target", .9f);
                Require(target.position.z > home.z && target.position.z < pushed.z,
                    "Knockback did not travel home after holding and standing.");
                Require(Vector3.Dot(target.up, Vector3.up) > .99f, "Knockback returned in a leaning posture.");
                Require(!_stage.TargetsReady, "Knockback marked ready before returning home.");
                Capture("KnockBack-Return");
            }
            yield return routine;
            Restored("target", home, rotation);
            Debug.Log("REACTION VISUAL PASS: " + kind);
        }
        var centered = _views["center-pivot"].transform;
        var centeredHome = centered.position; var centeredRotation = centered.rotation;
        var away = Vector3.ProjectOnPlane(centeredHome - _views["source"].transform.position, Vector3.up).normalized;
        var centeredRoutine = StartCoroutine(_stage.React(Hit("center-pivot", HitReaction.KnockUp)));
        yield return new WaitForSecondsRealtime(.26f);
        SampleReactionPhase("center-pivot", .35f);
        Require(Vector3.Dot(centered.position - centeredHome, away) > .7f,
            "Centre-pivot model did not launch away from attack direction.");
        Require(Vector3.Dot(centered.up, Vector3.up) > .96f && centered.position.y > centeredHome.y + 1,
            "Centre-pivot model rotated flat or orbited its origin during flight.");
        Capture("Center-Pivot-Launch");
        SampleReactionPhase("center-pivot", .74f);
        Require(Mathf.Abs(centered.GetComponentInChildren<Renderer>().bounds.min.y) < .01f,
            "Centre-pivot model floated above ground on landing.");
        Require(Vector3.Dot(centered.position - centeredHome, away) > 1.5f,
            "Centre-pivot model lost its landing displacement.");
        Capture("Center-Pivot-Landing");
        yield return centeredRoutine; Restored("center-pivot", centeredHome, centeredRotation);
        var productionHome = production.transform.position; var productionRotation = production.transform.rotation;
        var productionRoutine = StartCoroutine(_stage.React(Hit("production", HitReaction.KnockUp)));
        yield return new WaitForSecondsRealtime(.26f);
        SampleReactionPhase("production", .35f);
        Require(production.transform.position.z > productionHome.z + .7f &&
            Vector3.Dot(production.transform.up, Vector3.up) > .96f, "Production prefab did not fly upright and away.");
        Capture("Production-Prefab-Launch");
        SampleReactionPhase("production", .74f);
        Require(Mathf.Abs(production.GetComponentInChildren<Renderer>().bounds.min.y) < .01f,
            "Production centre-pivot prefab hovered on landing.");
        Require(production.transform.position.z > productionHome.z + 1.5f,
            "Production prefab snapped to formation instead of falling at its landing point.");
        Capture("Production-Prefab-Landing");
        yield return productionRoutine; Restored("production", productionHome, productionRotation);
        // A rig can expose named locomotion states without trigger parameters.
        var runController = new UnityEditor.Animations.AnimatorController();
        runController.AddLayer("Base Layer");
        var idleState = runController.layers[0].stateMachine.AddState("Idle");
        var runState = runController.layers[0].stateMachine.AddState("Run");
        var idleClip = new AnimationClip();
        idleClip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 1, 0));
        var runClip = new AnimationClip();
        runClip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 1, 0));
        idleState.motion = idleClip; runState.motion = runClip;
        runController.layers[0].stateMachine.defaultState = idleState;
        var runRig = new GameObject("Return Animator"); runRig.transform.SetParent(target, false);
        var runAnimator = runRig.AddComponent<Animator>();
        runAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        runAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        runAnimator.runtimeAnimatorController = runController;
        yield return null;
        var authoredReturn = StartCoroutine(_stage.React(Hit("target", HitReaction.KnockBack)));
        yield return null; SampleReactionPhase("target", .8f);
        yield return new WaitForSecondsRealtime(.07f);
        Require(runAnimator.GetCurrentAnimatorStateInfo(0).IsName("Run"), "Return did not play the authored Run state.");
        yield return authoredReturn;
        yield return new WaitForSecondsRealtime(.07f);
        Require(runAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Completed return left the rig running.");
        var interruptedReturn = StartCoroutine(_stage.React(Hit("target", HitReaction.KnockBack)));
        yield return null; SampleReactionPhase("target", .8f);
        StopCoroutine(interruptedReturn); _stage.SnapToPlanning();
        yield return new WaitForSecondsRealtime(.07f);
        Require(runAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Skip did not exit the Run state.");
        Restored("target", home, rotation);
        Destroy(runRig); Destroy(runController); Destroy(idleClip); Destroy(runClip);
        foreach (var hitCount in new[] { 2, 3, 10 })
        {
            for (var index = 1; index < hitCount; index++)
            {
                var packet = Hit("target", HitReaction.KnockUp); packet.HitIndex = index; packet.HitCount = hitCount;
                yield return _stage.React(packet, .1f);
                Require(!_stage.TargetsReady, "Intermediate hit recovered before the combo finished.");
            }
            yield return new WaitForSecondsRealtime(.7f);
            Require(target.position.y > home.y + 1 && !_stage.TargetsReady,
                "Knockup fell/recovered between hits or was squeezed to the barrage interval.");
            Require(target.position.z > home.z + .7f && Vector3.Dot(target.up, Vector3.up) > .96f,
                "Held barrage launch lost displacement or lay flat in midair.");
            Capture("Sustained-KnockUp-" + hitCount);
            var final = Hit("target", HitReaction.KnockUp); final.HitCount = final.HitIndex = hitCount;
            var completion = StartCoroutine(_stage.React(final, .1f));
            yield return new WaitForSecondsRealtime(.08f);
            Require(!_stage.TargetsReady && target.position.y > home.y,
                "Final hit cut off the launch/fall recovery.");
            yield return completion;
            Require(_stage.TargetsReady, "Final-hit caller returned before the target was ready.");
            Restored("target", home, rotation);
        }
        foreach (var kind in new[] { HitReaction.KnockBack, HitReaction.KnockDown, HitReaction.KnockUp })
        {
            var early = Hit("target", kind); early.HitIndex = 1; early.HitCount = 10;
            yield return _stage.React(early, .1f);
            yield return new WaitForSecondsRealtime(.25f);
            Require(!_stage.TargetsReady, "Strong response did not hold until the no-more-hits boundary.");
            // A skipped/evaded final packet has no DamageApplied event. The action barrier releases it.
            yield return _stage.RecoverAttacker();
            Require(_stage.TargetsReady, "Action recovery finished while a target was still displaced.");
            Restored("target", home, rotation);
        }
        var earlyLaunch = Hit("target", HitReaction.KnockUp); earlyLaunch.HitIndex = 1; earlyLaunch.HitCount = 10;
        yield return _stage.React(earlyLaunch, .1f);
        yield return new WaitForSecondsRealtime(.7f);
        var changedReaction = Hit("target", HitReaction.KnockBack); changedReaction.HitIndex = 2; changedReaction.HitCount = 10;
        yield return _stage.React(changedReaction, .1f);
        yield return new WaitForSecondsRealtime(.4f);
        Require(Mathf.Abs(target.GetComponentInChildren<Renderer>().bounds.min.y) < .01f && target.position.z > home.z + 1.4f,
            "A held reaction override stalled its blend or lost knockback displacement.");
        yield return _stage.WaitForTargetReactions(); Restored("target", home, rotation);
        yield return _stage.React(earlyLaunch, .1f);
        yield return new WaitForSecondsRealtime(.2f);
        var finalNone = Hit("target", HitReaction.None); finalNone.HitIndex = finalNone.HitCount = 10;
        yield return _stage.React(finalNone, .1f);
        Require(_stage.TargetsReady, "A final no-reaction packet failed to release an existing launch.");
        Restored("target", home, rotation);
        yield return _stage.React(earlyLaunch, .1f);
        yield return new WaitForSecondsRealtime(.2f);
        _stage.SnapToPlanning();
        Require(_stage.TargetsReady, "Skip left a held reaction running.");
        Restored("target", home, rotation);
        foreach (var range in new[] { AttackRange.Close, AttackRange.Long })
        {
            var sourceHome = _views["source"].transform.position;
            yield return _stage.BeginExecution("source", "target", 1, false);
            // Observe from Action entry, including the approach before the first hit.
            var sourceActor = _views["source"].GetComponent<ActorCardPresentation>();
            Action<ActorCardPhase> observe = phase => { if (phase == ActorCardPhase.Action) HoldCamera(); };
            sourceActor.PhaseChanged += observe;
            yield return _stage.BeginDamageAttack("source", new[] { "target", "stance" }, 10, range, true, HitReaction.KnockUp);
            sourceActor.PhaseChanged -= observe;
            for (var index = 1; index <= 10; index++)
            {
                yield return _stage.DamageHit(index, new[] { "target", "stance" });
                var packet = Hit("target", HitReaction.KnockUp); packet.HitIndex = index; packet.HitCount = 10;
                var resisted = Hit("stance", HitReaction.None); resisted.HitIndex = index; resisted.HitCount = 10;
                yield return _stage.ReactMultiple(new[] { packet, resisted }, .1f);
                if (index < 10)
                {
                    Require(!_stage.TargetsReady, "Barrage advanced without retaining the launched target.");
                    var point = _camera.WorldToViewportPoint(target.GetComponentInChildren<Renderer>().bounds.center);
                    Require(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1,
                        "Displaced airborne recipient was outside the action shot.");
                }
            }
            yield return _stage.WaitForLastHit();
            yield return _stage.RecoverAttacker();
            Require(_stage.TargetsReady && Vector3.Distance(_views["source"].transform.position, sourceHome) < .001f,
                "Card boundary did not recover both attacker and recipients.");
            Restored("target", home, rotation);
            _checkCamera = false;
            _stage.SnapToPlanning();
        }
        var stance = _views["stance"].transform; var stanceHome = stance.position; var stanceRotation = stance.rotation;
        _stage.SetStance("stance", true);
        var aoe = StartCoroutine(_stage.ReactMultiple(new[] { Hit("target", HitReaction.KnockDown), Hit("stance", HitReaction.None) }));
        yield return new WaitForSecondsRealtime(.29f);
        Restored("stance", stanceHome, stanceRotation); Capture("AOE-Stance-Tolerance");
        yield return aoe; Restored("target", home, rotation);
        var cancel = Hit("stance", HitReaction.KnockBack); cancel.WasStanceCancelled = true;
        var overrideRoutine = StartCoroutine(_stage.ReactMultiple(new[] { cancel, Hit("stance", HitReaction.KnockUp) }));
        yield return new WaitForSecondsRealtime(.2f);
        Require(stance.position.z > stanceHome.z + .4f && Mathf.Abs(stance.GetComponentInChildren<Renderer>().bounds.min.y) < .01f,
            "Stance cancel did not override knockup with grounded knockback.");
        Capture("Stance-Cancel-Override"); yield return overrideRoutine; Restored("stance", stanceHome, stanceRotation);
        var skipped = StartCoroutine(_stage.React(Hit("target", HitReaction.KnockUp)));
        yield return new WaitForSecondsRealtime(.18f); StopCoroutine(skipped); _stage.SnapToPlanning();
        Restored("target", home, rotation); Restored("stance", stanceHome, stanceRotation);
        yield return _stage.React(Hit("target", HitReaction.KnockDown));
        _stage.BeginDefeat("target"); yield return _stage.WaitForDefeatAnimations();
        Require(!_views["target"].activeSelf, "Reaction delayed or restored a defeated actor.");
        Debug.Log("REACTION VISUAL PASS: " + _checks + " checks, all reactions, sustained combos, no-more-hits barriers, ultimate defaults, mixed AOE, stance override, recovery, skip and death.");
        EditorApplication.Exit(0);
    }
}
#endif
