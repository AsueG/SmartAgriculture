# Working on this mod

## Build and deploy

```sh
cd Source                      # the project is not at the repo root
dotnet build -t:Deploy -v:m
```

`Deploy` copies `About/`, `1.6/` and `Languages/` into the RimWorld `Mods/` folder. Override the
managed folder with `-p:RimWorldManaged="…\RimWorldWin64_Data\Managed"` if the Steam path guess fails.

`Deploy` first runs `Tools\preflight.ps1` (also reachable as `dotnet build -t:Preflight`), which fails
the build on anything Steam would reject — an over-long title or description, an oversized or missing
`Preview.png`, an empty `<modVersion>`, a non-numeric `PublishedFileId.txt` — and on a translation key
or def label that exists in one language but not the other. Fix the cause; do not bypass it.

`About/About.xml` carries `<modVersion>`, which the game only ever displays — nothing parses it. Bump it
on release; leaving it out is what makes the mod list read "unknown".

`About/PublishedFileId.txt` is the Workshop item id and is committed on purpose: without it an upload
publishes a *second* item instead of updating the existing one. It is not a secret — the same number is
in the public Workshop URL — and only the owning Steam account can push an update.

The Workshop description is capped at **8000 characters** by `SteamUGC.SetItemDescription`, and RimWorld
passes `<description>` through untruncated, so going over fails the upload with a bare
`OnItemSubmitted failure. Result: InvalidParam`. The description is also read at process start, so an
edit needs a game restart before the upload will pick it up.

`1.6/Assemblies/SmartAgriculture.dll` is committed on purpose — a GitHub download has to be playable
without a build step. Commit it with the source change that produced it.

**RimWorld loads defs and assemblies at process start.** Reloading a save picks up neither, so any XML
or C# change needs a full game restart. Say so when reporting a change as done.

## Verify before you write

Do not assume a vanilla API. Decompile it and read it. A decompiler and a reflection probe live in
`%LOCALAPPDATA%\Temp\rwdecomp` and `%LOCALAPPDATA%\Temp\rwprobe` (`dotnet run -- Namespace.Type`), and
vanilla XML is under `<RimWorld>\Data\Core\Defs` with DLC data in `Data\<Dlc>\Defs`.

Namespaces are not guessable — `RoofCollapseUtility`, `AutoBuildRoofAreaSetter`, `ThingFilterUI`,
`Listing_TreeThingFilter`, `PlaceWorker` and `ThingDef` are in `Verse`, while `Building_Storage`,
`StorageSettings`, `PlantProperties` and `GenConstruct` are in `RimWorld`.

## Architecture constraints

These are load-bearing. Anything added holds to them.

1. **No vanilla class involved in save data is subclassed or altered.** Per-zone state lives in a
   `MapComponent` keyed by `Zone.ID`, which is why removing the mod still leaves a loadable save.
   Subclassing pure UI helpers (`TabRecord`, `ITab`) and `Building` / `Building_Storage` is fine.
2. **Harmony patches are non-destructive** — Postfix or light Transpiler only, never `return false`
   from a Prefix. Every patch resolves its target with `AccessTools` in `Prepare()` and logs a
   `[SACL]` error naming the module that goes idle if the method is gone.
3. **Module toggles take effect mid-game**, with no restart, and a module that is off does nothing.
4. **Plant lists are queried from `DefDatabase<ThingDef>` at runtime** (`plant != null &&
   plant.Sowable`), so crops from other mods appear with no patch. Never hardcode a crop list.
5. **No third-party assembly is ever referenced.** Soft dependencies go through
   `LoadedModManager.RunningModsListForReading`, `DefDatabase<T>.GetNamedSilentFail` and reflection
   only — see `Source/Core/ModCompat.cs`.

## Things that are not what they look like

- `PlantProperties` has no `<sowable>` field: `Sowable => !sowTags.NullOrEmpty()` and
  `IsTree => harvestTag == "Wood" || forceIsTree`. `Plant_Fibercorn` is therefore a tree.
- `Building_Storage.GetParentStoreSettings()` reads `def.building.fixedStorageSettings` **live from
  the def**, so mutating it at startup also fixes crates already in a save. It is a hard gate, not
  just a UI filter: `Listing_TreeThingFilter.Visible` hides anything the parent refuses, which is how
  the crate's storage tab stays free of weapons and apparel.
- A saved `ThingFilter` is never re-derived from the def, which is why the crate has a reset gizmo.
- Roofs are held up only by edifices with `holdsRoof` within `RoofCollapseUtility
  .RoofMaxSupportDistance` (6.9). The glazing frame is deliberately not an edifice, so it must never
  roof a bare cell — nothing would support it and nothing would ever check.
- Drag placement needs `<drawStyleCategory>` on the def, otherwise `DesignatorManager` never starts a
  drag.

## Translations

English lives in the defs plus `Languages/English/Keyed/`; French mirrors it and adds `DefInjected/`.
Both keyed files must stay in sync — this catches it:

```sh
grep -rhoE '"SACL\.[A-Za-z0-9_]+"' Source --include=*.cs | tr -d '"' | sort -u > /tmp/used.txt
grep -rhoE '<SACL\.[A-Za-z0-9_]+>' Languages/English/Keyed/SACL.xml | tr -d '<>' | sort -u > /tmp/en.txt
grep -rhoE '<SACL\.[A-Za-z0-9_]+>' Languages/French/Keyed/SACL.xml  | tr -d '<>' | sort -u > /tmp/fr.txt
comm -23 /tmp/used.txt /tmp/en.txt   # used in C# but undefined
comm -3  /tmp/en.txt   /tmp/fr.txt   # the two languages disagree
```

A few keys are reached as `key + "Bare"` (see `Dialog_FieldPlan.FallowTooltip`), so an "unused" key is
not necessarily dead.

The main menu's **CleanupTranslationFiles** button rewrites the translation files — never click it.
"Save translation report" is the safe one.

## Working with the author

The author writes in French; answer in French. Keep code, comments and committed documentation in
English.
