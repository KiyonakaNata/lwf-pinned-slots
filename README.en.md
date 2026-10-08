[日本語](README.md)

# LWF Pinned Slots

Pins item icons to fixed places in the inventory of **Lazy Witch's Factory**

![LWF Pinned Slots](img/icon.png)

**[Download the latest release](https://github.com/KiyonakaNata/lwf-pinned-slots/releases/latest)** / **[Thunderstore](https://thunderstore.io/c/lazy-witchs-factory/p/KiyonakaNata/LwfPinnedSlots/)** (easy install with a mod manager)

---

## Features

### Pin item icons in place

![Pin item icons in place](img/pin-keep.webp)

Pins the position of icons in the inventory

- A pinned slot shows a marker
- At zero stock the slot shows a dimmed icon and `0`

### Pin / unpin

![Pin and unpin](img/pin-toggle.webp)

R-Click a slot

| Slot | Result |
|---|---|
| Unpinned | Pins the item |
| Pinned | Unpins |

### Swap

Drag a slot and drop it on another slot

- The item and the pin move together
- An item in the target slot swaps places

---

## Install (manual)

1. Install **BepInEx 5** — [releases](https://github.com/BepInEx/BepInEx/releases)
   - Download `BepInEx_win_x64_5.4.x.zip`
   - Extract it into the game folder (next to `LazyWitchsFactory.exe`)

     > **Where is the game folder?** (Steam)
     > Right-click the game in your library → Manage → Browse local files

2. Run the game once and quit, so that `BepInEx/plugins` is created
3. Put `LwfPinnedSlots.dll` from this mod's zip into **`BepInEx/plugins/`**
4. Start the game, R-Click a slot in the inventory and confirm the marker appears

## Uninstall

**This mod only**

- `BepInEx/plugins/LwfPinnedSlots.dll`
- Also delete `BepInEx/config/kiyonakanata.lwfpinnedslots.cfg` to remove settings

**BepInEx entirely** (stops all other mods too)

- The `BepInEx` folder
- `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` in the game folder

---

## Settings

`BepInEx/config/kiyonakanata.lwfpinnedslots.cfg` (created after running the game once)

Change pins with R-Click and drag-and-drop in game, not in this file

**[1. General]**

| Entry | Default | Values |
|---|---|---|
| Enabled | `true` | |

**[3. View]** — No change needed. Only for the look of the marker and numbers

| Entry | Default | Values |
|---|---|---|
| Show 0 and 1 on pinned slots | `true` | |
| Empty pinned slot icon brightness (%) | `35` | 0–100 |
| Pin marker size | `14` | 2–64 |
| Pin marker color | `#FFFFFF` | `#RRGGBB` |

---

## Requirements

| | |
|---|---|
| Lazy Witch's Factory | Tested on **ver 0.29.1** |
| BepInEx | Tested on **5.4.23.5** (any 5.4.x should work) |

If a game update breaks this mod, that is the end of its life — remove it.

## Troubleshooting

Check `BepInEx/LogOutput.log` first

| Log | State |
|---|---|
| No `[boot] LWF Pinned Slots ...` line | **Not loaded** — check where the DLL is |
| Fewer than `patches=2` | **Inactive** — game version mismatch |

**Bug reports** should include

- A screenshot
- `BepInEx/LogOutput.log`

---

## Disclaimer

- **Unofficial mod** — not supported by the developer
- Any issues, crashes, or save corruption while modded are at your own risk
- Made in accordance with the [official modding policy](https://store.steampowered.com/news/app/3971650/view/699897618302503133)

Source code is MIT licensed (screenshots in `img/` are captures of the game; rights belong to the developer)
