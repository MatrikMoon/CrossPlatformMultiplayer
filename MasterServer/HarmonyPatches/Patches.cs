using HarmonyLib;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/**
 * Patching method taken appreciatively from Beat Saber Multiplayer on 10/15/2020 by Moon
 * Other methods crafted for Cross Platform purposes
 */

namespace MasterServer.HarmonyPatches
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

    //PATCH INFO: MasterServerMessageHandler needs to change some of what this method does,
    //but we cannot, since we inhereit from MessageHandler, and the method is called in the constructor.
    //So, we'll just disable it, and do it ourselves again later
    [HarmonyPatch(typeof(MessageHandler))]
    public class MessageHandlerPatch
    {
        [HarmonyPatch("RegisterHandshakeMessageHandlers")]
        [HarmonyPatch(new Type[] { })]
        static bool Prefix()
        {
            Logger.Debug("Patched MessageHandler RegisterHandshakeCallbacks!");
            return false;
        }
    }
}
