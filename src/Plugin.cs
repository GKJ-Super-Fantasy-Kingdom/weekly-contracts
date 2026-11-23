using System;
using System.Linq;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WeeklyContracts
{
    [BepInPlugin("sfk.weeklycontracts", "Weekly Contracts", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Contract Current, Restored;
        internal static Plugin Instance;
        private Harmony harmony;
        private UIOverlayMessage card;
        private bool announce;
        private int index;
        private bool waitForRelease;

        private void Awake()
        {
            Instance = this;
            harmony = new Harmony("sfk.weeklycontracts");
            harmony.PatchAll(typeof(SaveHooks.Read));
            harmony.PatchAll(typeof(SaveHooks.Save));
            harmony.PatchAll(typeof(CalendarLink));
            GameManager.OnGameInit += ResetRun;
            DaytimeManager.OnMorningStart += Dawn;
            DaytimeManager.OnDayStart += Day;
            DaytimeManager.OnNightStart += Night;
            Logger.LogInfo("Weekly Contracts ready.");
        }

        private void OnDestroy()
        {
            GameManager.OnGameInit -= ResetRun;
            DaytimeManager.OnMorningStart -= Dawn;
            DaytimeManager.OnDayStart -= Day;
            DaytimeManager.OnNightStart -= Night;
            harmony?.UnpatchSelf();
            ResetRun();
            Instance = null;
        }

        private void ResetRun()
        {
            Current = null;
            Restored = null;
            card = null;
            announce = false;
        }

        private void Dawn(int day)
        {
            string run = GameDataManager.Instance.GetRunID();
            if (string.IsNullOrEmpty(run)) return;
            if (Current == null || Current.run != run)
                Current = Restored != null && Restored.run == run ? Restored : new Contract { run = run };
            Restored = null;
            announce = Kingdom.SupportsBread && Current.AtDawn(day);
        }

        private static void Day(int day) => Current?.Advance(day, false, false);
        private static void Night(int day) => Current?.Advance(day, false, true);

        private void Update()
        {
            if (waitForRelease && !SubmitHeld()) waitForRelease = false;
            if (!announce || Current == null) return;
            if (Current.stage != Stage.Offered) { announce = false; return; }
            if (GameManager.Instance == null || !GameManager.Instance.IsGameRunning() ||
                UIManager.Instance == null || !UIManager.Instance.IsWorldInteractable() ||
                DaytimeManager.Instance == null || !DaytimeManager.Instance.IsMorning()) return;
            announce = false;
            Show();
        }

        private void Refresh()
        {
            var clock = DaytimeManager.Instance;
            if (Current != null && clock != null)
                Current.Advance(clock.CurrentDay(), clock.IsMorning(), clock.IsNight());
        }

        internal static bool Accessible => Current != null &&
            (Current.stage == Stage.Offered || Current.stage == Stage.Active || Current.stage == Stage.Earned);

        private void Close()
        {
            card = null;
            OverlayManager.Instance.CloseOverlay(false);
        }

        private void Show()
        {
            announce = false;
            Refresh();
            string terms = $"Bread for the watch\nDeliver 6 bread before night starts on day {Current.deadline}.";
            switch (Current.stage)
            {
                case Stage.Offered:
                    Page(terms + "\nChoose 1 of 3 run relics. Accepting costs nothing. Reopen from the calendar.",
                        "Accept", () => { Current.Decide(true); Close(); }, "Decline…", Decline, true);
                    break;
                case Stage.Active:
                    int count = Kingdom.Stock();
                    Page(terms + $"\n{count} unreserved bread available.",
                        count >= Contract.Price ? "Deliver 6…" : null, ReviewDelivery, "Back", Close);
                    break;
                case Stage.Earned:
                    Page("Bread delivered. Your relic choice is available for the rest of this run.",
                        "Collect reward", () => { index = 0; Reward(); }, "Later", Close);
                    break;
                default:
                    Page(Current.stage == Stage.Claimed ? "Relic already collected." : "This contract is closed.",
                        null, null, "Back", Close);
                    break;
            }
        }

        private void Decline()
        {
            Page("Decline Bread for the watch for this week? Your stock stays the same.",
                "Decline", () => { Current.Decide(false); Close(); }, "Keep offer", Show);
        }

        private void ReviewDelivery()
        {
            int before = Kingdom.Stock();
            if (Current.stage != Stage.Active || before < Contract.Price) { Show(); return; }
            Page($"Commit 6 bread? Your available stock will go from {before} to {before - Contract.Price}.",
                "Confirm", () =>
                {
                    if (Current.stage != Stage.Active || Kingdom.Stock() != before) { Show(); return; }
                    if (Current.Deliver(Kingdom.Choices(), Kingdom.Spend)) Show();
                    else Page("Delivery unavailable. You need six free bread and three eligible relics.",
                        null, null, "Back", Show);
                }, "Back", Show);
        }

        private void Reward()
        {
            if (Current.stage != Stage.Earned) { Show(); return; }
            if (!Current.rewards.Any(RelicManager.Instance.CanAddRelic))
            {
                Page("All three relics are currently unavailable. Take your six bread back or keep the reward for later.",
                    "Return bread", () =>
                    {
                        if (Current.stage != Stage.Earned || Current.rewards.Any(RelicManager.Instance.CanAddRelic))
                        { Show(); return; }
                        Current.stage = Stage.Refunded;
                        ResourceManager.Instance.AddResource(ResourceType.Bread, Contract.Price);
                        Close();
                    }, "Later", Close);
                return;
            }
            string id = Current.rewards[index];
            var relic = AddressablesManager.Instance.GetRelic(id);
            bool available = relic != null && RelicManager.Instance.CanAddRelic(id);
            string title = relic != null ? relic.GetTitle() : id;
            string detail = available ? relic.GetDescription() : "Currently unavailable.";
            Page($"Relic {index + 1} of 3  •  {title}\n{detail}", available ? "Choose…" : null,
                () => ConfirmReward(id, title), "Next relic", () =>
                {
                    index = (index + 1) % Current.rewards.Length;
                    Reward();
                }, true);
        }

        private void ConfirmReward(string id, string title)
        {
            Page($"Collect {title} as your contract reward?", "Collect", () =>
            {
                if (Current.Collect(id, Kingdom.Grant)) Close();
                else Reward();
            }, "Back", Reward);
        }

        private static bool SubmitHeld()
        {
            var input = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            return input != null && input.submit != null && input.submit.action.IsPressed();
        }

        private void Page(string message, string primary, Action accept, string secondary, Action other,
            bool allowLater = false)
        {
            if (card != null && card.gameObject.activeInHierarchy)
                card.gameObject.SetActive(false);
            var next = OverlayManager.Instance.OverlayMessage(true);
            card = next;
            int opened = Time.frameCount;
            var origin = Current;
            waitForRelease = SubmitHeld();
            void Invoke(Action action)
            {
                if (card != next || !next.gameObject.activeInHierarchy || Current != origin ||
                    Time.frameCount == opened || waitForRelease || GameManager.Instance.IsUnloading()) return;
                Refresh();
                action();
            }

            next.SetMessage(message);
            var yes = next.transform.Find("Buttons/BuyButton").GetComponent<Button>();
            var no = next.transform.Find("Buttons/Cancel").GetComponent<Button>();
            yes.onClick = new Button.ButtonClickedEvent();
            no.onClick = new Button.ButtonClickedEvent();
            if (primary != null) next.SetAccept(primary, () => Invoke(accept));
            else next.RemoveAccept();
            next.SetCancel(secondary, () => Invoke(other));
            if (allowLater)
            {
                // OverlayMessage has a native vertical layout. Add a separate row for pointer Back.
                var later = Instantiate(no, next.transform);
                later.GetComponentInChildren<TextMeshProUGUI>().text = "Later";
                later.onClick = new Button.ButtonClickedEvent();
                later.onClick.AddListener(() => Invoke(Close));
                LinkButtons(primary != null ? yes : null, no, later);
            }
            var focus = primary != null ? yes : no;
            GamepadManager.Instance.SetGameObjectToSelect(focus.gameObject, true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
        }

        private static void LinkButtons(Button primary, Button secondary, Button later)
        {
            foreach (var current in new[] { primary, secondary, later })
            {
                if (current == null) continue;
                var navigation = current.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                var neighbor = current == primary ? secondary : primary;
                navigation.selectOnLeft = navigation.selectOnRight = neighbor;
                navigation.selectOnDown = navigation.selectOnUp = current == later ? primary ?? secondary : later;
                current.navigation = navigation;
            }
        }

        // The existing cycle caption becomes a selectable entry while there is contract work.
        // Up from day one reaches it, and Down returns to the native calendar grid.
        [HarmonyPatch(typeof(UICalendar), "OnEnable")]
        private static class CalendarLink
        {
            private static void Postfix(TextMeshProUGUI ___textCycle, GameObject ___firstDay)
            {
                if (!Accessible) return;
                var caption = ___textCycle;
                var button = caption.GetComponent<Button>();
                if (button == null) button = caption.gameObject.AddComponent<Button>();
                caption.text += "  •  Contract";
                caption.enableAutoSizing = true;
                caption.raycastTarget = true;
                button.targetGraphic = caption;
                var colors = button.colors;
                colors.selectedColor = new Color(1f, 0.79f, 0.28f);
                colors.highlightedColor = colors.selectedColor;
                button.colors = colors;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() =>
                {
                    if (!Accessible || !OverlayManager.Instance.IsCalendarOpen()) return;
                    OverlayManager.Instance.CloseOverlay(false);
                    Instance.Show();
                });
                var first = ___firstDay.GetComponent<Selectable>();
                if (first != null)
                {
                    var fromLink = button.navigation;
                    fromLink.mode = Navigation.Mode.Explicit;
                    fromLink.selectOnDown = first;
                    button.navigation = fromLink;
                    var fromGrid = first.navigation;
                    // Preserve the grid's existing exits when upgrading automatic navigation.
                    if (fromGrid.mode != Navigation.Mode.Explicit)
                    {
                        fromGrid.selectOnRight = first.FindSelectableOnRight();
                        fromGrid.selectOnDown = first.FindSelectableOnDown();
                        fromGrid.selectOnLeft = first.FindSelectableOnLeft();
                    }
                    fromGrid.mode = Navigation.Mode.Explicit;
                    fromGrid.selectOnUp = button;
                    first.navigation = fromGrid;
                }
                GamepadManager.Instance.SetGameObjectToSelect(button.gameObject, true);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
        }
    }
}
