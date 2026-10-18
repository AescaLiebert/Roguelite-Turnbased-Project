using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    [TestFixture]
    public sealed class ActorHealVisualTests
    {
        private GameObject _actor;
        private GameObject _stageObject;
        private BattleStagePresenter _stage;

        [SetUp]
        public void SetUp()
        {
            _actor = new GameObject("TestActor");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model";
            body.transform.SetParent(_actor.transform, false);
            body.transform.localPosition = Vector3.up;

            _stageObject = new GameObject("TestStage");
            _stage = _stageObject.AddComponent<BattleStagePresenter>();
            _stage.Initialize(new Dictionary<string, GameObject>
            {
                ["fighter1"] = _actor
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_actor != null) Object.DestroyImmediate(_actor);
            if (_stageObject != null) Object.DestroyImmediate(_stageObject);
        }

        [Test]
        public void GetOrCreate_InitializesAllFiveEmittersWithCorrectShadersAndSettings()
        {
            var visual = ActorHealVisual.GetOrCreate(_actor.transform, 2.2f, 0.7f, 0f);

            Assert.IsNotNull(visual, "ActorHealVisual should be created.");
            Assert.AreEqual(2.2f, visual.Height, 0.01f);
            Assert.AreEqual(0.7f, visual.Radius, 0.01f);

            var systems = visual.GetComponentsInChildren<ParticleSystem>();
            Assert.AreEqual(5, systems.Length, "Should have 5 particle emitters (Aura, Cross, Streaks, Sparkles, Ground Ring).");

            var aura = visual.transform.Find("Heal Aura Wisps");
            var cross = visual.transform.Find("Heal Cross Symbols");
            var needle = visual.transform.Find("Heal Ascending Streaks");
            var sparkle = visual.transform.Find("Heal Sparkles");
            var ring = visual.transform.Find("Heal Ground Ring");

            Assert.IsNotNull(aura, "Aura wisps emitter must exist.");
            Assert.IsNotNull(cross, "Cross symbols emitter must exist.");
            Assert.IsNotNull(needle, "Needle streaks emitter must exist.");
            Assert.IsNotNull(sparkle, "Sparkles emitter must exist.");
            Assert.IsNotNull(ring, "Ground ring emitter must exist.");

            foreach (var ps in systems)
            {
                Assert.IsTrue(ps.main.useUnscaledTime, "All heal emitters should use unscaled time for responsive presentation.");
                Assert.IsFalse(ps.main.playOnAwake, "Heal emitters should not play on awake.");
                var rend = ps.GetComponent<ParticleSystemRenderer>();
                Assert.IsNotNull(rend, "Emitter must have ParticleSystemRenderer.");
                Assert.IsNotNull(rend.sharedMaterial, "Emitter must have a material assigned.");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, rend.shadowCastingMode);
                Assert.IsFalse(rend.receiveShadows);
            }
        }

        [Test]
        public void Play_StartsParticleSystems()
        {
            var visual = ActorHealVisual.GetOrCreate(_actor.transform, 2.0f, 0.6f, 0f);
            visual.Play();

            Assert.IsTrue(visual.IsPlaying, "Visual should be in playing state after Play().");

            visual.Stop();
            Assert.IsFalse(visual.IsPlaying, "Visual should not be playing after Stop().");
        }

        [Test]
        public void SetHidden_TogglesVisibilityAndStopsParticles()
        {
            var visual = ActorHealVisual.GetOrCreate(_actor.transform, 2.0f, 0.6f, 0f);
            visual.Play();

            visual.SetHidden(true);
            Assert.IsFalse(visual.gameObject.activeSelf, "Visual object should be inactive when hidden.");
            Assert.IsFalse(visual.IsPlaying, "Visual should stop particles when hidden.");

            visual.SetHidden(false);
            Assert.IsTrue(visual.gameObject.activeSelf, "Visual object should become active when unhidden.");
        }

        [Test]
        public void BattleStagePresenter_PlayHealFeedback_CreatesAndPlaysHealVisualOnTarget()
        {
            _stage.PlayHealFeedback("fighter1");

            var visual = _actor.GetComponentInChildren<ActorHealVisual>();
            Assert.IsNotNull(visual, "BattleStagePresenter.PlayHealFeedback should create ActorHealVisual on fighter.");
            Assert.IsTrue(visual.IsPlaying, "ActorHealVisual should be playing.");
        }

        [Test]
        public void ActorVisualEffect_PlayHeal_TriggersActorHealVisual()
        {
            var ave = _actor.AddComponent<ActorVisualEffect>();
            ave.PlayHeal();

            var visual = _actor.GetComponentInChildren<ActorHealVisual>();
            Assert.IsNotNull(visual, "ActorVisualEffect.PlayHeal should create and trigger ActorHealVisual.");
            Assert.IsTrue(visual.IsPlaying, "ActorHealVisual should be playing.");
        }

        [Test]
        public void MultipleHeals_ReusesSameVisualComponentWithoutDuplicateGameObjects()
        {
            _stage.PlayHealFeedback("fighter1");
            _stage.PlayHealFeedback("fighter1");
            _stage.PlayHealFeedback("fighter1");

            var visuals = _actor.GetComponentsInChildren<ActorHealVisual>();
            Assert.AreEqual(1, visuals.Length, "Rapid successive heals must reuse the single ActorHealVisual component.");
        }
    }
}
