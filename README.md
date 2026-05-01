# MapNotify-3.28

MapNotify-3.28 is a plugin for Exile API designed to highlight map modifiers and provide detailed information overlays for maps, invitations, contracts, and logbooks.

![hippo](https://i.imgur.com/6X2qUWh.gif)

![hippo](https://i.imgur.com/FDQVQBm.gif)

![Image](https://i.imgur.com/ch17XYQ.png)

![Image](https://i.imgur.com/TGwiiOs.png)

![Image](https://i.imgur.com/Wzh48nV.png)

![Image](https://i.imgur.com/fSFfByJ.png)

![Image](https://i.imgur.com/5sdGnSM.png)

![Image](https://i.imgur.com/q4mYaWD.png)

![Image](https://i.imgur.com/jGrlgZ3.png)

![Image](https://i.imgur.com/QGH69ym.png)

## Features

### Mod Highlighting
*   **Good/Bad Mod Filtering:** Highlights maps based on user-defined "Good" and "Bad" modifier lists.
*   **Bricked Maps:** Identifies "bricking" modifiers with specific border highlights.
*   **Mod Preview Window:** Capture modifiers from hovered items using a hotkey to categorize them, set colors, or mark them as bricking.
*   **Mod Browser:** A built-in database to search and add modifiers manually (Generic, Uber, Expedition, and Valdo mods).

### Tooltip Overlays
*   **Stat Breakdown:** Displays Item Quantity (IIQ), Item Rarity (IIR), and Pack Size (PS).
*   **Prefix/Suffix Breakdown:** Shows the contribution of prefixes and suffixes to map stats.
*   **Heist Info:** Displays Area Level and required Job levels for Contracts and Blueprints.
*   **Logbook Info:** Displays Faction information and implicit modifiers.
*   **Originator/Nightmare Stats:** Displays bonus stats for specialized map types.

### Atlas Highlighting
*   **Completion Tracking:** Highlights nodes on the Atlas that are not completed.
*   **Bonus Objectives:** Highlights maps with missing bonus objectives.
*   **Maven Witness:** Highlights maps currently witnessed by the Maven.

### Supported UI Elements
*   Inventory and Stash (including Map Stash).
*   Heist and Expedition Lockers.
*   Trade and Purchase/Shop windows.
*   Maven Invitations.

## Usage

1.  **Hotkey:** Use the configured hotkey (Default: F1 or as set in Core Settings) while hovering over a map to open the **Map Mod Preview** window.
2.  **Configuration:**
    *   **Active Mods:** View and delete currently tracked modifiers.
    *   **Captured Mods:** Categorize mods from the last hovered item.
    *   **Profiles:** Create and switch between different mod filter profiles.
3.  **Atlas:** Type `a|e` in the Atlas search box to force the client to load node data for highlighters to function correctly.

## Configuration Files

The plugin stores settings and mod lists in:
*   `config/MapNotify-3.28/Profiles/[ProfileName]/GoodMods.txt`
*   `config/MapNotify-3.28/Profiles/[ProfileName]/BadMods.txt`

# Credits
*   **Original Plugin**: Lachrymatory https://github.com/Sirais/MapNotify
*   **Edited by**: Xcesius https://github.com/Xcesius/MapNotify/
*   Thanks to doubleespressobro for the WheresMyShitMapsAt plugin https://github.com/doubleespressobro/WheresMyShitMapsAt-PoE1
*   **(vibecoded) Updated by**: NokTham