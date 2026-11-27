using System;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Tavern;
using UnityEngine;

namespace WeeklyContracts
{
    internal static class SaveHooks
    {
        [Serializable]
        private sealed class Envelope
        {
#pragma warning disable CS0649
            public string runID;
            public string hash;
            public int coins;
            public string[] relics;
            public Contract sfkWeeklyContracts;
#pragma warning restore CS0649
        }

        private static Envelope selected;
        private static bool reading;

        internal static void BeginRead()
        {
            selected = null;
            Plugin.Current = Plugin.Restored = null;
            reading = true;
        }

        internal static void Observe(string path, string json)
        {
            if (!reading || path == null || !path.EndsWith("_game", StringComparison.Ordinal)) return;
            selected = null;
            if (!string.IsNullOrEmpty(json) && json.Contains("\"sfkWeeklyContracts\""))
            {
                // The player build reads the scalar fields with JsonUtility but drops
                // this nested mod-defined record when loading the tavern checkpoint.
                try { selected = JsonConvert.DeserializeObject<Envelope>(json); }
                catch (JsonException) { /* The game can try its backup next. */ }
            }
        }

        internal static void Restore(GameData game)
        {
            var entry = selected;
            if (game != null && entry != null && entry.runID == game.runID &&
                entry.hash == game.hash && entry.sfkWeeklyContracts?.run == game.runID)
                Plugin.Restored = entry.sfkWeeklyContracts;
        }

        internal static void EndRead()
        {
            reading = false;
            selected = null;
        }

        [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.PositionWorldBuildings))]
        internal static class Read
        {
            private static void Prefix() => BeginRead();
            private static void Postfix(GameData ___m_GameData) => Restore(___m_GameData);
            private static void Finalizer() => EndRead();
        }

        [HarmonyPatch(typeof(TavernSaveManager), "Load")]
        internal static class Tavern
        {
            private static void Prefix() => BeginRead();
            private static void Finalizer() => EndRead();
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadGameData))]
        internal static class TavernData
        {
            // The tavern clears the hash after this method returns.
            private static void Postfix(GameData __result)
            {
                if (reading) Restore(__result);
            }
        }

        [HarmonyPatch]
        internal static class Raw
        {
            private static MethodBase TargetMethod() => AccessTools.FirstMethod(typeof(SaveManager),
                m => m.Name == "Load" && !m.IsGenericMethod && m.ReturnType == typeof(string));
            private static void Postfix(string __0, string __result) => Observe(__0, __result);
        }

        [HarmonyPatch]
        internal static class Save
        {
            private static MethodBase TargetMethod() => ResolveTarget(typeof(SaveManager));

            // Both 0.8.070 writers use this overload. doBackUp controls file rotation,
            // so the same checkpoint payload is needed for either value.
            internal static MethodInfo ResolveTarget(Type manager) =>
                AccessTools.DeclaredMethod(manager, nameof(SaveManager.Save), new[] { typeof(string), typeof(string), typeof(bool) }) ??
                AccessTools.DeclaredMethod(manager, nameof(SaveManager.Save), new[] { typeof(string), typeof(string) });

            internal static void Prefix(string __0, ref string __1)
            {
                var contract = Plugin.Current ?? Plugin.Restored;
                if (contract == null || __0 == null || !__0.EndsWith("_game", StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(__1)) return;
                int end = __1.LastIndexOf('}');
                if (end < 0) return;
                var checkpoint = JsonUtility.FromJson<Envelope>(__1);
                if (checkpoint.runID != contract.run) return;
                contract = contract.ForCheckpoint(checkpoint.coins, checkpoint.relics);
                __1 = __1.Insert(end, ",\"sfkWeeklyContracts\":" + JsonUtility.ToJson(contract));
            }
        }
    }
}
