#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Content;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public static class StudioBridgeChecks
    {
        public static string Run()
        {
            var passed=new List<string>();void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Bridge: "+message);passed.Add(message);}
            var sources=CharacterObjectRegistrySO.LoadAll().Where(c=>c!=null).ToList();
            var sourcePaths=sources.SelectMany(c=>new UnityEngine.Object[]{c,c.Skill1,c.Skill2,c.Ultimate,c.PassiveDefinition}).Where(o=>o!=null).Select(AssetDatabase.GetAssetPath).Append("Assets/Resources/CharacterObjectRegistry.asset").Distinct().ToList();
            var original=sourcePaths.ToDictionary(p=>p,Hash);var source=sources.Single(c=>c.DefinitionId=="fighter.kaeya");
            var baseline=CharacterObjectCatalogBuilder.CreateDefinition(source);
            Check(sources.All(c=>StudioCompiler.Compile(StudioDocumentService.ImportCharacter(CharacterObjectCatalogBuilder.CreateDefinition(c))).Success),"All registered character sources import and compile through the general path.");
            var suffix=Guid.NewGuid().ToString("N").Substring(0,10);var id="fighter.bridge-check-"+suffix;var numeric=800000+Math.Abs(suffix.GetHashCode()%100000);
            while(sources.Any(c=>c.ID==numeric))numeric++;
            var draft=StudioDraftRepository.Create(source,AssetDatabase.GenerateUniqueAssetPath(StudioDraftRepository.Root+"/Drafts/BridgeVerification.asset"));
            CharacterObject published=null;var draftPath=AssetDatabase.GetAssetPath(draft);var folder=StudioProductionBridge.RuntimeRoot+"/"+id.Substring(8);
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var dirty=scene.isDirty;
            try
            {
                StudioDraftRepository.TryRead(draft,out var doc,out var library,out _);
                // Compose the existing migrated rule; nested tree must survive actual .asset import.
                doc.Actions.Add(new StudioActionNode {Id="bridge.sequence",Kind=StudioActionKind.Sequence,Children=new List<string>{"bridge.gauge"}});
                doc.Actions.Add(new StudioActionNode {Id="bridge.gauge",Kind=StudioActionKind.Command,Command=new PassiveCommandDefinition {Kind=PassiveCommandKind.ExecuteEffect,
                    Effect=new CardEffectOperationDefinition {Id="bridge.op",Kind=CardEffectOperationKind.ChangePowerGauge,Target=EffectTargetScope.AllAllies,Magnitude=2}}});
                doc.Rules[0].ActionRootId="bridge.sequence";
                doc.AbilityVariants.Add(new StudioAbilityVariant {SkillId=baseline.Skills[0].Id,Definition=new AbilityVariantDefinition {Id="bridge.ex",Name="Verification EX",InitiallyUnlocked=true,Ranks=baseline.Skills[0].Ranks.ConvertAll(r=>r.Clone())}});
                doc.AbilityOverrides.Add(new StudioAbilityOverride {SkillId=baseline.Skills[0].Id,Definition=new AbilityValueOverride {Id="bridge.value",Field=AbilityValueField.CoefficientBp,Value=17777}});
                doc.Description.Locales[0].Blocks[0].Source="Bridge verification wording {{value:param.opening-gauge|integer}}";
                StudioDraftRepository.Edit(draft,doc,library,"Verification bridge authoring");StudioDraftRepository.Save(draft);
                var plan=StudioProductionBridge.Review(draft,id,numeric,"Bridge verification",false,StudioPublishWording.Passive);
                Check(plan.Paths.Length==5 && plan.Paths.All(p=>!p.Contains("/Editor/")),"Review produces exactly five runtime source assets outside Editor folders.");
                Check(!Directory.Exists(folder),"Review performs native roundtrip validation without writing a package.");
                var stale=plan;doc.Parameters.First(p=>p.Id=="param.opening-gauge").Value=3;StudioDraftRepository.Edit(draft,doc,library,"Verification stale revision");
                var rejected=false;try{StudioProductionBridge.Publish(draft,stale);}catch(InvalidOperationException){rejected=true;}
                Check(rejected && !Directory.Exists(folder),"A revision changed after review cannot publish.");StudioDraftRepository.Save(draft);
                plan=StudioProductionBridge.Review(draft,id,numeric,"Bridge verification",false,StudioPublishWording.Passive);
                StudioProductionBridge.VerificationCheckpoint=_=>throw new IOException("Injected partial write");
                rejected=false;try{StudioProductionBridge.Publish(draft,plan);}catch(IOException){rejected=true;}finally{StudioProductionBridge.VerificationCheckpoint=null;}
                Check(rejected && !Directory.Exists(folder) && draft.PublishedCharacter==null,"Partial creation failure removes the staged package and preserves the previous receipt.");
                plan=StudioProductionBridge.Review(draft,id,numeric,"Bridge verification",false,StudioPublishWording.Passive);published=StudioProductionBridge.Publish(draft,plan);
                Check(plan.Paths.All(File.Exists),"Publishing writes CharacterObject, both skills, ultimate and passive assets.");
                Check(published.StudioOwned && published.Skill1.StudioOwned && published.Skill2.StudioOwned && published.Ultimate.StudioOwned && published.PassiveDefinition.StudioOwned,"All five assets have explicit Studio source ownership.");
                var guids=plan.Paths.ToDictionary(p=>p,AssetDatabase.AssetPathToGUID);
                foreach(var path in plan.Paths)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                published=AssetDatabase.LoadAssetAtPath<CharacterObject>(plan.Paths[0]);var native=CharacterObjectCatalogBuilder.CreateDefinition(published);
                Check(native.Passive.Reactions.Single(r=>r.Id==doc.Rules[0].Id).Actions[0].Children.Count==1,"Composed actions survive native asset save and reimport.");
                Check(native.Passive.Reactions.Single(r=>r.Id==doc.Rules[0].Id).Conditions[0].Children.Count==2,"Nested All/Any/Not conditions survive native asset save and reimport.");
                Check(native.Skills[0].Variants.Any(v=>v.Id=="bridge.ex") && native.Skills[0].ValueOverrides.Any(v=>v.Id=="bridge.value"),"Skill variants and upgrade overrides survive native SO reconstruction.");
                Check(Plain(native.PassiveSource.Description).Contains("Bridge verification wording 3"),"Authored passive wording reaches the normal runtime definition.");
                Check(published.Skill1.GetRankData(1).runtimeEffect!=null && published.Ultimate.GetLevelData(0).runtimeEffect!=null,"Existing native card consumers resolve full canonical effects.");
                var content=ScriptableObject.CreateInstance<FightingAllstar.Presentation.Combat.CombatContentSource>();
                ContentCatalog catalog;try{catalog=content.LoadCatalog();}finally{UnityEngine.Object.DestroyImmediate(content);}
                Check(catalog.Characters.Any(c=>c.Id==id) && ContentValidator.Validate(catalog).Count==0,"The normal CombatContentSource discovers the new character without a Studio override.");
                var enemy=baseline.Clone();enemy.Id="fighter.bridge.enemy";var battle=BattleLabSimulation.Create(new BattleLabConfiguration {Actors=new List<BattleLabFighter>{new BattleLabFighter {Definition=catalog.Characters.Single(c=>c.Id==id)}},Targets=new List<BattleLabFighter>{new BattleLabFighter {Definition=enemy}}},out _);
                Check(battle.Player.Fighters[0].PowerGauge>=2,"The persisted composed passive executes through the production engine after native reconstruction.");
                var beforeUpdate=plan.Paths.ToDictionary(p=>p,Hash);
                doc.Parameters.First(p=>p.Id=="param.opening-gauge").Value=4;StudioDraftRepository.Edit(draft,doc,library,"Verification update");StudioDraftRepository.Save(draft);
                var update=StudioProductionBridge.Review(draft,id,numeric,"Bridge updated",true,StudioPublishWording.Passive);
                StudioProductionBridge.VerificationCheckpoint=_=>throw new IOException("Injected update failure");
                rejected=false;try{StudioProductionBridge.Publish(draft,update);}catch(IOException){rejected=true;}finally{StudioProductionBridge.VerificationCheckpoint=null;}
                Check(rejected && beforeUpdate.All(p=>Hash(p.Key)==p.Value),"Partial update failure restores the previous five native asset files.");
                update=StudioProductionBridge.Review(draft,id,numeric,"Bridge updated",true,StudioPublishWording.Passive);StudioProductionBridge.Publish(draft,update);
                Check(guids.All(p=>AssetDatabase.AssetPathToGUID(p.Key)==p.Value),"Updates preserve all five asset GUIDs.");
                Check(Plain(CharacterObjectCatalogBuilder.CreateDefinition(draft.PublishedCharacter).PassiveSource.Description).Contains("wording 4"),"A reviewed update installs new wording into normal runtime data.");
                // Unsaved edits are protected, not silently overwritten by rollback.
                EditorUtility.SetDirty(draft.PublishedCharacter);rejected=false;try{StudioProductionBridge.Rollback(draft);}catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Rollback protects unsaved external package edits.");AssetDatabase.SaveAssetIfDirty(draft.PublishedCharacter);
                StudioProductionBridge.Rollback(draft);
                Check(beforeUpdate.All(p=>Hash(p.Key)==p.Value),"Rollback restores the preceding native package byte-for-byte.");
                Check(guids.All(p=>AssetDatabase.AssetPathToGUID(p.Key)==p.Value),"Rollback preserves existing asset identities.");
                var ownedPassive=draft.PublishedCharacter.PassiveDefinition;rejected=false;try{ownedPassive.SetDefinition(new PassiveDefinition());}catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Legacy passive generators cannot replace Studio-owned payloads.");
                var conflictPlan=StudioProductionBridge.Review(draft,id,numeric,"Bridge verification",true);
                var originalName=draft.PublishedCharacter.name;draft.PublishedCharacter.name="External change";EditorUtility.SetDirty(draft.PublishedCharacter);AssetDatabase.SaveAssetIfDirty(draft.PublishedCharacter);
                rejected=false;try{StudioProductionBridge.Publish(draft,conflictPlan);}catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Externally changed package files block update publishing.");
                draft.PublishedCharacter.name=originalName;EditorUtility.SetDirty(draft.PublishedCharacter);AssetDatabase.SaveAssetIfDirty(draft.PublishedCharacter);
                // Restore exact tracked file bytes before testing the create rollback.
                Check(beforeUpdate.All(p=>Hash(p.Key)==p.Value),"The verification edit is restored to the reviewed package bytes.");
                StudioProductionBridge.Rollback(draft);published=null;
                Check(!Directory.Exists(folder) && !CharacterObjectRegistrySO.LoadAll().Any(c=>c.DefinitionId==id),"Rolling back creation removes the package from the runtime registry.");
                Check(original.All(p=>Hash(p.Key)==p.Value),"All original roster source assets remain byte-for-byte unchanged.");
                Check(scene==UnityEngine.SceneManagement.SceneManager.GetActiveScene() && scene.isDirty==dirty && !EditorApplication.isPlaying,"Publishing leaves the current scene, dirty state and Play Mode unchanged.");
                var result=JsonConvert.SerializeObject(new{Success=true,Checks=passed,OriginalCharacters=sources.Count,NativeAssetCount=5,SourceFilesChecked=original.Count,Scene=scene.path,CapturedUtc=DateTime.UtcNow.ToString("O"),Scope="Native asset create/update/import, normal runtime catalog and engine, conflict checks, injected failure recovery and rollback; disposable verification package removed."},Formatting.Indented);
                Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/Bridge");File.WriteAllText("Artifacts/CharacterCreatorStudio/Bridge/native-evidence.json",result);return result;
            }
            finally
            {
                StudioProductionBridge.VerificationCheckpoint=null;
                if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
                AssetDatabase.DeleteAsset(draftPath);FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
            }
        }
        private static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        private static string Plain(string text)=>System.Text.RegularExpressions.Regex.Replace(text ?? "","<[^>]+>","");
    }
}
#endif
