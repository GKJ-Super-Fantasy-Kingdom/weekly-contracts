using System;
using UnityEngine;
using UnityEngine.UI;

namespace WeeklyContracts
{
    // The native focus sprite has its own white edge. Hover and press use dark-edged plates.
    internal sealed class NativeButtonEdge : MonoBehaviour
    {
        private const int Border = 3;
        private const int SpriteSize = 2 * Border + 1;
        private static readonly Lazy<Sprite> Step = new Lazy<Sprite>(CreateStep);
        private Button button;
        private Image plate, edge;

        internal static void Add(Button button)
        {
            if (button.transition != Selectable.Transition.SpriteSwap || !(button.targetGraphic is Image plate) ||
                plate.type != Image.Type.Sliced || plate.sprite == null ||
                (plate.sprite.name != "UI_21" && plate.sprite.name != "UI_23")) return;

            var effect = button.gameObject.AddComponent<NativeButtonEdge>();
            effect.button = button;
            effect.plate = plate;
            var layer = new GameObject("Contract interaction edge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            layer.GetComponent<LayoutElement>().ignoreLayout = true;
            effect.edge = layer.GetComponent<Image>();
            var rect = effect.edge.rectTransform;
            rect.SetParent(plate.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            effect.edge.sprite = Step.Value;
            effect.edge.type = Image.Type.Sliced;
            effect.edge.fillCenter = false;
            effect.edge.raycastTarget = false;
            effect.edge.maskable = plate.maskable;
            effect.edge.enabled = false;
        }

        private void LateUpdate()
        {
            var current = plate.overrideSprite;
            var states = button.spriteState;
            bool show = button.isActiveAndEnabled && button.IsInteractable() && plate.isActiveAndEnabled &&
                current != null && (current == states.highlightedSprite || current == states.pressedSprite);
            edge.enabled = show;
            if (!show) return;

            // One native slice pixel beyond the dark edge, with no paint over the button face.
            var rect = edge.rectTransform;
            var outset = Vector2.one / (plate.pixelsPerUnit * plate.pixelsPerUnitMultiplier);
            if (rect.offsetMax != outset)
            {
                rect.offsetMin = -outset;
                rect.offsetMax = outset;
            }
            float multiplier = plate.pixelsPerUnitMultiplier * plate.sprite.pixelsPerUnit / edge.sprite.pixelsPerUnit;
            if (!Mathf.Approximately(edge.pixelsPerUnitMultiplier, multiplier)) edge.pixelsPerUnitMultiplier = multiplier;
        }

        private static Sprite CreateStep()
        {
            var pixels = new Color32[SpriteSize * SpriteSize];
            for (int y = 0; y < SpriteSize; ++y)
            for (int x = 0; x < SpriteSize; ++x)
                if (!Inside(x, y) && (Inside(x - 1, y) || Inside(x + 1, y) || Inside(x, y - 1) || Inside(x, y + 1)))
                    pixels[y * SpriteSize + x] = Color.white;

            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(.5f, .5f),
                100f, 0, SpriteMeshType.FullRect, Vector4.one * Border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // The native five-pixel tiles step in twice at each corner.
        private static bool Inside(int x, int y)
        {
            int horizontal = Math.Min(x - 1, SpriteSize - 2 - x);
            int vertical = Math.Min(y - 1, SpriteSize - 2 - y);
            return horizontal >= 0 && vertical >= 0 && horizontal + vertical >= 2;
        }
    }
}
