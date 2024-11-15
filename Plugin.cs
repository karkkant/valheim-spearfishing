using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace Spearfishing
{
    [BepInPlugin(pluginId, pluginName, pluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        const string pluginId = "org.bepinex.plugins.spearfishing";
        const string pluginName = "Spearfishing";
        const string pluginVersion = "1.0.4";

        private readonly Harmony _harmony = new Harmony(pluginId);
        private static readonly int _SE_harpooned_hash = "Harpooned".GetStableHashCode();

        ConfigSync configSync = new ConfigSync(pluginId) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };
        private static ConfigEntry<bool> LockConfig;
        private static ConfigEntry<bool> EnableHarpoons;
        private static ConfigEntry<bool> EnableSpears;
        private static ConfigEntry<bool> EnableBows;
        private static ConfigEntry<bool> EnableCrossbows;

        private void Awake()
        {
            LockConfig = config("General", "LockConfig", true, "If on, the configuration is locked and can be changed by server admins only. [Synced with server]");
            EnableHarpoons = config("General", "EnableHarpoons", true, "Enable fishing with harpoon");
            EnableSpears = config("General", "EnableSpears", true, "Enable fishing with spears");
            EnableBows = config("General", "EnableBows", true, "Enable fishing with bows");
            EnableCrossbows = config("General", "EnableCrossbows", true, "Enable fishing with crossbows");

            _ = configSync.AddLockingConfigEntry(LockConfig);
            _harmony.PatchAll();
        }

        #region ServerSync

        ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

        #endregion

        public static bool IsEnabledProjectile(string projectile)
        {
            return projectile switch
            {
                string when projectile.Contains("projectile_chitinharpoon") => EnableHarpoons.Value,
                string when projectile.Contains("bow_projectile") => EnableBows.Value,
                string when projectile.Contains("spear_projectile") => EnableSpears.Value,
                string when projectile.Contains("projectile_splitner") => EnableSpears.Value,
                string when projectile.Contains("projectile_wolffang") => EnableSpears.Value,
                string when projectile.Contains("arbalest_projectile") => EnableCrossbows.Value,
                _ => false
            };
        }

        [HarmonyPatch(typeof(Projectile))]
        class ProjectilePatch
        {
            [HarmonyPatch("OnHit")]
            [HarmonyPrefix]
            private static bool Spearfishing(Projectile __instance, Collider collider)
            {
                GameObject go = (bool)(UnityEngine.Object)collider ? Projectile.FindHitObject(collider) : (GameObject)null;
                Fish fish = go?.GetComponent<Fish>();

                if(fish == null || fish.IsOutOfWater() || !IsEnabledProjectile(__instance.name.ToLower())) return true;

                if(fish.m_speed > 0f)
                {
                    fish.Stop(Time.fixedDeltaTime);
                    fish.m_speed = 0f;
                    fish.m_acceleration = 0f;
                    fish.m_turnRate = 0f;

                    var harpooned = false;

                    if(__instance.m_statusEffectHash == _SE_harpooned_hash)
                    {
                        harpooned = fish.Pickup((Humanoid)__instance.m_owner);

                        if (harpooned)
                        {
                            __instance.m_owner.Message(MessageHud.MessageType.Center, fish.m_name + " $msg_harpoon_harpooned");
                            var prefab = ZNetScene.instance.GetPrefab("fx_TickBloodHit");

                            if ((bool)prefab)
                            {
                                Instantiate(prefab, fish.transform.position, Quaternion.identity);
                            }
                        }
                    }
                    
                    if(!harpooned)
                    {
                        var drop = fish.m_pickupItem.GetComponent<ItemDrop>();
                        var floater = drop.gameObject.AddComponent<Floating>();

                        drop.m_nview.ClaimOwnership();

                        floater.m_waterLevelOffset = 0.65f;
                        floater.SetLiquidLevel(fish.GetWaterLevel(fish.transform.position), LiquidType.Water, null);
                        drop.m_itemData.m_stack = fish.m_pickupItemStackSize;
                        drop.m_floating = floater;

                        DestroyImmediate(fish);

                        __instance.m_hitEffects.Create(drop.transform.position, Quaternion.identity, null, 2, -1);
                        var prefab = ZNetScene.instance.GetPrefab("fx_TickBloodHit");

                        if ((bool)prefab)
                        {
                            Instantiate(prefab, drop.transform.position, Quaternion.identity);
                        }

                        drop.transform.Rotate(new Vector3(0, 0, Random.Range(-100, 100) > 0 ? 90 : -90));
                    }
                }

                return false;
            }
        }
    }
}
