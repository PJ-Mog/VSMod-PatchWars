using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Tavis;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.ServerMods.NoObf;

namespace PatchWars {
  public class NewModJsonPatchLoader : ModJsonPatchLoader {
    protected ICoreAPI api;
    protected int disabledCount = 0;
    protected int unmetConditionCount = 0;
    protected int targetFileNotFound = 0;
    protected int errorCount = 0;
    protected int appliedCount = 0;

    protected virtual string FromFolder => "patches/";
    protected virtual string SystemName => "Json Patch Loader";

    public override void AssetsLoaded(ICoreAPI api) {
      this.api = api;

      foreach (var asset in api.Assets.GetMany(FromFolder)) {
        TryApplyPatch(asset);
      }
      LogSummary();
    }

    protected virtual void LogSummary() {
      StringBuilder sb = new StringBuilder();
      var total = disabledCount + unmetConditionCount + targetFileNotFound + errorCount + appliedCount;
      sb.Append(SystemName + ": ");
      if (total == 0) {
        sb.Append(Lang.Get("Nothing to patch"));
      }
      else {
        sb.Append(Lang.Get("{0} patches total", total));

        if (appliedCount > 0) {
          sb.Append(Lang.Get(", successfully applied {0} patches", appliedCount));
        }
        if (disabledCount > 0) {
          sb.Append(Lang.Get(", {0} patches were disabled", disabledCount));
        }
        if (targetFileNotFound > 0) {
          sb.Append(Lang.Get(", missing files on {0} patches", targetFileNotFound));
        }
        if (unmetConditionCount > 0) {
          sb.Append(Lang.Get(", unmet conditions on {0} patches", unmetConditionCount));
        }
        if (errorCount > 0) {
          sb.Append(Lang.Get(", had errors on {0} patches", errorCount));
        }
        else {
          sb.Append(Lang.Get(", no errors"));
        }
      }

      api.Logger.Notification(sb.ToString());
      api.Logger.VerboseDebug(SystemName + " finished.");
    }

    protected virtual void TryApplyPatch(IAsset asset) {
      JsonPatchExtended[] patches = null;
      try {
        patches = asset.ToObject<JsonPatchExtended[]>();
      }
      catch (Exception e) {
        errorCount++;
        api.Logger.Error("Failed loading patches file {0}: {1}", asset.Location, e);
        return;
      }

      for (int i = 0; i < patches.Length; i++) {
        patches[i].SourceFileForLogging = asset.Location;
        patches[i].IndexForLogging = i;
        ApplyPatch(patches[i]);
      }
    }

    protected virtual void ApplyPatch(JsonPatchExtended patch) {
      if (!Enabled(patch) || !DependenciesSatisfied(patch) || !ConditionsSatisfied(patch)) {
        return;
      }

      if (patch.File == null) {
        errorCount++;
        api.Logger.Error("Patch {0} in {1} failed because it is missing the target file property.", patch.IndexForLogging, patch.SourceFileForLogging);
        return;
      }

      if (patch.File.Path.EndsWith("*")) {
        foreach (IAsset foundAsset in api.Assets.GetMany(patch.File.Path.TrimEnd('*'), patch.File.Domain, loadAsset: false)) {
          patch.File = foundAsset.Location;
          ApplyPatch(patch);
        }
        return;
      }

      if (!patch.File.Path.EndsWith(".json")) {
        patch.File.Path += ".json";
      }

      IAsset targetAsset = api.Assets.TryGet(patch.File);
      if (targetAsset == null) {
        targetFileNotFound++;

        if (patch.File.Category == null) {
          api.Logger.VerboseDebug("Patch {0} in {1}: File {2} not found. Wrong asset category.", patch.IndexForLogging, patch.SourceFileForLogging, patch.File);
          return;
        }

        EnumAppSide catSide = patch.File.Category.SideType;
        if (catSide != EnumAppSide.Universal && api.Side != catSide) {
          api.Logger.VerboseDebug("Patch {0} in {1}: File {2} not found. Hint: This asset is usually only loaded {3} side.", patch.IndexForLogging, patch.SourceFileForLogging, patch.File, catSide);
        }
        else {
          api.Logger.VerboseDebug("Patch {0} in {1}: File {2} not found.", patch.IndexForLogging, patch.SourceFileForLogging, patch.File);
        }

        return;
      }

      JToken targetToken;
      try {
        targetToken = JToken.Parse(targetAsset.ToText());
      }
      catch (Exception e) {
        errorCount++;
        api.Logger.Error("Patch {0} in {1}: Failed probably because the syntax of the value is broken (target: {2}): {3}", patch.IndexForLogging, patch.SourceFileForLogging, patch.File, e);
        return;
      }

      if (ApplyPatch(patch, targetToken)) {
        string text = ((object)targetToken).ToString();
        targetAsset.Data = Encoding.UTF8.GetBytes(text);
      }
    }

