# Camera Presets for Car Mechanic Simulator 2018

Version 1.1.0 · Windows · BepInEx 5 Mono

Save four garage repair views. Each preset remembers orbit angle, zoom distance, field of view, and look sensitivity. Recall applies the saved view around the part or assembly the camera is currently focused on. It keeps the current part selection and uses the game's camera limits. On a car, the orbit angle follows the car's heading.

## Install

1. Close CMS 2018.
2. Extract the release ZIP into the game directory containing `cms2018.exe` and your existing BepInEx 5 installation.
3. Check that the DLL landed at `BepInEx/plugins/CameraPresets/CameraPresets.dll`. Launch the game and enter a repair view in the garage.

## Escape menu

1. Enter a repair view on a car or engine stand. Use the normal camera controls to set your angle and zoom.
2. Press Escape and select **Camera Presets**. Choose a slot, then select **Save Current View**. An occupied slot asks before overwriting.
3. To recall a saved view, open the slot and select **Load and Resume**. The game returns to your current repair focus and applies the saved camera view and sensitivity.

**Look Sensitivity** adjusts the camera speed in 10 percentage-point steps, resets it to 100%, or toggles HUD notifications. The current sensitivity is included the next time you save a view. **Back** or Escape returns to the previous menu page.

The four slots start empty and are named Engine Bay, Undercarriage, Wheels, and Engine Stand. Presets work in garage orbit views used for repairs and inspection. Finish any active bolt operation before opening the Escape menu. Your existing 1.0.0 presets carry forward.

## Optional shortcuts

| Default shortcut | Action |
| --- | --- |
| Left Alt + Left Shift + 1–4 | Save or overwrite a slot with the current view and sensitivity |
| Left Alt + 1–4 | Recall a slot |
| Left Alt + [ or ] | Decrease or increase repair orbit sensitivity by 10 percentage points |
| Left Alt + 0 | Return repair orbit sensitivity to 100% |


## Configuration and removal

BepInEx creates `BepInEx/config/local.cms18.camerapresets.cfg` on first launch. Edit it with the game closed to rename slots, rebind shortcuts, or turn notifications off. The sensitivity buttons and recalled values affect the repair orbit until you reset them or restart the game. Saved slot values survive restarts in `BepInEx/config/local.cms18.camerapresets.json`.

To uninstall, close the game and remove `BepInEx/plugins/CameraPresets/CameraPresets.dll`. Keep the config and JSON to retain your settings and views for a later reinstall.
