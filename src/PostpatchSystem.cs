using Vintagestory.API.Common;

namespace PatchWars {
  public class PostpatchSystem : NewModJsonPatchLoader {
    private static AssetCategory postpatchesCategory;

    public override double ExecuteOrder() => base.ExecuteOrder() + 0.00001;

    protected override string FromFolder => "postpatches/";
    protected override string SystemName => "Json PostPatch Loader";

    public override void StartPre(ICoreAPI api) {
      base.StartPre(api);
      postpatchesCategory = new AssetCategory("postpatches", AffectsGameplay: true, EnumAppSide.Universal);
    }
  }
}
