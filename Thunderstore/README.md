# Piggyback
Have you ever been in a situation where your mate went for a bathroom break or just went AFK for whatever reason and you
thought "if only I could carry them"? Then this mod is for you! This mod allows you to carry other players even when
they're not passed out.

## Installation
Drag and drop the **Piggyback.dll** file into your PEAK BepInEx plugins folder.

## Usage
Only the players who intend to carry others need to have the mod installed, the players being carried don't need it,
but it is recommended that they do if they want to use the spectate view while being carried instead of the
first-person view and for a better experience overall.

If you have a backpack equipped, you will need to manually drop it first before you can carry another player.

The player being carried can get down by themselves by jumping or crouching.\
If a player is sprinting, climbing, jumping, crouching, holding an item, or reaching for another player, you won't be
able to pick them up. This is to help prevent trolling or picking them up on accident.

## Config
- **SpectateView**: If enabled, the camera will switch to the spectate view while being carried.
If disabled, the camera will remain in the first-person view while being carried.
Note that you'll only be able to spectate the player carrying you.
- **HoldToCarryTime**: The time in seconds you need to hold the interact button to start carrying another player.
If 0, you will start carrying the player immediately upon pressing the interact button.
If the player is passed out, you will start carrying them immediately even if this is set to a value greater than 0.
Defaults to 1.5 seconds.
