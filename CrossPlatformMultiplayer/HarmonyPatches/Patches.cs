using HarmonyLib;
using MasterServer;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/**
 * Patching method taken appreciatively from Beat Saber Multiplayer on 10/15/2020 by Moon
 * Other methods crafted for Cross Platform purposes
 */

namespace CrossPlatformMultiplayer.HarmonyPatches
{
    public static class Patches
    {
        public static void Patch()
        {
            var instance = new Harmony(typeof(Patches).FullName);

            Logger.Debug("Patching...");
            foreach (var type in Assembly.GetExecutingAssembly()
                            .GetTypes()
                            .Where(x => x.IsClass && x.Namespace == typeof(Patches).Namespace))
            {
                List<MethodInfo> patchedMethods = instance.CreateClassProcessor(type).Patch();
                if (patchedMethods != null && patchedMethods.Count > 0)
                {
                    foreach (var method in patchedMethods)
                    {
                        Logger.Debug($"Patched {method.DeclaringType}.{method.Name}!");
                    }
                }
            }
            Logger.Info("Applied patches!");
        }
    }

    [HarmonyPatch(typeof(NetworkConfigSO))]
    public class NetworkConfigSOPatch
    {
        [HarmonyPatch(MethodType.Getter)]
        [HarmonyPatch("masterServerEndPoint")]
        [HarmonyPatch(new Type[] { typeof(MasterServerEndPoint) })]
        static void Postfix(NetworkConfigSO __instance, ref MasterServerEndPoint __result)
        {
            __result = new MasterServerEndPoint("server1.networkauditor.org", 2328);
            Logger.Debug("Patched master server endpoint!");
        }
    }

    [HarmonyPatch(typeof(BaseClientMessageHandler))]
    public class BaseClientMessageHandlerPatch
    {
        [HarmonyPatch("VerifySignature")]
        [HarmonyPatch(new[] { typeof(byte[]), typeof(byte[]), typeof(byte[]), typeof(byte[]), typeof(byte[][]) })]
        static bool Prefix(BaseClientMessageHandler __instance, ref Task<bool> __result)
        {
            __result = Task.Run(() => true);
            Logger.Debug("Patched server signature verification!");
            return false;
        }
    }
}
