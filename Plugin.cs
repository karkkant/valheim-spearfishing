using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Spearfishing
{
    [BepInPlugin("org.bepinex.plugins.spearfishing", "Spearfishing", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony _harmony = new Harmony("org.bepinex.plugins.spearfishing");

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

                if (fish == null || fish.IsOutOfWater()) return true;

                if(fish.m_speed > 0f)
                {
                    fish.Stop(Time.fixedDeltaTime);
                    fish.m_speed = 0f;

                    var harpooned = false;

                    if(__instance.m_statusEffect.Equals("Harpooned"))
                    {
                        harpooned = fish.Pickup((Humanoid)__instance.m_owner);

                        if (harpooned)
                        {
                            __instance.m_owner.Message(MessageHud.MessageType.Center, fish.m_name + " $msg_harpoon_harpooned");
                        }
                    } 
                    
                    if(!harpooned)
                    {
                        var drop = fish.m_pickupItem.GetComponent<ItemDrop>();
                        drop.m_itemData.m_stack = fish.m_pickupItemStackSize;
                        drop.gameObject.AddComponent<Floating>();
                        UnityEngine.Object.Instantiate<ItemDrop>(drop, fish.GetTransform().position, Quaternion.identity);

                        ZNetView component = fish.GetComponent<ZNetView>();

                        if ((bool)(UnityEngine.Object)component && component.IsValid())
                        {
                            component.Destroy();
                        }
                    }
                }

                return false;
            }
        }
    }
}
