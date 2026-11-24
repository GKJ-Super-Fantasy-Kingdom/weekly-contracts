// Minimal in-memory implementations of the game calls used by the production
// inventory, relic and checkpoint adapters. No native Unity process or profile.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Reflection;

namespace SuperFantasyKingdom.Buildings
{
    internal enum BuildingType { None, Bakery }
    internal sealed class Building
    {
        public ResourceStorage Storage = new();
        public ResourceStorage GetStorage() => Storage;
    }
}

namespace SuperFantasyKingdom
{
    using Buildings;
    internal enum ResourceType { None, Water, Wood, Board, Stone, Wheat = 7, Bread = 9, Fish = 11, Coins, Alternate = 37 }
    internal enum Races { Human, Undead, None }
    internal sealed class ResourceData
    {
        public ResourceType type;
        public string DisplayName;
        public string GetName() => DisplayName ?? type.ToString();
        public BuildingType buildingToTakeFrom = BuildingType.Bakery;
    }
    internal record ResourceAmount(ResourceType type, int amount);
    internal sealed record ResourcePacket(int Id, ResourceType Type, int Amount, bool Reserved)
    {
        public int GetIdentifier() => Id;
        public int GetAmount() => Amount;
        public ResourceType GetResourceType() => Type;
        public bool IsReserved() => Reserved;
    }
    internal sealed class ResourceStorage
    {
        public readonly List<ResourcePacket> Packets = new();
        public bool UsesPackets = true;
        public int Loose;
        public int BlockAfter = int.MaxValue;
        private int serial = 100;
        public bool ShouldGenerateResourcePackets() => UsesPackets;
        public List<ResourcePacket> GetAllResourcePackets() => Packets;
        public int GetResourceQuantity(ResourceType t, bool capped) =>
            UsesPackets ? Packets.Where(p => p.Type == t).Sum(p => p.Amount) : Loose;
        public ResourcePacket TakeResourcePacket(int id, bool announce)
        {
            var p = Packets.Find(x => x.Id == id);
            if (BlockAfter-- <= 0 || p == null || p.Reserved) return null;
            Packets.Remove(p);
            return p;
        }
        public void AddResourcePacket(ResourcePacket p, bool announce) => Packets.Add(p);
        public void GenerateResource(ResourceType t, int n)
        {
            if (!UsesPackets) { Loose += n; return; }
            for (int i = 0; i < n; i++) Packets.Add(new ResourcePacket(++serial, t, 1, false));
        }
        public void RemoveResourceWithoutPackage(ResourceType t, int n) => Loose -= n;
    }
    internal sealed class RaceManager
    {
        public static RaceManager Instance = new();
        public ResourceData Food = new() { type = ResourceType.Bread };
        public Dictionary<ResourceType, ResourceData> Materials = new();
        public ResourceData GetResourceData(ResourceType t) => t == ResourceType.Bread || t == Food?.type ? Food : Materials.GetValueOrDefault(t);
        public Races GetRace() => Races.Human;
    }
    internal sealed class ResourceManager
    {
        public static ResourceManager Instance = new();
        public readonly Dictionary<ResourceType,int> Starting = new();
        public int Limit = int.MaxValue;
        public bool AllowSpend = true;
        public Action Changed, BeforeCredit, AfterCredit;
        public int Coins;
        public int GetCoins() => Coins;
        public void AddToCoins(int n) { BeforeCredit?.Invoke(); Coins += n; AfterCredit?.Invoke(); }
        public bool CanBeBuilt(ResourceAmount cost) => AllowSpend && GetResourceAvailableAmount(cost.type, true) >= cost.amount;
        public void ResourceAmountChanged(ResourceType type) => Changed?.Invoke();
        public Dictionary<ResourceType,int> GetStartingResourcesDictionary() => Starting;
        public int GetResourceAvailableAmount(ResourceType t, bool capped) => Math.Min(Limit,
            CityManager.Instance.Store?.Storage.GetResourceQuantity(t, capped) ?? Starting.GetValueOrDefault(t));
        public void RegisterStartingResource(ResourceAmount resource) =>
            Starting[resource.type] = Starting.GetValueOrDefault(resource.type) + resource.amount;
    }
    internal sealed class StatisticsManager
    {
        public static StatisticsManager Instance = new();
        public int Earned;
        public void AddCoins(int n) => Earned += n;
    }
    internal sealed class CityManager
    {
        public static CityManager Instance = new();
        public Building Store = new();
        public Building GetBuilding(BuildingType t) => t == BuildingType.None ? null : Store;
    }
    internal sealed class Relic
    {
        public string Id, Unit;
        public string name => Id;
        public Relic Upgrade;
        public bool Law, Racial, Generic;
        public Races Race = Races.None;
        public int Level;
        public string GetIdentifier() => Id;
        public Relic GetUpgrade() => Upgrade;
        public int GetRequiredKingdomLevel() => Level;
        public string GetRelatedUnit() => Unit;
        public bool IsLaw() => Law;
        public bool IsRacial() => Racial;
        public bool IsGeneric() => Generic;
        public Races GetKingdom() => Race;
    }
    internal sealed class RelicManager
    {
        public static RelicManager Instance = new();
        public HashSet<string> Unlocked = new();
        public Dictionary<string,Relic> Owned = new();
        public bool HasRelic(string id) => Owned.ContainsKey(id);
        public Relic GetRelic(string id) => Owned.GetValueOrDefault(id);
        public bool IsUnlocked(string id) => Unlocked.Contains(id);
        public bool CanAddRelic(string id) => HasRelic(id) ? Owned[id].Upgrade != null :
            AddressablesManager.Instance.GetRelic(id) != null;
        public Action BeforeGrant, Found;
        public Relic AddRelic(string id)
        {
            BeforeGrant?.Invoke();
            if (!CanAddRelic(id)) return null;
            var result = HasRelic(id) ? Owned[id].Upgrade : AddressablesManager.Instance.GetRelic(id);
            Owned.Remove(id);
            Owned[result.Id] = result;
            Found?.Invoke();
            return result;
        }
    }
    internal sealed class AddressablesManager
    {
        public static AddressablesManager Instance = new();
        public Dictionary<string,Relic> Definitions = new();
        public Relic GetRelic(string id) => Definitions.GetValueOrDefault(id);
        public Dictionary<string,Relic> GetAllRelics() => Definitions;
    }
    internal sealed class RaceDataManager
    {
        public static RaceDataManager Instance = new();
        public int GetKingdomLevel() => 3;
    }
    internal sealed class UnitManager
    {
        public static UnitManager Instance = new();
        public object GetUnit(string id) => null;
    }
    internal sealed class GameData { public string runID; public string hash; }
    internal sealed class SaveManager { public void Save() { } public void LoadGameData() { } }
    internal sealed class GameDataManager
    {
        public static GameDataManager Instance;
        public string RunId;
        public string GetRunID() => RunId;
        public void PositionWorldBuildings() { }
    }
    internal sealed class GameManager
    {
        public static GameManager Instance;
        public bool Loaded, Unloading;
        public bool IsGameLoaded() => Loaded;
        public bool IsUnloading() => Unloading;
    }
    internal sealed class DaytimeManager
    {
        public static DaytimeManager Instance;
        public int Day;
        public bool Night;
        public int CurrentDay() => Day;
        public bool IsNight() => Night;
    }
}
namespace SuperFantasyKingdom.Tavern { internal sealed class TavernSaveManager { } }
namespace MoreRelics
{
    internal sealed class Definition { public string id; public bool bossOnly; }
    internal sealed class Entry
    {
        public Definition Def { get; set; }
        public bool Available { get; set; } = true;
    }
    internal static class Registry { public static Entry[] Entries; }
}
namespace WeeklyContracts { internal static class Plugin { public static Contract Current, Restored; } }
namespace HarmonyLib
{
    internal sealed class HarmonyPatch : Attribute { public HarmonyPatch(params object[] args) { } }
    internal static class AccessTools
    {
        public static MethodInfo FirstMethod(Type t, Func<MethodInfo,bool> match) => t.GetMethods().First(match);
    }
}
namespace UnityEngine
{
    internal static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
        public static string ToJson(object value) => JsonSerializer.Serialize(value, Options);
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
    }
}
