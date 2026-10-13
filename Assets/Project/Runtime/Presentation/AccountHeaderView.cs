using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation
{
    /// <summary>One header template and account binding for every non-combat screen.</summary>
    public static class AccountHeaderView
    {
        public static void Bind(VisualElement root, Action back = null)
        {
            var header = root.Q("account-header");
            if (header == null || header.userData is Binding) return;
            header.userData = new Binding(root, header, back);
        }

        public static void OverrideBack(VisualElement root, object owner, Action back, string hint = "Back")
        {
            Bind(root);
            if (root.Q("account-header")?.userData is Binding binding) binding.OverrideBack(owner, back, hint);
        }
        public static void ClearBackOverride(VisualElement root, object owner)
        {
            if (root.Q("account-header")?.userData is Binding binding) binding.ClearBack(owner);
        }

        public static void Refresh(VisualElement root)
        {
            var inventory = PlayerInventoryService.Instance;
            var wallet = root.Q<Label>("account-wallet");
            if (wallet == null) return;
            wallet.text = inventory == null ? "—" : inventory.Diamonds.ToString("N0");
            wallet.tooltip = inventory == null ? "Inventory unavailable" : wallet.text + " Diamonds";
            var coins = root.Q<Label>("account-coins");
            if (coins != null) coins.text = inventory == null ? "—" : inventory.Coins.ToString("N0");
            wallet.style.fontSize = wallet.text.Length > 9 ? 19 : 25;
            if (coins != null) coins.style.fontSize = coins.text.Length > 9 ? 19 : 25;
            var caption = root.Q<Label>("account-caption");
            if (caption != null) caption.text = inventory == null || (inventory.SubjectId ?? "").StartsWith("guest:", StringComparison.Ordinal)
                ? "LOCAL PLAYER" : "PLAYER";
        }

        private sealed class Binding
        {
            private readonly VisualElement _root;
            private readonly EventCallback<GeometryChangedEvent> _resize;
            private bool _subscribed;
            private readonly Button _backButton;
            private readonly Action _back;
            private sealed class BackAction { public object Owner; public Action Action; public string Hint; }
            private readonly List<BackAction> _backStack = new List<BackAction>();
            private BackAction CurrentBack => _backStack.Count == 0 ? null : _backStack[_backStack.Count - 1];
            public Binding(VisualElement root, VisualElement header, Action back)
            {
                _root = root;
                _backButton = header.Q<Button>("account-back"); _back = back;
                UpdateBack();
                if (_backButton != null) _backButton.clicked += () => (CurrentBack?.Action ?? _back)?.Invoke();
                var diamonds = header.Q<Button>("account-diamond-channel");
                var coins = header.Q<Button>("account-coin-channel");
                if (diamonds != null) diamonds.clicked += () => ItemRewardView.Show(root,
                    FightingAllstar.Core.Content.ItemCatalog.Find(FightingAllstar.Core.Content.ItemCatalog.Diamond), anchor: diamonds);
                if (coins != null) coins.clicked += () => ItemRewardView.Show(root,
                    FightingAllstar.Core.Content.ItemCatalog.Find(FightingAllstar.Core.Content.ItemCatalog.Coin), anchor: coins);
                _resize = evt => Resize(evt.newRect.width, evt.newRect.height);
                header.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
                header.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
                Resize(root.contentRect.width, root.contentRect.height);
                if (header.panel != null) Subscribe();
                Refresh(root);
            }
            public void OverrideBack(object owner, Action action, string hint)
            {
                _backStack.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));
                _backStack.Add(new BackAction { Owner = owner, Action = action, Hint = hint }); UpdateBack();
            }
            public void ClearBack(object owner) { _backStack.RemoveAll(entry => ReferenceEquals(entry.Owner, owner)); UpdateBack(); }
            private void UpdateBack()
            {
                if (_backButton == null) return;
                _backButton.style.display = (CurrentBack?.Action ?? _back) == null ? DisplayStyle.None : DisplayStyle.Flex;
                _backButton.tooltip = CurrentBack?.Hint ?? "Back";
            }
            private void Resize(float width, float height)
            {
                if (width <= 0) return;
                _root.EnableInClassList("compact-layout", width < 1400 || height < 850);
                _root.EnableInClassList("narrow-layout", width < 850);
            }
            private void Subscribe()
            {
                if (!_subscribed)
                {
                    PlayerInventoryService.InventoryChanged += Changed;
                    _root.RegisterCallback(_resize); _subscribed = true;
                }
                Changed();
            }
            private void Unsubscribe()
            {
                if (!_subscribed) return;
                PlayerInventoryService.InventoryChanged -= Changed;
                _root.UnregisterCallback(_resize); _subscribed = false;
            }
            private void Changed() => Refresh(_root);
        }
    }
}
