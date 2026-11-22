using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CMS18.CameraPresets
{
    [BepInPlugin(Id, "Camera Presets", "1.1.0")]
    [BepInProcess("cms2018.exe")]
    public sealed partial class CameraPresetsPlugin : BaseUnityPlugin
    {
        private const string Id = "local.cms18.camerapresets";
        private const int SlotCount = 4;
        private const float MinimumSensitivity = 0.25f;
        private const float MaximumSensitivity = 3f;
        private static CameraPresetsPlugin instance;

        private readonly ConfigEntry<string>[] names = new ConfigEntry<string>[SlotCount];
        private readonly ConfigEntry<KeyboardShortcut>[] saveKeys = new ConfigEntry<KeyboardShortcut>[SlotCount];
        private readonly ConfigEntry<KeyboardShortcut>[] recallKeys = new ConfigEntry<KeyboardShortcut>[SlotCount];
        private ConfigEntry<KeyboardShortcut> slowerKey;
        private ConfigEntry<KeyboardShortcut> fasterKey;
        private ConfigEntry<KeyboardShortcut> resetKey;
        private ConfigEntry<bool> showNotifications;
        private float sensitivity = 1f;
        private string presetPath;
        private PresetFile presets;
        private Harmony harmony;
        private OrbitFields fields;

        private void Awake()
        {
            string[] defaultNames = { "Engine Bay", "Undercarriage", "Wheels", "Engine Stand" };
            for (int i = 0; i < SlotCount; i++)
            {
                int number = i + 1;
                KeyCode key = KeyCode.Alpha1 + i;
                names[i] = Config.Bind("Presets", "Name" + number, defaultNames[i], "Display name for this slot.");
                saveKeys[i] = BindKey("Save" + number, key, "Overwrite this slot with the current repair view.", KeyCode.LeftShift);
                recallKeys[i] = BindKey("Recall" + number, key, "Apply this slot to the current repair focus.");
            }
            slowerKey = BindKey("DecreaseSensitivity", KeyCode.LeftBracket, "Decrease repair orbit sensitivity by 10 percentage points.");
            fasterKey = BindKey("IncreaseSensitivity", KeyCode.RightBracket, "Increase repair orbit sensitivity by 10 percentage points.");
            resetKey = BindKey("ResetSensitivity", KeyCode.Alpha0, "Restore the game's normal repair orbit sensitivity.");
            showNotifications = Config.Bind("General", "ShowNotifications", true, "Show feedback in the game's notification area.");

            presetPath = Path.Combine(Paths.ConfigPath, Id + ".json");
            presets = LoadPresets();

            fields = new OrbitFields();
            harmony = new Harmony(Id);
            MethodInfo getCameraLook = AccessTools.Method(typeof(OrbitCamera), "GetCameraLook", Type.EmptyTypes);
            if (getCameraLook == null || getCameraLook.ReturnType != typeof(Vector2))
                throw new MissingMethodException(typeof(OrbitCamera).FullName, "GetCameraLook");
            harmony.Patch(getCameraLook, postfix: new HarmonyMethod(typeof(CameraPresetsPlugin), "ScaleOrbitLook"));
            PatchPauseMenu();
            instance = this;
            Logger.LogInfo("Ready. Open Escape > Camera Presets in the garage.");
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
            ClosePresetPage();
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

            ApplyPendingPreset();
            if (!Application.isFocused || Time.timeScale <= 0f || !Input.anyKeyDown)
                return;

            OrbitCamera orbit = GetRepairOrbit();
            if (!orbit || fields.Blocked(orbit))
                return;

            PieMenuController pie = FindObjectOfType<PieMenuController>();
            if (pie && pie.GetIsPieMenuOpen())
                return;

            for (int i = 0; i < SlotCount; i++)
            {
                if (saveKeys[i].Value.IsDown())
                {
                    SaveSlot(i, orbit);
                    return;
                }
                if (recallKeys[i].Value.IsDown())
                {
                    RecallSlot(i, orbit);
                    return;
                }
            }

            if (resetKey.Value.IsDown())
                SetSensitivity(1f);
            else if (slowerKey.Value.IsDown())
                SetSensitivity(sensitivity - 0.1f);
            else if (fasterKey.Value.IsDown())
                SetSensitivity(sensitivity + 0.1f);
        }

        private void SaveSlot(int index, OrbitCamera orbit)
        {
            Preset view = fields.Capture(orbit, FocusHeading(orbit), sensitivity);
            if (!view.IsValid())
            {
                Notify("This view is unavailable.");
                return;
            }

            Notify(StoreSlot(index, view) ? "Saved " + SlotName(index) + "." : "Could not save " + SlotName(index) + ".");
        }

        private bool StoreSlot(int index, Preset view)
        {
            Preset previous = presets.slots[index];
            presets.slots[index] = view;
            string temporary = presetPath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(presets, true));
                if (File.Exists(presetPath))
                    File.Replace(temporary, presetPath, null);
                else
                    File.Move(temporary, presetPath);
            }
            catch (Exception error)
            {
                presets.slots[index] = previous;
                Logger.LogWarning("Could not write camera presets. " + error.Message);
                return false;
            }
            return true;
        }


        private void RecallSlot(int index, OrbitCamera orbit)
        {
            Preset view = presets.slots[index];
            if (view == null || !view.IsValid())
            {
                Notify(SlotName(index) + " is empty.");
                return;
            }
            ApplyView(orbit, view);
            Notify(SlotName(index) + " applied.");
        }

        private void ApplyView(OrbitCamera orbit, Preset view)
        {
            fields.Apply(orbit, view, FocusHeading(orbit));
            sensitivity = view.sensitivity;
        }

        private static float FocusHeading(OrbitCamera orbit)
        {
            if (!orbit.target)
                return 0f;

            CarLoader car = orbit.target.GetComponentInParent<CarLoader>();
            return car ? car.transform.eulerAngles.y : 0f;
        }

        private void SetSensitivity(float value)
        {
            sensitivity = Mathf.Clamp(value, MinimumSensitivity, MaximumSensitivity);
            Notify("Sensitivity " + SensitivityPercent);
        }

        private string SensitivityPercent
        {
            get { return (sensitivity * 100f).ToString("0", CultureInfo.InvariantCulture) + "%"; }
        }

        private string SlotName(int index)
        {
            string name = names[index].Value.Trim();
            if (name.Length == 0)
                return "Preset " + (index + 1);
            return name.Length > 32 ? name.Substring(0, 32) : name;
        }

        private PresetFile LoadPresets()
        {
            PresetFile result = new PresetFile();
            if (!File.Exists(presetPath))
                return result;

            try
            {
                PresetFile loaded = JsonUtility.FromJson<PresetFile>(File.ReadAllText(presetPath));
                if (loaded != null && loaded.version == 1 && loaded.slots != null)
                    Array.Copy(loaded.slots, result.slots, Math.Min(loaded.slots.Length, SlotCount));
                else
                    Logger.LogWarning("Camera preset file has an unsupported format.");
            }
            catch (Exception error)
            {
                Logger.LogWarning("Could not read camera presets. " + error.Message);
            }
            return result;
        }

        private void Notify(string message)
        {
            if (!showNotifications.Value)
                return;
            try
            {
                UIManager ui = UIManager.Get();
                if (ui && ui.transform.Find("MovingPopup"))
                    ui.ShowMovingPopup("Camera Presets", message);
            }
            catch (Exception error)
            {
                Logger.LogDebug("Camera notification unavailable. " + error.Message);
            }
        }

        [Serializable]
        private sealed class PresetFile
        {
            public int version = 1;
            public Preset[] slots = new Preset[SlotCount];
        }

        [Serializable]
        private sealed class Preset
        {
            public float yaw;
            public float pitch;
            public float distance;
            public float fieldOfView;
            public float sensitivity;

            public Preset WithSensitivity(float value)
            {
                Preset copy = (Preset)MemberwiseClone();
                copy.sensitivity = value;
                return copy;
            }

            public bool IsValid()
            {
                return yaw >= -180f && yaw <= 180f && pitch >= -180f && pitch <= 180f
                    && distance > 0f && !float.IsInfinity(distance)
                    && fieldOfView > 0f && fieldOfView < 180f
                    && sensitivity >= MinimumSensitivity && sensitivity <= MaximumSensitivity;
            }
        }

        private sealed class OrbitFields
        {
            private readonly FieldInfo yaw = Find<float>("xDeg");
            private readonly FieldInfo pitch = Find<float>("yDeg");
            private readonly FieldInfo minimumDistance = Find<float>("minDistance");
            private readonly FieldInfo maximumDistance = Find<float>("maxDistance");
            private readonly FieldInfo targetFov = Find<float>("targetFOV");
            private readonly FieldInfo previousPosition = Find<Vector3>("prevPosition");
            private readonly FieldInfo elapsedTransition = Find<float>("currentLerpTime");
            private readonly FieldInfo blocked = Find<bool>("block");

            public bool Blocked(OrbitCamera orbit)
            {
                return (bool)blocked.GetValue(orbit);
            }

            public Preset Capture(OrbitCamera orbit, float heading, float sensitivity)
            {
                return new Preset
                {
                    yaw = Mathf.DeltaAngle(heading, (float)yaw.GetValue(orbit)),
                    pitch = Mathf.DeltaAngle(0f, (float)pitch.GetValue(orbit)),
                    distance = orbit.GetDistance(),
                    fieldOfView = (float)targetFov.GetValue(orbit),
                    sensitivity = sensitivity
                };
            }

            public void Apply(OrbitCamera orbit, Preset view, float heading)
            {
                yaw.SetValue(orbit, Mathf.DeltaAngle(0f, heading + view.yaw));
                pitch.SetValue(orbit, view.pitch);
                orbit.SetDistance(Mathf.Clamp(view.distance,
                    (float)minimumDistance.GetValue(orbit), (float)maximumDistance.GetValue(orbit)));
                targetFov.SetValue(orbit, view.fieldOfView);
                previousPosition.SetValue(orbit, orbit.transform.position);
                elapsedTransition.SetValue(orbit, 0f);
            }

            private static FieldInfo Find<T>(string name)
            {
                FieldInfo field = AccessTools.Field(typeof(OrbitCamera), name);
                if (field == null || field.FieldType != typeof(T))
                    throw new MissingFieldException(typeof(OrbitCamera).FullName, name);
                return field;
            }
        }
    }
}
