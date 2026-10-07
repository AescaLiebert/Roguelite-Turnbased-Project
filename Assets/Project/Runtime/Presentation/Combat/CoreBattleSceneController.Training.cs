using FightingAllstar.Presentation.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class CoreBattleSceneController
    {
        private void BindTrainingControls()
        {
            if (battleDocument == null) return;
            var root = battleDocument.rootVisualElement;
            var controls = new VisualElement { name = "training-controls" };
            controls.style.position = Position.Absolute;
            controls.style.top = 12;
            controls.style.left = 12;
            controls.style.backgroundColor = new Color(0.04f, 0.07f, 0.13f, .95f);
            controls.style.paddingLeft = controls.style.paddingRight = 12;
            controls.style.paddingTop = controls.style.paddingBottom = 8;
            controls.style.color = Color.white;
            controls.Add(new Label("TRAINING · 1v1 · Both fighters: Infinite HP · Dummy: Kyo98 / No PG"));
            controls.Add(new Label("All ranks · Free ultimate · One action per side"));
            var buttons = new VisualElement(); buttons.style.flexDirection = FlexDirection.Row;
            buttons.Add(new Button(() => SceneManager.LoadScene("Training")) { text = "RESTART ROUND" });
            buttons.Add(new Button(ExitTraining) { text = "EXIT TRAINING" });
            controls.Add(buttons);
            root.Add(controls);
            // Keep practice navigation usable over intros, playback, inspectors and the KO panel.
            root.schedule.Execute(() => controls.BringToFront()).Every(100);
            var surrender = root.Q<Button>("surrender-button");
            if (surrender != null) surrender.text = "Exit training";
            var result = root.Q<Button>("result-continue");
            if (result != null) result.text = "Return to Main Menu";
            if (!TrainingContext.IsReady) controls.Add(new Label("Choose a fighter from Main Menu → Training Mode."));
        }

        private void ExitTraining()
        {
            TrainingContext.Clear();
            SceneManager.LoadScene("MainMenu");
        }
    }
}
