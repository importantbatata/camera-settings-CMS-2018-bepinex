using System;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CMS18.CameraPresets
{
    [BepInPlugin(Id, "Camera Settings", "1.3.0")]
    [BepInProcess("cms2018.exe")]
    public sealed partial class CameraPresetsPlugin : BaseUnityPlugin
    {
        private const string Id = "local.cms18.camerapresets";
        private const float MinimumSensitivity = 0.25f;
        private const float MaximumSensitivity = 3f;
        private const int MinimumFov = 40;
        private const int MaximumFov = 130;
        private static CameraPresetsPlugin instance;

        private ConfigEntry<KeyboardShortcut> slowerKey;
        private ConfigEntry<KeyboardShortcut> fasterKey;
        private ConfigEntry<KeyboardShortcut> resetKey;
        private ConfigEntry<bool> showNotifications;
        private ConfigEntry<float> savedSensitivity;
        private ConfigEntry<int> savedFov;
        private int fieldOfView;
        private Camera adjustedCamera;
        private float nativeFov;
        private float sensitivity = 1f;
        private Harmony harmony;

        private void Awake()
        {
            slowerKey = BindKey("DecreaseSensitivity", KeyCode.LeftBracket, "Decrease look sensitivity by 10 percentage points.");
            fasterKey = BindKey("IncreaseSensitivity", KeyCode.RightBracket, "Increase look sensitivity by 10 percentage points.");
            resetKey = BindKey("ResetSensitivity", KeyCode.Alpha0, "Restore the game's normal look sensitivity.");
            showNotifications = Config.Bind("General", "ShowNotifications", true, "Show feedback in the game's notification area.");
            savedSensitivity = Config.Bind("Camera", "LookSensitivity", 1f,
                new ConfigDescription("Look sensitivity multiplier for garage walking and repair cameras.",
                    new AcceptableValueRange<float>(MinimumSensitivity, MaximumSensitivity)));
            savedFov = Config.Bind("Camera", "FieldOfView", 0,
                "Vertical field of view in degrees. Set to 0 for the game's default, or use 40 to 130.");
            sensitivity = Mathf.Clamp(savedSensitivity.Value, MinimumSensitivity, MaximumSensitivity);
            fieldOfView = savedFov.Value == 0 ? 0 : Mathf.Clamp(savedFov.Value, MinimumFov, MaximumFov);

            harmony = new Harmony(Id);
            MethodInfo getCameraLook = AccessTools.Method(typeof(OrbitCamera), "GetCameraLook", Type.EmptyTypes);
            if (getCameraLook == null || getCameraLook.ReturnType != typeof(Vector2))
                throw new MissingMethodException(typeof(OrbitCamera).FullName, "GetCameraLook");
            harmony.Patch(getCameraLook, postfix: new HarmonyMethod(typeof(CameraPresetsPlugin), "ScaleOrbitLook"));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(FPSCamera), "GetCameraLook"),
                postfix: new HarmonyMethod(typeof(CameraPresetsPlugin), "ScaleWalkingLook"));
            HarmonyMethod reapplyFov = new HarmonyMethod(typeof(CameraPresetsPlugin), "ReapplyFov");
            harmony.Patch(AccessTools.DeclaredMethod(typeof(CameraManager), "ChangeCamera"), postfix: reapplyFov);
            harmony.Patch(AccessTools.DeclaredMethod(typeof(FPSCamera), "UpdateFOV"), postfix: reapplyFov);
            harmony.Patch(AccessTools.DeclaredMethod(typeof(OrbitCamera), "Update"), postfix: reapplyFov);
            PatchPauseMenu();
            instance = this;
            Camera.onPreCull += BeforeCameraRender;
            Logger.LogInfo("Ready. Open Escape > Camera Settings in the garage.");
        }

        private ConfigEntry<KeyboardShortcut> BindKey(string name, KeyCode key, string description, params KeyCode[] modifiers)
        {
            KeyCode[] keys = new KeyCode[modifiers.Length + 1];
            keys[0] = KeyCode.LeftAlt;
            Array.Copy(modifiers, 0, keys, 1, modifiers.Length);
            return Config.Bind("Controls", name, new KeyboardShortcut(key, keys), description);
        }

        private void OnDestroy()
        {
            CloseCameraPage();
            Camera.onPreCull -= BeforeCameraRender;
            RestoreFov();
            if (instance == this)
                instance = null;
            if (harmony != null)
                harmony.UnpatchSelf();
        }

        private static void ScaleOrbitLook(OrbitCamera __instance, ref Vector2 __result)
        {
            CameraPresetsPlugin mod = instance;
            if (mod != null && mod.sensitivity != 1f && mod.GetRepairOrbit() == __instance)
                __result *= mod.sensitivity;
        }

        private static void ScaleWalkingLook(ref Vector2 __result)
        {
            CameraPresetsPlugin mod = instance;
            if (mod != null && mod.sensitivity != 1f)
                __result *= mod.sensitivity;
        }

        private Camera GameplayCamera()
        {
            GameScript game = GameScript.Get();
            if (!game || game.CurrentSceneType == SceneType.Menu || game.CurrentSceneType == SceneType.None)
                return null;
            Camera camera = Camera.main;
            return camera && !camera.orthographic ? camera : null;
        }

        private static void ReapplyFov()
        {
            CameraPresetsPlugin mod = instance;
            if (mod != null && mod.fieldOfView != 0)
                mod.UpdateFov();
        }

        private void BeforeCameraRender(Camera camera)
        {
            if (fieldOfView != 0 && camera == adjustedCamera)
                camera.fieldOfView = fieldOfView;
        }

        private void UpdateFov()
        {
            Camera camera = fieldOfView == 0 ? null : GameplayCamera();
            if (camera != adjustedCamera)
            {
                RestoreFov();
                if (camera)
                {
                    adjustedCamera = camera;
                    nativeFov = camera.fieldOfView;
                }
            }
            if (camera && camera.fieldOfView != fieldOfView)
                camera.fieldOfView = fieldOfView;
        }

        private void RestoreFov()
        {
            if (adjustedCamera)
                adjustedCamera.fieldOfView = nativeFov;
            adjustedCamera = null;
        }

        private OrbitCamera GetRepairOrbit()
        {
            GameScript game = GameScript.Get();
            GameMode mode = GameMode.Get();
            CameraManager camera = CameraManager.Get();
            if (!game || game.CurrentSceneType != SceneType.Garage || !mode || !camera
                || camera.CurrentCameraState != CameraState.Orbit)
                return null;

            switch (mode.GetCurrentMode())
            {
                case gameMode.GarageDisassemble:
                case gameMode.GarageAssemble:
                case gameMode.InteriorDisassemble:
                case gameMode.InteriorAssemble:
                case gameMode.PartSelect:
                case gameMode.PartSelectMount:
                case gameMode.ExamineTools:
                case gameMode.ExamineCondition:
                    OrbitCamera orbit = camera.MyOrbitCamera;
                    return orbit && orbit.isActiveAndEnabled && !orbit.SimulateInputMode ? orbit : null;
                default:
                    return null;
            }
        }

        private void LateUpdate()
        {
            if (instance != this)
                return;

            UpdateFov();
            if (!Application.isFocused || Time.timeScale <= 0f || !Input.anyKeyDown)
                return;

            if (openMenu || !GameplayCamera())
                return;

            PieMenuController pie = FindObjectOfType<PieMenuController>();
            if (pie && pie.GetIsPieMenuOpen())
                return;

            if (resetKey.Value.IsDown())
                SetSensitivity(1f);
            else if (slowerKey.Value.IsDown())
                SetSensitivity(sensitivity - 0.1f);
            else if (fasterKey.Value.IsDown())
                SetSensitivity(sensitivity + 0.1f);
        }

        private void SetSensitivity(float value)
        {
            sensitivity = Mathf.Clamp(value, MinimumSensitivity, MaximumSensitivity);
            SaveCameraSettings();
            Notify("Sensitivity " + SensitivityPercent);
        }

        private void SaveCameraSettings()
        {
            if (savedSensitivity.Value == sensitivity && savedFov.Value == fieldOfView)
                return;

            bool autoSave = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try
            {
                savedSensitivity.Value = sensitivity;
                savedFov.Value = fieldOfView;
                Config.Save();
            }
            catch (Exception error)
            {
                Logger.LogWarning("Could not save camera settings. " + error.Message);
            }
            finally
            {
                Config.SaveOnConfigSet = autoSave;
            }
        }

        private string SensitivityPercent
        {
            get { return (sensitivity * 100f).ToString("0", CultureInfo.InvariantCulture) + "%"; }
        }

        private void Notify(string message)
        {
            if (!showNotifications.Value)
                return;
            try
            {
                UIManager ui = UIManager.Get();
                if (ui && ui.transform.Find("MovingPopup"))
                    ui.ShowMovingPopup("Camera Settings", message);
            }
            catch (Exception error)
            {
                Logger.LogDebug("Camera notification unavailable. " + error.Message);
            }
        }
    }
}

