using CrossPlatformMultiplayer.HarmonyPatches;
using IPA;
using Shared;

namespace CrossPlatformMultiplayer
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public string Name => SharedConstructs.Name;
        public string Version => SharedConstructs.Version;

        [OnEnable]
        public void OnEnable()
        {
            Patches.Patch();
        }
    }
}
