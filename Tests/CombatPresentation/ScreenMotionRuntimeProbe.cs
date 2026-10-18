#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FightingAllstar.Presentation;
using FightingAllstar.Presentation.Route;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

/// <summary>Only copied into the disposable Play mode verification project.</summary>
public sealed class ScreenMotionRuntimeProbe : MonoBehaviour
{
    private const string Folder = "Artifacts/ScreenMotion";
    private readonly List<string> notes = new List<string>();
    private readonly List<string> runtimeErrors = new List<string>();
    private UIDocument document;
    private VisualElement root, panel;
    private RenderTexture toolkitTarget, canvasTarget;
    private Camera captureCamera;
    private RectTransform canvas, card;
    private CanvasGroup group;
    private int assertions, staleCallbacks, detachedFrames;
    private string phase;
    private Vector2 position;
    private Vector3 scale;
    private float originalTimeScale;
    private static ScreenMotionRuntimeProbe active;
    private void Awake()
    {
        if (active != null && active != this) { Destroy(gameObject); return; }
        active = this; DontDestroyOnLoad(gameObject); originalTimeScale = Time.timeScale;
        Application.logMessageReceived += OnLog;
    }
    private IEnumerator Start()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        while (stack.Count > 0)
        {
            bool pending; object yielded;
            var checks = stack.Peek();
            try { pending = checks.MoveNext(); yielded = pending ? checks.Current : null; }
            catch (Exception error) { Finish(error); yield break; }
            if (!pending) { stack.Pop(); continue; }
            if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            yield return yielded;
        }
        Finish(null);
    }
    private IEnumerator Checks()
    {
        phase = "initial scene entry";
        SetupToolkit(); SetupCanvas();
        // Warm the first imported font/shader frame before checking intermediate motion frames.
        yield return new WaitForSecondsRealtime(.7f);
        var layoutReadyBy = Time.realtimeSinceStartup + 3f;
        while (root.resolvedStyle.opacity < .99f && Time.realtimeSinceStartup < layoutReadyBy) yield return null;
        Check(root.resolvedStyle.opacity > .99f, "Scene UIDocument entry did not settle: resolved=" + root.resolvedStyle.opacity +
            " inline=" + root.style.opacity + " display=" + root.resolvedStyle.display + " attached=" + (root.panel != null) +
            " time=" + Time.realtimeSinceStartup + " frame=" + Time.frameCount);
        ScreenMotion.Enter(root); yield return new WaitForSecondsRealtime(.09f);
        Check(root.resolvedStyle.opacity > 0 && root.resolvedStyle.opacity < 1, "Scene root entry lacks a visible fade after layout is ready: " + root.resolvedStyle.opacity);
        CaptureToolkit("SceneEntry"); yield return new WaitForSecondsRealtime(.5f);

        phase = "Canvas motion while paused"; Time.timeScale = 0;
        group.alpha = .8f; position = card.anchoredPosition; scale = card.localScale;
        ScreenMotion.Enter(card);
        yield return new WaitForSecondsRealtime(.09f);
        Check(group.alpha > 0 && group.alpha < .8f && card.anchoredPosition.y < position.y && Mathf.Abs(card.anchoredPosition.x - position.x) < .01f,
            "Canvas entry lacks visible unscaled fade and upward slide.");
        Check(card.localScale.x < scale.x && card.Find("UI entry flash") != null, "Canvas entry lacks scale and flash feedback.");
        CaptureCanvas("CanvasEnterPaused");
        yield return new WaitForSecondsRealtime(.5f);
        CanvasRestored(); Check(card.Find("UI entry flash") == null, "Canvas entry flash leaked after settling.");
        ScreenMotion.Exit(card, () => staleCallbacks++);
        yield return new WaitForSecondsRealtime(.06f);
        Check(group.alpha > 0 && group.alpha < .8f && !group.interactable && !group.blocksRaycasts,
            "Canvas exit lacks fade or permits duplicate interaction.");
        Check(card.anchoredPosition.y > position.y && Mathf.Abs(card.anchoredPosition.x - position.x) < .01f,
            "Canvas exit does not move upward without sideways drift.");
        CaptureCanvas("CanvasExitPaused"); ScreenMotion.Enter(card);
        yield return new WaitForSecondsRealtime(.5f);
        Check(staleCallbacks == 0 && card.gameObject.activeSelf, "Reopening Canvas did not cancel stale deactivation."); CanvasRestored();
        var completed = false; ScreenMotion.Exit(card, () => completed = true);
        yield return new WaitForSecondsRealtime(.35f);
        Check(completed && !card.gameObject.activeSelf, "Canvas exit did not deactivate after its animation.");
        ScreenMotion.Enter(card); yield return new WaitForSecondsRealtime(.5f); CanvasRestored();

        phase = "Toolkit cancel and detach";
        panel = new VisualElement { name = "motion-probe" };
        panel.style.width = 400; panel.style.height = 180; panel.style.marginLeft = 120; panel.style.marginTop = 140;
        panel.style.backgroundColor = new Color(.15f, .29f, .4f); panel.style.opacity = .8f;
        panel.style.translate = new Translate(11, 7); panel.style.scale = new Scale(new Vector3(1.06f, .98f, 1));
        var title = new Label("ARTIFACT WORKSHOP / MOTION"); title.style.color = Color.white; title.style.fontSize = 26; panel.Add(title); root.Add(panel);
        yield return new WaitForSecondsRealtime(.1f);
        ScreenMotion.ShowModal(panel);
        yield return new WaitForSecondsRealtime(.09f);
        Check(panel.resolvedStyle.opacity > 0 && panel.resolvedStyle.opacity < .8f && panel.resolvedStyle.translate.y > 7 && Mathf.Abs(panel.resolvedStyle.translate.x - 11) < .01f,
            "Toolkit entry lacks visible unscaled fade and upward slide.");
        Check(panel.Q("screen-motion-sparkle") != null, "Toolkit modal entry has no sparkle feedback."); CaptureToolkit("ToolkitEnterPaused");
        yield return new WaitForSecondsRealtime(.7f); ToolkitRestored();
        Check(panel.Q("screen-motion-sparkle") == null, "Toolkit sparkle was not removed.");
        ScreenMotion.Remove(panel, () => staleCallbacks++);
        yield return new WaitForSecondsRealtime(.06f);
        Check(root.Q("motion-probe") == null && panel.panel != null, "Exiting popup query was not released before detaching.");
        Check(panel.resolvedStyle.translate.y < 7 && Mathf.Abs(panel.resolvedStyle.translate.x - 11) < .01f,
            "Toolkit exit does not move upward without sideways drift.");
        ScreenMotion.Enter(panel); yield return new WaitForSecondsRealtime(.5f);
        Check(staleCallbacks == 0 && root.Q("motion-probe") == panel, "Toolkit reopen did not restore its query name/cancel stale callback."); ToolkitRestored();
        ScreenMotion.Animate(panel, 300, _ => detachedFrames++);
        ScreenMotion.Hide(panel, () => staleCallbacks++);
        yield return new WaitForSecondsRealtime(.06f); panel.RemoveFromHierarchy(); var detachedCount = detachedFrames;
        yield return new WaitForSecondsRealtime(.4f);
        Check(detachedFrames == detachedCount && staleCallbacks == 0, "Detached Toolkit tween kept running or invoked completion.");
        root.Add(panel); yield return new WaitForSecondsRealtime(.1f); ToolkitRestored();
        ScreenMotion.Pulse(panel); yield return new WaitForSecondsRealtime(.1f);
        Check(panel.resolvedStyle.scale.value.x > 1.06f && panel.Q("screen-motion-sparkle") != null, "Pulse does not scale and sparkle the intended content.");
        CaptureToolkit("ToolkitPulsePaused"); yield return new WaitForSecondsRealtime(.7f); ToolkitRestored();
        notes.Add("PASS: Toolkit and Canvas enter/exit, original geometry, duplicate input guards, pause independence, canceled stale exit, detach cancellation and effect cleanup.");

        phase = "stationary scene surface and animated child panel";
        var lateObject = new GameObject("Late enabled UI"); var late = lateObject.AddComponent<UIDocument>(); late.panelSettings = document.panelSettings;
        var lateRoot = late.rootVisualElement; lateRoot.style.backgroundColor = new Color(.12f, .2f, .3f);
        yield return new WaitForSecondsRealtime(.16f);
        Check(lateRoot.resolvedStyle.opacity > .99f, "Scene-wide document root should remain fully visible without an entry animation.");
        var latePanel = new VisualElement { name = "late-child-panel" }; latePanel.style.width = 220; latePanel.style.height = 100; lateRoot.Add(latePanel);
        ScreenMotion.Enter(latePanel); yield return new WaitForSecondsRealtime(.09f);
        Check(latePanel.resolvedStyle.opacity > 0 && latePanel.resolvedStyle.opacity < 1 && latePanel.resolvedStyle.translate.y > 0,
            "A panel inside a scene should retain its upward entry motion.");
        yield return new WaitForSecondsRealtime(.5f); Check(latePanel.resolvedStyle.opacity > .99f, "Child panel entry did not settle."); Destroy(lateObject);

        phase = "immediate same scene dungeon document switch";
        var first = CreateDocument("Dungeon entry fixture"); var second = CreateDocument("Dungeon route fixture");
        second.gameObject.SetActive(false);
        var flow = new GameObject("Dungeon flow fixture").AddComponent<DungeonFlowController>(); flow.enabled = false;
        yield return new WaitForSecondsRealtime(.5f);
        var firstRoot = first.GetComponent<UIDocument>().rootVisualElement;
        var initializeCount = 0;
        var switchDocument = typeof(DungeonFlowController).GetMethod("PresentDocument", BindingFlags.Instance | BindingFlags.NonPublic);
        switchDocument.Invoke(flow, new object[] { second, first, (Action)(() => initializeCount++) });
        Check(initializeCount == 1 && !first.gameObject.activeSelf && second.gameObject.activeSelf && second.GetComponent<UIDocument>().rootVisualElement.resolvedStyle.opacity > .99f,
            "Dungeon root documents should switch immediately without fading or scaling the whole scene.");
        switchDocument.Invoke(flow, new object[] { first, second, (Action)(() => initializeCount++) });
        Check(initializeCount == 2 && first.gameObject.activeSelf && !second.gameObject.activeSelf && firstRoot.resolvedStyle.visibility == Visibility.Visible,
            "Dungeon document reversal left the destination hidden or failed to deactivate the outgoing document.");
        Destroy(first.gameObject); Destroy(second.gameObject); Destroy(flow.gameObject);
        notes.Add("PASS: same-scene dungeon root documents switch immediately without scene-wide motion.");

        phase = "persistent driver black overlay scene roundtrip";
        var driver = FindFirstObjectByType<ScreenMotionDriver>();
        Check(driver != null, "Scene driver is missing."); var driverId = driver.GetInstanceID();
        ScreenMotion.LoadScene("MotionSceneB");
        yield return new WaitForSecondsRealtime(.06f);
        var transitionGroup = GameObject.Find("Scene transition black overlay")?.GetComponent<CanvasGroup>();
        Check(SceneManager.GetActiveScene().name == "MotionSceneA" && transitionGroup != null && transitionGroup.alpha > 0 && transitionGroup.alpha < 1 && root.resolvedStyle.opacity > .99f,
            "Scene navigation should fade a black overlay while leaving the scene root unchanged.");
        yield return UntilScene("MotionSceneB");
        SetupToolkit(); yield return new WaitForSecondsRealtime(.09f);
        transitionGroup = GameObject.Find("Scene transition black overlay")?.GetComponent<CanvasGroup>();
        Check(document.rootVisualElement.resolvedStyle.opacity > .99f && transitionGroup != null && transitionGroup.alpha > 0 && transitionGroup.alpha < 1,
            "New scene should fade out the black overlay without moving or fading the scene root.");
        yield return new WaitForSecondsRealtime(.6f);
        Check(transitionGroup.alpha < .001f && !transitionGroup.blocksRaycasts, "Black transition overlay did not fully clear or kept blocking input.");
        Check(FindFirstObjectByType<ScreenMotionDriver>().GetInstanceID() == driverId, "Scene transition replaced the persistent motion driver.");
        ScreenMotion.LoadScene("MotionSceneA"); yield return UntilScene("MotionSceneA"); yield return new WaitForSecondsRealtime(.6f);
        Check(FindObjectsByType<ScreenMotionDriver>(FindObjectsSortMode.None).Length == 1, "Scene roundtrip duplicated the motion driver.");
        Check(runtimeErrors.Count == 0, "Presentation raised runtime errors: " + string.Join("; ", runtimeErrors));
        notes.Add("PASS: late enabled UIDocument, visible scene exit/entry at timeScale zero and one persistent motion driver across a real asynchronous scene roundtrip.");
    }
    private static IEnumerator UntilScene(string expected)
    {
        var deadline = Time.realtimeSinceStartup + 15;
        while (SceneManager.GetActiveScene().name != expected)
        { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Scene did not open: " + expected); yield return null; }
    }
    private void SetupToolkit()
    {
        document = FindFirstObjectByType<UIDocument>();
#if UNITY_EDITOR
        if (document.panelSettings == null)
            document.panelSettings = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Project/Scenes/Fixed1920PanelSettings.asset"));
#endif
        root = document.rootVisualElement;
        toolkitTarget = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32); toolkitTarget.Create();
        document.panelSettings.targetTexture = toolkitTarget;
        root.style.width = 960; root.style.height = 540; root.style.backgroundColor = new Color(.04f, .07f, .1f);
    }
    private void SetupCanvas()
    {
        canvasTarget = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32); canvasTarget.Create();
        captureCamera = new GameObject("Canvas capture").AddComponent<Camera>(); captureCamera.orthographic = true;
        captureCamera.clearFlags = CameraClearFlags.SolidColor; captureCamera.backgroundColor = new Color(.04f, .07f, .1f); captureCamera.targetTexture = canvasTarget;
        var canvasObject = new GameObject("Canvas motion fixture", typeof(RectTransform), typeof(Canvas)); canvas = (RectTransform)canvasObject.transform;
        var ui = canvasObject.GetComponent<Canvas>(); ui.renderMode = RenderMode.ScreenSpaceCamera; ui.worldCamera = captureCamera; ui.planeDistance = 1;
        var cardObject = new GameObject("Artifact upgrade content", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        card = (RectTransform)cardObject.transform; card.SetParent(canvas, false); card.sizeDelta = new Vector2(460, 210); card.anchoredPosition = new Vector2(16, 5);
        card.localScale = new Vector3(1.04f, .97f, 1); cardObject.GetComponent<Image>().color = new Color(.23f, .39f, .5f);
        group = cardObject.GetComponent<CanvasGroup>();
    }
    private ScreenMotionFixtureDocument CreateDocument(string name)
    {
        var owner = new GameObject(name); var ui = owner.AddComponent<UIDocument>(); ui.panelSettings = document.panelSettings;
        return owner.AddComponent<ScreenMotionFixtureDocument>();
    }
    private void CanvasRestored()
    {
        Check(Mathf.Abs(group.alpha - .8f) < .01f && Vector2.Distance(card.anchoredPosition, position) < .1f && Vector3.Distance(card.localScale, scale) < .01f,
            "Canvas original alpha/position/scale were not restored.");
        Check(group.interactable && group.blocksRaycasts, "Canvas interaction remained disabled after reopening.");
    }
    private void ToolkitRestored()
    {
        Check(Mathf.Abs(panel.resolvedStyle.opacity - .8f) < .01f && Mathf.Abs(panel.resolvedStyle.translate.x - 11) < .1f && Mathf.Abs(panel.resolvedStyle.translate.y - 7) < .1f,
            "Toolkit original opacity/translation were not restored.");
        Check(Mathf.Abs(panel.resolvedStyle.scale.value.x - 1.06f) < .01f && Mathf.Abs(panel.resolvedStyle.scale.value.y - .98f) < .01f && panel.enabledSelf,
            "Toolkit original scale/interaction were not restored.");
    }
    private void LateUpdate()
    {
        if (document != null && document.rootVisualElement?.panel != null) PumpToolkit();
    }
    private void CaptureToolkit(string name) { PumpToolkit(); Capture(toolkitTarget, name); }
    private void PumpToolkit()
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; var runtimePanel = document.rootVisualElement.panel;
        typeof(PanelSettings).GetMethod("ApplyPanelSettings", flags)?.Invoke(document.panelSettings, null);
        runtimePanel.GetType().GetMethod("Update", flags, null, Type.EmptyTypes, null)?.Invoke(runtimePanel, null);
        runtimePanel.GetType().GetMethod("Repaint", flags, null, new[] { typeof(Event) }, null)?.Invoke(runtimePanel, new object[] { new Event { type = EventType.Repaint } });
        runtimePanel.GetType().GetMethod("Render", flags, null, Type.EmptyTypes, null)?.Invoke(runtimePanel, null);
    }
    private void CaptureCanvas(string name) { Canvas.ForceUpdateCanvases(); captureCamera.Render(); Capture(canvasTarget, name); }
    private static void Capture(RenderTexture target, string name)
    {
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
        File.WriteAllBytes(Folder + "/" + name + ".png", texture.EncodeToPNG()); Destroy(texture); RenderTexture.active = previous;
    }
    private void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    private void OnLog(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && stack.Contains("FightingAllstar.Presentation"))
            runtimeErrors.Add(message);
    }
    private void Finish(Exception error)
    {
        Time.timeScale = originalTimeScale;
        Application.logMessageReceived -= OnLog;
        notes.Add(error == null ? "PASS: " + assertions + " assertions in native Unity Play mode at timeScale zero."
            : "FAIL during " + phase + ": " + error);
        Directory.CreateDirectory(Folder); File.WriteAllLines(Folder + "/Verification.txt", notes);
        if (error != null) Debug.LogException(error);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(error == null ? 0 : 1);
#endif
    }
}
public sealed class ScreenMotionFixtureDocument : MonoBehaviour { }
#endif
