using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using MenuGraphic = FightingAllstar.Presentation.Navigation.ReferenceMenuGraphic;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Live UI laid out in the original mockup's 1672 x 941 coordinate space.</summary>
    internal static class MainMenuView
    {
        private const float Width = 1672, Height = 941;
        private static readonly Color Ink = new Color(.035f, .062f, .094f, .92f);
        private static readonly Color Teal = new Color(.28f, .87f, .79f);
        private static readonly Color Gold = new Color(.9f, .73f, .44f);
        private static readonly Color Blue = new Color(.46f, .68f, .93f);
        private static readonly Vector2[] Rectangle = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        private static readonly Vector2[] Card = { new Vector2(.135f, 0), new Vector2(1, 0), new Vector2(.865f, 1), new Vector2(0, 1) };
        private static readonly Vector2[] CutCorner = { new Vector2(.05f, 0), new Vector2(1, 0), new Vector2(1, .85f), new Vector2(.95f, 1), new Vector2(0, 1), new Vector2(0, .14f) };

        public static void Initialize(VisualElement root)
        {
            var viewport = root.Q("menu-viewport");
            var stage = root.Q("menu-stage");
            if (viewport == null || stage == null || stage.userData != null) return;
            stage.userData = true;
            Action fit = () =>
            {
                var available = viewport.contentRect;
                if (available.width <= 0 || available.height <= 0) return;
                // Convert device safe area from screen pixels to this panel's logical coordinates.
                var safe = Screen.safeArea;
                var safeWidth = Screen.width > 0 ? safe.width * available.width / Screen.width : available.width;
                var safeHeight = Screen.height > 0 ? safe.height * available.height / Screen.height : available.height;
                var offsetX = Screen.width > 0 ? safe.x * available.width / Screen.width : 0;
                var offsetY = Screen.height > 0 ? (Screen.height - safe.yMax) * available.height / Screen.height : 0;
                var scale = Mathf.Min(safeWidth / Width, safeHeight / Height);
                stage.style.scale = new Scale(new Vector3(scale, scale, 1));
                stage.style.left = offsetX + (safeWidth - Width * scale) / 2;
                stage.style.top = offsetY + (safeHeight - Height * scale) / 2;
            };
            viewport.RegisterCallback<GeometryChangedEvent>(_ => fit());
            viewport.schedule.Execute(fit);

            var atlas = Resources.Load<Texture2D>("UI/MainMenu/ReferenceAtlas");
            Surface(root, "header-surface", new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(.95f, 1), new Vector2(0, 1) }, new Color(.025f, .045f, .07f, .87f), new Color(.3f, .39f, .47f, .5f));
            Art(root, "brand-art", atlas, new Rect(40, 14, 300, 46));
            Art(root, "diamond-art", atlas, new Rect(1245, 21, 40, 33));
            Art(root, "avatar-art", atlas, new Rect(1448, 4, 69, 70));
            Surface(root, "route-surface", Rectangle, Ink, new Color(.4f, .59f, .69f, .8f));
            Art(root, "route-art", atlas, new Rect(61, 398, 356, 173), new[] { new Vector2(0, 0), new Vector2(.83f, 0), new Vector2(1, 1), new Vector2(0, 1) });
            Art(root, "team-icon", atlas, new Rect(456, 459, 31, 27));
            Art(root, "path-icon", atlas, new Rect(456, 498, 31, 28));
            Art(root, "reward-icon", atlas, new Rect(456, 538, 31, 27));
            Surface(root, "battle-surface", Rectangle, new Color(.78f, .065f, .095f, .96f), new Color(1, .31f, .28f));
            Art(root, "battle-emblem", atlas, new Rect(51, 599, 190, 81));
            Surface(root, "caption-surface", Card, Ink, new Color(.42f, .62f, .72f));
            Surface(root, "summon-surface", CutCorner, new Color(.07f, .073f, .07f, .94f), Gold);
            Surface(root, "training-surface", Rectangle, new Color(.045f, .08f, .135f, .94f), Blue);
            Art(root, "summon-icon", atlas, new Rect(1036, 782, 88, 70));
            Art(root, "training-icon", atlas, new Rect(1365, 787, 84, 58));
            AddAccents(root.Q("battle-surface"), new Color(1, .32f, .28f, .48f));
            AddAccents(root.Q("summon-surface"), new Color(.9f, .73f, .44f, .25f));
            AddAccents(root.Q("training-surface"), new Color(.46f, .68f, .93f, .3f));
            var routeCorner = new MenuGraphic(null, default, new[] { new Vector2(.95f, 0), new Vector2(1, 0), new Vector2(1, .17f) }, new Color(.95f, .17f, .18f), Color.clear);
            root.Q("route-surface")?.Add(routeCorner);
            BindLineup(root, atlas);
        }

        private static void BindLineup(VisualElement root, Texture2D atlas)
        {
            var inventory = PlayerInventoryService.Instance;
            var formation = inventory?.GetPlayerFormation();
            var isFormation = formation != null && formation.Count > 0;
            var starterIds = new[] { "fighter.kyo94", "fighter.chin94", "fighter.kensou94", "fighter.king94" };
            var portraits = new[] { new Rect(18, 714, 220, 118), new Rect(223, 714, 220, 118), new Rect(428, 714, 220, 118), new Rect(633, 714, 220, 118) };
            root.Q<Label>("lineup-label").text = isFormation ? "YOUR LINEUP" : "STARTER LINEUP";
            root.Q("squad-panel").tooltip = isFormation ? "Your saved formation: three active fighters and one reserve." : "Starter lineup preview. Choose your formation when entering a dungeon.";
            for (var slot = 0; slot < 4; slot++)
            {
                var member = isFormation ? formation.FirstOrDefault(f => f.slotPosition == slot)?.character : inventory?.FindDefinition(starterIds[slot]);
                var owned = member == null ? null : inventory?.FindOwnedByDefinition(member.DefinitionId);
                var holder = root.Q("portrait-" + slot);
                Surface(root, "portrait-" + slot, Card, Ink, slot == 3 ? new Color(.57f, .66f, .77f) : Teal);
                if (member != null && owned != null)
                {
                    var sourceIndex = Array.IndexOf(starterIds, member.DefinitionId);
                    if (sourceIndex >= 0 && atlas != null)
                    {
                        var art = new MenuGraphic(atlas, portraits[sourceIndex], new[] { new Vector2(.135f, 0), new Vector2(1, 0), new Vector2(.91f, .66f), new Vector2(.045f, .66f) }, Color.white, Color.clear, .66f);
                        holder.Add(art);
                    }
                    else if (member.FighterIcon != null)
                    {
                        var image = new Image { sprite = member.FighterIcon, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
                        image.style.position = Position.Absolute; image.style.left = 30; image.style.top = 2; image.style.right = 20; image.style.height = 118;
                        holder.Add(image);
                    }
                    root.Q<Label>("fighter-name-" + slot).text = sourceIndex >= 0 ? new[] { "KYO", "CHIN", "KENSOU", "KING" }[sourceIndex] : ShortName(member.FighterName);
                    root.Q("slot-" + slot).tooltip = member.FighterName + " · C" + owned.constellationTier + "/6" + (isFormation ? "" : " · Starter preview");
                }
                else
                {
                    root.Q<Label>("fighter-name-" + slot).text = "EMPTY";
                    root.Q<Label>("fighter-role-" + slot).text = "CHOOSE";
                }
                Surface(root, "badge-" + slot, new[] { new Vector2(.07f, 0), new Vector2(1, 0), new Vector2(.78f, 1), new Vector2(0, 1) }, slot == 3 ? new Color(.61f, .72f, .84f) : Teal, Color.clear);
                // Frame is drawn after the art so the angled silhouette remains legible.
                holder.Add(new MenuGraphic(null, default, Card, Color.clear, slot == 3 ? new Color(.57f, .66f, .77f) : Teal));
            }
            var featured = inventory?.FindOwnedByDefinition(starterIds[0]);
            root.Q<Label>("fighter-caption-label").text = featured != null ? "YOUR FIGHTER" : "FEATURED FIGHTER";
            root.Q<Label>("fighter-tier").text = featured != null ? "C" + featured.constellationTier + " / 6" : "KYO";
        }

        private static string ShortName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "FIGHTER";
            return value.Split(' ')[0].ToUpperInvariant();
        }

        private static void Art(VisualElement root, string name, Texture2D texture, Rect source, Vector2[] polygon = null)
        {
            if (texture != null) root.Q(name)?.Add(new MenuGraphic(texture, source, polygon ?? Rectangle, Color.white, Color.clear));
        }

        private static void Surface(VisualElement root, string name, Vector2[] polygon, Color fill, Color border)
        {
            root.Q(name)?.Add(new MenuGraphic(null, default, polygon, fill, border));
        }

        private static void AddAccents(VisualElement holder, Color color)
        {
            if (holder == null) return;
            holder.Add(new MenuGraphic(null, default, new[] { new Vector2(0, .55f), new Vector2(.07f, 0), new Vector2(.12f, 0), new Vector2(0, .95f) }, color, Color.clear));
            holder.Add(new MenuGraphic(null, default, new[] { new Vector2(.93f, 1), new Vector2(1, .5f), new Vector2(1, .65f), new Vector2(.95f, 1) }, color, Color.clear));
        }

    }
}
