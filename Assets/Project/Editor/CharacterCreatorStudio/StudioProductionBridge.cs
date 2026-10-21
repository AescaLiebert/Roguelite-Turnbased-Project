#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Content;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public enum StudioPublishWording { Preserve, Passive, Skill1, Skill2, Ultimate }
    public sealed class StudioPublishPlan
    {
        public string DraftRevision, BundleHash, Folder, SourceHash, DefinitionJson, ExistingFilesJson;
        public bool Update;
        public StudioPublishWording Wording;
        public string[] Paths;
        public string CharacterId;
        public int CategoryId;
    }
    [Serializable] internal sealed class StudioPublishBackup
    {
        public string[] Paths;
        public bool Created;
        public Dictionary<string,string> Before = new Dictionary<string,string>();
        public Dictionary<string,string> After = new Dictionary<string,string>();
        public string PreviousFiles, PreviousBackup, PreviousCharacter;
    }

    /// <summary>Review → candidate native roundtrip → staged write → registry validation → receipt.
    /// Only owned five-asset packages can be updated. Existing legacy packs are cloned, never overwritten.</summary>
    public static partial class StudioProductionBridge
    {
        public const string RuntimeRoot = "Assets/Project/Data/Character/Studio";
        internal static Action<string> VerificationCheckpoint;
        public static StudioPublishPlan Review(StudioDraftAsset draft,string characterId,int categoryId,string displayName,
            bool update=false,StudioPublishWording wording=StudioPublishWording.Preserve)
        {
            EditingOnly();
            if(!StudioDraftRepository.TryRead(draft,out var doc,out var library,out var readError))throw new InvalidOperationException(readError);
            var compiled=StudioCompiler.Compile(doc,library);
            if(!compiled.Success)throw new InvalidOperationException(string.Join("\n",compiled.Diagnostics));
            if(!Regex.IsMatch(characterId ?? "",@"^fighter\.[a-z0-9][a-z0-9._-]*$") || categoryId<=0 || string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException("Choose a fighter.* stable ID, positive numeric ID and character name.");
            var sourceHash=SourceFingerprint(draft.SourceCharacter);
            var expectedSource=string.IsNullOrEmpty(draft.ImportedSourceHash) ? DefinitionHash(doc.LegacyCharacter) : draft.ImportedSourceHash;
            var actualSource=expectedSource.StartsWith("source-v1:") ? sourceHash : SourceHash(draft.SourceCharacter);
            if(draft.SourceCharacter!=null && actualSource!=expectedSource)throw new InvalidOperationException("The imported source changed. Reimport and review its changes before publishing this frozen draft.");
            var target=draft.PublishedCharacter;
            if(update && (target==null || !target.StudioOwned || target.DefinitionId!=characterId || target.ID!=categoryId))
                throw new InvalidOperationException("Update is restricted to this draft's published identity. Use a new package for a new identity.");
            var folder=update ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(target)).Replace('\\','/') : RuntimeRoot+"/"+characterId.Substring(8);
            if(!IsAllowedRuntimeFolder(folder))throw new InvalidOperationException("Publish target must be inside the Studio runtime directory or an approved Pre-Release series folder.");
            var paths=update
                ? new[]{AssetDatabase.GetAssetPath(target),AssetDatabase.GetAssetPath(target.Skill1),AssetDatabase.GetAssetPath(target.Skill2),AssetDatabase.GetAssetPath(target.Ultimate),AssetDatabase.GetAssetPath(target.PassiveDefinition)}
                : new[]{folder+"/Character.asset",folder+"/Skill1.asset",folder+"/Skill2.asset",folder+"/Ultimate.asset",folder+"/Passive.asset"};
            if(update && (paths.Any(string.IsNullOrWhiteSpace) || paths.Any(path=>!string.Equals(Path.GetDirectoryName(path).Replace('\\','/'),folder,StringComparison.Ordinal)) || paths.Distinct(StringComparer.Ordinal).Count()!=5))
                throw new InvalidOperationException("The published Studio package must keep its five assets together in one folder.");
            if(!update && (AssetDatabase.IsValidFolder(folder) || Directory.Exists(folder)))throw new InvalidOperationException("That package directory already exists. Choose a new ID or update its owning draft.");
            if(update)CheckFiles(draft.PublishedFilesJson,paths);
            foreach(var existing in CharacterObjectRegistrySO.LoadAll().Where(c=>c!=null))
                if(existing!=target || !update)
                    if(existing.DefinitionId==characterId || existing.ID==categoryId)throw new InvalidOperationException("Character definition ID or numeric ID already exists: "+existing.DefinitionId);
            var definition=compiled.Character.Clone();
            ApplyWording(doc,library,definition,wording);
            definition=Reidentify(definition,characterId);definition.CategoryId=categoryId;definition.DisplayName=displayName;definition.RuntimeReady=true;
            var plan=new StudioPublishPlan { DraftRevision=StudioDraftRepository.Revision(draft.DocumentJson,draft.LibraryJson),BundleHash=compiled.Hashes.Bundle,
                Folder=folder,Paths=paths,Update=update,Wording=wording,SourceHash=sourceHash,ExistingFilesJson=draft.PublishedFilesJson,
                CharacterId=characterId,CategoryId=categoryId,DefinitionJson=SnapshotJson.Serialize(definition) };
            ValidateNativeRoundtrip(draft.SourceCharacter,definition,compiled.Hashes.Bundle);
            ValidateCandidate(definition,update ? target : null);
            return plan;
        }
        public static CharacterObject Publish(StudioDraftAsset draft,StudioPublishPlan reviewed)
        {
            EditingOnly();if(reviewed==null)throw new InvalidOperationException("Review the exact publish revision first.");
            var requested=SnapshotJson.Deserialize<CharacterDefinition>(reviewed.DefinitionJson);
            var fresh=Review(draft,requested.Id,requested.CategoryId,requested.DisplayName,reviewed.Update,reviewed.Wording);
            if(fresh.SourceHash!=reviewed.SourceHash || fresh.DraftRevision!=reviewed.DraftRevision || fresh.BundleHash!=reviewed.BundleHash || fresh.DefinitionJson!=reviewed.DefinitionJson || fresh.ExistingFilesJson!=reviewed.ExistingFilesJson)
                throw new InvalidOperationException("The draft or source changed after review. Review again.");
            var backup=new StudioPublishBackup { Paths=reviewed.Paths,Created=!reviewed.Update,PreviousFiles=draft.PublishedFilesJson,
                PreviousBackup=draft.LastPublishBackup,PreviousCharacter=AssetDatabase.GetAssetPath(draft.PublishedCharacter) };
            if(reviewed.Update)foreach(var path in reviewed.Paths)backup.Before[path]=Convert.ToBase64String(File.ReadAllBytes(path));
            var backupFolder=Path.GetFullPath("Library/CharacterCreatorStudio/PublishBackups/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backupFolder);
            var manifest=Path.Combine(backupFolder,"manifest.json");WriteManifest(manifest,backup); // Before the first mutation.
            try
            {
                EnsureFolder(reviewed.Folder);
                var character=LoadOrCreate<CharacterObject>(reviewed.Paths[0]);
                var skill1=LoadOrCreate<SkillCardSO>(reviewed.Paths[1]);var skill2=LoadOrCreate<SkillCardSO>(reviewed.Paths[2]);
                var ultimate=LoadOrCreate<UltimateCardSO>(reviewed.Paths[3]);var passive=LoadOrCreate<PassiveDefinitionSO>(reviewed.Paths[4]);
                Apply(character,skill1,skill2,ultimate,passive,requested,draft.SourceCharacter,reviewed.BundleHash);
                if(!StudioDraftRepository.TryRead(draft,out var authoring,out var library,out var readError))throw new InvalidOperationException(readError);
                EmbedAuthoring(character,authoring,library,authoring.SourceCharacterId);
                foreach(var obj in new Object[]{skill1,skill2,ultimate,passive,character}) { EditorUtility.SetDirty(obj);AssetDatabase.SaveAssetIfDirty(obj);VerificationCheckpoint?.Invoke(AssetDatabase.GetAssetPath(obj)); }
                foreach(var path in reviewed.Paths)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                character=AssetDatabase.LoadAssetAtPath<CharacterObject>(reviewed.Paths[0]);
                if(GameplayHash(CharacterObjectCatalogBuilder.CreateDefinition(character))!=GameplayHash(requested))throw new InvalidOperationException("Native asset reimport changed the reviewed gameplay definition.");
                FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
                var errors=ContentValidator.Validate(CharacterObjectCatalogBuilder.Load(null));
                if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
                backup.After=reviewed.Paths.ToDictionary(p=>p,FileHash);WriteManifest(manifest,backup);
                draft.PublishedCharacter=character;draft.PublishedFilesJson=JsonConvert.SerializeObject(backup.After);draft.LastPublishBackup=manifest;
                EditorUtility.SetDirty(draft);if(EditorUtility.IsPersistent(draft))AssetDatabase.SaveAssetIfDirty(draft);return character;
            }
            catch
            {
                Restore(backup);FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
                draft.PublishedCharacter=AssetDatabase.LoadAssetAtPath<CharacterObject>(backup.PreviousCharacter ?? "");draft.PublishedFilesJson=backup.PreviousFiles;draft.LastPublishBackup=backup.PreviousBackup;
                EditorUtility.SetDirty(draft);throw;
            }
        }
        public static void Rollback(StudioDraftAsset draft)
        {
            EditingOnly();if(string.IsNullOrEmpty(draft?.LastPublishBackup) || !File.Exists(draft.LastPublishBackup))throw new InvalidOperationException("No retained local publish backup is available.");
            var backup=JsonConvert.DeserializeObject<StudioPublishBackup>(File.ReadAllText(draft.LastPublishBackup));
            CheckFiles(JsonConvert.SerializeObject(backup.After),backup.Paths); // Never overwrite edits made after publishing.
            Restore(backup);FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
            draft.PublishedCharacter=AssetDatabase.LoadAssetAtPath<CharacterObject>(backup.PreviousCharacter ?? "");draft.PublishedFilesJson=backup.PreviousFiles;draft.LastPublishBackup=backup.PreviousBackup;
            EditorUtility.SetDirty(draft);if(EditorUtility.IsPersistent(draft))AssetDatabase.SaveAssetIfDirty(draft);
        }
        public static void ValidateNativeRoundtrip(CharacterObject source,CharacterDefinition definition,string hash)
        {
            var character=ScriptableObject.CreateInstance<CharacterObject>();var a=ScriptableObject.CreateInstance<SkillCardSO>();var b=ScriptableObject.CreateInstance<SkillCardSO>();
            var u=ScriptableObject.CreateInstance<UltimateCardSO>();var p=ScriptableObject.CreateInstance<PassiveDefinitionSO>();
            try
            {
                Apply(character,a,b,u,p,definition,source,hash);
                foreach(var obj in new Object[]{a,b,u,p,character})EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(obj),obj);
                // Transient objects have no asset GUIDs. Their actual persisted links are
                // verified after writing/reimport; reattach only these preview references.
                character.Skill1=a;character.Skill2=b;character.Ultimate=u;
                var links=new SerializedObject(character);links.FindProperty("passiveDefinition").objectReferenceValue=p;links.ApplyModifiedPropertiesWithoutUndo();
                var roundtrip=CharacterObjectCatalogBuilder.CreateDefinition(character);
                if(GameplayHash(roundtrip)!=GameplayHash(definition))throw new InvalidOperationException("The native SO adapter cannot preserve this definition. Publishing was blocked.");
            }
            finally { foreach(var obj in new Object[]{character,a,b,u,p})Object.DestroyImmediate(obj); }
        }
        private static void Apply(CharacterObject character,SkillCardSO a,SkillCardSO b,UltimateCardSO ultimate,PassiveDefinitionSO passive,CharacterDefinition d,CharacterObject source,string hash)
        {
            if(source!=null && !EditorUtility.IsPersistent(character))EditorUtility.CopySerialized(source,character);
            var data=new SerializedObject(character);
            data.FindProperty("definitionId").stringValue=d.Id;data.FindProperty("runtimeReady").boolValue=true;
            data.FindProperty("id").intValue=d.CategoryId;data.FindProperty("fighterName").stringValue=d.DisplayName;
            data.FindProperty("familyId").stringValue=d.FamilyId ?? "";data.FindProperty("seriesId").stringValue=d.SeriesId ?? "";data.FindProperty("role").stringValue=d.Role ?? "";
            var traits=data.FindProperty("traitIds");traits.arraySize=d.TraitIds.Count;for(var i=0;i<traits.arraySize;i++)traits.GetArrayElementAtIndex(i).stringValue=d.TraitIds[i];
            data.FindProperty("passiveDefinition").objectReferenceValue=passive;
            data.FindProperty("passiveSourceDescription").stringValue=d.PassiveSource?.Description ?? "";
            data.FindProperty("passiveSourceType").intValue=(int)CharacterPassiveMetadata.ParseSourceType(d.PassiveSource?.SourceType);
            data.FindProperty("passiveRestriction").intValue=(int)CharacterPassiveMetadata.ParseRestriction(d.PassiveSource?.Restriction);
            if(source!=null)
                foreach(var name in new[]{"fighterPic","fighterIcon","fighterFull","fighterPassive","fighterCard1","fighterCard2","fighterUltimate","fighter3DPrefab","fighter3DMesh","fighter3DMaterial"})
                    data.FindProperty(name).objectReferenceValue=new SerializedObject(source).FindProperty(name).objectReferenceValue;
            if(source!=null)data.FindProperty("fighterModelYawOffset").floatValue=source.FighterModelYawOffset;
            data.ApplyModifiedPropertiesWithoutUndo();
            character.Skill1=a;character.Skill2=b;character.Ultimate=ultimate;
            character.attack=d.BaseStats.Attack;character.defense=d.BaseStats.Defense;character.health=d.BaseStats.MaxHealth;character.Classpower=d.BaseStats.CombatClass;
            character.LevelGrowth=d.StatGrowth?.Clone();character.FighterAttribute=Parse<FighterAttribute>(d.AttributeId,"attribute.");character.FighterRarity=Parse<FighterRarity>(d.RarityId,"rarity.");
            var s=d.BaseStats;character.SecondaryStats=new CharacterSecondaryStats { PierceRate=s.PierceBp/100f,Resistance=s.ResistanceBp/100f,Regenerate=s.RegenerationBp/100f,
                CritChance=s.CritChanceBp/100f,CritDmg=s.CritDamageBp/100f,CritResistance=s.CritResistanceBp/100f,CritDefense=s.CritDefenseBp/100f,
                RecoveryRate=s.RecoveryBp/100f,BlockChance=s.BlockChanceBp/100f,BlockPower=s.BlockPowerBp/100f,LifeSteal=s.LifeStealBp/100f,
                AvoidanceRate=s.AvoidanceBp/100f,EvadeRate=s.EvadeBp/100f,ControlRate=s.ControlBp/100f,PerceptionRate=s.PerceptionBp/100f };
            character.SetStudioMetadata(d,hash);a.cardName=d.DisplayName+" · Skill 1";b.cardName=d.DisplayName+" · Skill 2";ultimate.ultimateName=d.DisplayName+" · Ultimate";
            a.cardIcon=source?.Skill1?.cardIcon;b.cardIcon=source?.Skill2?.cardIcon;ultimate.icon=source?.Ultimate?.icon;
            a.statusVisuals=source?.Skill1?.statusVisuals?.ToList() ?? new List<FightingAllstar.Presentation.Combat.StatusVisualData>();
            b.statusVisuals=source?.Skill2?.statusVisuals?.ToList() ?? new List<FightingAllstar.Presentation.Combat.StatusVisualData>();
            ultimate.statusVisuals=source?.Ultimate?.statusVisuals?.ToList() ?? new List<FightingAllstar.Presentation.Combat.StatusVisualData>();
            passive.statusVisuals=source?.PassiveDefinition?.statusVisuals?.ToList() ?? new List<FightingAllstar.Presentation.Combat.StatusVisualData>();
            a.SetStudioDefinition(d.Skills.Single(x=>x.Slot==1));b.SetStudioDefinition(d.Skills.Single(x=>x.Slot==2));
            if(d.HasUltimateCardSettings)ultimate.SetStudioCardSettings(d.UltimateCategory,d.UltimateTargetScope,d.UltimateDamageAnimation);
            else ultimate.ClearStudioCardSettings();
            ultimate.SetStudioDefinition(d.UltimateTiers);passive.SetStudioDefinition(d.Passive);
        }
        private static T Parse<T>(string value,string prefix) where T:struct
        { if(value==null || !value.StartsWith(prefix) || !Enum.TryParse(value.Substring(prefix.Length),true,out T parsed))throw new InvalidOperationException("Unsupported native "+prefix+" value: "+value);return parsed; }
        private static CharacterDefinition Reidentify(CharacterDefinition d,string id)
        {
            var old=d.Id;var token=JToken.Parse(SnapshotJson.Serialize(d));
            foreach(var value in ((JContainer)token).Descendants().OfType<JValue>().Where(v=>v.Type==JTokenType.String))
            { var text=(string)value.Value;if(text==old || text.StartsWith(old+".",StringComparison.Ordinal))value.Value=id+text.Substring(old.Length); }
            return SnapshotJson.Deserialize<CharacterDefinition>(token.ToString(Formatting.None));
        }
        private static void ApplyWording(StudioDocument doc,DescriptionLibrary library,CharacterDefinition character,StudioPublishWording destination)
        {
            if(destination==StudioPublishWording.Preserve)return;
            string Render(int rank,int tier)
            { var text=DescriptionCompiler.Compile(doc.Description,library,DescriptionAuthoring.Context(doc,character,doc.Description.DefaultLocale,1,rank,tier,""));if(!text.Success)throw new InvalidOperationException("Repair the selected wording configuration before publishing.");return text.TmpRichText; }
            if(destination==StudioPublishWording.Passive)
            { if(character.PassiveSource==null)character.PassiveSource=new PassiveSourceDefinition();character.PassiveSource.Description=Render(1,0); character.PassiveSource.Wording=new AuthoredDescription { Text=character.PassiveSource.Description }; }
            else if(destination==StudioPublishWording.Ultimate)foreach(var tier in character.UltimateTiers){tier.Description=Render(1,tier.Tier);tier.SourceDescription=tier.Description;tier.Wording=new AuthoredDescription {Text=tier.Description};}
            else foreach(var rank in character.Skills.Single(s=>s.Slot==(destination==StudioPublishWording.Skill1?1:2)).Ranks){rank.Description=Render(rank.Rank,0);rank.SourceDescription=rank.Description;rank.Wording=new AuthoredDescription {Text=rank.Description};}
        }
        private static void ValidateCandidate(CharacterDefinition definition,CharacterObject replaced)
        {
            var catalog=CharacterObjectCatalogBuilder.Load(null);catalog.Characters.RemoveAll(c=>replaced!=null && c.Id==replaced.DefinitionId);catalog.Characters.Add(definition);
            var errors=ContentValidator.Validate(catalog);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
        }
        private static string SourceHash(CharacterObject source)=>source==null ? "" : DefinitionHash(CharacterObjectCatalogBuilder.CreateDefinition(source));
        public static string SourceFingerprint(CharacterObject source)
        {
            if(source==null)return "";
            var text=SourceHash(source)+string.Join("\n",new Object[]{source,source.Skill1,source.Skill2,source.Ultimate,source.PassiveDefinition}.Select(o=>o==null ? "null":EditorJsonUtility.ToJson(o)));
            return "source-v1:"+StudioDraftRepository.Revision(text,"");
        }
        private static string LegacyCompatibleFingerprint(CharacterObject source)
        {
            string WithoutAddedFields(string json)
            {
                bool EnumIsDefault(JToken value, int numeric, string name) => value.Type == JTokenType.Integer
                    ? (int?)value == numeric
                    : string.Equals((string)value, name, StringComparison.Ordinal);
                bool DamageAnimationIsDefault(JToken value)
                {
                    if (value.Type == JTokenType.Null) return true;
                    if (!(value is JObject animation)) return false;
                    foreach (var field in animation.Properties())
                    {
                        if (field.Name == "HitCount" && (int?)field.Value == 1) continue;
                        if (field.Name == "Range" && EnumIsDefault(field.Value, (int)AttackRange.Close, AttackRange.Close.ToString())) continue;
                        if (field.Name == "Reaction" && EnumIsDefault(field.Value, (int)HitReaction.Hit, HitReaction.Hit.ToString())) continue;
                        if (field.Name == "ReactionTiming" && EnumIsDefault(field.Value, (int)HitReactionTiming.LastHit, HitReactionTiming.LastHit.ToString())) continue;
                        return false;
                    }
                    return true;
                }
                var token = JToken.Parse(json);
                foreach (var property in ((JContainer)token).DescendantsAndSelf().OfType<JObject>().SelectMany(o => o.Properties()).ToArray())
                {
                    // Core schema additions can appear after a domain reload while the
                    // editor session still holds a pre-upgrade source fingerprint.
                    // Derived root settings on legacy definitions are safe to omit here;
                    // authored Ultimate root settings are accepted only at their defaults.
                    if (property.Name == "DamageAnimation" || property.Name == "UltimateDamageAnimation" ||
                        property.Name == "HasUltimateCardSettings" || property.Name == "UltimateCategory" ||
                        property.Name == "UltimateTargetScope")
                    {
                        property.Remove();
                        continue;
                    }
                    if (property.Name == "hasStudioCardSettings")
                    {
                        if ((bool?)property.Value == true) return null;
                        property.Remove();
                        continue;
                    }
                    if (property.Name == "studioCategory")
                    {
                        var isDefault = EnumIsDefault(property.Value, (int)CardCategory.Attack, CardCategory.Attack.ToString());
                        if (!isDefault) return null;
                        property.Remove();
                        continue;
                    }
                    if (property.Name == "studioTargetScope")
                    {
                        var isDefault = EnumIsDefault(property.Value, (int)EffectTargetScope.SelectedEnemy, EffectTargetScope.SelectedEnemy.ToString());
                        if (!isDefault) return null;
                        property.Remove();
                        continue;
                    }
                    if (property.Name == "studioDamageAnimation")
                    {
                        if (!DamageAnimationIsDefault(property.Value)) return null;
                        property.Remove();
                        continue;
                    }
                    if (property.Name != "Wording" && property.Name != "references") continue;
                    if (property.Name == "Wording" && property.Value.Type != JTokenType.Null && (int?)property.Value["rid"] != -2) return null;
                    if (property.Name == "references" && property.Value["RefIds"] is JArray refs && refs.Any(r => (long?)r["rid"] >= 0)) return null;
                    property.Remove();
                }
                return token.ToString(Formatting.None);
            }
            var definition = WithoutAddedFields(SnapshotJson.Serialize(CharacterObjectCatalogBuilder.CreateDefinition(source)));
            if (definition == null) return null;
            var native = new Object[] { source, source.Skill1, source.Skill2, source.Ultimate, source.PassiveDefinition }
                .Select(o => o == null ? "null" : WithoutAddedFields(EditorJsonUtility.ToJson(o))).ToArray();
            if (native.Any(s => s == null)) return null;
            var text = StudioDraftRepository.Revision(definition, "") + string.Join("\n", native);
            return "source-v1:" + StudioDraftRepository.Revision(text, "");
        }
        private static string DefinitionHash(CharacterDefinition value)=>StudioDraftRepository.Revision(SnapshotJson.Serialize(value),"");
        private static string GameplayHash(CharacterDefinition value)=>StudioHash.Compute("native-bridge-v1",StudioCompiler.GameplayProjection(value));
        private static string FileHash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        private static void CheckFiles(string json,string[] expected)
        {
            var files=JsonConvert.DeserializeObject<Dictionary<string,string>>(json ?? "null");
            if(files==null || files.Count!=5)throw new InvalidOperationException("The five-asset publish receipt is missing or incomplete.");
            foreach(var path in expected)
            {
                if(!files.ContainsKey(path) || !File.Exists(path) || FileHash(path)!=files[path])throw new InvalidOperationException("Published source changed on disk: "+path+". Reconcile it before updating/reverting.");
                if(EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(path)))throw new InvalidOperationException("Unsaved source edits exist: "+path+". Reconcile them before updating/reverting.");
            }
        }
        private static void Restore(StudioPublishBackup backup)
        {
            if(backup?.Paths==null || backup.Paths.Length!=5 || backup.Paths.Any(path=>path.Contains("..") || !IsAllowedRuntimeFolder(Path.GetDirectoryName(path).Replace('\\','/'))))
                throw new InvalidOperationException("Unsafe backup target.");
            if(!backup.Created)
                foreach(var path in backup.Paths)
                {
                    var loaded=AssetDatabase.LoadMainAssetAtPath(path);
                    if(loaded!=null) {EditorUtility.ClearDirty(loaded);Resources.UnloadAsset(loaded);}
                }
            foreach(var path in backup.Paths)
            {
                if(backup.Created) { if(AssetDatabase.LoadMainAssetAtPath(path)!=null)AssetDatabase.DeleteAsset(path); }
                else { File.WriteAllBytes(path,Convert.FromBase64String(backup.Before[path]));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate); }
            }
            if(backup.Created)
            {
                var folder=Path.GetDirectoryName(backup.Paths[0]).Replace('\\','/');
                if(AssetDatabase.IsValidFolder(folder) && Directory.GetFileSystemEntries(folder).Length==0)AssetDatabase.DeleteAsset(folder);
            }
        }
        private static void WriteManifest(string path,StudioPublishBackup backup)=>File.WriteAllText(path,JsonConvert.SerializeObject(backup,Formatting.Indented));
        private static bool IsAllowedRuntimeFolder(string folder)
        {
            if(string.IsNullOrWhiteSpace(folder))return false;
            folder=folder.Replace('\\','/');
            if(folder.StartsWith(RuntimeRoot+"/",StringComparison.Ordinal))return true;
            const string preReleaseRoot="Assets/Project/Data/Character/Pre-Release_Phase/";
            if(!folder.StartsWith(preReleaseRoot,StringComparison.Ordinal))return false;
            var series=folder.Substring(preReleaseRoot.Length);
            return series=="PR_KOF" || series=="PR_Tekken" || series=="PR_Genshin-Impact" || series=="PR_Soul-Calibur";
        }
        private static T LoadOrCreate<T>(string path)where T:ScriptableObject
        { var obj=AssetDatabase.LoadAssetAtPath<T>(path);if(obj!=null)return obj;obj=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(obj,path);return obj; }
        private static void EnsureFolder(string path)
        { if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
        private static void EditingOnly(){if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new InvalidOperationException("Publish from stable Edit Mode, with Battle Lab stopped.");}
    }
}
#endif
