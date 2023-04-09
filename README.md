# Features

## Prepatches

Patches found inside the `prepatches` folder of a mod's assets will run before any regular `patches` are processed, guaranteeing the ability to patch standard patches before they have run.

## Postpatches

Patches found inside the `postpatches` folder of a mod's assets will run after all `prepatches` and `patches` are processed.

## JPath Queries

Adds the ability to use Newtonsoft's JPath queries to define more expressive and dynamic patches.
This feature is available for use in patches found in `patches`, `prepatches`, and `postpatches`.

### Use

To utilize JPath queries, place your query inside the `path` field of your patch.

If you expect and want your patch to apply to multiple elements in the target file, be sure to add `"patchMultiple": true` to your patch.

When performing an `add` or `addeach` patch operation using JPath queries, because the element being added does not exist yet and would cause the query to fail, it must be left out of the `path` definition. Instead, add the new element name (or index) to `pathAppend`. The string found in `pathAppend` will be added to the end of the pointers found by your JPath query before patching the target file. `pathAppend` does not support JPath and must be in vanilla's standard JsonPointer notation.

### Examples

You'd like to mod the panning loot table so that temporal gears are far more common from both sand/gravel and bony soil. (Excerpt from `pan.json` as of Vintage Story v1.18.0)
```json
{
  code: "pan",
  attributes: {
    panningDrops: {
      "@(bonysoil|bonysoil-.*)": [
        // ... 17 other items not shown
        { type: "item", code: "gear-temporal",  chance: { avg: 0.001, var: 0 }  },
        // ...
      ],
      "@(sand|gravel)-.*": [
        // ... 19 other items not shown
        { type: "item", code: "gear-temporal",  chance: { avg: 0.0005, var: 0 }  },
        // ...
      ]
    }
  }
}
```

If you were to write a patch file for this in the vanilla patching system (or use modmaker.exe), your patch file would look like this:

```json
[
  {
    "op": "replace",
    "path": "/attributes/panningDrops/@(bonysoil|bonysoil-.*)/17/chance/avg",
    "value": 20,
    "file": "game:blocktypes/wood/pan.json",
    "side": "Server"
  },
  {
    "op": "replace",
    "path": "/attributes/panningDrops/@(sand|gravel)-.*/19/chance/avg",
    "value": 20,
    "file": "game:blocktypes/wood/pan.json",
    "side": "Server"
  }
]
```

These patches are brittle to any changes to the drop tables in vanilla as well as if any other mods make changes as the indexes of specific elements react to those changes. Also, you and anyone reading your patch have no idea which item in the drop table is being changed without referencing `pan.json`.

Using a JPath query instead, your patch might look like this:

```json
[
  {
    "op": "replace",
    "path": "$.attributes.panningDrops.*..[?(@..code == 'gear-temporal')].chance.avg",
    "value": 20,
    "patchMultiple": true,
    "file": "game:blocktypes/wood/pan.json",
    "side": "Server"
  }
]
```

Now the patch is expressive and resilient to vanilla and third party mod adjustments.
The leading `$` signifies the root element.
A `.` followed by a key finds the appropriately named child element.
`.*` selects all of the current element's children. In this example, this is both the `@(bonysoil|bonysoil-.*)` and `@(sand|gravel)-.*` elements, but also includes any potential additions and changes to `panningDrops`.
A `..` indicates any descendant element from the current point in the tree.
`[?()]` is a JPath filter expression. `@` inside the expression represents the current element. In this example, `[?(@..code == 'gear-temporal')]` finds any element which has any descendant element key named `code` with a value of `gear-temporal`.
The remaining portion of the query, `.chance.avg`, continues traversing the tree to the element where the changes need to be made.

This patch file now has the flexibility to handle many potential changes from vanilla and third party mods.

### Debugging

Log messages have been expanded to help with troubleshooting. In addition to standard errors and summaries that appear in `server-main` and `client-main`, there are additional messages located in `server-debug` and `client-debug` to further explain both failed and successful patches.
```
[VerboseDebug] Patch 0 in patchwars:prepatches/jpathtest1.json: Found 2 paths using supplied JPath ($.attributes.panningDrops.*..[?(@..code == 'gear-temporal')].chance.avg): /attributes/panningDrops/@(bonysoil|bonysoil-.*)/17/chance/avg, /attributes/panningDrops/@(sand|gravel)-.*/19/chance/avg
```
