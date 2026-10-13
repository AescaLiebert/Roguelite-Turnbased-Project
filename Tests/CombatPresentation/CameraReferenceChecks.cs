#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class CameraReferenceChecks
{
    static CameraReferenceChecks() { EditorApplication.update += Tick; }
    public static void RunFacing() { SessionState.SetBool("ActorChecksFacing", true); Run(); }
    public static void RunFocused() { SessionState.SetBool("ActorChecksFocused", true); Run(); }
    public static void RunMoves() { SessionState.SetBool("ActorChecksMoves", true); Run(); }
    public static void RunPolish() { SessionState.SetBool("ActorChecksPolish", true); Run(); }
    public static void Run()
    {
        var path = "Assets/AuthoredPhases.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) == null)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var name in new[] { "Idle", "WindUp", "Action", "Recovery" })
            {
                var clip = new AnimationClip { name = name };
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, .9f, 0));
                AssetDatabase.AddObjectToAsset(clip, controller);
                var state = controller.layers[0].stateMachine.AddState(name);
                state.motion = clip;
                if (name == "Idle") { state.tag = "Idle"; controller.layers[0].stateMachine.defaultState = state; }
            }
            AssetDatabase.SaveAssets();
        }
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/FacingModel.controller") == null)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/FacingModel.controller");
            var clip = new AnimationClip { name = "Sample_idle" };
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", AnimationCurve.Linear(0, 0, .9f, 0));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Linear(0, 0, .9f, 0));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Linear(0, 0, .9f, 0));
            AssetDatabase.AddObjectToAsset(clip, controller);
            var state = controller.layers[0].stateMachine.AddState("Idle"); state.motion = clip; state.tag = "Idle";
            controller.layers[0].stateMachine.defaultState = state;
            AssetDatabase.SaveAssets();
        }
        SessionState.SetBool("CameraChecks", true); EditorApplication.isPlaying = true;
    }
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
    void RearComposition(string id, string targetId)
    {
        var actor = views[id].transform;
        Require(Vector3.Dot(camera.transform.position - actor.position, actor.forward) < 0, "Rear camera crossed in front of caster");
        var bounds = views[id].GetComponent<Renderer>().bounds;
        Require(camera.transform.position.y - bounds.min.y < bounds.size.y * 1.6f, "Attack camera is too elevated");
        var shoulder = camera.WorldToViewportPoint(bounds.center + Vector3.up * bounds.extents.y * .55f);
        Require(shoulder.z > 0 && shoulder.x > -.05f && shoulder.x < .95f && shoulder.y > -.1f && shoulder.y < .9f,
            "Foreground caster shoulder is out of view: " + shoulder);
        Physics.SyncTransforms();
        var target = views[targetId].transform;
        var targetCenter = views[targetId].GetComponent<Renderer>().bounds.center;
        Require(Physics.Linecast(camera.transform.position, targetCenter, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
            (hit.transform == target || hit.transform.IsChildOf(target)), "Caster obscures target body at impact");
    }

    IEnumerator Continuous(IEnumerator action, string name)
    {
        bool done = false;
        var routine = StartCoroutine(Complete(action, () => done = true));
        var rotation = camera.transform.rotation;
        var phase = stage.ExecutionPhase;
        var wasMoving = stage.CameraTransitionActive;
        int frame = 0;
        while (!done)
        {
            yield return null;
            var angle = Quaternion.Angle(rotation, camera.transform.rotation);
            // A frame can span more than 1/60 s in batch rendering; reject a jump relative to elapsed time.
            if (phase == stage.ExecutionPhase && !wasMoving && !stage.CameraTransitionActive)
                Require(angle < Mathf.Max(35f, Time.unscaledDeltaTime * 650f), name + " abrupt rotation " + angle);
            phase = stage.ExecutionPhase;
            wasMoving = stage.CameraTransitionActive;
            rotation = camera.transform.rotation;
            if (++frame == 10) Capture(name + "-moving");
        }
        yield return routine;
    }
    IEnumerator Complete(IEnumerator action, Action finish) { yield return Guard(action); finish(); }
    IEnumerator HeldShot(IEnumerator action, string name)
    {
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        var fov = camera.fieldOfView;
        bool done = false;
        var routine = StartCoroutine(Complete(action, () => done = true));
        do
        {
            Require(Vector3.Distance(position, camera.transform.position) < .001f &&
                Quaternion.Angle(rotation, camera.transform.rotation) < .01f && Mathf.Abs(fov - camera.fieldOfView) < .001f,
                name + " changed the held shot");
            yield return null;
        } while (!done);
        yield return routine;
    }
    IEnumerator FastMove(IEnumerator action, string name)
    {
        var from = new Pose(camera.transform.position, camera.transform.rotation);
        var fromFov = camera.fieldOfView;
        var samples = new List<Pose>();
        var fovs = new List<float>();
        var sawTransition = false;
        var sawIntermediate = false;
        var slowFrame = false;
        bool done = false;
        var routine = StartCoroutine(Complete(action, () => done = true));
        do
        {
            samples.Add(new Pose(camera.transform.position, camera.transform.rotation));
            fovs.Add(camera.fieldOfView);
            sawTransition |= stage.CameraTransitionActive;
            slowFrame |= Time.unscaledDeltaTime >= .1f;
            yield return null;
        } while (!done);
        yield return routine;
        var to = new Pose(camera.transform.position, camera.transform.rotation);
        for (var i = 0; i < samples.Count; i++)
        {
            var atStart = Vector3.Distance(samples[i].position, from.position) < .001f &&
                Quaternion.Angle(samples[i].rotation, from.rotation) < .01f && Mathf.Abs(fovs[i] - fromFov) < .001f;
            var atEnd = Vector3.Distance(samples[i].position, to.position) < .001f &&
                Quaternion.Angle(samples[i].rotation, to.rotation) < .01f && Mathf.Abs(fovs[i] - camera.fieldOfView) < .001f;
            sawIntermediate |= !atStart && !atEnd;
        }
        var moved = Vector3.Distance(from.position, to.position) > .01f || Quaternion.Angle(from.rotation, to.rotation) > .1f;
        Require(!stage.CameraTransitionActive, name + " returned with an unfinished camera move");
        if (moved)
        {
            Require(sawTransition, name + " snapped without a camera transition");
            Require(sawIntermediate || slowFrame, name + " had no in-between camera poses");
        }
        yield return null; yield return null;
        Require(Vector3.Distance(to.position, camera.transform.position) < .001f &&
            Quaternion.Angle(to.rotation, camera.transform.rotation) < .01f, name + " drifted after arrival");
    }
    IEnumerator SingleCut(IEnumerator action, string name)
    {
        var previous = new Pose(camera.transform.position, camera.transform.rotation);
        var previousFov = camera.fieldOfView;
        var changes = 0;
        bool done = false;
        var routine = StartCoroutine(Complete(action, () => done = true));
        do
        {
            Require(!stage.CameraTransitionActive, name + " interpolated instead of cutting");
            if (Vector3.Distance(previous.position, camera.transform.position) > .001f ||
                Quaternion.Angle(previous.rotation, camera.transform.rotation) > .01f ||
                Mathf.Abs(previousFov - camera.fieldOfView) > .001f) changes++;
            previous = new Pose(camera.transform.position, camera.transform.rotation);
            previousFov = camera.fieldOfView;
            yield return null;
        } while (!done);
        yield return routine;
        Require(changes <= 1, name + " changed shots more than once");
    }
    IEnumerator AwaitCameraStart()
    {
        var deadline = Time.realtimeSinceStartup + 1f;
        while (!stage.CameraTransitionActive && Time.realtimeSinceStartup < deadline) yield return null;
        Require(stage.CameraTransitionActive, "Phase snapped or never started camera travel");
    }

    void SameShot(Pose pose, float fov, string message)
    {
        Require(Vector3.Distance(camera.transform.position, pose.position) < .001f &&
            Quaternion.Angle(camera.transform.rotation, pose.rotation) < .01f &&
            Mathf.Abs(camera.fieldOfView - fov) < .001f, message);
    }

    void UpperBodyInFrame(string id)
    {
        var bounds = views[id].GetComponent<Renderer>().bounds;
        var lower = bounds.min.y + bounds.size.y / 3f;
        foreach (var y in new[] { lower, bounds.max.y })
        foreach (var x in new[] { bounds.min.x, bounds.max.x })
        foreach (var z in new[] { bounds.min.z, bounds.max.z })
        {
            var p = camera.WorldToViewportPoint(new Vector3(x, y, z));
            Require(p.z > 0 && p.x > .02f && p.x < .98f && p.y > .02f && p.y < .98f,
                "Face shot clipped the upper two-thirds body: " + p);
        }
        var head = camera.WorldToViewportPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        var waist = camera.WorldToViewportPoint(new Vector3(bounds.center.x, lower, bounds.center.z));
        Require(head.y - waist.y < .8f && head.y - waist.y > .2f, "Face shot remained head-only or became too distant");
    }

    IEnumerator CameraPolishRegression(string prefix)
    {
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
        {
            var friendly = side == TeamSide.Player ? "p" : "e";
            var hostile = side == TeamSide.Player ? "e" : "p";
            var id = friendly + "1";
            foreach (var category in new[] { CardCategory.Buff, CardCategory.Recovery })
            foreach (var scope in new[] { 0, 1, 2 }) // Self, selected ally, full allied team.
            {
                var self = scope == 0;
                var area = scope == 2;
                var ids = self ? new[] { id } : area ? new[] { friendly + "0", id, friendly + "2" } : new[] { friendly + "2" };
                stage.SnapToPlanning();
                yield return stage.BeginExecution(id, ids[0], 1, false, category, ids, area, self);
                yield return stage.SupportAction(id, ids, category);
                var rankOneShot = new Pose(camera.transform.position, camera.transform.rotation);
                var rankOneFov = camera.fieldOfView;
                yield return stage.RecoverAttacker();
                yield return SingleCut(stage.BeginExecution(id, ids[0], 2, false, category, ids, area, self), "Rank 2 support face WindUp");
                Require(Vector3.Dot(camera.transform.position - views[id].transform.position, views[id].transform.forward) > 0,
                    "Rank 2 support omitted face WindUp");
                UpperBodyInFrame(id);
                if (side == TeamSide.Player) Capture(prefix + "-rank2-" + category + "-scope" + scope + "-windup");
                yield return SingleCut(stage.SupportAction(id, ids, category), "Rank 2 support face-to-rear Action cut");
                SameShot(rankOneShot, rankOneFov, "Rank 2 support did not cut to exactly its Rank 1 camera");
                if (!self) InFrame(friendly + "0", id, friendly + "2");
                if (side == TeamSide.Player) Capture(prefix + "-rank2-" + category + "-scope" + scope + "-action");
                yield return HeldShot(stage.RecoverAttacker(), "Rank 2 support recovery hold");
                yield return SingleCut(stage.CompleteExecution(false, side), "Rank 2 support empty queue");
                yield return stage.BeginExecution(id, ids[0], 3, false, category, ids, area, self);
                if (self) UpperBodyInFrame(id); else InFrame(friendly + "0", id, friendly + "2");
                yield return HeldShot(stage.SupportAction(id, ids, category), "Rank 3 support unchanged Action");
                yield return stage.RecoverAttacker();
            }
            foreach (var rank in new[] { 2, 3 })
            {
                yield return stage.BeginExecution(id, hostile + "1", rank, false);
                UpperBodyInFrame(id);
                if (side == TeamSide.Player) Capture(prefix + "-rank" + rank + "-two-thirds-windup");
                yield return stage.RecoverAttacker();
            }
            stage.SnapToPlanning();
        }
    }

    IEnumerator RapidMovesRegression()
    {
        foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
        {
            stage.SnapToPlanning();
            var skipRest = new Pose(camera.transform.position, camera.transform.rotation);
            var restFov = camera.fieldOfView;
            var id = side == TeamSide.Player ? "p1" : "e1";
            var target = side == TeamSide.Player ? "e1" : "p1";
            yield return FastMove(stage.BeginExecution(id, target, 1, false), "rapid Wind Up");
            var windUp = new Pose(camera.transform.position, camera.transform.rotation);
            var windUpFov = camera.fieldOfView;
            stage.SnapToPlanning();
            var prepare = stage.StartCoroutine(stage.BeginExecution(id, target, 1, false));
            yield return AwaitCameraStart();
            // Even the in-progress move must use its captured endpoint and pivot.
            views[id].transform.position += new Vector3(20, 10, 20);
            views[target].transform.position += new Vector3(-40, 20, 40);
            yield return stage.WaitForCameraTransition();
            SameShot(windUp, windUpFov, "Wind Up travel retargeted displaced actors");
            stage.StopCoroutine(prepare); stage.SnapToPlanning();

            yield return SingleCut(stage.BeginExecution(id, target, 3, false), "ranked face Wind Up");
            Require(Vector3.Dot(camera.transform.position - views[id].transform.position,
                views[id].transform.forward) > 0, "Ranked Wind Up missed the face cut");
            stage.SnapToPlanning();

            yield return stage.BeginExecution(id, target, 3, false);
            yield return FastMove(stage.BeginDamageAttack(id, new[] { target }, 1, AttackRange.Long, false), "rapid Attack");
            var attackPose = new Pose(camera.transform.position, camera.transform.rotation);
            var attackFov = camera.fieldOfView;
            stage.SnapToPlanning();
            yield return stage.BeginExecution(id, target, 3, false);
            var attack = stage.StartCoroutine(stage.BeginDamageAttack(id, new[] { target }, 1, AttackRange.Long, false));
            yield return AwaitCameraStart();
            views[target].transform.position += new Vector3(-40, 20, 40);
            yield return stage.WaitForCameraTransition();
            yield return attack; yield return null;
            SameShot(attackPose, attackFov, "Attack camera tracked the displaced target");
            views[id].transform.position += new Vector3(1, 0, .5f);
            yield return null; yield return null;
            Require(Vector3.Distance(camera.transform.position, attackPose.position) > .1f,
                "Offensive Action did not follow its caster");
            var followed = new Pose(camera.transform.position, camera.transform.rotation);
            var followedFov = camera.fieldOfView;
            views[target].transform.position += new Vector3(40, 20, -40);
            yield return null; yield return null;
            SameShot(followed, followedFov, "Caster follow also tracked its target");
            yield return stage.RecoverAttacker();
            SameShot(windUp, windUpFov, "Offensive Recovery did not restore its original rear Wind Up");
            stage.SnapToPlanning();

            foreach (var phase in new[] { ActorCardPhase.WindUp, ActorCardPhase.Action, ActorCardPhase.Rest })
            {
                if (phase != ActorCardPhase.WindUp) yield return stage.BeginExecution(id, target, 1, false);
                var pending = phase == ActorCardPhase.WindUp ? stage.BeginExecution(id, target, 1, false) :
                    phase == ActorCardPhase.Action ? stage.BeginDamageAttack(id, new[] { target }, 1, AttackRange.Long, false) :
                    stage.ReturnToPlanning(TeamSide.Player);
                stage.StartCoroutine(pending);
                yield return AwaitCameraStart();
                stage.SnapToPlanning();
                yield return new WaitForSecondsRealtime(.35f);
                Require(!stage.CameraTransitionActive, "Skip left a camera transition active");
                SameShot(skipRest, restFov, "Cancelled transition overwrote restored Rest");
                Require(stage.ExecutionPhase == ActorCardPhase.Rest, "Skip left actor in a card phase");
            }
        }
    }

    IEnumerator FacingRegression(string prefix)
    {
        var playerAnchor = GameObject.Find("CharHeroPosition2").transform;
        var enemyAnchor = GameObject.Find("EnemyHeroPosition2").transform;
        var direction = (enemyAnchor.position - playerAnchor.position).normalized;
        var playerRotation = BattleFighterView.FormationRotation(TeamSide.Player);
        var enemyRotation = BattleFighterView.FormationRotation(TeamSide.Opponent);
        Require(Vector3.Dot(playerRotation * Vector3.forward, direction) > .999f, "Player formation faces away from enemies");
        Require(Vector3.Dot(enemyRotation * Vector3.forward, -direction) > .999f, "Enemy formation faces away from players");
        // The scene's two anchors deliberately share a rotation. Position, not anchor yaw,
        // determines facing. The no-anchor path must match the fallback X-axis spawn rows.
        playerAnchor.name = "HiddenPlayerAnchor"; enemyAnchor.name = "HiddenEnemyAnchor";
        Require(Vector3.Dot(BattleFighterView.FormationRotation(TeamSide.Player) * Vector3.forward, Vector3.right) > .999f,
            "Fallback player formation faces wrong row");
        Require(Vector3.Dot(BattleFighterView.FormationRotation(TeamSide.Opponent) * Vector3.forward, Vector3.left) > .999f,
            "Fallback enemy formation faces wrong row");
        playerAnchor.name = "CharHeroPosition2"; enemyAnchor.name = "EnemyHeroPosition2";

        foreach (var yaw in new[] { 180f, 0f })
        {
            var prefab = new GameObject("FacingTestPrefab");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(prefab.transform, false); body.transform.localPosition = Vector3.up;
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube); face.name = "Face";
            face.transform.SetParent(prefab.transform, false);
            face.transform.localPosition = new Vector3(0, 1.55f, yaw == 180 ? -.45f : .45f);
            face.transform.localScale = new Vector3(.4f, .2f, .12f);
            var actors = new Dictionary<string, GameObject>
            {
                { "player", BattleFighterView.Create(prefab, playerAnchor.position, playerRotation, yaw) },
                { "enemy", BattleFighterView.Create(prefab, enemyAnchor.position, enemyRotation, yaw) }
            };
            prefab.SetActive(false);
            var facingStage = new GameObject("FacingRegressionStage").AddComponent<BattleStagePresenter>();
            facingStage.Initialize(actors);
            foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            {
                var id = side == TeamSide.Player ? "player" : "enemy";
                var target = side == TeamSide.Player ? "enemy" : "player";
                var actor = actors[id].transform;
                var model = actor.Find("Model");
                var rig = model.Find("Rig");
                var animator = rig.GetComponent<Animator>();
                if (animator == null) animator = rig.gameObject.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/FacingModel.controller");
                rig.localRotation = Quaternion.Euler(0, 45, 0);
                // Sample an authored root-rotation curve explicitly; Animator root-motion
                // settings can otherwise preserve the initial rotation in this fixture.
                animator.runtimeAnimatorController.animationClips[0].SampleAnimation(rig.gameObject, .2f);
                var actorForward = side == TeamSide.Player ? direction : -direction;
                Require(Quaternion.Angle(rig.localRotation, Quaternion.identity) < .01f, "Sampled idle did not overwrite the imported rig root");
                var visualForward = Vector3.ProjectOnPlane(rig.Find("Face").position - actor.position, Vector3.up).normalized;
                Require(Vector3.Dot(visualForward, actorForward) > .999f, "Visual model faces away from the opposing team");
                Require(Quaternion.Angle(model.localRotation, Quaternion.Euler(0, yaw, 0)) < .01f, "Model yaw correction was lost");
                foreach (var rank in new[] { 1, 2, 3 })
                {
                    yield return facingStage.BeginExecution(id, target, rank, false);
                    Require(Vector3.Dot(actor.forward, actorForward) > .999f, "WindUp reversed logical facing");
                    Require(Vector3.Dot(camera.transform.position - actor.position, actorForward) * (rank == 1 ? -1 : 1) > 0,
                        "WindUp camera used model yaw instead of actor facing");
                    yield return facingStage.BeginDamageAttack(id, new[] { target }, 1, AttackRange.Long, false);
                    Require(Vector3.Dot(camera.transform.position - actor.position, actorForward) < 0, "Action camera flipped in front of actor");
                    var rotation = rig.localRotation;
                    animator.enabled = false;
                    rig.localRotation = rotation * Quaternion.Euler(0, 90, 0);
                    yield return null; yield return null;
                    Require(Vector3.Dot(camera.transform.position - actor.position, actorForward) < 0, "Model animation flipped the camera");
                    rig.localRotation = rotation;
                    animator.enabled = true;
                    yield return facingStage.RecoverAttacker();
                    Require(Vector3.Dot(actor.forward, actorForward) > .999f, "Recovery restored wrong formation facing");
                    Require(Quaternion.Angle(model.localRotation, Quaternion.Euler(0, yaw, 0)) < .01f, "Recovery lost model correction");
                }
                yield return facingStage.BeginExecution(id, id, 1, false, CardCategory.Buff, new[] { id });
                yield return facingStage.SupportAction(id, new[] { id }, CardCategory.Buff);
                Require(Vector3.Dot(camera.transform.position - actor.position, actorForward) < 0, "Support camera faces actor from the front");
                yield return facingStage.RecoverAttacker();
                facingStage.SnapToPlanning();
                Require(Vector3.Dot(actor.forward, actorForward) > .999f, "Skip reset wrong formation facing");
            }
            facingStage.SnapToPlanning();
            Capture(prefix + "-facing-corrected-" + yaw);
            Destroy(facingStage.gameObject);
            foreach (var actor in actors.Values) Destroy(actor);
            Destroy(prefab); yield return null;
        }
    }
    IEnumerator Run()
    {
        output = Path.GetFullPath("Evidence"); Directory.CreateDirectory(output);
        camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.gameObject.AddComponent<AudioListener>();
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
            if (SessionState.GetBool("ActorChecksFacing", false))
            {
                yield return FacingRegression(prefix);
                camera.targetTexture.Release(); Destroy(camera.targetTexture); camera.targetTexture = null;
                continue;
            }
            if (SessionState.GetBool("ActorChecksMoves", false))
            {
                yield return RapidMovesRegression();
                camera.targetTexture.Release(); Destroy(camera.targetTexture); camera.targetTexture = null;
                continue;
            }
            if (SessionState.GetBool("ActorChecksPolish", false))
            {
                yield return CameraPolishRegression(prefix);
                camera.targetTexture.Release(); Destroy(camera.targetTexture); camera.targetTexture = null;
                continue;
            }
            foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            foreach (var category in new[] { CardCategory.Attack, CardCategory.AttackDebuff, CardCategory.Debuff,
                CardCategory.Stance, CardCategory.Buff, CardCategory.Recovery })
            for (var rank = 1; rank <= 3; rank++)
            {
                if (SessionState.GetBool("ActorChecksFocused", false)) continue;
                var friendly = side == TeamSide.Player ? "p" : "e";
                var hostile = side == TeamSide.Player ? "e" : "p";
                var sourceId = friendly + "1";
                var support = category == CardCategory.Buff || category == CardCategory.Recovery;
                var targetId = support ? friendly + "2" : category == CardCategory.Stance ? sourceId : hostile + "0";
                var ids = new[] { targetId };
                var source = views[sourceId].transform;
                var home = new Pose(source.position, source.rotation);
                var actor = source.GetComponent<ActorCardPresentation>() ?? source.gameObject.AddComponent<ActorCardPresentation>();
                var phases = new List<ActorCardPhase>();
                Action<ActorCardPhase> changed = phase => phases.Add(phase);
                actor.PhaseChanged += changed;
                yield return stage.ReturnToPlanning(side, .05f);
                var rest = camera.transform.position;
                yield return stage.BeginExecution(sourceId, targetId, rank, false, category, ids);
                var windUp = camera.transform.position;
                Require(stage.ExecutionPhase == (support && rank == 1 ? ActorCardPhase.Rest : ActorCardPhase.WindUp), "Wrong preparation state");
                if (support && rank != 2)
                {
                    InFrame(friendly + "0", friendly + "1", friendly + "2");
                    Require(Vector3.Dot(windUp - source.position, source.forward) < 0, "Support preparation did not cut to team rear");
                }
                else if (rank > 1)
                    Require(Vector3.Dot(camera.transform.position - source.position, source.forward) > 0, "portrait behind actor");
                else
                {
                    Require(Vector3.Distance(rest, windUp) > .1f, "Rank1 did not enter rear windup");
                    Require(Vector3.Dot(windUp - source.position, source.forward) < 0, "Rank1 entered face shot");
                }
                var name = prefix + "-" + side + "-" + category + "-rank" + rank;
                if (category == CardCategory.Attack || category == CardCategory.AttackDebuff)
                    yield return Continuous(stage.BeginDamageAttack(sourceId, ids, 1, AttackRange.Close, false), name);
                else yield return Continuous(stage.SupportAction(sourceId, ids, category), name);
                Require(stage.ExecutionPhase == ActorCardPhase.Action, "Action did not own camera");
                if (support)
                {
                    InFrame(friendly + "0", friendly + "1", friendly + "2");
                    Require(Vector3.Dot(camera.transform.position - source.position, source.forward) < 0, "Support is not rear-team view");
                }
                else if (targetId == sourceId) InFrame(sourceId);
                else { InFrame(targetId); RearComposition(sourceId, targetId); }
                if (side == TeamSide.Player) Capture(name);
                var before = camera.transform.position;
                var beforeRotation = camera.transform.rotation;
                var target = views[targetId].transform;
                var targetPosition = target.position;
                if (target != source)
                {
                    target.position += new Vector3(45, 30, 50);
                    yield return null; yield return null;
                    Require(Vector3.Distance(before, camera.transform.position) < .001f &&
                        Quaternion.Angle(beforeRotation, camera.transform.rotation) < .01f, "Camera locked to target Transform");
                    target.position = targetPosition;
                }
                if (support) yield return HeldShot(stage.RecoverAttacker(), name + "-recovery");
                else yield return Continuous(stage.RecoverAttacker(), name + "-recovery");
                Require(stage.ExecutionPhase == ActorCardPhase.Rest, "Recovery did not finish");
                Require(Vector3.Distance(source.position, home.position) < .001f && Quaternion.Angle(source.rotation, home.rotation) < .01f,
                    "Actor did not return to formation");
                Require(Vector3.Dot(camera.transform.position - source.position, home.rotation * Vector3.forward) < 0,
                    "Recovery did not reset behind actor");
                if (support) Require(Vector3.Distance(camera.transform.position, before) < .001f,
                    "Team support lost its team shot during Recovery");
                var expected = support && rank == 1 ? 3 : 4;
                Require(phases.Count == expected && phases[expected - 3] == ActorCardPhase.Action &&
                    phases[expected - 2] == ActorCardPhase.Recovery && phases[expected - 1] == ActorCardPhase.Rest, "Wrong phase sequence");
                actor.PhaseChanged -= changed;
                var queued = camera.transform.position;
                yield return stage.CompleteExecution(true, side);
                Require(Vector3.Distance(queued, camera.transform.position) < .001f, "Queued card returned to rest");
                yield return stage.CompleteExecution(false, side);
                Require(Vector3.Distance(rest, camera.transform.position) < .001f, "Empty queue did not return to rest");
            }
            // Self support: Rank 2 face cuts to Rank 1 rear at Action entry;
            // Rank 3 holds face through Action, then cuts behind at Recovery.
            foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            foreach (var category in new[] { CardCategory.Buff, CardCategory.Recovery, CardCategory.Stance })
            foreach (var rank in new[] { 1, 2, 3 })
            {
                var id = side == TeamSide.Player ? "p1" : "e1";
                yield return stage.ReturnToPlanning(side, .05f);
                var support = category != CardCategory.Stance;
                var prepare = stage.BeginExecution(id, id, rank, false, category, new[] { id }, false, true);
                if (support) yield return SingleCut(prepare, "self-support preparation");
                else yield return prepare;
                var selfBegan = camera.transform.position;
                var action = stage.SupportAction(id, new[] { id }, category);
                if (support && rank == 2) yield return SingleCut(action, "Rank 2 self-support Action rear cut");
                else if (support) yield return HeldShot(action, "self-support Action");
                else if (rank > 1) yield return SingleCut(action, prefix + "-self-" + side + "-" + category + rank);
                else yield return FastMove(action, prefix + "-self-" + side + "-" + category + rank);
                var direction = Vector3.Dot(camera.transform.position - views[id].transform.position, views[id].transform.forward);
                Require(support && rank == 3 ? direction > 0 : direction < 0, "Self action used the wrong side of the actor");
                if (support && rank <= 2)
                {
                    var enemy = side == TeamSide.Player ? "e" : "p";
                    InFrame(enemy + "0", enemy + "1", enemy + "2");
                    RearComposition(id, enemy + "1");
                }
                if (!support && rank > 1)
                    Require(Vector3.Distance(selfBegan, camera.transform.position) > .1f, "Self action did not transition from portrait/rest");
                if (side == TeamSide.Player && rank == 3) Capture(prefix + "-self-" + category);
                if (support) yield return SingleCut(stage.RecoverAttacker(), "self Recovery rear cut");
                else yield return Continuous(stage.RecoverAttacker(), "self Stance Recovery");
                Require(Vector3.Dot(camera.transform.position - views[id].transform.position, views[id].transform.forward) < 0,
                    "Self Recovery did not restore behind the actor");
            }
            // Single-ally and whole-team support use the same fixed allied rear shot,
            // Rank 2 adds face WindUp, then cuts to that shot at Action entry.
            foreach (var side in new[] { TeamSide.Player, TeamSide.Opponent })
            foreach (var category in new[] { CardCategory.Buff, CardCategory.Recovery })
            foreach (var rank in new[] { 1, 2, 3 })
            foreach (var area in new[] { false, true })
            {
                var friendly = side == TeamSide.Player ? "p" : "e";
                var hostile = side == TeamSide.Player ? "e" : "p";
                var id = friendly + "1";
                var ids = area ? new[] { friendly + "0", id, friendly + "2" } : new[] { friendly + "2" };
                yield return stage.ReturnToPlanning(side, .05f);
                yield return SingleCut(stage.BeginExecution(id, ids[0], rank, false, category, ids, area, false), "team-support preparation");
                if (rank != 2) InFrame(friendly + "0", id, friendly + "2");
                Require(Vector3.Dot(camera.transform.position - views[id].transform.position, views[id].transform.forward) * (rank == 2 ? 1 : -1) > 0,
                    "Team support used the wrong WindUp shot");
                if (rank == 2) yield return SingleCut(stage.SupportAction(id, ids, category), "Rank 2 team-support Action cut");
                else yield return HeldShot(stage.SupportAction(id, ids, category), "team-support Action");
                InFrame(friendly + "0", id, friendly + "2");
                if (side == TeamSide.Player && area && rank == 3) Capture(prefix + "-team-" + category);
                yield return HeldShot(stage.RecoverAttacker(), "team-support Recovery");
                yield return HeldShot(stage.CompleteExecution(true, side), "team-support queued hold");
                yield return SingleCut(stage.BeginExecution(friendly + "0", hostile + "0", 1, false), "support-to-attack queue");
                yield return stage.RecoverAttacker();
            }
            yield return stage.ReturnToPlanning(TeamSide.Player, .05f);
            var turnPivot = Vector3.zero;
            var radius = camera.transform.position.magnitude;
            bool turned = false;
            var turnMove = StartCoroutine(Complete(stage.Turn(TeamSide.Opponent, .75f), () => turned = true));
            while (!turned)
            {
                yield return null;
                Require(Vector3.Distance(camera.transform.position, turnPivot) > radius * .95f, "Turn camera crossed through the arena");
                Require(Vector3.Dot(camera.transform.forward,
                    (turnPivot + Vector3.up * 1.3f - camera.transform.position).normalized) > .99f,
                    "Opponent turn camera looked away from the arena");
            }
            yield return turnMove;
            turned = false;
            turnMove = StartCoroutine(Complete(stage.Turn(TeamSide.Player, .75f), () => turned = true));
            while (!turned)
            {
                yield return null;
                Require(Vector3.Distance(camera.transform.position, turnPivot) > radius * .95f, "Reverse turn camera crossed through the arena");
                Require(Vector3.Dot(camera.transform.forward,
                    (turnPivot + Vector3.up * 1.3f - camera.transform.position).normalized) > .99f,
                    "Player turn camera looked away from the arena");
            }
            yield return turnMove;
            // Different owners chain directly; Rank 1 support cuts on card entry.
            yield return stage.BeginExecution("p0", "e0", 1, false);
            yield return stage.BeginDamageAttack("p0", new[] { "e0" }, 1, AttackRange.Close, false);
            RearComposition("p0", "e0");
            yield return stage.RecoverAttacker();
            yield return FastMove(stage.BeginExecution("p2", "e2", 1, false), prefix + "-queued-windup");
            yield return stage.BeginDamageAttack("p2", new[] { "e2" }, 1, AttackRange.Long, false);
            yield return stage.RecoverAttacker();
            var previousRear = camera.transform.position;
            yield return stage.BeginExecution("p0", "p2", 1, false, CardCategory.Recovery, new[] { "p2" });
            Require(Vector3.Distance(previousRear, camera.transform.position) > .1f, "Queued heal did not cut to team view on entry");
            yield return HeldShot(stage.SupportAction("p0", new[] { "p2" }, CardCategory.Recovery), "queued heal Action");
            InFrame("p0", "p1", "p2");
            yield return SingleCut(stage.CompleteExecution(false, TeamSide.Player), "support empty-queue rest");
            foreach (var rank in new[] { 2, 3 })
            {
                yield return stage.BeginExecution("p1", "e1", rank, false);
                var head = camera.WorldToViewportPoint(views["p1"].transform.position + Vector3.up * .7f);
                Require(head.z > 0 && head.x > .1f && head.x < .9f && head.y > .1f && head.y < .9f, "Face shot clipped head");
                Capture(prefix + "-rank" + rank + "-face-windup");
                yield return stage.BeginDamageAttack("p1", new[] { "e1" }, 1, AttackRange.Long, false);
                yield return stage.RecoverAttacker();
            }
            // AoE aim/framing keeps the full enemy formation even with only one survivor.
            yield return stage.BeginExecution("p1", "e1", 1, false, CardCategory.Attack, new[] { "e0", "e1", "e2" }, true);
            var areaShot = camera.transform.position;
            yield return stage.RecoverAttacker();
            views["e0"].SetActive(false); views["e2"].SetActive(false);
            yield return stage.BeginExecution("p1", "e1", 1, false, CardCategory.Attack, new[] { "e1" }, true);
            Require(Vector3.Distance(areaShot, camera.transform.position) < .001f, "One-survivor AoE narrowed to single target");
            yield return stage.BeginDamageAttack("p1", new[] { "e1" }, 3, AttackRange.Long, true);
            InFrame("e0", "e1", "e2");
            RearComposition("p1", "e1");
            yield return stage.DamageHit(2, new[] { "e1" });
            yield return stage.DamageHit(3, new[] { "e1" });
            Capture(prefix + "-one-survivor-area");
            stage.SnapToPlanning(); views["e0"].SetActive(true); views["e2"].SetActive(true);
            var saved = camera.transform.position; yield return null; yield return null;
            Require(Vector3.Distance(saved, camera.transform.position) < .001f, "stale camera tracking after skip");
            Require(stage.ExecutionPhase == ActorCardPhase.Rest, "skip did not clear actor phase");
            yield return stage.BeginExecution("p0", "e1", 3, false);
            stage.SnapToPlanning();
            Require(stage.ExecutionPhase == ActorCardPhase.Rest, "skip during portrait did not clear state");
            yield return stage.BeginExecution("p0", "e1", 1, true);
            yield return stage.BeginDamageAttack("p0", new[] { "e1" }, 1, AttackRange.Long, false);
            yield return stage.RecoverAttacker();
            Require(stage.ExecutionPhase == ActorCardPhase.Rest, "Ultimate did not recover");
            // Authored phases use their real clip end in addition to the procedural minimum.
            var model = new GameObject("AuthoredAnimator"); model.transform.SetParent(views["p1"].transform, false);
            var animator = model.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/AuthoredPhases.controller");
            yield return null;
            var began = Time.realtimeSinceStartup;
            yield return stage.BeginExecution("p1", "e1", 1, false);
            Require(Time.realtimeSinceStartup - began >= .85f, "Authored WindUp clip was cut short");
            yield return stage.Attack("p1", "e1");
            began = Time.realtimeSinceStartup;
            yield return stage.RecoverAttacker();
            Require(Time.realtimeSinceStartup - began >= .85f, "Authored Recovery clip was cut short");
            Require(stage.ExecutionPhase == ActorCardPhase.Rest, "Authored actor did not return to Rest");
            foreach (var selfTarget in new[] { false, true })
            foreach (var category in new[] { CardCategory.Buff, CardCategory.Recovery })
            foreach (var rank in new[] { 1, 2, 3 })
            {
                var id = selfTarget ? "p1" : "p2";
                yield return stage.ReturnToPlanning(TeamSide.Player, .05f);
                yield return SingleCut(stage.BeginExecution("p1", id, rank, false, category, new[] { id }, false, selfTarget),
                    "authored support preparation");
                if (rank == 2) yield return SingleCut(stage.SupportAction("p1", new[] { id }, category), "authored Rank 2 support Action cut");
                else yield return HeldShot(stage.SupportAction("p1", new[] { id }, category), "authored support Action");
                var actor = views["p1"].GetComponent<ActorCardPresentation>();
                float recoveryNormalizedTime = 0;
                Action<ActorCardPhase> observe = phase =>
                {
                    if (phase == ActorCardPhase.Recovery)
                        recoveryNormalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                };
                actor.PhaseChanged += observe;
                if (selfTarget) yield return SingleCut(stage.RecoverAttacker(), "authored self-support recovery cut");
                else yield return HeldShot(stage.RecoverAttacker(), "authored team-support recovery hold");
                actor.PhaseChanged -= observe;
                Require(recoveryNormalizedTime >= .99f, "Support Recovery changed shot before authored Action ended");
                Require(stage.ExecutionPhase == ActorCardPhase.Rest, "Authored support did not finish Recovery");
            }
            Destroy(model); yield return null;
            yield return FacingRegression(prefix);
            camera.targetTexture.Release(); Destroy(camera.targetTexture); camera.targetTexture = null;
        }
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS " + checks + " camera assertions");
        Debug.Log("CAMERA CHECK PASS " + checks); EditorApplication.Exit(0);
    }
}

#endif
