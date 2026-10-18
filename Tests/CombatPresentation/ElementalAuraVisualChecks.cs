#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class ElementalAuraVisualChecks
{
    static ElementalAuraVisualChecks() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("AuraChecks", true); EditorApplication.EnterPlaymode(); }
    private static void Tick()
    {
        if (!SessionState.GetBool("AuraChecks", false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool("AuraChecks", false);
        new GameObject("Aura verification").AddComponent<ElementalAuraVisualRunner>();
    }
}
public sealed class ElementalAuraVisualRunner : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private int checks;
    private string output;
    private void Check(bool passed, string message) { checks++; if (!passed) throw new Exception(message); }
    private IEnumerator Start()
    {
        output = Path.GetFullPath("Artifacts/ElementalAuras"); Directory.CreateDirectory(output);
        var routine = Verify();
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception error) { File.WriteAllText(Path.Combine(output,"result.txt"), "FAIL: "+error); Debug.LogException(error); EditorApplication.Exit(1); yield break; }
            yield return next;
        }
    }
    private IEnumerator Verify()
    {
        Application.targetFrameRate=60;Application.runInBackground=true;QualitySettings.vSyncCount=0;
        var camera = new GameObject("Camera",typeof(Camera)).GetComponent<Camera>(); camera.tag="MainCamera";
        camera.transform.position=new Vector3(0,4,-12);camera.transform.LookAt(new Vector3(0,.8f,0)); camera.orthographic=true; camera.orthographicSize=4.5f; camera.aspect=1280f/720;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.045f,.075f);
        var light=new GameObject("Key Light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-35,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Wet ground test floor";floor.transform.position=new Vector3(0,-2.1f,0);floor.transform.localScale=new Vector3(13,.15f,4);
        var floorMaterial=new Material(Shader.Find("Standard"));floorMaterial.color=new Color(.06f,.11f,.16f);floorMaterial.SetFloat("_Glossiness",.9f);floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
        var ui=new GameObject("Aura UI"); ui.SetActive(false); var doc=ui.AddComponent<UIDocument>();
        doc.panelSettings=ScriptableObject.CreateInstance<PanelSettings>();
        doc.panelSettings.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
        doc.panelSettings.targetTexture=new RenderTexture(1280,720,24); ui.SetActive(true);
        var root=doc.rootVisualElement; root.style.position=Position.Absolute; root.style.left=root.style.right=root.style.top=root.style.bottom=0;
        Label Text(string text,int x,int y,int size=22)
        {
            var label=new Label(text); label.style.position=Position.Absolute;label.style.left=x;label.style.top=y;label.style.fontSize=size;label.style.color=Color.white;root.Add(label);return label;
        }
        Text("ELEMENTAL AURAS",48,30,32); Text("Neutral  ·  3 target turn ends  ·  Cannot stack or refresh",48,80,22);
        var fighter=new FighterState { Id="aura-target",Health=1000,Definition=new CharacterDefinition { BaseStats=new StatBlock { MaxHealth=1000 } } };
        var elements=new[] { ElementId.Pyro,ElementId.Hydro,ElementId.Electro,ElementId.Cryo,ElementId.Dendro };
        StatusVisualData.ResetRegistry(); StatusVisualData.InitializeRegistry();
        for(var i=0;i<elements.Length;i++)
        {
            var element=elements[i];fighter.ElementStatuses.Apply(element,"source",element.ToString());
            var visual=StatusVisualData.Get("status.element."+element.ToString().ToLowerInvariant(),StatusPolarity.Neutral);
            Check(visual!=null && visual.Icon!=null && visual.Polarity==StatusPolarity.Neutral,"Missing authored neutral icon: "+element);
            var icon=new Image { sprite=visual.Icon };icon.style.position=Position.Absolute;icon.style.left=100+i*235;icon.style.top=150;icon.style.width=110;icon.style.height=110;root.Add(icon);
            Text(element.ToString(),110+i*235,275).style.color=GenshinCombatTextStyle.ElementColor(element);
        }
        Check(fighter.Statuses.Instances.Count==0 && CoreFighterHud.DisplayStatuses(fighter).Count==0,"Aura display mutated debuff state.");
        var hudRoot=new GameObject("HUD",typeof(RectTransform));hudRoot.SetActive(false);
        var panel=new GameObject("StatusContainer",typeof(RectTransform),typeof(UnityEngine.UI.GridLayoutGroup));panel.transform.SetParent(hudRoot.transform,false);
        var hud=hudRoot.AddComponent<CoreFighterHud>();typeof(CoreFighterHud).GetField("_statusPanel",Private).SetValue(hud,panel);hudRoot.SetActive(true);
        hud.SetStatuses(CoreFighterHud.DisplayStatuses(fighter));hud.SetElementStatuses(fighter.ElementStatuses.Instances);
        var elementPanel=(GameObject)typeof(CoreFighterHud).GetField("_elementStatusPanel",Private).GetValue(hud);
        Check(panel.transform.childCount==0 && elementPanel.transform.childCount==5,"Production HUD did not show exactly five aura icons.");
        foreach(var aura in fighter.ElementStatuses.Instances)
            Check(elementPanel.GetComponentsInChildren<UnityEngine.UI.Image>(true).Any(image=>image.sprite==StatusVisualData.GetSprite(aura.ToDisplayStatus().RecipeId,StatusPolarity.Neutral)),"Production HUD used fallback art: "+aura.Element);
        fighter.ElementStatuses.AdvanceTargetTurnEnd();hud.SetElementStatuses(fighter.ElementStatuses.Instances);
        Check(elementPanel.transform.childCount==5 && panel.transform.childCount==0 && fighter.ElementStatuses.Instances.All(a=>a.RemainingDuration==2),"HUD duplicated icons when clocks advanced.");
        var roster=GenshinStarterRoster.Create().Take(4).ToArray();var state=BattleEngine.Create("aura-visual",roster,null,roster,null,21);
        var controller=new GameObject("Controller").AddComponent<CoreBattleSceneController>();controller.enabled=false;
        void Set(string name,object value)=>typeof(CoreBattleSceneController).GetField(name,Private).SetValue(controller,value);
        object Call(string name,params object[] args)=>typeof(CoreBattleSceneController).GetMethod(name,Private).Invoke(controller,args);
        Set("_snapshot",state);Set("_displayState",state.Clone());
        var layer=new VisualElement();layer.style.position=Position.Absolute;layer.style.left=layer.style.right=layer.style.top=layer.style.bottom=0;root.Add(layer);Set("_effectsLayer",layer);
        var views=(Dictionary<string,GameObject>)typeof(CoreBattleSceneController).GetField("_fighterViews",Private).GetValue(controller);
        for(var i=0;i<3;i++) { var view=GameObject.CreatePrimitive(PrimitiveType.Capsule);view.name="Target "+i;view.transform.position=new Vector3(-4+i*4,-1,0);views["target"+i]=view; }
        var stage=new GameObject("Stage").AddComponent<BattleStagePresenter>();stage.Initialize(views);Set("_stage",stage);
        // Slow only the capture fixture: image encoding must not skip the ground and rising phases.
        Check(Mathf.Approximately((float)typeof(BattleStagePresenter).GetField("electrifyTravelDuration",Private).GetValue(stage),.5f),"Unexpected default lightning travel time.");
        typeof(BattleStagePresenter).GetField("electrifyTravelDuration",Private).SetValue(stage,2f);
        typeof(BattleStagePresenter).GetField("electrifyFadeDuration",Private).SetValue(stage,1.4f);
        var catalog=CharacterObjectCatalogBuilder.Load(null);
        foreach(var character in catalog.Characters.Where(c=>c.TraitIds.Contains(ElementalRuleSet.GenshinTrait)))
        {
            Check(ElementalContentValidator.Validate(character,catalog.ElementalRules).Count==0,"Invalid authored Genshin kit: "+character.Id);
            Check(character.Skills.SelectMany(s=>s.Ranks).All(r=>r.Effect.ElementalApplication==null),"Retired first-hit metadata remained at runtime.");
        }
        Text("ELECTRO-CHARGED  ·  3D WET-GROUND SPREAD",48,335,25);Text("Charged target → wet ground → eligible Hydro neighbor. Dry targets are untouched.",48,660,18);Text("Capture at ¼ speed",1030,625,16);
        yield return null; yield return null;
        Call("FloatText","target0","Hydro","status",1,1f,AttributeAffinity.Neutral,ElementId.Hydro,ElementalReactionId.None,state.Player.Fighters[0].Id);
        var caption=layer.Query<Label>().ToList().FirstOrDefault(l=>l.text=="Hydro");
        Check(caption!=null && caption.style.color.value==GenshinCombatTextStyle.ElementColor(ElementId.Hydro),"Aura application label lost its element color.");
        caption.style.display=DisplayStyle.None;
        controller.StartCoroutine((IEnumerator)Call("PresentReactionFeedback",new BattleEvent { TargetId="target1",TargetIds=new List<string>{"target0"},ElementalReaction=ElementalReactionId.ElectroCharged,DamageElement=ElementId.Hydro,TriggerElement=ElementId.Electro }));
        yield return new WaitForSecondsRealtime(.08f);
        var effect=stage.transform.Find("Electrify Spread 3D");
        Check(effect!=null && effect.GetComponentInChildren<ParticleSystem>()!=null,"World-space Electrify particles did not start.");
        Check(effect.GetComponentInChildren<ParticleSystem>().main.simulationSpace==ParticleSystemSimulationSpace.World,"Electrify particles are not world space.");
        Check(effect.GetComponentsInChildren<MeshRenderer>().Length==3,"Wet-ground sheen was not created along the path.");
        Check(layer.Q<VisualElement>("electrify-spread")==null,"Legacy screen-space lightning overlay is still active.");
        var line=effect.GetComponentsInChildren<LineRenderer>().First(l=>l.name=="Lightning core");
        var crossedGround=false;
        for(var frame=0;frame<120 && effect!=null && effect.gameObject.activeSelf;frame++)
        {
            var head=line.GetPosition(line.positionCount-1);
            if(head.y < -1.75f && head.x > -2.4f && head.x < -.1f) { crossedGround=true;break; }
            yield return null;
        }
        Check(crossedGround,"Lightning did not travel over the ground between targets.");
        Check(line.GetPosition(0).x < -3 && line.GetPosition(line.positionCount-1).x < 1,"Spread reached an unrelated dry target.");
        Check(line.GetPosition(0).y > -1.5f && line.GetPositions(new Vector3[line.positionCount])>0,"Lightning did not descend from the charged body.");
        FightingAllstar.EditorTools.BattlePreviewCapture.Save(Path.Combine(output,"01-auras-and-electrify.png"),doc);
        var roseIntoTarget=false;
        for(var frame=0;frame<120 && effect!=null && effect.gameObject.activeSelf;frame++)
        {
            var head=line.GetPosition(line.positionCount-1);
            if(head.y > -1.3f && Mathf.Abs(head.x)<.5f) { roseIntoTarget=true;break; }
            yield return null;
        }
        Check(roseIntoTarget,"Ground lightning did not rise into the eligible neighbor.");
        FightingAllstar.EditorTools.BattlePreviewCapture.Save(Path.Combine(output,"02-ground-lightning.png"),doc);
        for(var frame=0;frame<360 && stage.transform.Find("Electrify Spread 3D")!=null;frame++)yield return null;
        yield return new WaitForSecondsRealtime(.2f);
        Check(layer.Query<Label>().ToList().Any(l=>l.text=="Electro-Charged"),"Spread reaction caption missing.");
        Check(stage.transform.Find("Electrify Spread 3D")==null,"Completed spread left 3D particles or wet-ground meshes behind.");
        FightingAllstar.EditorTools.BattlePreviewCapture.Save(Path.Combine(output,"03-reaction.png"),doc);
        controller.StartCoroutine((IEnumerator)Call("PresentReactionFeedback",new BattleEvent { TargetId="target1",TargetIds=new List<string>{"target0"},ElementalReaction=ElementalReactionId.ElectroCharged,DamageElement=ElementId.Hydro,TriggerElement=ElementId.Electro }));
        yield return null; Call("OnDisable");
        Check(layer.childCount==0 && !stage.GetComponentsInChildren<ParticleSystem>(true).Any(p=>p.gameObject.activeInHierarchy),"Interrupted spread left UI or 3D particles behind.");
        File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: "+checks+" Unity aura icons, production HUD, authored content, labels, 3D wet-ground lightning/particles and cleanup checks.");
        EditorApplication.Exit(0);
    }
}
#endif
