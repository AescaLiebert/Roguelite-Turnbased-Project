using System;
using System.Collections.Generic;
using System.IO;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed class StudioPreviewChecks
    {
        [Test]
        public void ActorReplayCopiesCommittedFactsAndMissingClipsAreDeterministic()
        {
            var active = SceneManager.GetActiveScene();
            var events = new List<BattleEvent>
            {
                new BattleEvent { Id = 1, Kind = BattleEventKind.CardPlayed, SourceId = "actor" },
                new BattleEvent { Id = 2, Kind = BattleEventKind.DamageApplied, SourceId = "actor", TargetId = "target",
                    Amount = 250, HealthAfter = 750 },
                new BattleEvent { Id = 3, Kind = BattleEventKind.HealApplied, SourceId = "actor", TargetId = "target",
                    Amount = 125, HealthAfter = 875 }
            };
            var preview = new StudioPreviewActor();
            var fixtureScene = default(Scene);
            try
            {
                preview.SetScenario(null, events, "actor");
                fixtureScene = preview.Actor.scene;
                Assert.AreNotEqual(active, fixtureScene, "Actor lives in a disposable preview scene.");
                events[1].HealthAfter = 999; // A later engine/input edit cannot alter frozen playback.
                preview.Sample(1.05f);
                var first = preview.Target.transform.position;
                var firstRotation = preview.Target.transform.rotation;
                Assert.AreEqual(750, preview.HealthByFighter["target"]);
                StringAssert.Contains("deterministic fallback", preview.Status);
                Assert.Greater(Vector3.Distance(first, new Vector3(1.8f, 0, 0)), .05f);
                preview.Sample(0);
                Assert.AreEqual(new Vector3(1.8f, 0, 0), preview.Target.transform.position);
                Assert.IsEmpty(preview.HealthByFighter);
                preview.Sample(1.05f);
                Assert.AreEqual(first, preview.Target.transform.position);
                Assert.Less(Quaternion.Angle(firstRotation, preview.Target.transform.rotation), .001f);
                preview.Sample(preview.Duration);
                Assert.AreEqual(875, preview.HealthByFighter["target"]);
                Assert.AreEqual(new Vector3(1.8f, 0, 0), preview.Target.transform.position);
                Assert.AreEqual(3, preview.CommittedEventCount);
                Assert.AreEqual(999, events[1].HealthAfter);
                Assert.AreEqual(active, SceneManager.GetActiveScene());
                Assert.IsFalse(EditorApplication.isPlaying);
            }
            finally { preview.Dispose(); }
            Assert.IsFalse(preview.HasFixture);
            Assert.IsNull(preview.Actor);
            Assert.IsNull(preview.Target);
            Assert.IsFalse(fixtureScene.IsValid(), "Disposal must close the preview scene, not just clear references.");
            Assert.Throws<ObjectDisposedException>(() => preview.Sample(0));
        }

        [Test]
        public void ActorPlaybackWithoutDamageDoesNotInventAReactionOrHealthChange()
        {
            using (var preview = new StudioPreviewActor())
            {
                preview.SetScenario(null, new[] { new BattleEvent { Kind = BattleEventKind.CardPlayed, SourceId = "actor" } }, "actor");
                preview.Sample(.5f);
                Assert.AreEqual(new Vector3(1.8f, 0, 0), preview.Target.transform.position);
                Assert.IsEmpty(preview.HealthByFighter);
                preview.Sample(preview.Duration);
                Assert.AreEqual(new Vector3(-1.8f, 0, 0), preview.Actor.transform.position);
            }
        }

        [Test]
        public void OpeningGaugeTargetDoesNotReplaceTheCommittedDamageTarget()
        {
            var events = new[]
            {
                new BattleEvent { Id = 1, Kind = BattleEventKind.PowerGaugeChanged, SourceId = "support", TargetId = "ally1",
                    PowerGaugeAfter = 2 },
                new BattleEvent { Id = 2, Kind = BattleEventKind.CardPlayed, SourceId = "actor" },
                new BattleEvent { Id = 3, Kind = BattleEventKind.DamageApplied, SourceId = "actor", TargetId = "enemy",
                    Amount = 250, HealthAfter = 750, HasHitReaction = true, Reaction = HitReaction.Hit }
            };
            using (var preview = new StudioPreviewActor())
            {
                preview.SetScenario(null, events, "actor");
                Assert.AreEqual("enemy", preview.TargetId, "An opening utility event must not steal the visible damage target.");
                preview.Sample(1.05f);
                Assert.AreEqual(750, preview.HealthByFighter["enemy"]);
                Assert.IsFalse(preview.HealthByFighter.ContainsKey("ally1"), "Gauge feedback must not invent an ally damage/health projection.");
                Assert.Greater(Vector3.Distance(preview.Target.transform.position, new Vector3(1.8f, 0, 0)), .05f,
                    "The enemy receiving the committed Hit must visibly react.");
                StringAssert.Contains("actor → enemy", preview.Status);
                StringAssert.Contains("DamageApplied", preview.Status);
            }
        }

        [Test]
        public void ActualTmpParsesApprovedMarkupAndUsesAnOwnedAtlas()
        {
            var original = Resources.Load<TMP_FontAsset>(StudioPreviewText.BattleFontResource);
            Assert.IsNotNull(original);
            var before = EditorJsonUtility.ToJson(original);
            var active = SceneManager.GetActiveScene();
            var preview = new StudioPreviewText(StudioTextFontProfile.BattleInspector);
            var fixtureScene = default(Scene);
            TMP_FontAsset transientFont = null;
            try
            {
                const string source = "<b>Heal</b> <color=#4EE2A3>20%</color> <i>HP</i>";
                preview.SetText(source);
                fixtureScene = preview.TmpText.gameObject.scene;
                transientFont = preview.TmpText.font;
                Assert.AreEqual(source, preview.ToolkitLabel.text);
                Assert.IsTrue(preview.ToolkitLabel.enableRichText);
                Assert.IsInstanceOf<TextMeshProUGUI>(preview.TmpText);
                Assert.AreNotSame(original, preview.TmpText.font);
                Assert.AreEqual(original.sourceFontFile, preview.TmpText.font.sourceFontFile);
                Assert.AreNotSame(original.material, preview.TmpText.font.material);
                StringAssert.DoesNotContain("<", preview.TmpText.GetParsedText());
                StringAssert.Contains("20%", preview.TmpText.GetParsedText());
                var information = preview.TmpText.textInfo;
                Assert.Greater(information.characterCount, 8);
                Assert.IsTrue((information.characterInfo[0].style & FontStyles.Bold) != 0);
                var colored = false;
                for (var i = 0; i < information.characterCount; i++)
                    if (information.characterInfo[i].character == '2')
                    {
                        var color = information.characterInfo[i].color;
                        colored = color.r == 0x4E && color.g == 0xE2 && color.b == 0xA3;
                    }
                Assert.IsTrue(colored, "The actual TMP glyph retains the approved color.");
                Assert.IsEmpty(preview.MissingCodepoints);
                Assert.AreEqual(before, EditorJsonUtility.ToJson(original), "Preview must not populate or edit the persistent game font.");
                Assert.AreEqual(active, SceneManager.GetActiveScene());
            }
            finally { preview.Dispose(); }
            Assert.IsFalse(preview.HasFixture);
            Assert.IsNull(preview.TmpText);
            Assert.IsFalse(fixtureScene.IsValid());
            Assert.IsTrue(transientFont == null, "Disposal must destroy the transient TMP atlas owner.");
            Assert.Throws<ObjectDisposedException>(() => preview.SetText("disposed"));
        }

        [Test]
        public void DefaultEditorFontRendersThaiAndProfileSwitchesPreserveSourceWithoutLeakingAtlases()
        {
            const string source = "<b>ฟื้นฟู</b> HP <color=#4EE2A3>20%</color> เมื่อเริ่มเทิร์น";
            const string plain = "ฟื้นฟู HP 20% เมื่อเริ่มเทิร์น";
            var editorFont = Resources.Load<Font>(StudioPreviewText.EditorFontResource);
            var gameFont = Resources.Load<TMP_FontAsset>(StudioPreviewText.BattleFontResource);
            Assert.IsNotNull(editorFont);
            Assert.IsNotNull(gameFont);
            var editorFontState = EditorJsonUtility.ToJson(editorFont);
            var gameFontState = EditorJsonUtility.ToJson(gameFont);
            var editorFontPath = AssetDatabase.GetAssetPath(editorFont);
            var editorFontHash = AssetDatabase.GetAssetDependencyHash(editorFontPath);
            var active = SceneManager.GetActiveScene();
            var preview = new StudioPreviewText();
            TMP_FontAsset finalTransient = null;
            try
            {
                Assert.AreEqual(StudioTextFontProfile.EditorStandard, preview.FontProfile);
                preview.SetText(source);
                Assert.AreEqual(source, preview.SourceText);
                Assert.AreEqual(source, preview.ToolkitLabel.text);
                Assert.AreEqual(editorFont, preview.ToolkitLabel.style.unityFontDefinition.value.font);
                Assert.AreEqual(editorFont, preview.TmpText.font.sourceFontFile);
                Assert.AreEqual(plain, preview.TmpText.GetParsedText());
                Assert.IsEmpty(preview.MissingCodepoints);
                // Font.HasCharacter may involve OS fallback. Only the actual owned TMP
                // atlas proves these Thai base letters and combining marks were added.
                foreach (var character in plain)
                    if (!char.IsWhiteSpace(character))
                        Assert.IsTrue(preview.TmpText.font.HasCharacter(character, false, false),
                            "The isolated standard editor atlas must contain U+" + ((int)character).ToString("X4"));
                var firstTransient = preview.TmpText.font;
                preview.SetFontProfile(StudioTextFontProfile.BattleInspector);
                Assert.IsTrue(firstTransient == null, "Switching profiles must destroy the previous transient atlas owner.");
                Assert.AreEqual(source, preview.SourceText);
                Assert.AreEqual(source, preview.ToolkitLabel.text);
                Assert.AreEqual(gameFont.sourceFontFile, preview.TmpText.font.sourceFontFile);
                Assert.AreNotSame(gameFont, preview.TmpText.font);
                var secondTransient = preview.TmpText.font;
                preview.SetFontProfile(StudioTextFontProfile.EditorStandard);
                Assert.IsTrue(secondTransient == null);
                Assert.AreEqual(source, preview.SourceText);
                Assert.AreEqual(plain, preview.TmpText.GetParsedText());
                Assert.IsEmpty(preview.MissingCodepoints);
                finalTransient = preview.TmpText.font;
                var evidencePath = Path.GetFullPath("Artifacts/CharacterCreatorStudio/PreviewEvidence/standard-editor-thai-tmp.png");
                preview.CaptureTmp(evidencePath);
                Assert.IsTrue(File.Exists(evidencePath));
                Assert.Greater(new FileInfo(evidencePath).Length, 1024, "Actual TMP output must contain rendered glyph image data.");
                Assert.AreEqual(editorFontState, EditorJsonUtility.ToJson(editorFont));
                Assert.AreEqual(gameFontState, EditorJsonUtility.ToJson(gameFont));
                Assert.AreEqual(editorFontHash, AssetDatabase.GetAssetDependencyHash(editorFontPath));
                Assert.AreEqual(active, SceneManager.GetActiveScene());
            }
            finally { preview.Dispose(); }
            Assert.IsFalse(preview.HasFixture);
            Assert.IsTrue(finalTransient == null);
        }

        /// <summary>Can be called from the Studio's validation service without opening a game scene.</summary>
        public static void CaptureEvidence(string outputDirectory, CharacterObject source, IReadOnlyList<BattleEvent> committedEvents,
            string description, string actorId = null)
        {
            Directory.CreateDirectory(outputDirectory);
            using (var text = new StudioPreviewText())
            {
                text.SetText(description);
                text.CaptureTmp(Path.Combine(outputDirectory, "description-tmp.png"));
                File.WriteAllText(Path.Combine(outputDirectory, "description-renderer.txt"), text.Diagnostics + "\n" + text.TmpText.GetParsedText());
            }
            using (var actor = new StudioPreviewActor())
            {
                actor.SetScenario(source, committedEvents, actorId);
                float? actionTime = null;
                float? reactionTime = null;
                var nextCueTime = .15f;
                if (committedEvents != null)
                    foreach (var item in committedEvents)
                    {
                        if (item == null || (item.Kind != BattleEventKind.CardPlayed && item.Kind != BattleEventKind.DamageApplied &&
                            item.Kind != BattleEventKind.HealApplied && item.Kind != BattleEventKind.FighterDefeated)) continue;
                        if (!actionTime.HasValue && item.Kind == BattleEventKind.CardPlayed && item.SourceId == actor.ActorId)
                            actionTime = nextCueTime + .3f;
                        if (!reactionTime.HasValue && item.Kind == BattleEventKind.DamageApplied &&
                            (!item.HasHitReaction || item.Reaction != HitReaction.None) &&
                            (item.TargetId == actor.ActorId || item.TargetId == actor.TargetId))
                            reactionTime = nextCueTime + .2f;
                        nextCueTime += item.Kind == BattleEventKind.CardPlayed ? .7f : .45f;
                    }
                var statuses = new List<string>();
                actor.Sample(0);
                actor.Capture(Path.Combine(outputDirectory, "actor-rest.png"));
                statuses.Add("Rest: " + actor.Status);
                CaptureEventPhase(actor, actionTime, outputDirectory, "action", statuses);
                CaptureEventPhase(actor, reactionTime, outputDirectory, "reaction", statuses);
                actor.Sample(actor.Duration);
                actor.Capture(Path.Combine(outputDirectory, "actor-final.png"));
                statuses.Add("Final: " + actor.Status);
                File.WriteAllLines(Path.Combine(outputDirectory, "actor-playback.txt"), statuses);
            }
        }

        private static void CaptureEventPhase(StudioPreviewActor actor, float? sampleTime, string outputDirectory, string phase,
            List<string> statuses)
        {
            var path = Path.Combine(outputDirectory, "actor-" + phase + ".png");
            if (!sampleTime.HasValue)
            {
                if (File.Exists(path)) File.Delete(path);
                statuses.Add(phase + ": no eligible committed event; no phase screenshot captured.");
                return;
            }
            actor.Sample(sampleTime.Value);
            actor.Capture(path);
            statuses.Add(phase + " at " + sampleTime.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s: " + actor.Status);
        }

        /// <summary>Capture the actual editor Toolkit panel after a repaint, without private panel reflection.</summary>
        public static void CaptureEditorWindowEvidence(EditorWindow window, string absolutePngPath)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            var width = Mathf.Max(64, Mathf.RoundToInt(window.position.width * EditorGUIUtility.pixelsPerPoint));
            var height = Mathf.Max(64, Mathf.RoundToInt(window.position.height * EditorGUIUtility.pixelsPerPoint));
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                if (!UnityEditorInternal.InternalEditorUtility.CaptureEditorWindow(window, target))
                    throw new InvalidOperationException("The editor window has not painted yet. Capture after its next repaint.");
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(absolutePngPath)));
                File.WriteAllBytes(absolutePngPath, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
