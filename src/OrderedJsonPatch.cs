using Newtonsoft.Json;
using Vintagestory.ServerMods.NoObf;

namespace PatchWars {
  public class OrderedJsonPatch : JsonPatch {
    public double Order = 1.0;
    public string JsonPath;
    public string JsonPathAppend;
    public bool PatchMultiple = false;

    public override string ToString() {
      return JsonConvert.SerializeObject(this);
    }
  }
}