    protected virtual bool ApplyPatch(JsonPatchExtended patch, JToken targetToken) {
      if (patch.Path == null || patch.Path == "") {
        errorCount++;
        api.Logger.Error("Patch {0} in {1}: Path not set.", patch.IndexForLogging, patch.SourceFileForLogging);
        return false;
      }

      if (patch.Path.StartsWith("$")) {
        return ApplyJPathQueryPatch(patch, targetToken);
      }

      patch.Path += patch.PathAppend;
      Operation op = null;
      switch (patch.Op) {
        case EnumJsonPatchOp.Add:
          if (patch.Value == null) {
            errorCount++;
            api.Logger.Error("Patch {0} in {1} failed probably because it is an add operation and the value property is not set or misspelled", patch.IndexForLogging, patch.SourceFileForLogging);
            return false;
          }
          op = new AddOperation {
            Path = new JsonPointer(patch.Path),
            Value = patch.Value.Token
          };
          break;
        case EnumJsonPatchOp.AddEach:
          if (patch.Value == null) {
            errorCount++;
            api.Logger.Error("Patch {0} in {1} failed probably because it is an add each operation and the value property is not set or misspelled", patch.IndexForLogging, patch.SourceFileForLogging);
            return false;
          }
          op = new AddEachOperation {
            Path = new JsonPointer(patch.Path),
            Value = patch.Value.Token
          };
          break;
        case EnumJsonPatchOp.Remove:
          op = new RemoveOperation {
            Path = new JsonPointer(patch.Path)
          };
          break;
        case EnumJsonPatchOp.Replace:
          if (patch.Value == null) {
            errorCount++;
            api.Logger.Error("Patch {0} in {1} failed probably because it is a replace operation and the value property is not set or misspelled", patch.IndexForLogging, patch.SourceFileForLogging);
            return false;
          }
          op = new ReplaceOperation {
            Path = new JsonPointer(patch.Path),
            Value = patch.Value.Token
          };
          break;
        case EnumJsonPatchOp.Copy:
          op = new CopyOperation {
            Path = new JsonPointer(patch.Path),
            FromPath = new JsonPointer(patch.FromPath)
          };
          break;
        case EnumJsonPatchOp.Move:
          op = new MoveOperation {
            Path = new JsonPointer(patch.Path),
            FromPath = new JsonPointer(patch.FromPath)
          };
          break;
      }

      PatchDocument patchdoc = new PatchDocument(op);
      try {
        patchdoc.ApplyTo(targetToken);
      }
      catch (PathNotFoundException p) {
        errorCount++;
        api.Logger.Error("Patch {0} (target: {4}) in {1} failed because supplied path {2} is invalid: {3}", patch.IndexForLogging, patch.SourceFileForLogging, patch.Path, p.Message, patch.File);
        return false;
      }
      catch (Exception e) {
        errorCount++;
        api.Logger.Error("Patch {0} (target: {3}) in {1} failed, following Exception was thrown: {2}", patch.IndexForLogging, patch.SourceFileForLogging, e.Message, patch.File);
        return false;
      }
      appliedCount++;
      return true;
    }

