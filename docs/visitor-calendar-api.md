# Weekly Contracts calendar API

Available since Weekly Contracts 0.1.2. The BepInEx plugin ID is `sfk.weeklycontracts`. Visitor Calendar can discover the public static method below without referencing WeeklyContracts.dll at build time.

```csharp
WeeklyContracts.CalendarApi.TryGetActiveDelivery(
    out string title, out int quantity, out string resourceName, out int deadlineDay)
```

The method returns a `bool`. Call it on the Unity main thread when opening or refreshing the calendar. A successful call gives the accepted contract's title, its full delivery quantity, the current kingdom's display name for the resource, and the saved **absolute run day** at whose nightfall the job expires. For example, a job due on day 14 is still active during the day on day 14. If a resource definition is unavailable, the name falls back to the contract's resource slot, as on its details card. The call reads the current state and does not accept, deliver, collect, advance the clock, or modify the save.

A false result clears both strings to null and both numbers to zero. Remove any previously displayed deadline on false. A job appears after acceptance and disappears immediately after successful delivery, including while the reward is waiting for collection. Offers, declined or expired jobs, and claims or refunds have no delivery deadline to display. Unloaded, unloading and foreign runs also return false. Continue and checkpoint rollback use the state of the loaded checkpoint, so an earlier accepted-but-unpaid job can reappear. Read the query again after loading and on calendar refresh. Cache only the bound delegate.

## Optional binding sample

```csharp
using System;
using System.Reflection;
using BepInEx.Bootstrap;

internal delegate bool ReadDelivery(out string title, out int quantity,
    out string resourceName, out int deadlineDay);

internal static class WeeklyContractsBridge
{
    internal static ReadDelivery Bind()
    {
        if (!Chainloader.PluginInfos.TryGetValue("sfk.weeklycontracts", out var info) ||
            info.Instance == null) return null;
        var api = info.Instance.GetType().Assembly.GetType("WeeklyContracts.CalendarApi");
        var text = typeof(string).MakeByRefType();
        var number = typeof(int).MakeByRefType();
        var method = api?.GetMethod("TryGetActiveDelivery", BindingFlags.Public | BindingFlags.Static,
            null, new[] { text, number, text, number }, null);
        return method?.ReturnType == typeof(bool)
            ? (ReadDelivery)Delegate.CreateDelegate(typeof(ReadDelivery), method)
            : null;
    }
}
```

Bind after BepInEx has initialized plugins, for example in `Start`. A missing plugin or an older build yields a null delegate. Keep the optional integration out of the consumer's Thunderstore dependencies.

```csharp
if (readDelivery != null &&
    readDelivery(out var title, out var quantity, out var resourceName, out var deadlineDay))
{
    // Display title and quantity/resourceName on deadlineDay.
}
```
