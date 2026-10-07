using UnityEngine;
using UnityEngine.UIElements;
using UiToolkitImage = UnityEngine.UIElements.Image;
using UguiImage = UnityEngine.UI.Image;

namespace FightingAllstar.Presentation
{
    /// <summary>Creates the shared UXML character icon and binds it to a CharacterObject.</summary>
    public static class CharacterIconView
    {
        private static VisualTreeAsset _template;

        public static VisualElement Create(CharacterObject character, float size, string badge = null)
        {
            return CreateInternal(character, size, false, badge);
        }

        public static VisualElement CreateFilling(CharacterObject character, string badge = null)
        {
            return CreateInternal(character, 0, true, badge);
        }

        private static VisualElement CreateInternal(CharacterObject character, float size, bool fill, string badge)
        {
            if (_template == null) _template = Resources.Load<VisualTreeAsset>("UI/CharacterIcon");
            if (_template == null)
            {
                Debug.LogError("CharacterIcon UXML could not be loaded from Resources/UI/CharacterIcon.uxml.");
                return new VisualElement();
            }

            var root = _template.CloneTree();
            root.name = "character-icon";
            if (fill)
            {
                root.style.width = Length.Percent(100);
                root.style.height = Length.Percent(100);
                root.style.flexGrow = 1;
            }
            else
            {
                root.style.width = size;
                root.style.height = size;
            }
            root.style.flexShrink = 0;
            Bind(root, character, badge);
            return root;
        }

        public static void Bind(VisualElement root, CharacterObject character, string badge = null)
        {
            if (root == null) return;
            var art = CharacterIconArtSO.Load();
            var rarity = character == null || art == null ? null : art.Rarity(character.FighterRarity);
            if (rarity == null && art != null) rarity = art.Rarity(FighterRarity.SSR);

            SetImage(root.Q<UiToolkitImage>("rarity-background"), rarity?.background, ScaleMode.StretchToFill);
            SetImage(root.Q<UiToolkitImage>("char-icon"), character == null ? null :
                (character.FighterIcon != null ? character.FighterIcon : character.FighterPic));
            SetImage(root.Q<UiToolkitImage>("rarity-border"), rarity?.frame);
            SetImage(root.Q<UiToolkitImage>("attribute"), character == null || art == null ? null : art.Attribute(character.FighterAttribute));
            SetImage(root.Q<UiToolkitImage>("rarity-label"), rarity?.label);
            SetImage(root.Q<UiToolkitImage>("series-label"), character == null || art == null ? null : art.Series(character.SeriesId));

            var badgeLabel = root.Q<Label>("slot-label");
            if (badgeLabel != null)
            {
                badgeLabel.text = badge ?? string.Empty;
                badgeLabel.style.display = string.IsNullOrEmpty(badge) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public static CharacterObject FindCharacter(string definitionId)
        {
            foreach (var character in CharacterObjectRegistrySO.LoadAll())
                if (character != null && character.DefinitionId == definitionId) return character;
            return null;
        }

        public static RectTransform CreateUGUI(Transform parent, string name, CharacterObject character)
        {
            var rootObject = new GameObject(name, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            var root = (RectTransform)rootObject.transform;
            var art = CharacterIconArtSO.Load();
            var rarity = character == null || art == null ? null : art.Rarity(character.FighterRarity);
            if (rarity == null && art != null) rarity = art.Rarity(FighterRarity.SSR);
            AddLayer(root, "Rarity background", rarity?.background, false);
            AddLayer(root, "Character portrait", character == null ? null :
                (character.FighterIcon != null ? character.FighterIcon : character.FighterPic), true);
            AddLayer(root, "Rarity frame", rarity?.frame, true);
            AddLayer(root, "Attribute", character == null || art == null ? null : art.Attribute(character.FighterAttribute), true, .68f);
            AddLayer(root, "Rarity label", rarity?.label, true, .55f);
            AddLayer(root, "Series label", character == null || art == null ? null : art.Series(character.SeriesId), true, .5f);
            return root;
        }

        private static void AddLayer(RectTransform parent, string name, Sprite sprite, bool preserveAspect, float inset = 0f)
        {
            if (sprite == null) return;
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(UguiImage));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            if (name == "Character portrait")
            {
                rect.anchorMin = new Vector2(.025f, .025f); rect.anchorMax = new Vector2(.975f, .975f);
            }
            else if (name == "Attribute")
            {
                rect.anchorMin = new Vector2(.70f, .70f); rect.anchorMax = new Vector2(.98f, .98f);
            }
            else if (name == "Rarity label")
            {
                rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(.44f, .44f);
            }
            else if (name == "Series label")
            {
                rect.anchorMin = new Vector2(.43f, 0f); rect.anchorMax = new Vector2(1f, .28f);
            }
            else
            {
                rect.anchorMin = new Vector2(inset * .5f, inset * .5f);
                rect.anchorMax = new Vector2(1f - inset * .5f, 1f - inset * .5f);
            }
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = gameObject.GetComponent<UguiImage>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
        }

        private static void SetImage(UiToolkitImage target, Sprite sprite, ScaleMode scaleMode = ScaleMode.ScaleToFit)
        {
            if (target == null) return;
            target.sprite = sprite;
            target.scaleMode = scaleMode;
            target.style.display = sprite == null ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
