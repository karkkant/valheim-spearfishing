using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Spearfishing
{
    [BepInPlugin("org.bepinex.plugins.spearfishing", "Spearfishing", "1.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony _harmony = new Harmony("org.bepinex.plugins.spearfishing");
        private static readonly int _SE_harpooned_hash = "Harpooned".GetStableHashCode();

        private void Awake()
        {
            _harmony.PatchAll();
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

                if(fish == null || fish.IsOutOfWater()) return true;

                if(fish.m_speed > 0f)
                {
                    fish.Stop(Time.fixedDeltaTime);
                    fish.m_speed = 0f;

                    var harpooned = false;

                    if(__instance.m_statusEffectHash == _SE_harpooned_hash)
                    {
                        harpooned = fish.Pickup((Humanoid)__instance.m_owner);

                        if (harpooned)
                        {
                            __instance.m_owner.Message(MessageHud.MessageType.Center, fish.m_name + " $msg_harpoon_harpooned");
                            
                             var prefab = ZNetScene.instance.GetPrefab("fx_deatsquito_death");

                            if ((bool)prefab)
                                Instantiate(prefab, fish.transform.position, Quaternion.identity);
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

                        var prefab = ZNetScene.instance.GetPrefab("fx_deatsquito_death");

                        if ((bool)prefab)
                            Instantiate(prefab, drop.transform.position, Quaternion.identity);

                        drop.transform.Rotate(new Vector3(0, 0, Random.Range(-100, 100) > 0 ? 90 : -90));
                    }
                }

                return false;
            }
        }
    }
}
