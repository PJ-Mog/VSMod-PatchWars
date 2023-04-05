using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.ServerMods.NoObf;

namespace PatchWars {
  [HarmonyPatch]
  internal class ModJsonPatchLoaderPatch : ModSystem {
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
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> AssetsLoaded_Transpiler(IEnumerable<CodeInstruction> instructions) {
      return instructions;
    }
  }
}
