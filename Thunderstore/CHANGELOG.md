## v1.2.0
- Added a new config setting:
  - **SpectateView**: If true, the camera will switch to the spectate view while being carried.
  If false, the camera will remain in the first-person view while being carried.
  Note that you'll only be able to spectate the player carrying you.
- Reduced the max distance at which you can be from the player you want to carry to be able to pick them up.
If they are passed out the distance is still the same as vanilla.
- Players can no longer do certain actions while being carried:
  - They can no longer interact with nearby items.
  - They can no longer attempt to move, sprint, climb, or reach for other players.
  - If they have the mod installed they shouldn't be able to do these but if they do they will be dropped.
- The player you're carrying can now get down by themselves by jumping or crouching.
- If a player is sprinting, climbing, jumping, crouching, holding an item, or reaching for another player, you won't be
able to pick them up. This is to help prevent trolling or picking them up on accident.

## v1.1.0
- Added a config with the following setting:
  - **HoldToCarryTime**: The time in seconds you need to hold the interact button to start carrying another player.
  If 0, you will start carrying the player immediately upon pressing the interact button.
  If the player is passed out, you will start carrying them immediately even if this is set to a value greater than 0.
  Defaults to 1.5 seconds.
- Fixed a bug that allowed players to carry each other simultaneously.

## v1.0.1
- Manifest and Readme changes. No actual changes to the mod itself.

## v1.0.0
- Initial Release
