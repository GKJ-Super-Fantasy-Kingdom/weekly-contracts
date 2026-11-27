using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WeeklyContracts
{
    internal sealed class ContractModal : MonoBehaviour
    {
        private InputSystemUIInputModule input;
        private bool deselectOnBackgroundClick;
        private EventTrigger backdropEvents;
        private bool backdropEventsEnabled;
        private GameObject shield;
        private Button nativeLater, contractLater;

        internal bool IsOpen => isActiveAndEnabled && shield != null;

        internal void Bind(Transform container)
        {
            var overlay = container.parent;
            backdropEvents = overlay.GetComponent<EventTrigger>();
            if (backdropEvents != null)
            {
                // The native backdrop trigger also acts as a selection target for blank clicks.
                backdropEventsEnabled = backdropEvents.enabled;
                backdropEvents.enabled = false;
            }
            // Cover the native click-to-close backdrop, below the active contract controls.
            shield = new GameObject("Contract click shield", typeof(RectTransform), typeof(Image), typeof(ContractClickShield));
            var rect = (RectTransform)shield.transform;
            rect.SetParent(overlay, false);
            rect.SetSiblingIndex(container.GetSiblingIndex());
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            shield.GetComponent<Image>().color = Color.clear;

            input = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            if (input == null) return;
            deselectOnBackgroundClick = input.deselectOnBackgroundClick;
            input.deselectOnBackgroundClick = false;
        }

        internal Button ReplaceMerchantLater(Button native)
        {
            // The merchant screen is shared. Give this picker its own styled button for this visit.
            nativeLater = native;
            contractLater = Instantiate(native, native.transform.parent, false);
            contractLater.transform.SetSiblingIndex(native.transform.GetSiblingIndex() + 1);
            contractLater.name = "Contract Later";
            ContractCards.DismissFooter(contractLater);
            NativeButtonEdge.Add(contractLater);
            foreach (var prompt in contractLater.GetComponentsInChildren<SuperFantasyKingdom.UI.UIButtonPrompt>(true))
                prompt.enabled = false;
            native.gameObject.SetActive(false);
            return contractLater;
        }

        private void OnDisable()
        {
            if (shield != null)
            {
                shield.SetActive(false);
                Destroy(shield);
                shield = null;
            }
            if (contractLater != null)
            {
                contractLater.gameObject.SetActive(false);
                Destroy(contractLater.gameObject);
                contractLater = null;
                if (nativeLater != null) nativeLater.gameObject.SetActive(true);
                nativeLater = null;
            }
            if (backdropEvents != null) backdropEvents.enabled = backdropEventsEnabled;
            backdropEvents = null;
            if (input != null) input.deselectOnBackgroundClick = deselectOnBackgroundClick;
            input = null;
        }
    }

    internal sealed class ContractClickShield : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData _) { }
    }
}
