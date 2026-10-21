#if UNITY_EDITOR
using System;
using System.Linq;
using FightingAllstar.Core.Studio;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [SerializeField] private string _publishId="fighter.my-character",_publishName="New character";
        [SerializeField] private int _publishNumericId=60001;
        [SerializeField] private bool _publishUpdate;
        [SerializeField] private StudioPublishWording _publishWording;
        [NonSerialized] private StudioPublishPlan _publishPlan;
        private void BuildProductionPublish()
        {
            Section(_host,"PUBLISH TO NATIVE CHARACTER ASSETS");
            Body(_host,"Creates a dedicated runtime package: CharacterObject, two SkillCardSO assets, UltimateCardSO and PassiveDefinitionSO. Normal runtime loads it through the registry; no Studio override is needed.","studio-subtle");
            var update=new Toggle("Update this draft's published package"){value=_publishUpdate};update.SetEnabled(_draft.PublishedCharacter!=null);
            update.RegisterValueChangedCallback(e=>{_publishUpdate=e.newValue;_publishPlan=null;if(_publishUpdate){_publishId=_draft.PublishedCharacter.DefinitionId;_publishNumericId=_draft.PublishedCharacter.ID;_publishName=_draft.PublishedCharacter.FighterName;}Rebuild();});_host.Add(update);
            var id=new TextField("Runtime character ID"){value=_publishId,isDelayed=true};id.SetEnabled(!_publishUpdate);id.RegisterValueChangedCallback(e=>{_publishId=e.newValue;_publishPlan=null;});_host.Add(id);
            var numeric=new IntegerField("Unique numeric ID"){value=_publishNumericId,isDelayed=true};numeric.SetEnabled(!_publishUpdate);numeric.RegisterValueChangedCallback(e=>{_publishNumericId=e.newValue;_publishPlan=null;});_host.Add(numeric);
            var name=new TextField("Runtime character name"){value=_publishName,isDelayed=true};name.RegisterValueChangedCallback(e=>{_publishName=e.newValue;_publishPlan=null;});_host.Add(name);
            var wording=new EnumField("Authored wording destination",_publishWording);wording.RegisterValueChangedCallback(e=>{_publishWording=(StudioPublishWording)e.newValue;_publishPlan=null;});_host.Add(wording);
            Body(_host,"Preserve keeps all existing text. Other destinations materialize the current writer into the selected native slot, using the default locale and level 1. Skill ranks and ultimate tiers are resolved separately. Dynamic runtime locale/level text routing remains separate work.","studio-subtle");
            Button(_host,"Review native asset changes",()=>Guard(()=>{_publishPlan=StudioProductionBridge.Review(_draft,_publishId,_publishNumericId,_publishName,_publishUpdate,_publishWording);Rebuild();SetStatus("Native adapter roundtrip and candidate runtime catalog passed. Review the five assets before publishing.");})).name="studio-publish-review";
            if(_publishPlan!=null)
            {
                Body(_host,(_publishPlan.Update?"UPDATE OWNED PACKAGE":"CREATE NEW PACKAGE")+" · "+_publishPlan.CharacterId+" · numeric ID "+_publishPlan.CategoryId,"studio-success");
                foreach(var path in _publishPlan.Paths)Body(_host,path,"studio-code");
                Body(_host,"Reviewed bundle "+_publishPlan.BundleHash+"\nCanonical JSON payloads preserve recursive mechanics; native mirrors are read-only. Legacy source assets remain unchanged.","studio-subtle");
                Button(_host,"Publish reviewed character package",()=>Guard(()=>{var published=StudioProductionBridge.Publish(_draft,_publishPlan);_publishPlan=null;_publishUpdate=true;Rebuild();SetStatus("Published "+AssetDatabase.GetAssetPath(published)+". Normal runtime catalog validation passed.");})).name="studio-publish-commit";
            }
            if(_draft.PublishedCharacter!=null)Button(_host,"Select published CharacterObject",()=>Selection.activeObject=_draft.PublishedCharacter);
            if(!string.IsNullOrEmpty(_draft.LastPublishBackup))Button(_host,"Rollback last native publish",()=>Guard(()=>{StudioProductionBridge.Rollback(_draft);_publishPlan=null;_publishUpdate=_draft.PublishedCharacter!=null;Rebuild();SetStatus("Last native publish reverted. Edits made after publish are protected by conflict checks.");}));
            Body(_host,"Update/rollback reject external package edits. Local recovery backups live in Library/CharacterCreatorStudio/PublishBackups; retain Library while a rollback may be needed. Publishing is disabled during Play Mode.","studio-subtle");
        }
    }
}
#endif
