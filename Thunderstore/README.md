# Piggyback
Have you ever been in a situation where your mate went for a bathroom break or just went AFK for whatever reason and you
thought "if only I could carry them"? Then this mod is for you! This mod allows you to carry other players even when
they're not passed out.

## Installation
Drag and drop the **Piggyback.dll** file into your PEAK BepInEx plugins folder.

## Usage
Only the players who intend to carry others need to have the mod installed, the players being carried don't need it.\
This may not be the case in the future if I update the mod to allow the carried players to get down by themselves.\
**Note:** If you have a backpack equipped, you will need to manually drop it first before being able to carry others.

## Config
- **HoldToCarryTime**: The time in seconds you need to hold the interact button to start carrying another player.
If 0, you will start carrying the player immediately upon pressing the interact button.
If the player is passed out, you will start carrying them immediately even if this is set to a value greater than 0.
Defaults to 1.5 seconds.

## Known Issues
- There's currently no way for the carried players to forcefully remove themselves from the carried state,
in other words, the player carrying them must drop them if they want to regain control of their character.
Please don't use this to troll your mates!
- The player being carried can still attempt to grab/climb rocks and such. This will produce no effect while they're
being carried but may sometimes result in visual glitches.
- The player being carried can still hold any of the items they have on them, which can negatively affect the movement
of the player carrying them due to collision hitboxes.
