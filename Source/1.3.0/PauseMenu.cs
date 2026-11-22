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
        private const string EntryName = "CMS18CameraPresets";
        private static readonly FieldInfo MenuItems = AccessTools.Field(typeof(ButtonsWindowMenu), "items");
        private static readonly FieldInfo SelectedItem = AccessTools.Field(typeof(ButtonsWindowMenu), "selectedObject");
        private static readonly MethodInfo NativeButton = AccessTools.DeclaredMethod(typeof(PauseQuitMenu),
            "CreateButtonWithAction", new[] { typeof(Action), typeof(string) });

        private PauseQuitMenu openMenu;
        private Transform buttons;
        private Transform parkedButtons;
        private Text title;
        private string pauseTitle;
        private Slider fovSlider;
        private Slider sensitivitySlider;
        private Text fovLabel;
        private bool resettingSliders;

        private void PatchPauseMenu()
        {
            harmony.Patch(AccessTools.DeclaredMethod(typeof(PauseQuitMenu), "Show"),
                postfix: MenuPatch("AfterPauseShow"));
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

        private static void AfterPauseShow(PauseQuitMenu __instance)
        {
            CameraPresetsPlugin mod = instance;
            if (mod == null || mod.openMenu == __instance)
                return;

            GameScript game = GameScript.Get();
            if (!game || game.CurrentSceneType != SceneType.Garage || !__instance.IsActive())
                return;

            try
            {
                Transform group = __instance.transform.Find("Content/Buttons");
                List<GameObject> items = (List<GameObject>)MenuItems.GetValue(__instance);
                if (!group || items == null || group.Find(EntryName))
                    return;

                string continueLabel = Localization.Instance.Get("GUI_Pause_ContinueButton");
                int index = items.FindIndex(delegate(GameObject item)
                {
                    Transform label = item ? item.transform.Find("Text") : null;
                    Text text = label ? label.GetComponent<Text>() : null;
                    return text && text.text == continueLabel;
                });

                GameObject entry = AddNativeButton(__instance, group, "Camera Settings", EntryName,
                    delegate { mod.OpenCameraPage(__instance); });
                if (index >= 0)
                {
                    entry.transform.SetSiblingIndex(items[index].transform.GetSiblingIndex());
                    items.Insert(index, entry);
                }
                else
                    items.Add(entry);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group);
            }
            catch (Exception error)
            {
                mod.Logger.LogWarning("Could not add Camera Settings to the Escape menu. " + error.Message);
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
                mod.CloseCameraPage();
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
                mod.CloseCameraPage();
        }

        private void OpenCameraPage(PauseQuitMenu menu)
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
                ShowCameraPage();
            }
            catch (Exception error)
            {
                Logger.LogWarning("Could not open Camera Settings. " + error.Message);
                CloseCameraPage();
            }
        }

        private void ShowCameraPage()
        {
            title.text = "Camera Settings";
            MenuItems.SetValue(openMenu, new List<GameObject>());
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
            AddPageButton("Reset Camera Settings", ResetCameraSettings);
            AddPageButton("Back", CloseCameraPage);
            FinishPage(0);
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

        private void CloseCameraPage()
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

