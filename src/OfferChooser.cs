using System;
using System.Collections.Generic;
using System.IO;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeeklyContracts
{
    internal sealed class OfferChooser
    {
        // Match Hold the Line's wager cards and keep the game HUD clear at both UI scales.
        private const float CardWidth = 280f, CardHeight = 420f, Gap = 16f;
        private static readonly Lazy<Sprite> Frame = new Lazy<Sprite>(() => LoadArt("ContractFrame"));
        private static readonly Lazy<Sprite> FocusRim = new Lazy<Sprite>(() => LoadArt("ContractFocus"));
        private static readonly Dictionary<OrderKind, Sprite> Backdrops = new Dictionary<OrderKind, Sprite>();
        private static readonly Color Parchment = new Color32(255, 245, 220, 255);
        private readonly RectTransform panel;
        private readonly Vector2 center;
        private readonly float width;
        private Vector2 viewport;
        internal Button InitialFocus { get; }
        internal OrderKind FocusedOrder { get; private set; }

        internal OfferChooser(UIOverlayMessage page, Button later, Button decline, OrderKind[] orders,
            int deadline, OrderKind? preferred, Action<OrderKind> review)
        {
            panel = (RectTransform)page.transform;
            center = panel.anchoredPosition;
            width = CardWidth * orders.Length + Gap * (orders.Length - 1);
            AddBackdrop();
            Fit();
            var body = panel.Find("Message").GetComponent<TextMeshProUGUI>();
            body.GetComponent<LayoutElement>().preferredWidth = width;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            body.enableWordWrapping = true;
            body.color = Parchment;
            var pageLayout = panel.GetComponent<VerticalLayoutGroup>();
            pageLayout.childAlignment = TextAnchor.UpperCenter;
            pageLayout.padding = new RectOffset(0, 0, 8, 8);
            pageLayout.spacing = 14f;
            var heading = ContractCards.Block("Contract heading", panel, width, 52f);
            heading.SetAsFirstSibling();
            Caption(heading, body, "Title", "Weekly Contracts", 0, 0, width, 52f, 42f).fontStyle = FontStyles.Bold;

            var footer = later.transform.parent;
            var row = ContractCards.Block("Contract choices", panel, width, CardHeight);
            row.SetSiblingIndex(footer.GetSiblingIndex());
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = Gap;

            void Remember(Button button, OrderKind kind)
            {
                FocusedOrder = kind;
                Navigate(later, null, decline, button, null);
                Navigate(decline, later, null, button, null);
            }

            var choices = new Button[orders.Length];
            for (int i = 0; i < orders.Length; i++)
            {
                OrderKind kind = orders[i];
                var button = Card(row, body, kind, deadline);
                choices[i] = button;
                button.Selected = () => Remember(button, kind);
                button.onClick.AddListener(() => review(kind));
            }
            for (int i = 0; i < choices.Length; i++)
                Navigate(choices[i], i > 0 ? choices[i - 1] : null,
                    i + 1 < choices.Length ? choices[i + 1] : null, null,
                    i * 2 < choices.Length ? later : decline);

            int index = preferred.HasValue ? Array.IndexOf(orders, preferred.Value) : -1;
            index = Math.Max(0, index);
            InitialFocus = choices[index];
            Remember(InitialFocus, orders[index]);
        }

        internal void Fit()
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            var available = new Vector2(Screen.width, Screen.height) / SettingsManager.Instance.GetUIScaling();
            if (available == viewport) return;
            viewport = available;
            float scale = Mathf.Min(1f, available.x / (width + 80f), available.y / 756f);
            panel.localScale = Vector3.one * scale;
            panel.anchoredPosition = center + Vector2.down * (32f * scale);
        }

        private void AddBackdrop()
        {
            // The native SizeEmpty container owns both the scrim and chooser.
            var backdrop = new GameObject("Contract backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = backdrop.rectTransform;
            rect.SetParent(panel.parent, false);
            rect.SetSiblingIndex(panel.GetSiblingIndex());
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            backdrop.color = new Color32(9, 12, 19, 190);
            backdrop.raycastTarget = false;
            panel.GetComponent<Image>().enabled = false;
        }

        private static OfferCardButton Card(RectTransform row, TextMeshProUGUI font, OrderKind kind, int deadline)
        {
            var terms = OrderTerms.Get(kind);
            var material = Kingdom.Material(kind);
            bool supported = Kingdom.Supported(material);
            int stock = Kingdom.Stock(material);
            var card = ContractCards.Block(terms.Title, row, CardWidth, CardHeight);
            var button = card.gameObject.AddComponent<OfferCardButton>();
            // Draw the white edge behind the face so the original dark stepped rim stays visible.
            var rim = ContractCards.Box("Focus rim", card, -2f, -2f, CardWidth + 4f, CardHeight + 4f).gameObject.AddComponent<Image>();
            rim.sprite = FocusRim.Value;
            rim.raycastTarget = false;
            var face = ContractCards.Box("Card frame", card, 0, 0, CardWidth, CardHeight).gameObject.AddComponent<Image>();
            face.sprite = Frame.Value;
            button.Bind(face, rim);

            // Fill the same 264 × 212 inset as the wager cards, beneath the native resource sprite.
            var backdrop = ContractCards.Box("Resource backdrop", card, 8, 8, 264, 212).gameObject.AddComponent<Image>();
            if (!Backdrops.TryGetValue(kind, out var backdropSprite))
                Backdrops.Add(kind, backdropSprite = LoadArt($"Backdrop{kind}"));
            backdrop.sprite = backdropSprite;
            backdrop.raycastTarget = false;
            if (RaceManager.Instance.GetRace() == Races.Undead)
                backdrop.color = new Color32(178, 165, 218, 255);

            string name = supported ? material.GetName() : terms.Slot.ToString();
            if (material != null && material.icon != null)
                ContractCards.Icon(ContractCards.Box("Delivery icon", card, 62, 35, 156, 150), material.icon);
            else
                Caption(card, font, "Resource name", name, 24, 55, 232, 108, 35);
            // Leave the two ornaments at the ends of the title ribbon clear.
            var title = Caption(card, font, "Job", terms.Title, 34, 222, 212, 42, 30);
            title.fontStyle = FontStyles.Bold;
            title.enableWordWrapping = false;
            Caption(card, font, "Delivery", $"Deliver {terms.Quantity} {name}", 14, 271, 252, 30, 27);
            Caption(card, font, "Deadline", $"{terms.Difficulty}  ·  Before Night {deadline}", 14, 301, 252, 26, 23);

            var rewardType = terms.Coins > 0 ? ResourceType.Coins : ResourceType.Chest;
            ContractCards.Icon(ContractCards.Box("Reward icon", card, 23, 329, 25, 25),
                RaceManager.Instance.GetResourceIcon(rewardType));
            Caption(card, font, "Reward", terms.Coins > 0 ? terms.Reward : "Choose 1 of 3 relics",
                55, 327, 204, 29, 24).alignment = TextAlignmentOptions.MidlineLeft;
            string status = Kingdom.Readiness(terms, material, stock, out bool ready, showStock: false);
            if (supported)
                Caption(card, font, "Stock", $"{stock} available", 14, 357, 252, 22, 21);
            Caption(card, font, "Delivery status", status, 14, supported ? 379 : 357, 252, 25, 22).color =
                ready ? new Color32(204, 238, 161, 255) : new Color32(255, 205, 139, 255);
            return button;
        }

        private static TextMeshProUGUI Caption(RectTransform card, TextMeshProUGUI font, string name, string text,
            float x, float y, float width, float height, float size)
        {
            var label = ContractCards.Label(ContractCards.Box(name, card, x, y, width, height), font, text, size);
            label.alignment = TextAlignmentOptions.Center;
            label.color = Parchment;
            return label;
        }

        private static void Navigate(Button button, Button left, Button right, Button up, Button down)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = left,
                selectOnRight = right, selectOnUp = up, selectOnDown = down };
        }

        private static Sprite LoadArt(string name)
        {
            using var resource = typeof(OfferChooser).Assembly.GetManifestResourceStream($"WeeklyContracts.Art.{name}.png");
            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(texture, buffer.ToArray(), true);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }

    internal sealed class OfferCardButton : Button
    {
        private Image rim;
        internal Action Selected;

        internal void Bind(Image face, Image focusRim)
        {
            rim = focusRim;
            targetGraphic = face;
            transition = Transition.None;
            DoStateTransition(currentSelectionState, true);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            Selected?.Invoke();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (rim != null)
                rim.enabled = state == SelectionState.Highlighted || state == SelectionState.Selected ||
                    state == SelectionState.Pressed;
        }
    }
}
