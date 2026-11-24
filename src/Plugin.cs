using System;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace WeeklyContracts
{
    [BepInPlugin("sfk.weeklycontracts", "Weekly Contracts", "0.1.0")]
    [BepInDependency("ownly.morerelics", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Contract Current, Restored;
        internal static Plugin Instance;
        private Harmony harmony;
        private UIOverlayMessage card;
        private bool announce;
        private float worldReadySince = -1f;
        private int index;
        private bool waitForRelease;

        private void Awake()
        {
            Instance = this;
            Chainloader.PluginInfos.TryGetValue("ownly.morerelics", out var source);
            RewardPolicy.Bind(source?.Instance?.GetType().Assembly, message => Logger.LogWarning(message));
            harmony = new Harmony("sfk.weeklycontracts");
            harmony.PatchAll(typeof(SaveHooks.Read));
            harmony.PatchAll(typeof(SaveHooks.Raw));
            harmony.PatchAll(typeof(SaveHooks.Tavern));
            harmony.PatchAll(typeof(SaveHooks.TavernData));
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
            worldReadySince = -1f;
        }

        private void Dawn(int day)
        {
            string run = GameDataManager.Instance.GetRunID();
            if (string.IsNullOrEmpty(run)) return;
            if (Current == null || Current.run != run)
                Current = Restored != null && Restored.run == run ? Restored : new Contract { run = run };
            Restored = null;
            if (Current.AtDawn(day))
            {
                announce = true;
                worldReadySince = -1f;
            }
            if (Current.stage == Stage.Offered && (Current.offers == null || Current.offers.Length == 0))
            {
                Current.offers = Kingdom.Offers(day);
                if (Current.offers.Length == 0)
                {
                    Current.stage = Stage.Expired;
                    announce = false;
                }
            }
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
                DaytimeManager.Instance == null || !DaytimeManager.Instance.IsMorning() ||
                (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue()))
            {
                worldReadySince = -1f;
                return;
            }
            // Give native dawn events and visitor overlays a chance to claim the screen first.
            if (worldReadySince < 0f) worldReadySince = Time.unscaledTime;
            if (Time.unscaledTime - worldReadySince < .5f) return;
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
            var order = Current.Terms;
            var food = Kingdom.Material(Current.selected);
            string name = Kingdom.Supported(food) ? food.GetName() : order.Slot.ToString();
            switch (Current.stage)
            {
                case Stage.Offered:
                    Page(Current.OfferMessage,
                        "Later", Close, "Decline…", Decline, rows: Current.offers, heading: "Weekly Contracts");
                    break;
                case Stage.Active:
                    int count = Kingdom.Stock(food);
                    Page($"{count} unreserved {name} available.",
                        count >= order.Quantity ? $"Deliver {order.Quantity}…" : null, ReviewDelivery, "Back", Close,
                        heading: "Accepted Contract", summary: Current.selected);
                    break;
                case Stage.Earned:
                    Page("Delivery complete. Your reward is ready.",
                        "Collect Reward", () =>
                        {
                            if (order.Coins > 0)
                            {
                                if (Kingdom.CollectCoins(Current, WarnReward)) Close();
                                else RetryReward();
                            }
                            else { index = 0; Reward(); }
                        }, "Later", Close, heading: "Contract Reward", summary: Current.selected);
                    break;
                default:
                    Page(Current.stage == Stage.Claimed ? "Relic already collected." : "This contract is closed.",
                        null, null, "Back", Close);
                    break;
            }
        }

        private void ReviewOffer(OrderKind kind)
        {
            if (Current.stage != Stage.Offered) { Show(); return; }
            var order = OrderTerms.Get(kind);
            var food = Kingdom.Material(kind);
            var name = Kingdom.Supported(food) ? food.GetName() : order.Slot.ToString();
            Page($"{Kingdom.Stock(food)} {name} available. Accepting costs nothing.",
                "Accept", () => { if (Current.Accept(kind)) Close(); else Show(); }, "Back", Show,
                heading: "Contract Terms", summary: kind);
        }

        private void Decline()
        {
            Page("Decline all contracts for this week? Your stock stays the same.",
                "Decline", () => { Current.Decide(false); Close(); }, "Keep Offer", Show);
        }

        private void ReviewDelivery()
        {
            var food = Kingdom.Material(Current.selected);
            var order = Current.Terms;
            int cost = order.Quantity;
            int before = Kingdom.Stock(food);
            if (Current.stage != Stage.Active || before < cost) { Show(); return; }
            var type = food.type;
            Page($"Commit {cost} {food.GetName()}? Your available stock will go from {before} to {before - cost}.",
                "Confirm", () =>
                {
                    var currentFood = Kingdom.Material(Current.selected);
                    if (Current.stage != Stage.Active || !Kingdom.Supported(currentFood) ||
                        currentFood.type != type || Kingdom.Stock(currentFood) != before) { Show(); return; }
                    if (Kingdom.Deliver(Current, currentFood))
                    {
                        Show();
                    }
                    else Page($"Delivery unavailable. You need {cost} spendable {currentFood.GetName()}" +
                        (order.Coins == 0 ? " and three eligible relics." : "."),
                        null, null, "Back", Show);
                }, "Back", Show, heading: "Confirm Delivery", summary: Current.selected);
        }

        private void Reward()
        {
            if (Current.stage != Stage.Earned) { Show(); return; }
            if (Current.rewards == null || !Current.rewards.Any(Kingdom.CanClaim))
            {
                var paid = Kingdom.PaidFood(Current);
                string message = paid != null ? $"Take your six {paid.GetName()} back or keep the reward for later."
                    : "Restore the paid resource definition to collect a refund, or keep the reward for later.";
                Page("Your relics are currently unavailable. " + message, paid != null ? "Refund" : null, () =>
                    {
                        if (Kingdom.ReturnPayment(Current))
                        {
                            Close();
                        }
                        else Show();
                    }, "Later", Close);
                return;
            }
            string id = Current.rewards[index];
            var target = Kingdom.ClaimTarget(id);
            bool available = target != null;
            var relic = available || string.IsNullOrEmpty(id) ? target : AddressablesManager.Instance.GetRelic(id);
            string title = relic != null ? relic.GetTitle() : id;
            string detail = available ? relic.GetDescription() : "Currently unavailable.";
            Page($"Relic {index + 1} of 3  •  {title}\n{detail}", available ? "Choose…" : null,
                () => ConfirmReward(id, target, title, detail), "Next Relic", () =>
                {
                    index = (index + 1) % Current.rewards.Length;
                    Reward();
                }, true, heading: "Choose Your Relic", art: relic != null ? relic.GetIcon() : null);
        }

        private void ConfirmReward(string id, Relic target, string title, string description)
        {
            Page($"Collect {title} as your contract reward?", "Collect", () =>
            {
                if (target != null && Kingdom.ClaimTarget(id) == target && target.GetTitle() == title &&
                    target.GetDescription() == description)
                {
                    if (Kingdom.CollectRelic(Current, id, target, WarnReward)) Close();
                    else RetryReward();
                }
                else Reward();
            }, "Back", Reward, heading: "Collect Relic", art: target.GetIcon());
        }

        private void WarnReward(Exception error) => Logger.LogWarning($"Contract reward provider: {error.GetBaseException().Message}");

        private void RetryReward() => Page("Your reward was not received. You can collect it again.",
            "Back", Show, "Later", Close);

        private static bool SubmitHeld()
        {
            var input = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            return input != null && input.submit != null && input.submit.action.IsPressed();
        }

        private void Page(string message, string primary, Action accept, string secondary, Action other,
            bool allowLater = false, OrderKind[] rows = null, string heading = null, OrderKind? summary = null,
            Sprite art = null)
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
            if (heading != null) ContractCards.Heading(next, heading);
            if (art != null) ContractCards.RelicArt(next, art);
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
                var layout = next.GetComponent<VerticalLayoutGroup>();
                layout.padding.bottom = layout.padding.top;
                var later = Instantiate(no, next.transform);
                later.GetComponentInChildren<TextMeshProUGUI>().text = "Later";
                later.onClick = new Button.ButtonClickedEvent();
                later.onClick.AddListener(() => Invoke(Close));
                LinkButtons(primary != null ? yes : null, no, later);
            }
            var focus = primary != null ? yes : no;
            if (summary.HasValue) ContractCards.Order(next, yes, summary.Value, Current.deadline, false);
            if (rows != null && rows.Length > 0)
            {
                var choices = new Button[rows.Length];
                for (int i = 0; i < rows.Length; ++i)
                {
                    var kind = rows[i];
                    var button = choices[i] = ContractCards.Order(next, yes, kind, Current.deadline, true);
                    button.onClick.AddListener(() => Invoke(() => ReviewOffer(kind)));
                }
                for (int i = 0; i < choices.Length; ++i)
                    Navigate(choices[i], i == 0 ? yes : choices[i-1], i+1 == choices.Length ? yes : choices[i+1], null);
                Navigate(yes, choices[choices.Length-1], choices[0], no);
                Navigate(no, choices[choices.Length-1], choices[0], yes);
                focus = choices[0];
            }
            GamepadManager.Instance.SetGameObjectToSelect(focus.gameObject, true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
        }

        private static void Navigate(Button button, Button up, Button down, Button side)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up,
                selectOnDown = down, selectOnLeft = side, selectOnRight = side };
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

        // The stock cycle caption lives inside a disabled image over the day grid.
        // Put the link below the calendar in the game's own button prefab.
        [HarmonyPatch(typeof(UICalendar), "OnEnable")]
        private static class CalendarLink
        {
            private static void Postfix(UICalendar __instance, TextMeshProUGUI ___textCycle, GameObject ___firstDay)
            {
                var row = __instance.transform.Find("Contract row") as RectTransform;
                var button = __instance.transform.Find("Weekly contract")?.GetComponent<Button>();
                if (!Accessible)
                {
                    if (row != null) row.gameObject.SetActive(false);
                    if (button != null) button.gameObject.SetActive(false);
                    return;
                }
                if (row == null)
                {
                    row = (RectTransform)new GameObject("Contract row", typeof(RectTransform), typeof(LayoutElement)).transform;
                    row.SetParent(__instance.transform, false);
                    var template = Resources.Load<GameObject>("UI/OverlayMessage").transform.Find("Buttons/BuyButton");
                    var control = (RectTransform)Instantiate(template, __instance.transform, false);
                    control.name = "Weekly contract";
                    control.GetComponent<LayoutElement>().ignoreLayout = true;
                    control.anchorMin = control.anchorMax = control.pivot = new Vector2(.5f, 0f);
                    control.anchoredPosition = new Vector2(0f, 8f);
                    foreach (Transform child in control) child.gameObject.SetActive(child.name == "Text");
                    button = control.GetComponent<Button>();
                    float height = control.rect.height + 16f;
                    row.sizeDelta = new Vector2(1f, height);
                    row.GetComponent<LayoutElement>().preferredHeight = height;
                }
                row.gameObject.SetActive(true);
                button.gameObject.SetActive(true);
                var caption = button.transform.Find("Text").GetComponent<TextMeshProUGUI>();
                caption.GetComponent<LocalizeStringEvent>().enabled = false;
                caption.text = ___textCycle.text + " - Contract";
                button.interactable = true;
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
                    var grid = ___firstDay.transform.parent;
                    for (int i = 21; i < Math.Min(28, grid.childCount); ++i)
                    {
                        var day = grid.GetChild(i).GetComponent<Selectable>();
                        if (day == null) continue;
                        var edge = day.navigation;
                        if (edge.mode != Navigation.Mode.Explicit)
                        {
                            edge.selectOnLeft = day.FindSelectableOnLeft();
                            edge.selectOnRight = day.FindSelectableOnRight();
                            edge.selectOnUp = day.FindSelectableOnUp();
                            edge.selectOnDown = day.FindSelectableOnDown();
                        }
                        if (edge.selectOnDown != null && edge.selectOnDown != button) continue;
                        edge.mode = Navigation.Mode.Explicit;
                        edge.selectOnDown = button;
                        day.navigation = edge;
                        if (fromLink.selectOnUp == null || i == 24) fromLink.selectOnUp = day;
                    }
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
