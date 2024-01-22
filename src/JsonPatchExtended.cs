using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace PatchWars {
  public class JsonPatchExtended : Vintagestory.ServerMods.NoObf.JsonPatch {
    public string PathAppend;
    public bool PatchMultiple = false;

    public virtual AssetLocation SourceFileForLogging { get; set; }
    public virtual int IndexForLogging { get; set; }

    public override string ToString() {
      return JsonConvert.SerializeObject(this);
    }

    public JsonPatchExtended ShallowClone() {
      return new JsonPatchExtended {
        Op = Op,
        File = File,
        FromPath = FromPath,
        Path = Path,
        DependsOn = DependsOn,
        Enabled = Enabled,
        Side = Side,
        Condition = Condition,
        Value = Value,

        PathAppend = PathAppend,
        PatchMultiple = PatchMultiple,
        SourceFileForLogging = SourceFileForLogging,
        IndexForLogging = IndexForLogging
      };
    }
  }
}
