using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Tavis;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.ServerMods.NoObf;

namespace PatchWars {
  [HarmonyPatch]
  public class PrepatchSystem : ModJsonPatchLoader {
    private static AssetCategory prepatchesCategory;

    public override double ExecuteOrder() {
      return 0.03;
    }

    public override void StartPre(ICoreAPI api) {
      base.StartPre(api);
      prepatchesCategory = new AssetCategory("prepatches", AffectsGameplay: true, EnumAppSide.Universal);
    }

    public override void AssetsLoaded(ICoreAPI api) {
      var worldConfig = api.World.Config;
      if (worldConfig == null) {
        worldConfig = new TreeAttribute();
      }

      List<OrderedJsonPatch> prepatches = new List<OrderedJsonPatch>();
      foreach (var asset in api.Assets.GetMany("prepatches/")) {
        try {
          prepatches.AddRange(asset.ToObject<OrderedJsonPatch[]>());

        }
        catch (Exception e) {
          api.Logger.Error("Failed loading prepatches file {0}: {1}", asset.Location, e);
          continue;
        }
      }
      int appliedCount = 0;
      int notfoundCount = 0;
      int errorCount = 0;
      int totalCount = 0;
      int unmetConditionCount = 0;
      HashSet<string> loadedModIds = new HashSet<string>(api.ModLoader.Mods.Select((Mod m) => m.Info.ModID).ToList());
      foreach (var prepatch in prepatches.OrderBy(patch => patch.Order)) {
        if (!prepatch.Enabled) {
          continue;
        }

        if (prepatch.Condition != null) {
          IAttribute attr = worldConfig[prepatch.Condition.When];
          if (attr == null) {
            api.Logger.VerboseDebug("Prepatch {0} Condition not found in WorldConfig.", prepatch);
            continue;
          }
          if (prepatch.Condition.useValue) {
            prepatch.Value = new JsonObject(JToken.Parse(attr.ToJsonToken()));
          }
          else if (!prepatch.Condition.IsValue.Equals(attr.GetValue()?.ToString() ?? "", StringComparison.InvariantCultureIgnoreCase)) {
            api.Logger.VerboseDebug("Prepatch {0}: Unmet IsValue condition (actual: {1})", prepatch, attr.GetValue()?.ToString() ?? "");
            unmetConditionCount++;
            continue;
          }
        }

        if (prepatch.DependsOn != null) {
          bool enabled = true;
          PatchModDependence[] dependsOn = prepatch.DependsOn;
          foreach (PatchModDependence dependence in dependsOn) {
            bool loaded = loadedModIds.Contains(dependence.modid);
            enabled = enabled && (loaded ^ dependence.invert);
          }
          if (!enabled) {
            unmetConditionCount++;
            api.Logger.VerboseDebug("Prepatch {0}: Unmet DependsOn condition.", prepatch);
            continue;
          }
        }
        totalCount++;
        ApplyPatch(api, prepatch, ref appliedCount, ref notfoundCount, ref errorCount);
      }

      StringBuilder sb = new StringBuilder();
      sb.Append("JsonPrepatch Loader: ");
      if (totalCount == 0) {
        sb.Append(Lang.Get("Nothing to prepatch", totalCount));
      }
      else {
        sb.Append(Lang.Get("{0} prepatches total", totalCount));
        if (appliedCount > 0) {
          sb.Append(Lang.Get(", successfully applied {0} prepatches", appliedCount));
        }
        if (notfoundCount > 0) {
          sb.Append(Lang.Get(", missing files on {0} prepatches", notfoundCount));
        }
        if (unmetConditionCount > 0) {
          sb.Append(Lang.Get(", unmet conditions on {0} prepatches", unmetConditionCount));
        }
        if (errorCount > 0) {
          sb.Append(Lang.Get(", had errors on {0} prepatches", errorCount));
        }
        else {
          sb.Append(Lang.Get(", no errors", errorCount));
        }
      }
      api.Logger.Notification(sb.ToString());
      api.Logger.VerboseDebug("Prepatchloader finished");
    }

