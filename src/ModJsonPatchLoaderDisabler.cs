using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.ServerMods.NoObf;

namespace PatchWars {
  [HarmonyPatch]
  internal class ModJsonPatchLoaderDisabler : ModSystem {
    public override double ExecuteOrder() {
      return 0.001;
    }

    public override void StartPre(ICoreAPI api) {
      base.StartPre(api);
      new Harmony(Mod.Info.ModID).PatchAll(Assembly.GetExecutingAssembly());
    }

    public override void Dispose() {
      base.Dispose();
      new Harmony(Mod.Info.ModID).UnpatchAll(Mod.Info.ModID);
    }

    [HarmonyPatch(typeof(ModJsonPatchLoader), "AssetsLoaded")]
    [HarmonyPrefix]
    private static bool ShouldRunVanillaJsonPatcher(ICoreAPI api) {
      api.Logger.Notification("Patch Wars has disabled the vanilla Json Patch Loader.");
      return false;
    }
  }
}
