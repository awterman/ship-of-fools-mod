using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PrefabEntities;
using static Market;

namespace MarketRefresh
{
    //插件描述特性 分别为 插件ID 插件名字 插件版本(必须为数字)
    [BepInPlugin("me.z.plugin.MarketRefresh", "MarketRefresh", "1.0")]
    public class MarketRefresh : BaseUnityPlugin //继承BaseUnityPlugin
    {
        //Unity的Start生命周期
        void Awake()
        {
            //输出日志
            Logger.LogInfo("Hello from MarketRefresh!");
            var harmony = new Harmony("me.z.plugin.MarketRefresh");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Market), "Populate")]
    public class MarketPatch
    {
        static bool Prefix(Market __instance)
        {
            var logSource = Logger.CreateLogSource("MarketRefresh");
            logSource.LogInfo("Hello from Prefix");

            // 获取需要的私有成员
            var unlockableItemSetsField = typeof(Market).GetField("unlockableItemSets", BindingFlags.NonPublic | BindingFlags.Instance);
            var hardModeUnlockableItemsField = typeof(Market).GetField("hardModeUnlockableItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var showcasesField = typeof(Market).GetField("showcases", BindingFlags.NonPublic | BindingFlags.Instance);
            var gameStateProperty = typeof(Market).GetProperty("gameState", BindingFlags.NonPublic | BindingFlags.Instance);
            var populateItemsMethod = typeof(Market).GetMethod("PopulateItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var getUnlockableItemPriceMethod = typeof(Market).GetMethod("GetUnlockableItemPrice", BindingFlags.NonPublic | BindingFlags.Instance);
            var poolField = typeof(Market).GetField("pool", BindingFlags.NonPublic | BindingFlags.Instance);

            logSource.LogInfo("Reflection types initialized");

            var unlockableItemSets = (IEnumerable<UnlockableItemsSet>)unlockableItemSetsField.GetValue(__instance);
            logSource.LogInfo("unlockableItemSets got");
            var hardModeUnlockableItems = (IEnumerable<HardModeUnlockableItemsSet>)hardModeUnlockableItemsField.GetValue(__instance);
            logSource.LogInfo("hardModeUnlockableItems got");
            var showcases = (ShopShowcase[])showcasesField.GetValue(__instance);
            logSource.LogInfo("showcases got");
            //var gameState = gameStateProperty.GetValue(__instance);
            logSource.LogInfo("gameState got");

            var pool = (Pool)poolField.GetValue(__instance);
            logSource.LogInfo("pool got");

            logSource.LogInfo("Reflection values initialized");

            // 判断所有物品是否都已解锁
            /*
            bool allItemsUnlocked = unlockableItemSets
                .SelectMany(set => set.unlockableItems)
                .Concat(hardModeUnlockableItems
                    .SelectMany(set => set.unlockableItems))
                .All(item => (bool)gameState.GetType().GetMethod("HasUnlocked").Invoke(gameState, new object[] { item.GetComponent<PrefabID>(), null }));
            */

            if (true)
            {
                PrefabEntity[] randomsBy = pool.GetRandomsBy(showcases.Length, new Availability[]
                {
                    Availability.Shop
                }, new PrefabEntities.Type[]
                {
                    PrefabEntities.Type.Item
                }, null, null, Pool.IncludeRemoved.No, false, true, false, true, null);

                Func<Item, int> getPriceFunc = item => (int)getUnlockableItemPriceMethod.Invoke(__instance, new object[] { item });
                Func<Item, Currency> getCurrencyFunc = (_) => Currency.Shards;
                Action<Item> removeFunc = (item) =>
                {
                    PrefabID prefabID;
                    if (item && item.TryGetComponent<PrefabID>(out prefabID))
                    {
                        PrefabEntity byPrefabID = pool.GetByPrefabID(prefabID);
                        if (byPrefabID)
                        {
                            byPrefabID.Removed = true;
                        }
                    }
                };

                var allEntities = pool.GetAllBy(new Availability[]
                {
                    Availability.Shop
                }, new PrefabEntities.Type[]
                {
                    PrefabEntities.Type.Item
                }, null, null, Pool.IncludeRemoved.No, false);

                var allItems = (from entity in allEntities
                    select entity.prefab.GetComponent<Item>()).ToArray<Item>();

                Item fishingNet = null;
                foreach(var item in allItems )
                {
                    if (item.itemDescription.name == "FishingNet")
                    {
                        fishingNet = item;
                    }
                    logSource.LogInfo(item.itemDescription.name);
                }

                var items = (from entity in randomsBy
                    select entity.prefab.GetComponent<Item>()).ToArray<Item>();


                bool found = false;
                foreach( var item in items )
                {
                    if (item.itemDescription.name == "FishingNet")
                    { found = true; break; }
                }

                if (!found)
                {
                    items[0] = fishingNet;
                }

                populateItemsMethod.Invoke(__instance, new object[] { showcases, new ShopShowcase[0], items, getPriceFunc, getCurrencyFunc, ShopActionName.Buy, true, null, removeFunc });

                return false;
            }

            // 如果有物品未解锁，则让原始方法执行
            return true;
        }
    }
}
