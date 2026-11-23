using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

namespace WeeklyContracts
{
    // The extra JSON member travels with the actual kingdom checkpoint, including backup copies.
    // Unity ignores unknown members while reconstructing the game's own GameData.
    internal static class SaveHooks
    {
        [Serializable]
        private sealed class Envelope
        {
#pragma warning disable CS0649
            public string runID;
            public Contract sfkWeeklyContracts;
#pragma warning restore CS0649
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save), typeof(string), typeof(string))]
        internal static class Save
        {
            private static void Prefix(string __0, ref string __1)
            {
                var contract = Plugin.Current ?? Plugin.Restored;
                if (contract == null || !__0.EndsWith("_game", StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(__1) || __1[__1.Length - 1] != '}') return;
                if (JsonUtility.FromJson<Envelope>(__1).runID != contract.run) return;
                __1 = __1.Substring(0, __1.Length - 1) + ",\"sfkWeeklyContracts\":" +
                    JsonUtility.ToJson(contract) + "}";
            }
        }

        [HarmonyPatch(typeof(GameDataManager), "PositionWorldBuildings")]
        internal static class Read
        {
            // Observe exactly the same primary/backup JSON the game actually deserializes.
            // Delegate the native parse so other fields and the game's hash check are unchanged.
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo wrapper = AccessTools.Method(typeof(Read), nameof(Parse));
                bool found = false;
                foreach (var instruction in instructions)
                {
                    if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(JsonUtility) &&
                        method.Name == "FromJson" && method.ReturnType == typeof(GameData))
                    {
                        instruction.operand = wrapper;
                        found = true;
                    }
                    yield return instruction;
                }
                if (!found) throw new MissingMethodException("Kingdom checkpoint reader changed.");
            }

            private static GameData Parse(string json)
            {
                var game = JsonUtility.FromJson<GameData>(json);
                var saved = JsonUtility.FromJson<Envelope>(json).sfkWeeklyContracts;
                Plugin.Restored = saved != null && saved.run == game.runID ? saved : null;
                return game;
            }
        }
    }
}
