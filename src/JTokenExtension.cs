using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PatchWars {
  public static class JTokenExtension {
    public static string GetJsonPointer(this JToken thisToken) {
      if (thisToken.Parent == null) {
        return "/";
      }
      IList<JToken> list = thisToken.AncestorsAndSelf().Reverse().ToList();
      List<string> results = new List<string>();
      for (int i = 0; i < list.Count; i++) {
        JToken jToken = list[i];
        JToken nextJToken = null;
        if (i + 1 < list.Count) {
          nextJToken = list[i + 1];
        }
        else if (jToken.Type == JTokenType.Property) {
          nextJToken = list[i];
        }

        if (nextJToken == null) {
          continue;
        }

        switch (jToken.Type) {
          case JTokenType.Property:
            results.Add((jToken as JProperty)?.Name);
            continue;
          case JTokenType.Array:
          case JTokenType.Constructor:
            results.Add(((IList<JToken>)jToken).IndexOf(nextJToken).ToString());
            continue;
        }
      }

      return "/" + string.Join("/", results);
    }
  }
}
