#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
[InitializeOnLoad]
public static class InspectorPreviewChecks
{
    static InspectorPreviewChecks(){EditorApplication.update+=Tick;}
    public static void Run(){SessionState.SetBool("InspectorChecks",true);EditorApplication.isPlaying=true;}
    static void Tick(){if(!SessionState.GetBool("InspectorChecks",false)||!EditorApplication.isPlaying)return;SessionState.SetBool("InspectorChecks",false);new GameObject().AddComponent<InspectorPreviewRunner>();}
}
public sealed class InspectorPreviewRunner:MonoBehaviour
{
    int checks;
    IEnumerator Start(){yield return Guard(Run());}
    IEnumerator Guard(IEnumerator routine)
    {
        while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){Debug.LogError("INSPECTOR FAIL "+e);EditorApplication.Exit(1);yield break;}if(next is IEnumerator nested)yield return Guard(nested);else yield return next;}
    }
    void Require(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    void Click(BattleCharacterInspector inspector,string name){inspector.GetComponentsInChildren<Button>().First(b=>b.name==name).onClick.Invoke();}
    bool Has(BattleCharacterInspector inspector,string text)=>inspector.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains(text));
    IEnumerator Run()
    {
        var state=new BattleState();
        var names=new[]{"King 94","Chin Gentsai 94","Benimaru 94","Sie Kensou 94"};
        for(int i=0;i<4;i++)state.Player.Fighters.Add(new FighterState{Id="ally"+i,Side=TeamSide.Player,IsReserve=i==3,
            Definition=new CharacterDefinition{Id="ally"+i,DisplayName=names[i],BaseStats=new StatBlock{Attack=9994,Defense=9170,MaxHealth=136629,PierceBp=14440}},Health=120000});
        var f=state.Player.Fighters[0];
        f.PassiveContributions.Add(new PassiveStatContribution{OwnerId="ally3",RuleId="team-pierce",Modifier=new StatModifierDefinition{Stat=StatId.Pierce,Operation=ModifierOperation.PercentagePoints,Amount=800}});
        state.Player.Fighters[3].Definition.PassiveSource=new PassiveSourceDefinition{Description="Increases allies' Pierce Rate while this character is alive, including in the SUB slot."};
        for(int slot=1;slot<=2;slot++)
        {
            var skill=new SkillDefinition{Slot=slot,Category=CardCategory.Attack,TargetScope=EffectTargetScope.SelectedEnemy};
            for(int rank=1;rank<=3;rank++)skill.Ranks.Add(new SkillRankDefinition{Rank=rank,Description="Skill "+slot+" rank "+rank+" deals "+rank*100+"% Attack damage. Applies a debuff for 2 turns.",Effect=new EffectDefinition{CoefficientBp=rank*10000}});
            f.Definition.Skills.Add(skill);
        }
        for(int level=0;level<=6;level++)f.Definition.UltimateTiers.Add(new UltimateTierDefinition{Tier=level,Category=CardCategory.Attack,Description="Ultimate level "+level+" deals "+(300+level*50)+"% Attack damage.",Effect=new EffectDefinition{Target=EffectTargetScope.AllEnemies,CoefficientBp=30000+level*5000}});
        var recipe=new StatusRecipeDefinition{Id="protection",Polarity=StatusPolarity.Buff,DefaultDuration=3,Modifiers={new StatModifierDefinition{Stat=StatId.Attack,Operation=ModifierOperation.PercentOfBase,Amount=1500},new StatModifierDefinition{Stat=StatId.Defense,Operation=ModifierOperation.PercentOfBase,Amount=-1000}}};
        f.Statuses.Instances.Add(new StatusInstance{Recipe=recipe,RecipeId=recipe.Id,SourceFighterId="ally1",RemainingDuration=2});
        var visual=ScriptableObject.CreateInstance<StatusVisualData>();var so=new SerializedObject(visual);
        so.FindProperty("id").stringValue="protection";so.FindProperty("displayName").stringValue="Protect";
        so.FindProperty("description").stringValue="Increases Attack by 15%. This long description checks wrapping, so every effect remains readable alongside the source owner and duration.";
        so.FindProperty("icon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/icon_buff_atk_add.png");so.ApplyModifiedProperties();StatusVisualData.Register(visual);
        var hudRoot=new GameObject("HUD",typeof(RectTransform));hudRoot.SetActive(false);
        var slider=new GameObject("Gauge",typeof(RectTransform),typeof(Slider)).GetComponent<Slider>();slider.transform.SetParent(hudRoot.transform);
        var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image)).GetComponent<Image>();fill.transform.SetParent(slider.transform);slider.fillRect=fill.rectTransform;
        var hud=hudRoot.AddComponent<CoreFighterHud>();var hso=new SerializedObject(hud);hso.FindProperty("powerGaugeSlider").objectReferenceValue=slider;
        hso.FindProperty("statusIconPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StatusIcon.prefab");hso.ApplyModifiedProperties();hudRoot.SetActive(true);
        var inspector=new GameObject("Inspector").AddComponent<BattleCharacterInspector>();
        inspector.Open(state,f.Id,id=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icon_18_King94.png"),id=>hud,()=>{},
            (fighter,slot)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icon_18_King94.png"),(fighter,slot)=>slot==0?"Venom Strike":slot==1?"Double Strike":"Illusion Dance",(fighter,slot,rank)=>FixtureTooltip(fighter,slot,rank));
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.05f);
        var canvas=inspector.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
        Require(Has(inspector,"152.4%"),"Missing effective Pierce");Require(Has(inspector,"#FF8894>8,253"),"Missing reduced-stat color");
        Require(!Has(inspector,"(0%)"),"Zero modifier should be hidden");
        Require(BattleCharacterInspector.StatValue(f,StatId.Recovery,StatusSystem.GetEffectiveStats(f))=="100%","Unmodified stat must contain only its total");
        Capture(camera,rt,"inspector-overview-landscape");
        Click(inspector,"Tab Effects");yield return null;yield return null;Canvas.ForceUpdateCanvases();
        Require(Has(inspector,"From Chin Gentsai 94"),"Missing source ownership");
        Require(inspector.GetComponentsInChildren<Image>().Any(i=>i.name.StartsWith("Passive ally3")),"Passive contribution must use a styled effect card");
        Require(inspector.GetComponentsInChildren<Image>().Any(i=>i.name=="Cooldown"&&Mathf.Abs(i.fillAmount-1f/3)<.01f),"Authored cooldown overlay not reused");
        Capture(camera,rt,"inspector-effects-landscape");
        Click(inspector,"Tab Cards");yield return null;yield return null;Capture(camera,rt,"inspector-cards-landscape");
        for(int slot=1;slot<=2;slot++)
        {
            Click(inspector,"Card "+slot);yield return null;
            for(int rank=1;rank<=3;rank++){Click(inspector,"Rank "+rank);yield return null;if(slot==1&&rank==1)Capture(camera,rt,"inspector-card-tooltip");Require(Has(inspector,"Skill "+slot+" rank "+rank+" deals"),"Wrong skill rank description");Require(Has(inspector,"Pierce: ignores 3x"),"Tooltip keyword explanation missing");Require(inspector.GetComponentsInChildren<Image>().Any(i=>i.name=="Tooltip status icon Ignite"&&i.sprite!=null),"Tooltip status badge missing");Require(inspector.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("#fbbf24")&&t.text.Contains("#60a5fa")),"Colored tooltip description missing");}
            Click(inspector,"Close card");yield return null;
        }
        Click(inspector,"Card 0");yield return null;
        for(int level=0;level<=6;level++){Click(inspector,"Level "+level);yield return null;Require(Has(inspector,"Ultimate level "+level+" deals"),"Wrong ultimate level description");}
        yield return null;Capture(camera,rt,"inspector-card-detail-landscape");
        Click(inspector,"Close card");yield return null;
        Click(inspector,"Select Sie Kensou 94");yield return null;Require(Has(inspector,"YOUR TEAM  /  SUB"),"SUB selection failed");
        Click(inspector,"Close");yield return null;Require(inspector==null,"Inspector did not close");
        // Characters below the former 40% cutoff are inspectable on both sides.
        var own=GameObject.CreatePrimitive(PrimitiveType.Cube);own.transform.position=camera.ViewportToWorldPoint(new Vector3(.3f,.25f,5));
        var enemy=GameObject.CreatePrimitive(PrimitiveType.Cube);enemy.transform.position=camera.ViewportToWorldPoint(new Vector3(.7f,.7f,5));
        var views=new Dictionary<string,GameObject>{{"own",own},{"enemy",enemy}};Physics.SyncTransforms();
        Require(BattleInspectorPicking.Pick(camera,camera.WorldToScreenPoint(own.transform.position),views)=="own","Allied lower-screen picking failed");
        Require(BattleInspectorPicking.Pick(camera,camera.WorldToScreenPoint(enemy.transform.position),views)=="enemy","Enemy picking failed");
        own.SetActive(false);Physics.SyncTransforms();Require(BattleInspectorPicking.Pick(camera,camera.WorldToScreenPoint(own.transform.position),views)!="own","Hidden SUB model should not be picked");
        Debug.Log("INSPECTOR CHECK PASS: "+checks+" checks; landscape layout, zero modifiers, styled passive/status rows, cooldown, all skill ranks and ultimate levels, SUB, close, allied/enemy picking.");EditorApplication.Exit(0);
    }
    CoreBattleSceneController.InspectorCardTooltipData FixtureTooltip(FighterState fighter,int slot,int rank)
    {
        var icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/icon_buff_atk_add.png");
        return new CoreBattleSceneController.InspectorCardTooltipData
        {
            Title="\""+(slot==0?"Venom Strike":slot==1?"Double Strike":"Illusion Dance")+"\"",
            Description=(slot==0?"Ultimate level "+rank+" deals ":"Skill "+slot+" rank "+rank+" deals ")+"<color=#fbbf24><b>150%</b></color> <color=#60a5fa><b>Attack</b></color> damage.",
            Keywords=slot==0?"":"Pierce: ignores 3x target Resistance.",CharacterIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icon_18_King94.png"),
            CardTypeIcon=icon,Statuses=slot==0?new List<CoreBattleSceneController.StatusBadgeData>():new List<CoreBattleSceneController.StatusBadgeData>{new CoreBattleSceneController.StatusBadgeData{Name="Ignite",Icon=icon,IsBuff=false}}
        };
    }
    void Capture(Camera camera,RenderTexture rt,string name)
    {Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();var path=Path.Combine(Application.dataPath,"../Evidence");Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,name+".png"),tex.EncodeToPNG());Destroy(tex);RenderTexture.active=null;}
}
#endif



