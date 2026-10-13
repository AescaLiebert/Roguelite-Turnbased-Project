using UnityEngine.UIElements;

namespace FightingAllstar.Presentation
{
    public static class ScreenMotion
    {
        public static void Show(VisualElement panel)
        {
            if (panel == null) return;
            panel.AddToClassList("animated-panel"); panel.AddToClassList("panel-enter");
            panel.style.display = DisplayStyle.Flex;
            panel.schedule.Execute(() => panel.RemoveFromClassList("panel-enter")).ExecuteLater(20);
        }
        public static void Hide(VisualElement panel)
        {
            if (panel == null) return;
            panel.style.display = DisplayStyle.None; panel.RemoveFromClassList("panel-enter");
        }
    }
}
