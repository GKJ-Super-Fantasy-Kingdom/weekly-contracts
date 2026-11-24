using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace WeeklyContracts
{
    // MoreRelics 1.1.0 keeps its boss-only flag on the source entry, outside
    // the game's Relic object. Ordinary native relics continue using native rules.
    internal static class RewardPolicy
    {
        private static readonly Dictionary<string, Func<bool>> rules = new Dictionary<string, Func<bool>>();

        internal static void Bind(Assembly assembly, Action<string> warn)
        {
            rules.Clear();
            if (assembly == null) return;
            try
            {
                const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                var registry = assembly.GetType("MoreRelics.Registry", true);
                var entries = registry.GetField("Entries", members)?.GetValue(null) as IEnumerable;
                if (entries == null) throw new MissingFieldException("Registry.Entries");
                foreach (var entry in entries)
                {
                    var type = entry.GetType();
                    var definition = type.GetProperty("Def");
                    var def = definition?.GetValue(entry);
                    var id = def?.GetType().GetField("id")?.GetValue(def) as string;
                    var bossOnly = def?.GetType().GetField("bossOnly");
                    var available = type.GetProperty("Available");
                    if (string.IsNullOrEmpty(id) || bossOnly?.FieldType != typeof(bool) ||
                        available?.PropertyType != typeof(bool)) throw new MissingMemberException(type.FullName);
                    bool reported = false;
                    rules[id] = () =>
                    {
                        try { return !(bool)bossOnly.GetValue(definition.GetValue(entry)) && (bool)available.GetValue(entry); }
                        catch (Exception error)
                        {
                            if (!reported) warn($"Cannot check {id}. Skipping this reward. {error.GetBaseException().Message}");
                            reported = true;
                            return false;
                        }
                    };
                }
            }
            catch (Exception error)
            {
                rules.Clear();
                warn("MoreRelics metadata unavailable. Using native reward rules. " + error.Message);
            }
        }

        internal static bool Allows(string id) => !rules.TryGetValue(id, out var allowed) || allowed();
    }
}
