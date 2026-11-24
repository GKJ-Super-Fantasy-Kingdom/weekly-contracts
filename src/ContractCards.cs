using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeeklyContracts
{
    internal static class ContractCards
    {
        private const float RowHeight = 82f;
        private static readonly Color Ink = new Color32(39, 26, 30, 255);

        private static float Width => Mathf.Clamp(Screen.width / SettingsManager.Instance.GetUIScaling() - 80f, 340f, 510f);

        internal static void Heading(UIOverlayMessage page, string title)
        {
            var body = page.transform.Find("Message").GetComponent<TextMeshProUGUI>();
            var width = Width;
            body.GetComponent<LayoutElement>().preferredWidth = width;
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            var layout = page.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 6f;
            var native = OverlayManager.Instance.transform.Find("Overlay/SizeSelection/HeaderContainer/Header");
            if (native == null)
            {
                body.text = title + "\n" + body.text;
                return;
            }
            var row = Block("Contract heading", page.transform, width, 46f);
            row.SetAsFirstSibling();
            var banner = (RectTransform)Object.Instantiate(native, row, false);
            banner.gameObject.SetActive(true);
            banner.anchorMin = Vector2.zero;
            banner.anchorMax = Vector2.one;
            banner.offsetMin = banner.offsetMax = Vector2.zero;
            var label = banner.Find("Text").GetComponent<TextMeshProUGUI>();
            var localized = label.GetComponent<LocalizeStringEvent>();
            if (localized != null) localized.enabled = false;
            label.text = title;
            foreach (var graphic in banner.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        }

        private static RectTransform Block(string name, Transform parent, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(width, height);
            var layout = rect.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = height;
            return rect;
        }

        // All positions are in the native UI canvas's reference pixels.
        private static RectTransform Box(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Label(RectTransform rect, TextMeshProUGUI native, string value, float size, bool right = false)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = native.font;
            label.fontSharedMaterial = native.fontSharedMaterial;
            label.color = Ink;
            label.fontSize = label.fontSizeMax = size;
            label.fontSizeMin = 14f;
            label.enableAutoSizing = true;
            label.enableWordWrapping = true;
            label.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.text = value;
        }

        private static void Icon(RectTransform rect, Sprite sprite)
        {
            if (sprite == null) return;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        internal static Button Order(UIOverlayMessage page, Button native, OrderKind kind, int deadline, bool selectable)
        {
            var terms = OrderTerms.Get(kind);
            var material = Kingdom.Material(kind);
            var name = Kingdom.Supported(material) ? material.GetName() : terms.Slot.ToString();
            var width = Width;
            var row = Block(terms.Title, page.transform, width, selectable ? RowHeight + 24f : RowHeight);
            row.SetSiblingIndex(native.transform.parent.GetSiblingIndex());
            var paper = row.gameObject.AddComponent<Image>();
            var original = (Image)native.targetGraphic;
            paper.sprite = original.sprite;
            paper.type = original.type;
            paper.pixelsPerUnitMultiplier = original.pixelsPerUnitMultiplier;
            paper.raycastTarget = selectable;
            var button = selectable ? row.gameObject.AddComponent<Button>() : null;
            if (button != null)
            {
                button.targetGraphic = paper;
                var colors = native.colors;
                colors.normalColor = new Color32(255, 243, 213, 255);
                colors.selectedColor = colors.highlightedColor = new Color32(255, 196, 80, 255);
                colors.pressedColor = new Color32(235, 157, 65, 255);
                button.colors = colors;
            }
            else paper.color = new Color32(255, 243, 213, 255);
            var font = native.transform.Find("Text").GetComponent<TextMeshProUGUI>();
            float half = width * .50f;
            Label(Box("Job", row, selectable ? 35 : 14, 4, half + (selectable ? -16 : 5), 32), font, terms.Title, 29);
            Label(Box("Deadline", row, half + 22, 4, half - 36, 32), font, $"{terms.Difficulty} / Night {deadline}", 24, true);
            Icon(Box("Delivery icon", row, 14, 39, 32, 32), material != null ? material.icon : null);
            Label(Box("Delivery", row, 54, 36, half - 62, 38), font, $"{terms.Quantity} {name}", 29);
            var rewardType = terms.Coins > 0 ? ResourceType.Coins : ResourceType.Chest;
            Icon(Box("Reward icon", row, half + 6, 39, 32, 32), RaceManager.Instance.GetResourceIcon(rewardType));
            Label(Box("Reward", row, half + 46, 36, half - 58, 38), font,
                terms.Coins > 0 ? terms.Reward : "Pick 1 of 3 relics", 27);
            if (selectable)
            {
                string status = Kingdom.Readiness(terms, material, Kingdom.Stock(material), out _);
                Label(Box("Stock", row, 16, 72, width - 32, 28), font, status, 24);
                var marker = Box("Focus arrow", row, 10, 4, 23, 32);
                Label(marker, font, ">", 29);
                button.gameObject.AddComponent<ContractFocus>().Bind(marker.gameObject);
            }
            return button;
        }

        internal static void MarkFocusedRelic(Button button)
        {
            var rect = (RectTransform)button.transform;
            var line = Box("Contract focus", rect, 0, 0, 0, 5);
            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(1f, 0f);
            line.offsetMin = new Vector2(7f, -7f);
            line.offsetMax = new Vector2(-7f, -3f);
            var image = line.gameObject.AddComponent<Image>();
            image.color = new Color32(255, 209, 87, 255);
            image.raycastTarget = false;
            button.gameObject.AddComponent<ContractFocus>().Bind(line.gameObject);
        }

        internal static void RelicArt(UIOverlayMessage page, Sprite sprite)
        {
            if (sprite == null) return;
            var art = Block("Relic artwork", page.transform, 48f, 48f);
            art.SetSiblingIndex(page.transform.Find("Message").GetSiblingIndex());
            Icon(art, sprite);
        }
    }

    internal sealed class ContractFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private GameObject marker;
        internal void Bind(GameObject value)
        {
            marker = value;
            marker.SetActive(EventSystem.current?.currentSelectedGameObject == gameObject);
        }
        public void OnSelect(BaseEventData _) => marker?.SetActive(true);
        public void OnDeselect(BaseEventData _) => marker?.SetActive(false);
    }
}
