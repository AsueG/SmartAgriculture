# Smart Agriculture, Climate & Colony Logistics

A RimWorld 1.6 mod: eight independent farming, climate and logistics modules. Every one of them can be
switched on or off from the mod settings at any time, mid-game, without restarting — and a module that
is off does nothing at all.

Available on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3797110254).

Most of it is configured from one place: select a grow zone and click the new button on its gizmo bar.

## Modules

1. **Field-wide crop rotation** — a zone follows an ordered list of 2 to 4 crops and only moves on once
   the last plant of the current one is gone, so a field never turns into a checkerboard. Fallow and
   "random crop that suits the weather" are available as steps.
2. **Stock-triggered farming** — a field watches the colony stock of its own produce and switches to a
   secondary crop, or stops sowing, above a high threshold; it resumes below a low one.
3. **Seasonal crop scheduler** — one instruction per calendar season: a fixed crop, the rotation, or
   nothing.
4. **Greenhouse climate control** — an enclosed room under a transparent roof warms with actual
   sunlight instead of tracking the outdoor temperature. Adds a glazing frame and a passive automated
   roof vent. A third-party skylight mod's roof is detected and gets the same thermal model.
5. **Field harvest crates** — a movable crate at the field edge that harvesters fill directly, then
   releases the whole batch for hauling in one trip.
6. **Weather-driven work priorities** — outdoor work types are held back while conditions outside are
   genuinely lethal.
7. **Pantry first** — cooks take the ingredients closest to spoiling rather than the closest ones.
8. **Soil rest** — a fallow step banks a fertility reserve that following crops spend by growing
   faster. The reserve is only ever added on top of the terrain's fertility, never subtracted.

## Building from source

Requires the .NET SDK and a RimWorld 1.6 install.

```sh
cd Source
dotnet build                  # compiles to ../1.6/Assemblies/
dotnet build -t:Deploy        # also copies the whole mod into RimWorld/Mods/
```

The project locates RimWorld's managed folder itself for the usual Steam paths. Override it if yours
differs:

```sh
dotnet build -p:RimWorldManaged="D:\Path\RimWorldWin64_Data\Managed"
```

RimWorld loads defs and assemblies at process start, so a **full game restart** is needed after any
build — reloading a save picks up neither.

## Layout

| Path | Contents |
| --- | --- |
| `About/` | mod metadata and preview image |
| `1.6/Defs/` | XML defs |
| `1.6/Textures/` | building textures |
| `1.6/Assemblies/` | build output |
| `Languages/` | English and French keyed strings and def injections |
| `Source/` | C# project, one folder per module under `Modules/` |
| `Tools/` | preflight checks, plus PowerShell generators for the preview image and the textures |

## Design constraints

These are deliberate and load-bearing; anything added should hold to them.

- **No vanilla class involved in save data is subclassed or altered.** Per-zone state lives in a
  `MapComponent` keyed by `Zone.ID`, which is why removing the mod still leaves a loadable save.
- **Harmony patches are non-destructive** — Postfix or light Transpiler only, never a `return false`
  from a Prefix.
- **Module toggles take effect mid-game**, with no restart and no trace left in the save when off.
- **Crop lists are queried from `DefDatabase<ThingDef>` at runtime**, so crops from other mods appear
  with no patch.
- **No third-party assembly is ever referenced.** Soft dependencies go through
  `LoadedModManager.RunningModsListForReading`, `DefDatabase<T>.GetNamedSilentFail` and reflection
  only (see `Source/Core/ModCompat.cs`).

## Translations

Ships with English and French. Adding a language means a new folder under `Languages/` mirroring
`Languages/English/`.

## License

MIT — see [LICENSE](LICENSE).
