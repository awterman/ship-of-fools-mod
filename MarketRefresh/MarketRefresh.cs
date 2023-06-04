using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using PrefabEntities;
using Random = UnityEngine.Random;
using Type = PrefabEntities.Type;

namespace MarketRefresh
{
    [BepInPlugin("me.z.plugin.MarketRefresh", "MarketRefresh", "1.0")]
    public class MarketRefresh : BaseUnityPlugin
    {
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
        [UsedImplicitly]
        // ReSharper disable once InconsistentNaming
        static bool Prefix(Market __instance)
        {
            var config = new ConfigFile(Path.Combine(Paths.ConfigPath, "MarketRefresh.cfg"), true);

            var predefinedItemNames = config.Bind("MarketRefresh", "PredefinedItems", "FishingNet", "预定义的物品列表")
                .Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            
            var market = Traverse.Create(__instance);
            var pool = market.Field("pool").GetValue<Pool>();
            var showcases = market.Field("showcases").GetValue<ShopShowcase[]>();
            
            var allEntities = pool.GetAllBy(new[] { Availability.Shop }, new[] { Type.Item });
            var allItems = allEntities.Select(entity => entity.prefab.GetComponent<Item>()).ToArray();
            var predefinedItems = allItems.Where(item => predefinedItemNames.Contains(item.itemDescription.name)).ToArray();
            
            var getUnlockableItemPriceMethod = typeof(Market).GetMethod("GetUnlockableItemPrice", BindingFlags.NonPublic | BindingFlags.Instance);
            var populateItemsMethod = typeof(Market).GetMethod("PopulateItems", BindingFlags.NonPublic | BindingFlags.Instance);
            
            var items = new Item[showcases.Length];
            // ensure that predefined items are in the first showcases
            for (var i = 0; i < predefinedItems.Length && i < items.Length; i++)
            {
                items[i] = predefinedItems[i];
            }
            
            // fill the rest of the showcases with random items
            for (var i = predefinedItems.Length; i < items.Length; i++)
            {
                var randomItem = allItems[Random.Range(0, allItems.Length)];
                while (items.Contains(randomItem))
                {
                    randomItem = allItems[Random.Range(0, allItems.Length)];
                }
                items[i] = randomItem;
            }
            
            Func<Item, int> getPriceFunc = item => (int)getUnlockableItemPriceMethod.Invoke(__instance, new object[] { item });
            Func<Item, Currency> getCurrencyFunc = _ => Currency.Shards;
            Action<Item> removeFunc = item =>
            {
                PrefabID prefabID;
                if (item && item.TryGetComponent(out prefabID))
                {
                    PrefabEntity byPrefabID = pool.GetByPrefabID(prefabID);
                    if (byPrefabID)
                    {
                        byPrefabID.Removed = true;
                    }
                }
            };
            
            // populate the market 
            populateItemsMethod.Invoke(__instance, new object[] { showcases, new ShopShowcase[0], items, getPriceFunc, getCurrencyFunc, ShopActionName.Buy, true, null, removeFunc });
            // market.Method("PopulateItems", showcases, new ShopShowcase[0], items, getPriceFunc, getCurrencyFunc, ShopActionName.Buy, true, null, removeFunc).GetValue();

            return false;
        }
    }
}