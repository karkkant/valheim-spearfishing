using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Spearfishing
{
    [BepInPlugin("org.bepinex.plugins.spearfishing", "Spearfishing", "1.0.3")]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony _harmony = new Harmony("org.bepinex.plugins.spearfishing");
        private static readonly int _SE_harpooned_hash = "Harpooned".GetStableHashCode();
        private static ConfigEntry<bool> EnableHarpoons;
        private static ConfigEntry<bool> EnableSpears;
        private static ConfigEntry<bool> EnableBows;
        private static ConfigEntry<bool> EnableCrossbows;

        private void Awake()
        {
            EnableHarpoons = Config.Bind("General", "EnableHarpoons", true, "Enable fishing with harpoon");
            EnableSpears = Config.Bind("General", "EnableSpears", true, "Enable fishing with spears");
            EnableBows = Config.Bind("General", "EnableBows", true, "Enable fishing with bows");
            EnableCrossbows = Config.Bind("General", "EnableCrossbows", true, "Enable fishing with crossbows");
            _harmony.PatchAll();
        }

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
