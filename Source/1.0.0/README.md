# Camera Presets for Car Mechanic Simulator 2018

Version 1.0.0 · Windows · BepInEx 5 Mono

Save four garage repair views. Each preset remembers orbit angle, zoom distance, field of view, and look sensitivity. Recall applies the saved view around the part or assembly the camera is currently focused on. It keeps the current part selection and uses the game's camera limits. On a car, the orbit angle follows the car's heading.

## Install

1. Close CMS 2018.
2. Extract the release ZIP into the game directory containing `cms2018.exe` and your existing BepInEx 5 installation.
3. Check that the DLL landed at `BepInEx/plugins/CameraPresets/CameraPresets.dll`. Launch the game and enter a repair view in the garage.

## Controls

| Default shortcut | Action |
| --- | --- |
| Left Alt + Left Shift + 1–4 | Save or overwrite a slot with the current view and sensitivity |
| Left Alt + 1–4 | Recall a slot |
| Left Alt + [ or ] | Decrease or increase repair orbit sensitivity by 10 percentage points |
| Left Alt + 0 | Return repair orbit sensitivity to 100% |

The four slots start empty and are named Engine Bay, Undercarriage, Wheels, and Engine Stand. Set up a view with the game's controls, then save it to the slot you want. For the same framing, return to the same focus before recalling. Recall around a different focus reuses the angle and zoom for that focus.

Presets work in garage orbit views used for car repairs, inspection, and engine-stand part selection. Close the radial menu and finish any active bolt operation before using the shortcuts. CMS displays feedback through its normal notifications.

## Configuration and removal

BepInEx creates `BepInEx/config/local.cms18.camerapresets.cfg` on first launch. Edit it with the game closed to rename slots, rebind shortcuts, or turn notifications off. The sensitivity buttons and recalled values affect the repair orbit until you reset them or restart the game. Saved slot values survive restarts in `BepInEx/config/local.cms18.camerapresets.json`.

To uninstall, close the game and remove `BepInEx/plugins/CameraPresets/CameraPresets.dll`. Keep the config and JSON to retain your settings and views for a later reinstall.