    public void ApplyPatch(ICoreAPI api, OrderedJsonPatch jsonPatch, ref int applied, ref int notFound, ref int errorCount) {
      if (((!jsonPatch.Side.HasValue) ? jsonPatch.File.Category.SideType : jsonPatch.Side.Value) != EnumAppSide.Universal && jsonPatch.Side != api.Side) {
        return;
      }
      if (jsonPatch.File == null) {
        api.World.Logger.Error("Prepatch {0} failed because it is missing the target file property", jsonPatch);
        return;
      }
      AssetLocation loc = jsonPatch.File.Clone();
      if (jsonPatch.File.Path.EndsWith("*")) {
        foreach (IAsset val in api.Assets.GetMany(jsonPatch.File.Path.TrimEnd('*'), jsonPatch.File.Domain, loadAsset: false)) {
          jsonPatch.File = val.Location;
          ApplyPatch(api, jsonPatch, ref applied, ref notFound, ref errorCount);
        }
        jsonPatch.File = loc;
        return;
      }
      if (!loc.Path.EndsWith(".json")) {
        loc.Path += ".json";
      }
      IAsset asset = api.Assets.TryGet(loc);
      if (asset == null) {
        if (jsonPatch.File.Category == null) {
          api.World.Logger.VerboseDebug("Prepatch {0}: File {1} not found. Wrong asset category", jsonPatch, loc);
        }
        else {
          EnumAppSide catSide = jsonPatch.File.Category.SideType;
          if (catSide != EnumAppSide.Universal && api.Side != catSide) {
            api.World.Logger.VerboseDebug("Prepatch {0}: File {1} not found. Hint: This asset is usually only loaded {2} side", jsonPatch, loc, catSide);
          }
          else {
            api.World.Logger.VerboseDebug("Prepatch {0}: File {1} not found", jsonPatch, loc);
          }
        }
        notFound++;
        return;
      }
      Operation op = null;
      switch (jsonPatch.Op) {
        case EnumJsonPatchOp.Add:
          if (jsonPatch.Value == null) {
            api.World.Logger.Error("Prepatch {0} failed probably because it is an add operation and the value property is not set or misspelled", jsonPatch);
            errorCount++;
            return;
          }
          op = new AddOperation {
            Path = new JsonPointer(jsonPatch.Path),
            Value = jsonPatch.Value.Token
          };
          break;
        case EnumJsonPatchOp.AddEach:
          if (jsonPatch.Value == null) {
            api.World.Logger.Error("Prepatch {0} failed probably because it is an add each operation and the value property is not set or misspelled", jsonPatch);
            errorCount++;
            return;
          }
          op = new AddEachOperation {
            Path = new JsonPointer(jsonPatch.Path),
            Value = jsonPatch.Value.Token
          };
          break;
        case EnumJsonPatchOp.Remove:
          op = new RemoveOperation {
            Path = new JsonPointer(jsonPatch.Path)
          };
          break;
        case EnumJsonPatchOp.Replace:
          if (jsonPatch.Value == null) {
            api.World.Logger.Error("Prepatch {0} failed probably because it is a replace operation and the value property is not set or misspelled", jsonPatch);
            errorCount++;
            return;
          }
          op = new ReplaceOperation {
            Path = new JsonPointer(jsonPatch.Path),
            Value = jsonPatch.Value.Token
          };
          break;
        case EnumJsonPatchOp.Copy:
          op = new CopyOperation {
            Path = new JsonPointer(jsonPatch.Path),
            FromPath = new JsonPointer(jsonPatch.FromPath)
          };
          break;
        case EnumJsonPatchOp.Move:
          op = new MoveOperation {
            Path = new JsonPointer(jsonPatch.Path),
            FromPath = new JsonPointer(jsonPatch.FromPath)
          };
          break;
      }
      PatchDocument patchdoc = new PatchDocument(op);
      JToken token;
      try {
        token = JToken.Parse(asset.ToText());
      }
      catch (Exception e2) {
        api.World.Logger.Error("Prepatch {0} (target: {2}) failed probably because the syntax of the value is broken: {1}", jsonPatch, e2, loc);
        errorCount++;
        return;
      }
      try {
        patchdoc.ApplyTo(token);
      }
      catch (PathNotFoundException p) {
        api.World.Logger.Error("Prepatch {0} (target: {3}) failed because supplied path {1} is invalid: {2}", jsonPatch, jsonPatch.Path, p.Message, loc);
        errorCount++;
        return;
      }
      catch (Exception e) {
        api.World.Logger.Error("Prepatch {0} (target: {2}) failed, following Exception was thrown: {1}", jsonPatch, e.Message, loc);
        errorCount++;
        return;
      }
      string text = ((object)token).ToString();
      asset.Data = Encoding.UTF8.GetBytes(text);
      applied++;
    }
  }
}
