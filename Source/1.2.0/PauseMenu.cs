using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CMS18.CameraPresets
{
    public sealed partial class CameraPresetsPlugin
    {
        private const string PauseWindow = "PauseQuitMenu";
        private const string EntryName = "CMS18CameraPresets";
        private static readonly FieldInfo MenuItems = AccessTools.Field(typeof(ButtonsWindowMenu), "items");
        private static readonly FieldInfo SelectedItem = AccessTools.Field(typeof(ButtonsWindowMenu), "selectedObject");
        private static readonly MethodInfo NativeButton = AccessTools.DeclaredMethod(typeof(PauseQuitMenu),
            "CreateButtonWithAction", new[] { typeof(Action), typeof(string) });

        private OrbitCamera pausedOrbit;
        private Preset pausedView;
        private OrbitCamera pendingOrbit;
        private Preset pendingView;
        private string pendingName;
        private float recallDeadline;
        private PauseQuitMenu openMenu;
        private Transform buttons;
        private Transform parkedButtons;
        private Text title;
        private string pauseTitle;
        private int selectedSlot;
        private bool confirmingOverwrite;
        private Slider fovSlider;
        private Slider sensitivitySlider;
        private Text fovLabel;
        private Text slotLabel;
        private Text saveLabel;
        private Text loadLabel;
        private bool resettingSliders;
        private bool openingPause;

        private void PatchPauseMenu()
        {
            harmony.Patch(AccessTools.DeclaredMethod(typeof(PauseQuitMenu), "Show"),
                prefix: MenuPatch("BeforePauseShow"), postfix: MenuPatch("AfterPauseShow"));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(ButtonsWindowMenu), "Update"),
                prefix: MenuPatch("BeforeMenuUpdate"));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(PauseQuitMenu), "Hide"),
                prefix: MenuPatch("BeforePauseHide"));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(PauseQuitMenu), "HideWithoutChangeMode"),
                prefix: MenuPatch("BeforePauseHide"));
        }

        private static HarmonyMethod MenuPatch(string method)
        {
            return new HarmonyMethod(typeof(CameraPresetsPlugin), method);
        }

        private static void BeforePauseShow(PauseQuitMenu __instance)
        {
            CameraPresetsPlugin mod = instance;
            if (mod == null || __instance.IsActive())
                return;

            mod.openingPause = true;
            OrbitCamera orbit = mod.GetRepairOrbit();
            mod.pausedOrbit = orbit && !mod.fields.Blocked(orbit) ? orbit : null;
            mod.pausedView = mod.pausedOrbit
                ? mod.fields.Capture(orbit, FocusHeading(orbit), mod.sensitivity)
                : null;
        }

        private static void AfterPauseShow(PauseQuitMenu __instance)
        {
            CameraPresetsPlugin mod = instance;
            if (mod == null || !mod.openingPause)
                return;
            mod.openingPause = false;

            GameScript game = GameScript.Get();
            if (!game || game.CurrentSceneType != SceneType.Garage || !__instance.IsActive())
                return;

            try
            {
                Transform group = __instance.transform.Find("Content/Buttons");
                List<GameObject> items = (List<GameObject>)MenuItems.GetValue(__instance);
                if (group && items != null && !group.Find(EntryName))
                    items.Add(AddNativeButton(__instance, group, "Camera Presets", EntryName,
                        delegate { mod.OpenPresetPage(__instance); }));
            }
            catch (Exception error)
            {
                mod.Logger.LogWarning("Could not add Camera Presets to the Escape menu. " + error.Message);
            }
        }

        private static bool BeforeMenuUpdate(ButtonsWindowMenu __instance)
        {
            CameraPresetsPlugin mod = instance;
            if (mod == null || mod.openMenu != __instance || !__instance.IsActive())
                return true;

            InputManager input = InputManager.Instance;
            if ((input && input.UICancelButtonDown()) || Input.GetKeyDown(KeyCode.Escape))
            {
                mod.Back();
                return false;
            }

            GameObject row = (GameObject)SelectedItem.GetValue(__instance);
            Slider slider = row ? row.GetComponentInChildren<Slider>() : null;
            if (slider && input)
            {
                if (input.UIHorizontalButtonLeftDown())
                    slider.value -= 1f;
                else if (input.UIHorizontalButtonRightDown())
                    slider.value += 1f;
            }
            return true;
        }

        private static void BeforePauseHide(PauseQuitMenu __instance)
        {
            CameraPresetsPlugin mod = instance;
            if (mod != null && mod.openMenu == __instance)
                mod.ClosePresetPage();
        }

        private void OpenPresetPage(PauseQuitMenu menu)
        {
            if (openMenu)
                return;

            buttons = menu.transform.Find("Content/Buttons");
            Transform heading = menu.transform.Find("Content/TextBG/Text");
            title = heading ? heading.GetComponent<Text>() : null;
            if (!buttons || !title)
                return;

            openMenu = menu;
            pauseTitle = title.text;
            parkedButtons = new GameObject("CameraPresetsPausedButtons", typeof(RectTransform)).transform;
            parkedButtons.SetParent(menu.transform, false);
            parkedButtons.gameObject.SetActive(false);

            while (buttons.childCount > 0)
                buttons.GetChild(0).SetParent(parkedButtons, false);

            try
            {
                ShowCameraPage(0, null);
            }
            catch (Exception error)
            {
                Logger.LogWarning("Could not open Camera Presets. " + error.Message);
                ClosePresetPage();
            }
        }

        private void ShowCameraPage(int selection, string status)
        {
            confirmingOverwrite = false;
            StartPage(status ?? "Camera Presets");
            fovSlider = AddSlider(MinimumFov, MaximumFov, DisplayFov(), FovText(), delegate(float value, Text label)
            {
                if (resettingSliders)
                    return;
                fieldOfView = Mathf.RoundToInt(value);
                UpdateFov();
                label.text = FovText();
            }, out fovLabel);
            Text unused;
            sensitivitySlider = AddSlider(MinimumSensitivity * 100f, MaximumSensitivity * 100f,
                Mathf.RoundToInt(sensitivity * 100f), "Look Sensitivity  " + SensitivityPercent,
                delegate(float value, Text label)
                {
                    if (!resettingSliders)
                        sensitivity = value / 100f;
                    label.text = "Look Sensitivity  " + SensitivityPercent;
                }, out unused);
            AddSlider(1f, SlotCount, selectedSlot + 1, SlotText(), delegate(float value, Text label)
            {
                selectedSlot = Mathf.RoundToInt(value) - 1;
                label.text = SlotText();
                UpdateSlotActions();
            }, out slotLabel);
            saveLabel = AddPageButton("Save View", SaveSelectedSlot);
            loadLabel = AddPageButton("Load and Resume", LoadSelectedSlot);
            UpdateSlotActions();
            AddPageButton("Reset Camera Settings", ResetCameraSettings);
            AddPageButton("Back", ClosePresetPage);
            FinishPage(selection);
        }

        private void UpdateSlotActions()
        {
            saveLabel.text = HasPreset(selectedSlot) ? "Overwrite View" : "Save View";
            loadLabel.text = HasPreset(selectedSlot) ? "Load and Resume" : "Load View  (Empty)";
        }

        private void SaveSelectedSlot()
        {
            if (!CanUseRepairView())
                return;
            if (!HasPreset(selectedSlot))
            {
                SaveFromMenu(selectedSlot);
                return;
            }

            confirmingOverwrite = true;
            StartPage("Overwrite " + SlotName(selectedSlot) + "?");
            AddPageButton("Overwrite", delegate { SaveFromMenu(selectedSlot); });
            AddPageButton("Cancel", Back);
            FinishPage(1);
        }

        private void LoadSelectedSlot()
        {
            if (!HasPreset(selectedSlot))
            {
                title.text = "This preset is empty";
                return;
            }
            if (CanUseRepairView())
                RecallFromMenu(selectedSlot);
        }

        private bool CanUseRepairView()
        {
            if (pausedOrbit && pausedView != null && pausedView.IsValid())
                return true;
            title.text = "Open a repair view to save or load";
            return false;
        }

        private string SlotText()
        {
            return (selectedSlot + 1) + " / " + SlotCount + "  " + SlotName(selectedSlot)
                + (HasPreset(selectedSlot) ? "" : " (Empty)");
        }

        private void SaveFromMenu(int slot)
        {
            if (pausedView == null)
                return;
            // Copy the frozen camera state. Sensitivity may have changed on the settings page.
            Preset view = pausedView.WithSensitivity(sensitivity);
            string status = StoreSlot(slot, view) ? "Saved " + SlotName(slot) : "Could not save " + SlotName(slot);
            if (confirmingOverwrite)
                ShowCameraPage(3, status);
            else
            {
                title.text = status;
                slotLabel.text = SlotText();
                UpdateSlotActions();
            }
        }

        private void RecallFromMenu(int slot)
        {
            if (!pausedOrbit || !HasPreset(slot))
                return;
            pendingOrbit = pausedOrbit;
            pendingView = presets.slots[slot];
            pendingName = SlotName(slot);
            recallDeadline = Time.realtimeSinceStartup + 2f;
            UIManager.Get().Hide(PauseWindow);
        }

        private void ApplyPendingPreset()
        {
            if (pendingView == null)
                return;

            OrbitCamera orbit = GetRepairOrbit();
            if (orbit && orbit == pendingOrbit && !fields.Blocked(orbit))
            {
                Preset view = pendingView;
                pendingView = null;
                pendingOrbit = null;
                ApplyView(orbit, view);
                Notify(pendingName + " applied.");
            }
            else if (!pendingOrbit || Time.realtimeSinceStartup > recallDeadline)
            {
                pendingView = null;
                pendingOrbit = null;
                Notify("Could not return to the repair view.");
            }
        }

        private bool HasPreset(int index)
        {
            Preset preset = presets.slots[index];
            return preset != null && preset.IsValid();
        }

        private int DisplayFov()
        {
            if (fieldOfView != 0)
                return fieldOfView;
            Camera camera = GameplayCamera();
            return camera ? Mathf.RoundToInt(camera.fieldOfView) : 60;
        }

        private string FovText()
        {
            return "Field of View  " + DisplayFov() + "°" + (fieldOfView == 0 ? " (Default)" : "");
        }

        private void ResetCameraSettings()
        {
            sensitivity = 1f;
            fieldOfView = 0;
            UpdateFov();
            resettingSliders = true;
            fovSlider.value = DisplayFov();
            sensitivitySlider.value = 100f;
            resettingSliders = false;
            fovLabel.text = FovText();
            SaveCameraSettings();
            title.text = "Camera settings reset";
        }

        private void Back()
        {
            if (confirmingOverwrite)
                ShowCameraPage(3, null);
            else
                ClosePresetPage();
        }

        private void StartPage(string heading)
        {
            RemovePageButtons();
            title.text = heading;
            MenuItems.SetValue(openMenu, new List<GameObject>());
        }

        private Text AddPageButton(string label, Action action)
        {
            List<GameObject> items = (List<GameObject>)MenuItems.GetValue(openMenu);
            GameObject row = AddNativeButton(openMenu, buttons, label, "CameraPresetAction" + items.Count, action);
            items.Add(row);
            return row.transform.Find("Text").GetComponent<Text>();
        }

        private Slider AddSlider(float minimum, float maximum, float value, string caption,
            Action<float, Text> change, out Text label)
        {
            Text text = AddPageButton(caption, delegate { });
            label = text;
            RectTransform row = (RectTransform)text.transform.parent;
            RectTransform labelRect = text.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = new Vector2(0.53f, 1f);
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleLeft;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = text.fontSize;
            text.raycastTarget = false;

            RectTransform control = SliderRect("Slider", row, new Vector2(0.56f, 0.15f), new Vector2(0.95f, 0.85f));
            Slider slider = control.gameObject.AddComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.wholeNumbers = true;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            Image hitArea = control.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0.01f);
            Image track = SliderImage("Track", control, new Vector2(0f, 0.41f), new Vector2(1f, 0.59f),
                new Color(0.12f, 0.12f, 0.12f, 0.9f));
            Image fill = SliderImage("Fill", track.rectTransform, Vector2.zero, Vector2.one,
                new Color(0.83f, 0.83f, 0.83f, 0.85f));
            RectTransform handleArea = SliderRect("Handle Area", control, Vector2.zero, Vector2.one);
            handleArea.offsetMin = new Vector2(7f, 0f);
            handleArea.offsetMax = new Vector2(-7f, 0f);
            Image handle = SliderImage("Handle", handleArea, new Vector2(0f, 0.17f), new Vector2(0f, 0.83f),
                Color.white);
            handle.rectTransform.sizeDelta = new Vector2(14f, 0f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.value = value;
            slider.onValueChanged.AddListener(delegate(float next)
            {
                if (!resettingSliders && openMenu)
                    openMenu.SelectItem(row.gameObject);
                change(next, text);
            });
            return slider;
        }

        private static RectTransform SliderRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image SliderImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            Image image = SliderRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void FinishPage(int selection)
        {
            List<GameObject> items = (List<GameObject>)MenuItems.GetValue(openMenu);
            openMenu.SelectItem(items[Mathf.Clamp(selection, 0, items.Count - 1)]);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)buttons);
        }

        private void RemovePageButtons()
        {
            while (buttons.childCount > 0)
            {
                Transform child = buttons.GetChild(0);
                child.gameObject.SetActive(false);

                child.SetParent(openMenu.transform, false);
                Destroy(child.gameObject);
            }
        }

        private void ClosePresetPage()
        {
            if (!openMenu || !buttons || !parkedButtons)
                return;

            SaveCameraSettings();
            RemovePageButtons();
            List<GameObject> restored = new List<GameObject>();
            while (parkedButtons.childCount > 0)
            {
                Transform child = parkedButtons.GetChild(0);
                child.SetParent(buttons, false);
                restored.Add(child.gameObject);
            }

            MenuItems.SetValue(openMenu, restored);
            Transform entry = buttons.Find(EntryName);
            if (entry)
                openMenu.SelectItem(entry.gameObject);
            if (title)
                title.text = pauseTitle;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)buttons);
            Destroy(parkedButtons.gameObject);
            openMenu = null;
            parkedButtons = null;
            buttons = null;
            title = null;
        }

        private static GameObject AddNativeButton(PauseQuitMenu menu, Transform group,
            string label, string objectName, Action action)
        {
            int index = group.childCount;
            NativeButton.Invoke(menu, new object[] { action, "GUI_Pause_ContinueButton" });
            Transform button = group.GetChild(index);
            button.name = objectName;
            Text text = button.Find("Text").GetComponent<Text>();
            text.supportRichText = false;
            text.text = label;
            return button.gameObject;
        }
    }
}
