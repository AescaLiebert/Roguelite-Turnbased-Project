#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>A saved M2 scenario contains the actual opening snapshot and exact submitted command, not a reconstructed approximation.</summary>
    public sealed class StudioScenarioAsset : ScriptableObject
    {
        public int SchemaVersion = 1;
        public string FixtureId = "studio.m2.fixed-fixture";
        public string Seed;
        public StudioDraftAsset SourceDraft;
        public string GameplayHash;
        public string TextHash;
        public string ExpectedStateHash;
        [HideInInspector] public string InitialStateJson;
        [HideInInspector] public string InputPlanJson;
        [HideInInspector] public string SourceDocumentJson;

        public static StudioScenarioAsset Save(StudioDraftAsset draft, StudioRunSnapshot run)
        {
            if (draft == null || run == null) throw new ArgumentNullException("A draft and completed run are required.");
            var initial = run.OpeningBattle;
            var plan = run.InputPlan;
            var scenario = CreateInstance<StudioScenarioAsset>();
            scenario.SourceDraft = draft; scenario.Seed = run.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            scenario.GameplayHash = run.Hashes.Gameplay; scenario.TextHash = run.Hashes.Text; scenario.ExpectedStateHash = run.StateHash;
            scenario.InitialStateJson = SnapshotJson.Serialize(initial); scenario.InputPlanJson = SnapshotJson.Serialize(plan);
            scenario.SourceDocumentJson = SnapshotJson.Serialize(run.Source);
            var path = AssetDatabase.GenerateUniqueAssetPath(StudioDraftRepository.Root + "/Drafts/" + draft.name + "Scenario.asset");
            AssetDatabase.CreateAsset(scenario, path); AssetDatabase.SaveAssetIfDirty(scenario);
            return scenario;
        }

        public bool Replay(out BattleState result, out string message)
        {
            result = null;
            try
            {
                if (SchemaVersion != 1) throw new InvalidOperationException("Unsupported scenario version; source preserved.");
                var initial = SnapshotJson.Deserialize<BattleState>(InitialStateJson);
                var plan = SnapshotJson.Deserialize<TurnPlan>(InputPlanJson);
                if (!BattleEngine.TryResolvePlan(initial, plan, out result, out var error)) throw new InvalidOperationException(error);
                var hash = StudioSimulation.StateHash(result);
                message = hash == ExpectedStateHash ? "PASS: saved state and exact command reproduce the frozen engine result."
                    : "Replay differs from the saved expected state. Compare engine/content versions before accepting it.";
                return hash == ExpectedStateHash;
            }
            catch (Exception exception) { message = exception.Message; return false; }
        }
    }
}
#endif
