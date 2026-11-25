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
    [BepInPlugin("sfk.weeklycontracts", "Weekly Contracts", "0.1.3")]
    [BepInDependency("ownly.morerelics", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Contract Current, Restored;
        internal static Plugin Instance;
        private Harmony harmony;
        private UIOverlayMessage card;
        private bool announce;
        private float worldReadySince = -1f;
        private bool waitForRelease;
        private bool backToParent;

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
            harmony.PatchAll(typeof(ContractBack));
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
            switch (Current.stage)
            {
                case Stage.Offered:
                    ShowOffers();
                    break;
                case Stage.Active:
                    int count = Kingdom.Stock(food);
                    var readiness = Kingdom.Readiness(order, food, count, out bool ready);
                    Page($"{readiness}. Deliver before nightfall on day {Current.deadline}.",
                        ready ? $"Deliver {order.Quantity}…" : null, ReviewDelivery, "Back", Close,
                        heading: "Accepted Contract", summary: Current.selected);
                    break;
                case Stage.Earned:
                    Page(order.Coins > 0 ? $"Delivery complete. Collect {order.Reward}." : "Delivery complete. Open your relic reward.",
                        "Collect Reward", () =>
                        {
                            if (order.Coins > 0)
                            {
                                if (Kingdom.CollectCoins(Current, WarnReward)) Close();
                                else RetryReward();
                            }
                            else Reward();
                        }, "Later", Close, heading: "Contract Reward");
                    break;
                default:
                    Page(Current.stage == Stage.Claimed ? "Reward already collected." : "This contract is closed.",
                        null, null, "Back", Close);
                    break;
            }
        }

        private void ShowOffers(OrderKind? focus = null)
        {
            if (Current.stage != Stage.Offered) { Show(); return; }
            Page(Current.OfferMessage, "Later", Close, "Decline…", Decline, rows: Current.offers,
                heading: "Weekly Contracts", preferredOffer: focus);
        }

        private void ReviewOffer(OrderKind kind)
        {
            if (Current.stage != Stage.Offered) { Show(); return; }
            var order = OrderTerms.Get(kind);
            var food = Kingdom.Material(kind);
            var name = Kingdom.Supported(food) ? food.GetName() : order.Slot.ToString();
            Page($"{Kingdom.Stock(food)} {name} available. Accepting costs nothing.",
                "Accept", () => { if (Current.Accept(kind)) Close(); else Show(); }, "Back", () => ShowOffers(kind),
                heading: "Contract Terms", summary: kind, nested: true);
        }

        private void Decline()
        {
            Page("Decline all contracts for this week? Your stock stays the same.",
                "Decline", () => { Current.Decide(false); Close(); }, "Keep Offer", Show, nested: true);
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
                }, "Back", Show, heading: "Confirm Delivery", summary: Current.selected, nested: true);
        }

        private void Reward(string preferred = null)
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
            var origin = Current;
            Close();
            var overlays = OverlayManager.Instance;
            var picker = overlays.GetMerchantScreen();
            picker.Init("Choose Your Relic");
            picker.SetText("Choose one relic as your contract reward.");
            overlays.SetCanCloseOverlay(true);
            var later = picker.SetCancel("Later").GetComponent<Button>();
            var choices = new System.Collections.Generic.List<Button>();
            Button preferredButton = null;
            int opened = Time.frameCount;
            waitForRelease = SubmitHeld();
            int column = 0;
            foreach (string id in origin.rewards)
            {
                var target = Kingdom.ClaimTarget(id);
                var relic = target ?? (string.IsNullOrEmpty(id) ? null : AddressablesManager.Instance.GetRelic(id));
                if (relic == null) continue;
                var native = picker.AddRelic(relic, checkIfCanAdd: false, isInShop: false).GetComponent<UICardRelic>();
                // The native keyword panel opens on the right. Keep the three card faces
                // clear of it without moving the shared merchant viewport or tooltip.
                foreach (RectTransform face in native.transform)
                    face.anchoredPosition += Vector2.left * (12f * column);
                column++;
                var button = native.GetCardButton();
                // The stock chest click grants its own reward. Route activation through the paid contract instead.
                button.onClick = new Button.ButtonClickedEvent();
                button.enabled = true;
                button.interactable = target != null;
                if (!button.interactable)
                {
                    native.GetCanvasGroup().alpha = .55f;
                    continue;
                }
                choices.Add(button);
                ContractCards.MarkFocusedRelic(button);
                if (id == preferred) preferredButton = button;
                string title = relic.GetTitle();
                string detail = relic.GetDescription();
                button.onClick.AddListener(() =>
                {
                    if (!button.isActiveAndEnabled || !button.interactable || Current != origin ||
                        Current.stage != Stage.Earned || Time.frameCount == opened || waitForRelease ||
                        GameManager.Instance.IsUnloading()) return;
                    Close();
                    if (Kingdom.ClaimTarget(id) == target) ConfirmReward(id, target, title, detail);
                    else Reward();
                });
            }
            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? choices[i - 1] : null,
                    selectOnRight = i + 1 < choices.Count ? choices[i + 1] : null,
                    selectOnDown = later };
            }
            var focus = preferredButton ?? choices.FirstOrDefault() ?? later;
            Navigate(later, focus, null, null);
            picker.Display();
            GamepadManager.Instance.SetGameObjectToSelect(focus.gameObject, true);
            EventSystem.current?.SetSelectedGameObject(focus.gameObject);
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
            }, "Back", () => Reward(id), heading: "Collect Relic", art: target.GetIcon(), nested: true);
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
            OrderKind[] rows = null, string heading = null, OrderKind? summary = null,
            Sprite art = null, OrderKind? preferredOffer = null, bool nested = false)
        {
            if (card != null && card.gameObject.activeInHierarchy)
                card.gameObject.SetActive(false);
            var next = OverlayManager.Instance.OverlayMessage(true);
            card = next;
            backToParent = nested;
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
                int index = preferredOffer.HasValue ? Array.IndexOf(rows, preferredOffer.Value) : -1;
                focus = choices[index >= 0 ? index : 0];
            }
            GamepadManager.Instance.SetGameObjectToSelect(focus.gameObject, true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
        }

        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.Cancel))]
        private static class ContractBack
        {
            private static bool Prefix(UIRelicDetails ___relicDetails, UIRename ___rename)
            {
                var owner = Instance;
                if (owner == null || !owner.backToParent || owner.card == null ||
                    !owner.card.gameObject.activeInHierarchy || ___relicDetails.IsActive() || ___rename.IsActive())
                    return true;
                owner.card.TriggerCancel();
                return false;
            }
        }

        private static void Navigate(Button button, Button up, Button down, Button side)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up,
                selectOnDown = down, selectOnLeft = side, selectOnRight = side };
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
