# Camera Presets release notes

## 1.0.0

- Added four persistent camera slots for garage repair and inspection views.
- Saved orbit position, zoom distance, field of view, and look sensitivity in each slot.
- Added configurable keyboard shortcuts, slot names, and native HUD notifications.
- Added in-game sensitivity adjustment and a reset to the game's default.

## Build from source

Run `powershell -ExecutionPolicy Bypass -File .\build.ps1` in this directory.
Pass `-GamePath "C:\path\to\Car Mechanic Simulator 2018"` for a non-default Steam library.
The build uses the installed game's managed assemblies and BepInEx 5. It writes `build\CameraPresets.dll`.
