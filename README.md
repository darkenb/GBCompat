# BUG_GBCompat

Makes GenesisBook 3.2.3 work on Dyson Sphere Program 0.10.35. The fixes are applied while the game runs; GenesisBook's own files are not changed, and removing this mod gives you plain GenesisBook 3.2.3 back.

> **This is a modified version derived from ProjectGenesis (创世之书), and it differs from the original.**
> ProjectGenesis is Copyright (C) 2022-2026 Awbugl and ProjectGenesis contributors: <https://github.com/HiddenCirno/ProjectGenesis>.
> BUG_GBCompat is not made, reviewed or endorsed by the ProjectGenesis authors. Please report problems with it here, not to them.

Most of the corrected code comes from ProjectGenesis 3.2.4, where its authors adapted GenesisBook to game version 0.10.35. This mod brings those changes to the 3.2.3 release.

If the installed GenesisBook is not 3.2.3, nothing is patched and the BepInEx log says so.

## What it fixes

Game version 0.10.35 changed several game methods that GenesisBook 3.2.3 patches. Without these fixes, GenesisBook stops part-way through loading (its vein patches fail, so the rest of its setup does not run), and some of its other changes break.

- **Veins:** planets get GenesisBook's vein types again. The vein patches now match the 0.10.35 vein generation, including its new underwater oil: oil follows the game's underwater rules, other veins keep GenesisBook's placement.
- **Gamma ray receiver:** 0.10.35 settles proliferator points per catalyst item, so only the catalyst use is multiplied by 10 (GenesisBook 3.2.3 also multiplied a field that no longer exists, which broke the receiver).
- **Starting system:** generating GenesisBook's starting star uses 0.10.35's planet generation (3.2.3 called a version of it that no longer exists).
- **Item filter window:** GenesisBook's extra tabs get their own pages again, and the enemy drop button sits after them.
- **Settings:** "Revert all" in the settings window (new in 0.10.35) also resets GenesisBook's settings.
- **Item grid:** the dark fog graviton lens (new in 0.10.35) moves below the other dark fog items instead of sitting among GenesisBook's items, and the full accumulator moves one slot along, as in ProjectGenesis 3.2.4.
- **Switching games:** no error when starting or loading a game while another is open (the camera drew one more frame after the player was removed).
- **Main menu:** no error after the main menu has been open for a while. GenesisBook rebuilds the star map's model renderer while the menu's background scene already runs, and the Dark Fog there kept using the old one.

## Compatibility

- Requires GenesisBook 3.2.3 and its own dependencies.
- Works with Galactic Scale: GenesisBook 3.2.3's Galactic Scale support already works with Galactic Scale 2.80.7, so nothing there is changed.
- This mod has to load before GenesisBook to put its fixes in place. It does this through its plugin ID; if another mod makes GenesisBook load first, the BepInEx log says so and nothing is changed.

## Licence

GPL-3.0-only, with the additional terms of ProjectGenesis (see `LICENSE`). The source code is at <https://github.com/darkenb/GBCompat>. This mod contains no GenesisBook art; its icon is its own.

## Building from source

You need a current .NET SDK and the .NET Framework 4.7.2 targeting pack. The game, Unity and BepInEx assemblies are referenced from your own installation and are not part of this repository.

```text
dotnet build -c Release "-p:DSPGamePath=D:\Games\Dyson Sphere Program" "-p:BepInExCorePath=C:\path\to\BepInEx\core"
```

The output is `bin/Release/net472/BUG_GBCompat.dll`. Copy it to `BepInEx/plugins/BUG_GBCompat/`.
