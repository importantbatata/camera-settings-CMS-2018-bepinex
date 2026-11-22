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
        private int page;
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
            if ((!input || !input.UICancelButtonDown()) && !Input.GetKeyDown(KeyCode.Escape))
                return true;

            mod.Back();
            return false;
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

            ShowSlots(0);
        }

        private void ShowSlots(int selection)
        {
            StartPage(-1, "Camera Presets");
            for (int i = 0; i < SlotCount; i++)
            {
                int slot = i;
                string label = SlotName(slot) + (HasPreset(slot) ? "" : "  (Empty)");
                AddPageButton(label, delegate { ShowSlot(slot, null, 0); });
            }
            AddPageButton("Look Sensitivity", delegate { ShowSensitivity(0); });
            AddPageButton("Back", ClosePresetPage);
            FinishPage(selection);
        }

        private void ShowSlot(int slot, string status, int selection)
        {
            bool canUseView = pausedOrbit && pausedView != null && pausedView.IsValid();
            string heading = status ?? SlotName(slot);
            if (!canUseView && status == null)
                heading = "Open a repair view first";

            StartPage(slot, heading);
            if (canUseView)
            {
                AddPageButton(HasPreset(slot) ? "Overwrite with Current View" : "Save Current View", delegate
                {
                    if (HasPreset(slot))
                        ConfirmOverwrite(slot);
                    else
                        SaveFromMenu(slot);
                });

                if (HasPreset(slot))
                    AddPageButton("Load and Resume", delegate { RecallFromMenu(slot); });
            }
            AddPageButton("Back", Back);
            FinishPage(selection);
        }

        private void ConfirmOverwrite(int slot)
        {
            StartPage(SlotCount + 1 + slot, "Overwrite " + SlotName(slot) + "?");
            AddPageButton("Overwrite", delegate { SaveFromMenu(slot); });
            AddPageButton("Cancel", Back);
            FinishPage(1);
        }

        private void SaveFromMenu(int slot)
        {
            if (pausedView == null)
                return;
            // Copy the frozen camera state. Sensitivity may have changed on the settings page.
            Preset view = pausedView.WithSensitivity(sensitivity);
            string status = StoreSlot(slot, view) ? "Saved " + SlotName(slot) : "Could not save " + SlotName(slot);
            ShowSlot(slot, status, HasPreset(slot) ? 1 : 0);
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

        private void ShowSensitivity(int selection)
        {
            StartPage(SlotCount, "Look Sensitivity  " + SensitivityPercent);
            AddPageButton("Decrease by 10%", delegate { ChangeSensitivity(sensitivity - 0.1f, 0); });
            AddPageButton("Increase by 10%", delegate { ChangeSensitivity(sensitivity + 0.1f, 1); });
            AddPageButton("Reset to 100%", delegate { ChangeSensitivity(1f, 2); });
            AddPageButton("Notifications  " + (showNotifications.Value ? "On" : "Off"), delegate
            {
                showNotifications.Value = !showNotifications.Value;
                ShowSensitivity(3);
            });
            AddPageButton("Back", Back);
            FinishPage(selection);
        }

        private void ChangeSensitivity(float value, int selection)
        {
            sensitivity = Mathf.Clamp(value, MinimumSensitivity, MaximumSensitivity);
            ShowSensitivity(selection);
        }

        private void Back()
        {
            if (page < 0)
                ClosePresetPage();
            else if (page > SlotCount)
                ShowSlot(page - SlotCount - 1, null, 0);
            else
                ShowSlots(page);
        }

        private void StartPage(int nextPage, string heading)
        {
            page = nextPage;
            RemovePageButtons();
            title.text = heading;
            MenuItems.SetValue(openMenu, new List<GameObject>());
        }

        private void AddPageButton(string label, Action action)
        {
            List<GameObject> items = (List<GameObject>)MenuItems.GetValue(openMenu);
            items.Add(AddNativeButton(openMenu, buttons, label, "CameraPresetAction" + items.Count, action));
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
