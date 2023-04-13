using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.ServerMods.NoObf;

namespace PatchWars {
  public class JsonPatchExtended : JsonPatch {
    public string PathAppend;
    public bool PatchMultiple = false;

    public virtual AssetLocation SourceFileForLogging { get; set; }
    public virtual int IndexForLogging { get; set; }

    public override string ToString() {
      return JsonConvert.SerializeObject(this);
    }

    public JsonPatchExtended ShallowClone() {
      return new JsonPatchExtended {
        Op = this.Op,
        File = this.File,
        FromPath = this.FromPath,
        Path = this.Path,
        DependsOn = this.DependsOn,
        Enabled = this.Enabled,
        Side = this.Side,
        Condition = this.Condition,
        Value = this.Value,

        PathAppend = this.PathAppend,
        PatchMultiple = this.PatchMultiple,
        SourceFileForLogging = this.SourceFileForLogging,
        IndexForLogging = this.IndexForLogging
      };
    }
  }
}