    protected virtual bool ApplyJPathQueryPatch(JsonPatchExtended patch, JToken targetToken) {
      List<string> matchedPaths = null;
      try {
        matchedPaths = targetToken.SelectTokens(patch.Path)?.Select(token => token.GetJsonPointer()).ToList();
      }
      catch (Exception e) {
        errorCount++;
        api.Logger.Error("Patch {0} in {1}: Failed parsing target file with supplied JPath {2}: {3}", patch.IndexForLogging, patch.SourceFileForLogging, patch.Path, e);
        return false;
      }

      if (matchedPaths.Count == 0) {
        errorCount++;
        api.Logger.VerboseDebug("Patch {0} in {1}: Failed because the supplied JPath ({2}) found no results.", patch.IndexForLogging, patch.SourceFileForLogging, patch.Path);
        return false;
      }

      if (matchedPaths.Count == 1) {
        patch.Path = matchedPaths[0];
        return ApplyPatch(patch, targetToken);
      }

      api.Logger.VerboseDebug("Patch {0} in {1}: Found {2} paths using supplied JPath ({3}): {4}", patch.IndexForLogging, patch.SourceFileForLogging, matchedPaths.Count, patch.Path, string.Join(", ", matchedPaths));

      if (!patch.PatchMultiple) {
        errorCount++;
        api.Logger.VerboseDebug("Patch {0} in {1}: Failed because the supplied JPath ({2}) found multiple results, but expected one. Set {3} to patch all results.", patch.IndexForLogging, patch.SourceFileForLogging, patch.Path, nameof(patch.PatchMultiple));
        return false;
      }

      bool modified = false;
      foreach (var path in matchedPaths) {
        patch.Path = path;
        modified |= ApplyPatch(patch, targetToken);
      }
      return modified;
    }

    protected virtual bool Enabled(JsonPatchExtended patch) {
      var side = patch.Side.HasValue ? patch.Side.Value : patch.File.Category.SideType;
      bool correctSide = side == EnumAppSide.Universal || side == api.Side;
      if (!patch.Enabled || !correctSide) {
        disabledCount++;
        return false;
      }
      return true;
    }

    protected virtual bool ConditionsSatisfied(JsonPatchExtended patch) {
      if (patch.Condition == null) {
        return true;
      }

      IAttribute attr = api.World.Config?[patch.Condition.When];
      if (attr == null) {
        unmetConditionCount++;
        api.Logger.VerboseDebug("Patch file {0}, patch {1}: WorldConfig '{2}' does not exist.", patch.SourceFileForLogging, patch.IndexForLogging, patch.Condition.When);
        return false;
      }

      if (patch.Condition.useValue) {
        patch.Value = new JsonObject(JToken.Parse(attr.ToJsonToken()));
        return true;
      }

      string configValue = attr.GetValue()?.ToString() ?? "";
      if (!patch.Condition.IsValue.Equals(configValue, StringComparison.InvariantCultureIgnoreCase)) {
        unmetConditionCount++;
        api.Logger.VerboseDebug("Patch file {0}, patch {1}: Unmet IsValue condition ({2} != {3})", patch.SourceFileForLogging, patch.IndexForLogging, patch.Condition.IsValue, configValue);
        return false;
      }

      return true;
    }

    protected virtual bool DependenciesSatisfied(JsonPatchExtended patch) {
      if (patch.DependsOn == null) {
        return true;
      }

      foreach (var dependency in patch.DependsOn) {
        if (api.ModLoader.IsModEnabled(dependency.modid) == dependency.invert) {
          unmetConditionCount++;
          api.Logger.VerboseDebug("Patch file {0}, patch {1}: Unmet DependsOn condition ({2})", patch.SourceFileForLogging, patch.IndexForLogging, (dependency.invert ? "!" : "") + dependency.modid);
          return false;
        }
      }

      return true;
    }
  }
}
