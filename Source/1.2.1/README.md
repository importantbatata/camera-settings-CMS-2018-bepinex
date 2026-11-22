# Camera Presets for Car Mechanic Simulator 2018

Version 1.2.1 · Windows · BepInEx 5 Mono

Save four garage repair views. Each preset remembers orbit angle and zoom distance. Recall applies the saved view around the part or assembly the camera is currently focused on. It keeps the current part selection and uses the game's camera limits. On a car, the orbit angle follows the car's heading.

Field of view and look sensitivity are shared settings. FOV applies to the main gameplay camera when you walk around or switch into a repair view. Look sensitivity applies to walking and repair orbit controls. Both settings carry forward when you load a view or restart the game.

## Install

1. Close CMS 2018.
2. Extract the release ZIP into the game directory containing `cms2018.exe` and your existing BepInEx 5 installation.
3. Check that the DLL landed at `BepInEx/plugins/CameraPresets/CameraPresets.dll`. Launch the game and enter a repair view in the garage.

## Escape menu

Open the garage's Escape menu and select **Camera Presets**, directly above **Continue**. Drag **Field of View** (40°–130°) and **Look Sensitivity** (25%–300%) to set them. With a slider selected, the menu's left and right controls adjust it one step at a time. **Reset Camera Settings** restores the stock FOV and 100% sensitivity.

1. Set your view using the normal camera controls while repairing a car or working at the engine stand.
2. Open **Camera Presets**, select a slot with the preset slider, and choose **Save View**. An occupied slot asks before overwriting.
3. Select a saved slot and choose **Load and Resume** to return to your current repair focus with the saved angle and zoom.

The four slots start empty and are named Engine Bay, Undercarriage, Wheels, and Engine Stand. Presets work in garage orbit views used for repairs and inspection. Finish any active bolt operation before opening the Escape menu. Views saved in earlier versions carry forward.

## Optional shortcuts

| Default shortcut | Action |
| --- | --- |
| Left Alt + Left Shift + 1–4 | Save or overwrite a slot with the current view |
| Left Alt + 1–4 | Recall a slot |
| Left Alt + [ or ] | Decrease or increase repair orbit sensitivity by 10 percentage points |
| Left Alt + 0 | Return repair orbit sensitivity to 100% |


## Configuration and removal

BepInEx creates `BepInEx/config/local.cms18.camerapresets.cfg` on first launch. Edit it with the game closed to rename slots, rebind shortcuts, or turn notifications off. Saved repair views are in `BepInEx/config/local.cms18.camerapresets.json`.

To uninstall, close the game and remove `BepInEx/plugins/CameraPresets/CameraPresets.dll`. Keep the config and JSON to retain your settings and views for a later reinstall.
