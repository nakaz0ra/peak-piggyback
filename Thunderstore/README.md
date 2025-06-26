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

The player being carried can get down by themselves by jumping or crouching.\
If a player is sprinting, climbing, jumping, crouching, holding an item, or reaching for another player, you won't be
able to pick them up. This is to help prevent trolling or picking them up on accident.

## Configuration
The following settings can be changed in the config file located at:
`<game directory>\BepInEx\config\nakazora.peak.piggyback.cfg`

- **EnablePiggyback**\
If `false`, you won't be able to piggyback others.\
The **AllowPiggybackByOthers**, **SwapBackpack**, and **GamepadDropKeybind** settings will still take effect,
but you won't be able to carry players that are not passed out.\
**Defaults to `true`**.


- **AllowPiggybackByOthers**\
If `false`, you'll be immediately dropped whenever a player attempts to carry you while
you're not passed out. Use this if you want to fully prevent players from picking you up.\
**Defaults to `true`**.


- **SpectateView**\
If `true`, the camera will switch to the spectate view while being carried.\
If `false`, the camera will remain in the first-person view while being carried.\
Note that you'll only be able to spectate the player carrying you.\
**Defaults to `true`**.


- **HoldToCarryTime**\
The time in seconds you need to hold the interact button to start carrying another player.\
If `0`, you will start carrying the player immediately upon pressing the interact button.\
If the player is passed out, you will start carrying them immediately even if this is set to a value greater than `0`.\
**Defaults to `1.5`**.


- **SwapBackpack** (Experimental)\
If `true`, if you have a backpack and start carrying another player who does not have a backpack,
the player you're carrying will automatically equip your backpack.
The backpack will be returned to you when you drop the player.\
If `false` or if the player you want to carry already has a backpack, you must manually drop your backpack before you
can carry that player.\
**Defaults to `true`**.


- **GamepadDropKeybind**\
The key binding to drop the player you're carrying.\
This only applies to Gamepad, the binding on keyboard should be the Number 4 key by default.\
You can combine multiple keys by separating them with a plus (`+`) sign. This would require you to press all the given
keys at the same time to drop the player.\
**Acceptable Gamepad Keys:**\
`DpadUp`, `DpadDown`, `DpadLeft`, `DpadRight`, `North`, `East`, `South`, `West`, `LeftStickButton`, `RightStickButton`, 
`LeftShoulder`, `RightShoulder`, `LeftTrigger`, `RightTrigger`, `Start`, `Select`\
**Defaults to `LeftShoulder+RightShoulder`**.
