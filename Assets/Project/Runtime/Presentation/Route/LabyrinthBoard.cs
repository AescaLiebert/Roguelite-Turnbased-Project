using System;
using System.Collections.Generic;
using FightingAllstar.Core.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Route
{
    /// <summary>Bottom-up, staggered stone platforms with the exact playable route connections.</summary>
    public sealed class LabyrinthBoard : VisualElement
    {
        private readonly RunState _run;
        private readonly Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();
        public VisualElement CurrentTile { get; private set; }
        public LabyrinthBoard(RunState run, Action<string> choose)
        {
            _run = run;
            AddToClassList("labyrinth-board");
            style.width = 620; style.height = 1420;
            foreach (var node in run.Nodes)
            {
                var count = run.Nodes.FindAll(n => n.Row == node.Row).Count;
                var x = 310 + (node.Column - (count - 1) / 2f) * 154;
                var y = 1310 - node.Row * 150 + (node.Column % 2 == 0 ? 15 : -15);
                _positions[node.Id] = new Vector2(x, y);
                var button = new Button(() => choose(node.Id));
                button.AddToClassList("labyrinth-tile");
                button.AddToClassList("tile-" + node.Type.ToString().ToLowerInvariant());
                button.AddToClassList("progress-" + node.Progress.ToString().ToLowerInvariant());
                button.style.left = x - 58; button.style.top = y - 72;
                button.text = node.Type.ToString().ToUpperInvariant();
                var icon = new VisualElement(); icon.AddToClassList("tile-icon"); icon.pickingMode = PickingMode.Ignore;
                button.Insert(0, icon);
                button.tooltip = node.Progress + (node.EnemyTeamSnapshot.Count == 0 ? "" : "\n" +
                    string.Join("\n", node.EnemyTeamSnapshot.ConvertAll(f => (f.IsReserve ? "Reserve: " : "Active " + (f.FormationSlot + 1) + ": ") + (f.Definition?.DisplayName ?? f.DefinitionId))));
                button.SetEnabled(node.Progress == RouteNodeProgress.Reachable);
                Add(button);
                if (node.Id == run.CurrentNodeId) CurrentTile = button;
            }
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            foreach (var node in _run.Nodes)
                foreach (var nextId in node.OutgoingNodeIds)
                {
                    var taken = _run.SelectedPath.Contains(node.Id) && _run.SelectedPath.Contains(nextId);
                    painter.strokeColor = taken ? new Color(.85f, .7f, .35f) : new Color(.24f, .39f, .48f);
                    painter.lineWidth = taken ? 5 : 2;
                    painter.BeginPath(); painter.MoveTo(_positions[node.Id]); painter.LineTo(_positions[nextId]); painter.Stroke();
                }
            foreach (var node in _run.Nodes)
            {
                var point = _positions[node.Id] + new Vector2(0, 20);
                var active = node.Progress == RouteNodeProgress.Reachable || node.Progress == RouteNodeProgress.Selected;
                painter.fillColor = active ? new Color(.2f, .36f, .42f) : new Color(.1f, .19f, .25f);
                painter.strokeColor = active ? new Color(.91f, .75f, .39f) : new Color(.26f, .41f, .49f);
                painter.lineWidth = active ? 3 : 1;
                painter.BeginPath();
                painter.MoveTo(point + new Vector2(0, -38)); painter.LineTo(point + new Vector2(72, 0));
                painter.LineTo(point + new Vector2(72, 18)); painter.LineTo(point + new Vector2(0, 56));
                painter.LineTo(point + new Vector2(-72, 18)); painter.LineTo(point + new Vector2(-72, 0));
                painter.ClosePath(); painter.Fill(); painter.Stroke();
            }
        }
        private static string Symbol(RouteNodeType type)
        {
            switch (type)
            {
                case RouteNodeType.Start: return "↑";
                case RouteNodeType.Battle: return "⚔";
                case RouteNodeType.Elite: return "♜";
                case RouteNodeType.Rest: return "+";
                case RouteNodeType.Boon: return "◆";
                default: return "♛";
            }
        }
    }
}
