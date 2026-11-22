# Camera Presets release notes

## 1.2.0

- Put camera controls and all four repair slots on one Escape-menu page.
- Added a 40°–130° FOV slider that carries across gameplay cameras.
- Replaced sensitivity steps in the menu with a 25%–300% slider shared by walking and repair controls.
- Persisted FOV and sensitivity separately from saved viewing positions.

## 1.1.0

- Added Camera Presets as its own section in the garage Escape menu.
- Added native pages to save, confirm overwrites, load and resume, and adjust look sensitivity.
- Added an in-menu notification toggle. Existing slots and shortcuts carry forward.

## 1.0.0

- Added four persistent camera slots for garage repair and inspection views.
- Saved orbit position, zoom distance, field of view, and look sensitivity in each slot.
- Added configurable keyboard shortcuts, slot names, and native HUD notifications.
- Added in-game sensitivity adjustment and a reset to the game's default.

## Build from source

Run `powershell -ExecutionPolicy Bypass -File .\build.ps1` in this directory.
Pass `-GamePath "C:\path\to\Car Mechanic Simulator 2018"` for a non-default Steam library.
The build uses the installed game's managed assemblies and BepInEx 5. It writes `build\CameraPresets.dll`.
