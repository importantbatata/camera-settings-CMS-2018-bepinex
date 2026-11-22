<div align="center">

# Camera Settings · CMS 2018

### Your view. Your pace. Your garage.

**Field of view and look sensitivity, directly in the garage pause menu.**

[Download v1.3.0](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/releases/tag/v1.3.0) · [All releases](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/releases) · [Report an issue](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/issues)

</div>

Camera Settings is a **BepInEx 5 Mono mod for Car Mechanic Simulator 2018 on Windows**. Adjust your field of view and look sensitivity from a native-style page in the garage Escape menu, then return to your build with settings that carry across walking and repair views.

## Current version: 1.3.0

| Setting | What it does |
| --- | --- |
| **Field of View** | Sets the main gameplay camera's vertical FOV from **40° to 130°**. |
| **Look Sensitivity** | Adjusts walking and repair look controls from **25% to 300%**. |
| **Reset Camera Settings** | Restores the game's default FOV and **100%** sensitivity. |

In the garage, press **Esc → Camera Settings**, directly above **Continue**. Drag a slider, or select it and use the menu's left/right controls. Settings are saved when you leave the page.

**Version 1.3.0 focuses on camera settings.** It removes the four saved-view slots and their save/load shortcuts. Earlier FOV and sensitivity settings carry forward; previously saved preset files can be reused with an older release.

## Installation

1. Close CMS 2018 and ensure **BepInEx 5 Mono** and Harmony are installed and working.
2. Open the [v1.3.0 release](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/releases/tag/v1.3.0) and download **CameraSettings-1.3.0.zip** under Assets.
3. Extract the ZIP into the game folder containing `cms2018.exe` and `BepInEx`, merging the included folders.
4. Keep **one copy** of the plugin. Replace the previous DLL and store any rollback copy outside `BepInEx/plugins`.
5. Launch CMS 2018, enter the garage, and open **Esc → Camera Settings**.

```text
Car Mechanic Simulator 2018/
├── cms2018.exe
└── BepInEx/
    └── plugins/
        └── CameraPresets/
            └── CameraPresets.dll
```

The DLL and folder retain the name **CameraPresets** for compatibility with earlier versions. The in-game name is **Camera Settings** from 1.3.0 onward. Download the named mod ZIP under Assets; GitHub's automatic Source code archives contain the repository's source history rather than an install-ready plugin.

## Optional shortcuts and configuration

| Shortcut | Action |
| --- | --- |
| **Left Alt + [** | Reduce sensitivity by 10 percentage points. |
| **Left Alt + ]** | Increase sensitivity by 10 percentage points. |
| **Left Alt + 0** | Reset sensitivity to 100%. |

BepInEx stores shortcuts, FOV, sensitivity, and the notification preference in:

```text
BepInEx/config/local.cms18.camerapresets.cfg
```

The source uses plugin ID `local.cms18.camerapresets`. In the configuration, `FieldOfView = 0` means the game's default; explicit values use the 40–130 range. `LookSensitivity = 1` means 100%. Close the game before manually editing the configuration file.

## Release history

Release titles and tags use simple version numbers. Packages are published oldest to newest, so **v1.3.0 appears first** on the Releases page. Each ZIP is a complete version; install one version at a time.

| Version | Main changes |
| --- | --- |
| [v1.3.0](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/releases/tag/v1.3.0) | Renames the mod and menu page to **Camera Settings**. Removes saved-view slots and save/load shortcuts; retains FOV, sensitivity, reset, and existing settings. |
| [v1.2.1](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/releases/tag/v1.2.1) | Moves Camera Presets directly above **Continue** in the garage Escape menu. |
| [v1.2.0](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/releases/tag/v1.2.0) | Combines controls and four repair slots on one page. Adds 40°–130° FOV and 25%–300% sensitivity sliders, saved separately from viewing positions. |
| [v1.1.0](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/releases/tag/v1.1.0) | Adds native Escape-menu pages for saving, overwrite confirmation, loading/resuming, sensitivity, and notification control. |
| [v1.0.0](https://github.com/importantbatata/camera-settings-CMS-2018-bepinex/releases/tag/v1.0.0) | Introduces four persistent garage repair/inspection camera slots, saved orbit/zoom/FOV/sensitivity, configurable shortcuts and names, and HUD notifications. |

The original cumulative [changelog](Source/1.3.0/CHANGELOG.md) and each version's README are included with the source history. Choose **v1.2.1 or earlier** if you specifically need saved camera views.

## Source and building

Each supplied source snapshot is preserved under `Source/<version>/`. The latest source is in [Source/1.3.0](Source/1.3.0):

- `CameraPresets.cs` contains plugin registration, settings persistence, sensitivity scaling, FOV application, and camera hooks.
- `PauseMenu.cs` adds the native menu entry and settings controls.
- `build.ps1` compiles the plugin against your installed CMS, Unity, BepInEx, and Harmony assemblies.

On Windows, open PowerShell in `Source/1.3.0` and run:

```powershell
.\build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\Car Mechanic Simulator 2018'
```

Omit `-GamePath` for the default Steam installation. The supplied script uses the .NET Framework 64-bit C# compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, checks required dependencies, and writes `build/CameraPresets.dll`. Copy that output into the plugin folder with CMS closed. Game and loader dependency assemblies are not included here.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| Camera Settings is absent | Enter the garage, check the DLL path and BepInEx 5 Mono installation, then inspect `BepInEx/LogOutput.log`. Look for “Ready. Open Escape > Camera Settings in the garage.” |
| Saved-view slots are missing | They were removed in 1.3.0. Use an earlier release if you need that feature. |
| Controls conflict with another mod | Check for other FOV, camera-input, or pause-menu modifications; review the log and ensure only one copy of this plugin is loaded. |
| Build dependency is missing | Check `-GamePath`, BepInEx/core, the game's Managed folder, and the .NET Framework compiler path. |

To uninstall, close CMS and remove `BepInEx/plugins/CameraPresets/CameraPresets.dll`.

## Package verification

All five supplied release ZIPs passed archive-integrity checks and are published without repacking. Sizes and SHA-256 hashes are recorded in [RELEASE-MANIFEST.json](RELEASE-MANIFEST.json). Source snapshots are preserved from the supplied folder. This publication did not rebuild the DLLs, independently verify each binary against its source, or perform in-game testing.

For reports, include the mod version, game/loader versions, reproduction steps, and relevant log entries. This is an unofficial mod and is not affiliated with or endorsed by the game's developers or publishers.
