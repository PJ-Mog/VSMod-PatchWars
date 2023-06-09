using Vintagestory.API.Common;

namespace PatchWars {
  public class PrepatchSystem : NewModJsonPatchLoader {
    private static AssetCategory prepatchesCategory;

    public override double ExecuteOrder() => 0.03;

    protected override string FromFolder => "prepatches/";
    protected override string SystemName => "Json PrePatch Loader";

    public override void StartPre(ICoreAPI api) {
      base.StartPre(api);
      prepatchesCategory = new AssetCategory("prepatches", AffectsGameplay: true, EnumAppSide.Universal);
    }
  }
}
