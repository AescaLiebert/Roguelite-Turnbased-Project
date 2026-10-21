#if UNITY_EDITOR
using System.Collections.Generic;
namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed class StudioDescriptionSurface
    {
        public string Name, Consumer;
        public int Width, Size;
        public StudioTextFontProfile Font;
    }
    public static class StudioDescriptionSurfaces
    {
        // Match renderer/font/typography contracts without loading or editing a live scene.
        public static readonly IReadOnlyList<StudioDescriptionSurface> All = new[] {
            new StudioDescriptionSurface { Name="Studio", Consumer="StudioPreviewText: actual Label and isolated TMP", Width=640, Size=18 },
            new StudioDescriptionSurface { Name="Character detail", Consumer="CharacterDetailPopup: Label hub-tooltip-description", Width=640, Size=23 },
            new StudioDescriptionSurface { Name="Battle card", Consumer="BattleCharacterInspector: TMP description", Width=500, Size=17, Font=StudioTextFontProfile.BattleInspector },
            new StudioDescriptionSurface { Name="Passive inspector", Consumer="BattleCharacterInspector: TMP passive", Width=640, Size=17, Font=StudioTextFontProfile.BattleInspector },
            new StudioDescriptionSurface { Name="Status tooltip", Consumer="BattleCharacterInspector: TMP status", Width=420, Size=16, Font=StudioTextFontProfile.BattleInspector }
        };
    }
}
#endif
