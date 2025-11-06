## v1.4.3
- Updated for the ROOTS update.

## v1.4.2
- Updated for the DILLYDALLY update (game version 1.30.a).
- Added the following new setting and respective feature:
  - **AccurateWeight** - If `true`, the full weight of the player you're carrying, including the items they are
  carrying, will be added to your own weight. This will also consider the weight reduction provided by any balloons
  that the carried player may have. If `false`, it won't add the weight of the items they are 
  carrying (vanilla behaviour). **Defaults to `false`**.
- Minor fixes.

## v1.4.1
- Updated for the Mesa update (game version 1.20.a).

## v1.4.0
- Ensured compatibility with the game's new cannibalism feature, now actually eating the players instead of carrying
  them when the prompt says "Eat".
  - If you see a player as a chicken, you will need to satiate your hunger before you can carry them. Note that even
  if you were to carry a player that you see as a chicken you'd just pass out from the added weight.

## v1.3.1
- Minor fixes.
- You can now set the **GamepadDropKeybind** setting to `None` to disable the custom keybind to drop the carried player.
  - Note: Since game version 1.6, this setting is no longer needed as you can now "select" the carried player and drop
  them when using a gamepad. However, you can still use this setting if you prefer to use your own keybind.

## v1.3.0
- Added the following new config settings and respective features:
  - **EnablePiggyback** - If `false`, you won't be able to piggyback others.
  The **AllowPiggybackByOthers**, **SwapBackpack**, and **GamepadDropKeybind** settings will still take effect,
  but you won't be able to carry players that are not passed out. **Defaults to `true`**.
  - **AllowPiggybackByOthers** - If `false`, you'll be immediately dropped whenever a player attempts to carry you while
  you're not passed out. Use this if you want to fully prevent players from picking you up. **Defaults to `true`**.
  - **SwapBackpack** - If `true`, if you have a backpack and start carrying another player who does not have a backpack,
  the player you're carrying will automatically equip your backpack. The backpack will be returned to you when you drop
  the player. If `false` or if the player you want to carry already has a backpack, you must manually drop your backpack
  before you can carry that player. **Defaults to `true`**.
  - **GamepadDropKeybind** - The key binding to drop the player you're carrying. This only applies to Gamepad,
  the binding on keyboard should be the Number 4 key by default. **Defaults to `LeftShoulder+RightShoulder`**.
- You'll no longer be able to pick up players while you're in the middle of climbing something.
- Reduced how much you can zoom out the camera in the spectate view while being carried when you're not passed out.
- Fixed a bug that could lock you in the spectate view if the player carrying you disconnected or left the game.

## v1.2.0
- Added a new config setting:
  - **SpectateView** - If `true`, the camera will switch to the spectate view while being carried.
  If `false`, the camera will remain in the first-person view while being carried. **Defaults to `true`**.
  Note that you'll only be able to spectate the player carrying you.
- Reduced the max distance at which you can be from the player you want to carry to be able to pick them up.
If they are passed out the distance is still the same as vanilla.
- Players can no longer do certain actions while being carried:
  - They can no longer interact with nearby items or hold items from their inventory.
  - They can no longer attempt to move, sprint, climb, or reach for other players.
  - If they have the mod installed they shouldn't be able to do these but if they do they will be dropped.
- The player you're carrying can now get down by themselves by jumping or crouching.
- If a player is sprinting, climbing, jumping, crouching, holding an item, or reaching for another player, you won't be
able to pick them up. This is to help prevent trolling or picking them up on accident.

## v1.1.0
- Added a config with the following setting:
  - **HoldToCarryTime** - The time in seconds you need to hold the interact button to start carrying another player.
  If `0`, you will start carrying the player immediately upon pressing the interact button.
  If the player is passed out, you will start carrying them immediately even if this is set to a value greater than 0.
  **Defaults to `1.5`**.
- Fixed a bug that allowed players to carry each other simultaneously.

## v1.0.1
- Manifest and Readme changes. No actual changes to the mod itself.

## v1.0.0
- Initial Release
