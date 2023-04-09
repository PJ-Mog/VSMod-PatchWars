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
  }
}
