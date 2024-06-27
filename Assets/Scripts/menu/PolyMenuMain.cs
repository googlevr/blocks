// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools;
using com.google.apps.peltzer.client.zandria;
using com.google.apps.peltzer.client.entitlement;
using TMPro;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.menu
{
    public class PolyMenuMain : MonoBehaviour
    {
        // Struct to hold information about the current state of the menu.
        public struct PolyMenuMode
        {
            public PolyMenuSection menuSection;
            public CreationType creationType;
            public int page;

            public PolyMenuMode(PolyMenuSection menuSection, CreationType creationType, int page)
            {
                this.menuSection = menuSection;
                this.creationType = creationType;
                this.page = page;
            }
        }

        // The types of Zandria creations that can be loaded.
        public enum CreationType { NONE, YOUR, FEATURED, LIKED }
        // The different sections of the PolyMenu.
        public enum PolyMenuSection { CREATION, OPTION, DETAIL, ENVIRONMENT, LABS }
        // The different actions available in the Details section.
        public enum DetailsMenuAction
        {
            OPEN, IMPORT, PUBLISH, DOWNLOAD, DELETE, UNLIKE, LIKE, CLOSE,
            OPEN_WITHOUT_SAVING, SAVE_THEN_OPEN, CANCEL_OPEN, CONFIRM_DELETE, CANCEL_DELETE
        }

        // Set in editor.
        public Sprite signedOutIcon;

        private static Color UNSELECTED_ICON_COLOR = new Color(1f, 1f, 1f, 0.117f);
        private static Color SELECTED_ICON_COLOR = new Color(1f, 1f, 1f, 1f);
        private static Color UNSELECTED_AVATAR_COLOR = new Color(1f, 1f, 1f, 150f / 255f);
        private static Color SELECTED_AVATAR_COLOR = new Color(1f, 1f, 1f, 1f);
        private static Vector3 DEFAULT_AVATAR_SCALE = new Vector3(0.1875f, 0.25f, 0.75f);
        private static Vector3 USER_AVATAR_SCALE = new Vector3(0.121875f, 0.1625f, 0.75f);

        private static float DETAIL_TILE_SIZE = 0.15f;

        private static string TAKE_HEADSET_OFF_FOR_SIGN_IN_PROMPT = "Take off your headset to sign in";

        private static StringBuilder BASE_CREATOR = new StringBuilder("by ");

        // The number of tiles for Zandria creations on the PolyMenu.
        private const int TILE_COUNT = 9;

        public PolyMenuMode yourCreations = new PolyMenuMode(PolyMenuSection.CREATION, CreationType.YOUR, 0);
        public PolyMenuMode featuredCreations = new PolyMenuMode(PolyMenuSection.CREATION, CreationType.FEATURED, 0);
        public PolyMenuMode likedCreations = new PolyMenuMode(PolyMenuSection.CREATION, CreationType.LIKED, 0);
        public PolyMenuMode options = new PolyMenuMode(PolyMenuSection.OPTION, CreationType.NONE, 0);
        public PolyMenuMode environment = new PolyMenuMode(PolyMenuSection.ENVIRONMENT, CreationType.NONE, 0);
        public PolyMenuMode labs = new PolyMenuMode(PolyMenuSection.LABS, CreationType.NONE, 0);

        public GameObject polyMenu;
        public GameObject toolMenu;
        public GameObject detailsMenu;

        // The placeholder gameObjects on the Zandria Menu.
        public GameObject[] placeholders = new GameObject[TILE_COUNT];

        private PaletteController paletteController;
        private ControllerMain controllerMain;
        private ZandriaCreationsManager creationsManager;
        private ZandriaCreationHandler currentCreationHandler;

        // The possible menuModes in the order they can be moved through using the palette touchpad.
        private PolyMenuMode[] menuModes;
        // Reference to the current menuMode the user is in.
        public int menuIndex;

        // Reference to the state of each menu panel.
        public enum Menu { POLY_MENU, TOOLS_MENU, DETAILS_MENU };
        private Menu activeMenu = Menu.TOOLS_MENU;

        // Menu panels.
        private GameObject optionsMenu;
        private GameObject labsMenu;
        private GameObject modelsMenu;
        private GameObject noSavedModelsMenu;
        private GameObject noLikedModelsMenu;
        private GameObject signedOutYourModelsMenu;
        private GameObject signedOutLikedModelsMenu;
        private GameObject offlineModelsMenu;
        private GameObject detailsPreviewHolder;
        private GameObject detailsThumbnail;
        private GameObject environmentMenu;

        // Menu button icons.
        private SpriteRenderer optionsIcon;
        private SpriteRenderer yourModelsIcon;
        private SpriteRenderer likedModelsIcon;
        private SpriteRenderer featuredModelsIcon;
        private SpriteRenderer environmentIcon;
        private SpriteRenderer labsIcon;

        // Pagination icons and text.
        private SpriteRenderer pageLeftIcon;
        private SpriteRenderer pageRightIcon;
        private TextMeshPro pageIndicator;

        private SelectablePaginationMenuItem pageLeftScript;
        private SelectablePaginationMenuItem pageRightScript;

        // Menu titles.
        private GameObject optionsTitle;
        private GameObject yourModelsTitle;
        private GameObject likedModelsTitle;
        private GameObject featuredModelsTitle;

        // Options buttons.
        private TextMeshPro signInText;
        private TextMeshPro addReferenceText;
        private string defaultSignInMessage;
        private GameObject signInButton;
        private GameObject signOutButton;
        private GameObject addReferenceButton;

        // User info.
        private Sprite defaultUserIcon;
        private string defaultDisplayName;
        private TextMesh displayName;

        // Pop-up dialogs for confirmation.
        private GameObject confirmSaveDialog;
        private GameObject confirmDeleteDialog;

        // Creation metadata.
        private GameObject creationTitle;
        private GameObject creatorName;
        private GameObject creationDate;

        // Detail menu buttons.
        // These aren't all the buttons only the ones that need to be changed depending on creationType.
        private GameObject openButton;
        private SpriteRenderer openButtonIcon;
        private TextMeshPro openButtonText;
        private DetailsMenuActionItem openButtonScript;

        private GameObject importButton;
        private SpriteRenderer importButtonIcon;
        private TextMeshPro importButtonText;
        private DetailsMenuActionItem importButtonScript;

        private GameObject deleteButton;
        private GameObject yourModelsMenuSpacer;
        private GameObject likedOrFeaturedModelsMenuSpacer;

        // Params for a lighting effect on the menu button when selected.
        private const float BUTTON_LIGHT_INTENSITY = 8f;
        private static readonly Color BUTTON_LIGHT_ON_COLOR = new Color(1f, .8f, 0.2f);
        private static readonly Color BUTTON_LIGHT_OFF_COLOR = new Color(0f, 0f, 0f);
        private static readonly Color BUTTON_EMISSIVE_COLOR = new Color(.5f, .4f, 0.2f);

        // Use this for initialization
        public void Setup(ZandriaCreationsManager creationsManager, PaletteController paletteController)
        {
            this.creationsManager = creationsManager;
            this.paletteController = paletteController;
            controllerMain = PeltzerMain.Instance.controllerMain;
            controllerMain.ControllerActionHandler += ControllerEventHandler;

            menuModes = new PolyMenuMode[6] { options, yourCreations, featuredCreations, likedCreations, environment, labs };

            // Set the default start up mode for the menu to be Your Models.
            SwitchToYourModelsSection();

            // Find all the appropriate GameObjects from the scene.
            optionsMenu = polyMenu.transform.Find("Options").gameObject;
            labsMenu = polyMenu.transform.Find("Labs").gameObject;
            modelsMenu = polyMenu.transform.Find("Models").gameObject;
            noSavedModelsMenu = polyMenu.transform.Find("Models-NoneSaved").gameObject;
            noLikedModelsMenu = polyMenu.transform.Find("Models-NoneLiked").gameObject;
            signedOutYourModelsMenu = polyMenu.transform.Find("Models-Signedout-Yours").gameObject;
            signedOutLikedModelsMenu = polyMenu.transform.Find("Models-Signedout-Likes").gameObject;
            offlineModelsMenu = polyMenu.transform.Find("Models-Offline").gameObject;
            detailsPreviewHolder = detailsMenu.transform.Find("Model/preview/preview_holder").gameObject;
            detailsThumbnail = detailsMenu.transform.Find("Model/Thumbnail").gameObject;
            environmentMenu = polyMenu.transform.Find("Environments").gameObject;

            optionsIcon = polyMenu.transform.Find("NavBar/Options/panel/ic").GetComponent<SpriteRenderer>();
            yourModelsIcon = polyMenu.transform.Find("NavBar/Your-Models/panel/ic").GetComponent<SpriteRenderer>();
            likedModelsIcon = polyMenu.transform.Find("NavBar/Liked-Models/panel/ic").GetComponent<SpriteRenderer>();
            featuredModelsIcon = polyMenu.transform.Find("NavBar/Featured-Models/panel/ic").GetComponent<SpriteRenderer>();
            environmentIcon = polyMenu.transform.Find("NavBar/Environments/panel/ic").GetComponent<SpriteRenderer>();
            labsIcon = polyMenu.transform.Find("NavBar/LabsSection/panel/ic").GetComponent<SpriteRenderer>();

            pageLeftIcon = polyMenu.transform.Find("Models/Pagination/Left/panel/ic").GetComponent<SpriteRenderer>();
            pageRightIcon = polyMenu.transform.Find("Models/Pagination/Right/panel/ic").GetComponent<SpriteRenderer>();
            pageIndicator = polyMenu.transform.Find("Models/Pagination/PageIndicator/txt").GetComponent<TextMeshPro>();

            pageLeftScript = polyMenu.transform.Find("Models/Pagination/Left/panel")
              .GetComponent<SelectablePaginationMenuItem>();
            pageRightScript = polyMenu.transform.Find("Models/Pagination/Right/panel")
              .GetComponent<SelectablePaginationMenuItem>();

            optionsTitle = polyMenu.transform.Find("Titles/options_title").gameObject;
            yourModelsTitle = polyMenu.transform.Find("Titles/your_models_title").gameObject;
            likedModelsTitle = polyMenu.transform.Find("Titles/likes_title").gameObject;
            featuredModelsTitle = polyMenu.transform.Find("Titles/featured_title").gameObject;

            signInText = polyMenu.transform.Find("Options/sign_in/bg/txt").GetComponent<TextMeshPro>();
            defaultSignInMessage = polyMenu.transform.Find("Options/sign_in/bg/txt").GetComponent<TextMeshPro>().text;

            signInButton = polyMenu.transform.Find("Options/sign_in").gameObject;
            signOutButton = polyMenu.transform.Find("Options/sign_out").gameObject;

            displayName = polyMenu.transform.Find("Options/sign_out/bg/txt").GetComponent<TextMesh>();
            defaultDisplayName = polyMenu.transform.Find("Options/sign_out/bg/txt").GetComponent<TextMesh>().text;

            confirmSaveDialog = detailsMenu.transform.Find("ConfirmSave").gameObject;
            confirmDeleteDialog = detailsMenu.transform.Find("ConfirmDelete").gameObject;

            creationTitle = detailsMenu.transform.Find("Metadata/txt-title").gameObject;
            creatorName = detailsMenu.transform.Find("Metadata/txt-name").gameObject;
            creationDate = detailsMenu.transform.Find("Metadata/txt-time").gameObject;

            openButton = detailsMenu.transform.Find("Buttons/Open").gameObject;
            openButtonIcon = detailsMenu.transform.Find("Buttons/Open/bg/ic").GetComponent<SpriteRenderer>();
            openButtonText = detailsMenu.transform.Find("Buttons/Open/bg/txt").GetComponent<TextMeshPro>();
            openButtonScript = detailsMenu.transform.Find("Buttons/Open/bg").GetComponent<DetailsMenuActionItem>();

            importButton = detailsMenu.transform.Find("Buttons/Import").gameObject;
            importButtonIcon = detailsMenu.transform.Find("Buttons/Import/bg/ic").GetComponent<SpriteRenderer>();
            importButtonText = detailsMenu.transform.Find("Buttons/Import/bg/txt").GetComponent<TextMeshPro>();
            importButtonScript = detailsMenu.transform.Find("Buttons/Import/bg").GetComponent<DetailsMenuActionItem>();

            deleteButton = detailsMenu.transform.Find("Buttons/Delete").gameObject;
            yourModelsMenuSpacer = detailsMenu.transform.Find("Buttons/bg-space").gameObject;
            likedOrFeaturedModelsMenuSpacer = detailsMenu.transform.Find("Buttons/bg-space2").gameObject;
        }

        // Whether the controller events indicate the user wants to toggle between the PolyMenu and ToolMenu.
        private bool IsTogglePolyMenuEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.ApplicationMenu
              && args.Action == ButtonAction.DOWN
              && !PeltzerMain.Instance.Zoomer.Zooming;
        }

        // Whether the controller events indicate the user wants to move up through the PolyMenu sections.
        private bool IsProgressMenuUpEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.DOWN
              && args.TouchpadLocation == TouchpadLocation.TOP;
        }

        // Whether the controller events indicate the user wants to move down through the PolyMenu sections.
        private bool IsProgressMenuDownEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.DOWN
              && args.TouchpadLocation == TouchpadLocation.BOTTOM;
        }

        // Whether the controller events indicate the user wants to move to the next page.
        private bool IsProgressPageRightEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.DOWN
              && args.TouchpadLocation == TouchpadLocation.RIGHT;
        }

        // Whether the controller events indicate the user wants to move back to the previous page.
        private bool IsProgressPageLeftEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.Touchpad
              && args.Action == ButtonAction.DOWN
              && args.TouchpadLocation == TouchpadLocation.LEFT;
        }

        private void ControllerEventHandler(object sender, ControllerEventArgs args)
        {
            if (!PeltzerMain.Instance.restrictionManager.menuActionsAllowed)
            {
                return;
            }

            // If the user hits toggle on the details page, or the poly menu, we take them to the tools menu.
            // If they hit toggle on the tools menu, we take them to the poly menu.
            if (IsTogglePolyMenuEvent(args))
            {
                // If menu switching is not allowed at this time, return.
                if (!PeltzerMain.Instance.restrictionManager.menuSwitchAllowed)
                {
                    return;
                }

                // Play some feedback.
                PeltzerMain.Instance.peltzerController.TriggerHapticFeedback();

                // Disable the 'click me' tooltip for toggling this menu.
                PeltzerMain.Instance.applicationButtonToolTips.TurnOff();

                // Toggle the state of the menus.
                if (activeMenu == Menu.TOOLS_MENU)
                {
                    SetActiveMenu(Menu.POLY_MENU);
                }
                else
                {
                    SetActiveMenu(Menu.TOOLS_MENU);
                }

                if (activeMenu != Menu.TOOLS_MENU)
                {
                    ChangeMenu();
                }
            }
            else
            {
                // Only handle controller events for moving through the Poly menu if the menu is open and we
                // are not currently zooming.
                if (polyMenu.activeInHierarchy && !PeltzerMain.Instance.Zoomer.Zooming)
                {
                    if (IsProgressMenuDownEvent(args))
                    {
                        // If we've hit the end of the menu the user can't carousel around.
                        if (menuIndex == menuModes.Length - 1)
                        {
                            // The current operation is invalid, play error...
                            paletteController.TriggerHapticFeedback(
                              HapticFeedback.HapticFeedbackType.FEEDBACK_2, /* durationSeconds */ 0.25f, /* strength */ 0.3f);
                        }
                        else
                        {
                            menuIndex++;
                            ChangeMenu();
                        }
                    }
                    else if (IsProgressMenuUpEvent(args))
                    {
                        // If we've hit the top of the menu the user can't carousel around.
                        if (menuIndex == 0)
                        {
                            // The current operation is invalid, play error...
                            paletteController.TriggerHapticFeedback(
                              HapticFeedback.HapticFeedbackType.FEEDBACK_2, /* durationSeconds */ 0.25f, /* strength */ 0.3f);
                        }
                        else
                        {
                            menuIndex--;
                            ChangeMenu();
                        }
                    }
                    else if (IsProgressPageRightEvent(args))
                    {
                        if (CurrentMenuSection() == PolyMenuSection.OPTION)
                        {
                            // There is no pagination for Options.
                            return;
                        }

                        // If we are already on the last page the user can't carousel around.
                        if (CurrentPage() == creationsManager.GetNumberOfPages(CurrentCreationType()) - 1)
                        {
                            // The current operation is invalid, play error...
                            paletteController.TriggerHapticFeedback(
                              HapticFeedback.HapticFeedbackType.FEEDBACK_2, /* durationSeconds */ 0.25f, /* strength */ 0.3f);
                        }
                        else
                        {
                            menuModes[menuIndex].page++;
                            ChangeMenu();
                        }
                    }
                    else if (IsProgressPageLeftEvent(args))
                    {
                        if (CurrentMenuSection() == PolyMenuSection.OPTION)
                        {
                            // There is no pagination for Options.
                            return;
                        }

                        // If we are already on the first page the user can't carousel around.
                        if (menuModes[menuIndex].page == 0)
                        {
                            // The current operation is invalid, play error...
                            paletteController.TriggerHapticFeedback(
                              HapticFeedback.HapticFeedbackType.FEEDBACK_2, /* durationSeconds */ 0.25f, /* strength */ 0.3f);
                        }
                        else
                        {
                            menuModes[menuIndex].page--;
                            ChangeMenu();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a light effect to the button when the poly menu is activated, and removes it when the menu
        /// is closed.
        /// </summary>
        private void ToggleButtonState()
        {
            if (activeMenu == Menu.POLY_MENU)
            {
                // Flip on
                paletteController.controllerGeometry.appMenuButton.GetComponent<Renderer>().material.SetColor("_EmissiveColor", BUTTON_EMISSIVE_COLOR);
            }
            else
            {
                // Flip off
                paletteController.controllerGeometry.appMenuButton.GetComponent<Renderer>().material.SetColor("_EmissiveColor", BUTTON_LIGHT_OFF_COLOR);
            }
        }

        /// <summary>
        ///   Updates the selected sub-section of the POLY_MENU to be 'Your Models'.
        /// </summary>
        public void SwitchToYourModelsSection()
        {
            menuIndex = 1;
        }

        /// <summary>
        ///   Updates the selected sub-section of the POLY_MENU to be 'Your Models'.
        /// </summary>
        public void SwitchToFeaturedSection()
        {
            menuIndex = 2;
        }

        /// <summary>
        ///   Set the active menu, and set GameObjects active as required.
        /// </summary>
        public void SetActiveMenu(Menu menu)
        {
            if (menu == activeMenu) return;

            // Clean up the details menu if it's being closed or opened.
            if (activeMenu == Menu.DETAILS_MENU || menu == Menu.DETAILS_MENU)
            {
                // Destroy the preview we created.
                for (int i = 0; i < detailsPreviewHolder.transform.childCount; i++)
                {
                    Destroy(detailsPreviewHolder.transform.GetChild(i).gameObject);
                }

                currentCreationHandler = null;
                detailsPreviewHolder.GetComponent<SelectZandriaCreationMenuItem>().meshes = null;
            }

            activeMenu = menu;
            ToggleButtonState();
            PeltzerMain.Instance.audioLibrary.PlayClip(PeltzerMain.Instance.audioLibrary.toggleMenuSound);

            polyMenu.SetActive(activeMenu == Menu.POLY_MENU);
            toolMenu.SetActive(activeMenu == Menu.TOOLS_MENU);
            detailsMenu.SetActive(activeMenu == Menu.DETAILS_MENU);

            if (activeMenu == Menu.TOOLS_MENU)
            {
                paletteController.ResetTouchpadOverlay();
                PeltzerMain.Instance.restrictionManager.undoRedoAllowed = true;
            }
            else
            {
                paletteController.ChangeTouchpadOverlay(TouchpadOverlay.MENU);
                PeltzerMain.Instance.restrictionManager.undoRedoAllowed = false;
            }
        }

        /// <summary>
        ///   Takes input from a user clicking on a Details section menu button and executes the correct action for the
        ///   button.
        /// </summary>
        /// <param name="action">The action to take.</param>
        public void InvokeDetailsMenuAction(DetailsMenuAction action)
        {
            switch (action)
            {
                case DetailsMenuAction.OPEN:
                    if (currentCreationHandler != null)
                    {
                        // Only show the save confirmation dialog if modified since last save.
                        if (PeltzerMain.Instance.ModelChangedSinceLastSave)
                        {
                            // The save confirmation dialog will call InvokeDetailsMenuAction according to the user's
                            // decision (CANCEL_OPEN, OPEN_WITH_SAVING or SAVE_THEN_OPEN).
                            confirmSaveDialog.SetActive(true);
                        }
                        else
                        {
                            // Not modified since last save, so we can clear without confirmation.
                            OpenCreation(currentCreationHandler);
                        }
                    }
                    break;
                case DetailsMenuAction.IMPORT:
                    // Import is the same action as quick selecting a zandria creation so we can just grab the meshes on the
                    // quick select script attached to the preview.
                    SelectCreation(
                      detailsPreviewHolder.GetComponent<SelectZandriaCreationMenuItem>().meshes,
                      currentCreationHandler.creationAssetId);
                    // Clear the detailSizedMeshes from the creation handler when importing, as import grabs a direct mutable
                    // reference to these to avoid any lag in generating a copy. Instead, the lag of generating a copy will
                    // happen the next time the user opens the details page for this model again.
                    currentCreationHandler.detailSizedMeshes.Clear();
                    break;
                case DetailsMenuAction.DELETE:
                    confirmDeleteDialog.SetActive(true);
                    break;
                case DetailsMenuAction.CANCEL_DELETE:
                    confirmDeleteDialog.SetActive(false);
                    break;
                case DetailsMenuAction.CONFIRM_DELETE:
                    confirmDeleteDialog.SetActive(false);
                    // Remove the asset from the list of creations displayed.
                    if (currentCreationHandler.creationAssetId != null)
                    {
                        creationsManager.RemoveSingleCreationAndRefreshMenu(
                          CurrentCreationType(), currentCreationHandler.creationAssetId);
                        // Invoke the RPC that removes the creation from storage
                        StartCoroutine(creationsManager.assetsServiceClient.DeleteAsset(currentCreationHandler.creationAssetId));
                    }
                    if (currentCreationHandler.creationLocalId != null)
                    {
                        creationsManager.RemoveSingleCreationAndRefreshMenu(
                          CurrentCreationType(), currentCreationHandler.creationLocalId);
                        creationsManager.DeleteOfflineModel(currentCreationHandler.creationLocalId);
                    }
                    SetActiveMenu(Menu.POLY_MENU);
                    break;
                case DetailsMenuAction.CLOSE:
                    SetActiveMenu(Menu.POLY_MENU);
                    break;
                case DetailsMenuAction.CANCEL_OPEN:
                    confirmSaveDialog.SetActive(false);
                    break;
                case DetailsMenuAction.OPEN_WITHOUT_SAVING:
                    // The user does not want to save their current changes so we can just clear the scene and load the creation.
                    OpenCreation(currentCreationHandler);
                    break;
                case DetailsMenuAction.SAVE_THEN_OPEN:
                    // The user wants to save before opening a new creation. We need to wait for the save to complete because
                    // saving makes the model unwritable so we can't clear it and add the new creation to the model until it's
                    // done.
                    confirmSaveDialog.SetActive(false);
                    PeltzerMain.Instance.saveCompleteAction = () =>
                    {
                        OpenCreation(currentCreationHandler);
                    };
                    PeltzerMain.Instance.SaveCurrentModel(publish: false, saveSelected: false);
                    break;
            }
        }

        /// <summary>
        ///   Performs the 'open' request on a creation.
        /// </summary>
        /// <param name="creationHandler"></param>
        private void OpenCreation(ZandriaCreationHandler creationHandler)
        {
            confirmSaveDialog.SetActive(false);
            PeltzerMain.Instance.CreateNewModel();

            PeltzerMain.LoadOptions options = new PeltzerMain.LoadOptions();
            options.cloneBeforeLoad = true;

            if (CurrentCreationType() == CreationType.YOUR)
            {
                // Creation belongs to the user, so don't override remix IDs (we leave them as-is on the file).
                // But we have to remember the ID of the asset so we can later save it with the same asset ID (to overwrite).
                if (currentCreationHandler.creationAssetId != null)
                {
                    PeltzerMain.Instance.AssetId = currentCreationHandler.creationAssetId;
                }
                else if (currentCreationHandler.creationLocalId != null)
                {
                    PeltzerMain.Instance.LocalId = currentCreationHandler.creationLocalId;
                }
            }
            else
            {
                // Creation doesn't belong to the user, so set the attribution appropriately by setting
                // the remix ID of all meshes.
                options.overrideRemixId = creationHandler.creationAssetId;
            }
            PeltzerMain.Instance.LoadPeltzerFileIntoModel(currentCreationHandler.peltzerFile, options);

            if (Features.adjustWorldSpaceOnOpen)
            {
                WorldSpaceAdjuster.AdjustWorldSpace();
            }


            SetActiveMenu(Menu.TOOLS_MENU);
        }

        /// <summary>
        ///   Changes the menu to a passed menuIndex. This is used when selecting menu options using the peltzerController
        ///   and not the palette touchpad.
        /// </summary>
        /// <param name="selectedMenuIndex">The new menu index that was selected.</param>
        /// <param name="setToFirstPage">Whether to also set the passed menuIndex to the first page.</param>
        public void ApplyMenuChange(int selectedMenuIndex, bool setToFirstPage = false)
        {
            if (setToFirstPage || selectedMenuIndex == menuIndex)
            {
                menuModes[selectedMenuIndex].page = 0;
            }
            menuIndex = selectedMenuIndex;
            ChangeMenu();
        }

        public void ApplyPageChange(int indexChange)
        {
            // The left and right buttons will disable themselves if there are no valid actions so we don't have to worry
            // about checking if the indexChange is valid.
            menuModes[menuIndex].page += indexChange;
            ChangeMenu();
        }

        private void ChangePaginationButtons()
        {
            int numPages = creationsManager.GetNumberOfPages(CurrentCreationType());

            bool pageLeftActive = CurrentPage() > 0;
            bool pageRightActive = CurrentPage() < numPages - 1;

            pageLeftIcon.color = pageLeftActive ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            pageRightIcon.color = pageRightActive ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;

            pageLeftScript.isActive = pageLeftActive;
            pageRightScript.isActive = pageRightActive;

            // Don't use a zero index for the page display.
            int currentPageForDisplay = CurrentPage() + 1;
            pageIndicator.text = string.Format("{0} of {1}", currentPageForDisplay, numPages);
        }

        private void ChangeMenu()
        {
            // Start by ensuring Poly Menu is active, to cover the case that 'details' was open and needs to be closed.
            SetActiveMenu(Menu.POLY_MENU);

            // Activate the correct title and deactivate all others.
            if (optionsTitle != null)
            {
                optionsTitle.SetActive(CurrentMenuSection() == PolyMenuSection.OPTION);
            }
            if (yourModelsTitle != null)
            {
                yourModelsTitle.SetActive(CurrentCreationType() == CreationType.YOUR);
            }
            if (likedModelsTitle != null)
            {
                likedModelsTitle.SetActive(CurrentCreationType() == CreationType.LIKED);
            }
            if (featuredModelsTitle != null)
            {
                featuredModelsTitle.SetActive(CurrentCreationType() == CreationType.FEATURED);
            }

            if (optionsIcon != null)
            {
                optionsIcon.color = CurrentMenuSection() ==
                  PolyMenuSection.OPTION ? SELECTED_AVATAR_COLOR : UNSELECTED_AVATAR_COLOR;
            }
            if (yourModelsIcon != null)
            {
                yourModelsIcon.color = CurrentCreationType() ==
                  CreationType.YOUR ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            }
            if (likedModelsIcon != null)
            {
                likedModelsIcon.color = CurrentCreationType() ==
                  CreationType.LIKED ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            }
            if (featuredModelsIcon != null)
            {
                featuredModelsIcon.color = CurrentCreationType() ==
                  CreationType.FEATURED ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            }
            if (environmentIcon != null)
            {
                environmentIcon.color = CurrentMenuSection() == PolyMenuSection.ENVIRONMENT ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            }
            if (labsIcon != null)
            {
                labsIcon.color = CurrentMenuSection() == PolyMenuSection.LABS ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            }

            // Activate or deactive the necessary menus.
            if (optionsMenu != null)
            {
                optionsMenu.SetActive(CurrentMenuSection() == PolyMenuSection.OPTION);
            }

            if (labsMenu != null)
            {
                labsMenu.SetActive(CurrentMenuSection() == PolyMenuSection.LABS);
            }
            if (environmentMenu != null)
            {
                environmentMenu.SetActive(CurrentMenuSection() == PolyMenuSection.ENVIRONMENT);
            }
            // Deactivate all the user prompt menus. If they need to be activated they will be in PopulateZandriaMenu().
            if (noSavedModelsMenu != null)
            {
                noSavedModelsMenu.SetActive(false);
            }
            if (noLikedModelsMenu != null)
            {
                noLikedModelsMenu.SetActive(false);
            }
            if (signedOutYourModelsMenu != null)
            {
                signedOutYourModelsMenu.SetActive(false);
            }
            if (signedOutLikedModelsMenu != null)
            {
                signedOutLikedModelsMenu.SetActive(false);
            }
            if (offlineModelsMenu != null)
            {
                offlineModelsMenu.SetActive(false);
            }
            // Activate or deactivate the models menu.
            if (modelsMenu != null)
            {
                modelsMenu.SetActive(CurrentMenuSection() == PolyMenuSection.CREATION);
                if (CurrentMenuSection() == PolyMenuSection.CREATION)
                {
                    // Update the pagination icons.
                    ChangePaginationButtons();

                    // Populate the menu with Zandria creations.
                    PopulateZandriaMenu(CurrentCreationType());
                }
            }
        }

        private void PopulateZandriaMenu(CreationType type)
        {
            // This is a naive approach. Every time you open the menu it will just attach the
            // available creations but we should keep adding creations as they load when the menu is open. This
            // should be implemented with our pagination.

            // First hide any gameObjects on the palette so we can show the correct ones.
            for (int i = 0; i < placeholders.Length; i++)
            {
                ZandriaCreationHandler[] creationHandlers =
                  placeholders[i].GetComponentsInChildren<ZandriaCreationHandler>();

                for (int j = 0; j < creationHandlers.Length; j++)
                {
                    creationHandlers[j].isActiveOnMenu = false;
                    creationHandlers[j].gameObject.SetActive(false);
                }
            }

            int from = CurrentPage() * TILE_COUNT;
            int upToNotIncluding = from + TILE_COUNT;
            List<GameObject> previews = creationsManager.GetPreviews(type, CurrentPage() * TILE_COUNT, upToNotIncluding);

            // If there are available previews load them onto the palette.
            if (previews.Count > 0)
            {
                for (int i = 0; i < TILE_COUNT && i < previews.Count; i++)
                {
                    GameObject zandriaCreationHolder = previews[i];
                    zandriaCreationHolder.GetComponent<ZandriaCreationHandler>().isActiveOnMenu = true;
                    zandriaCreationHolder.SetActive(true);

                    //  Parent the zandriaCreationHolder to the placeholders on the ZandriaMenu.
                    zandriaCreationHolder.transform.parent = placeholders[i].transform;

                    zandriaCreationHolder.transform.localPosition = Vector3.zero;
                    zandriaCreationHolder.transform.localRotation = Quaternion.Euler(new Vector3(90, 0, 0));
                }
            }

            // If there were no valid previews, replace the modelsMenu with a menu panel displaying a prompt to the user.
            // Unless its the FEATURED menu which has no prompt. We've just failed to load (or are loading) featured
            // models.
            if (modelsMenu != null)
            {
                // Even though there are no previews keep the modelsMenu active if there aren't any previews available
                // but the creationsManager is trying to load that type. The user has signed in, the load is just not ready.
                modelsMenu.SetActive(
                  (type == CreationType.FEATURED && creationsManager.HasPendingOrValidLoad(CreationType.FEATURED)) ||
                  (type == CreationType.YOUR && creationsManager.HasPendingOrValidLoad(CreationType.YOUR)) ||
                  (type == CreationType.LIKED && creationsManager.HasPendingOrValidLoad(CreationType.LIKED)));
            }

            bool modelsMenuActive = modelsMenu.activeInHierarchy;

            if (noSavedModelsMenu != null)
            {
                // Tell the user that they have no saved models if: The creations manager has tried to load your models
                // and it's invalid but the user is logged in.
                noSavedModelsMenu.SetActive(!modelsMenuActive && type == CreationType.YOUR
                  && !creationsManager.HasValidLoad(CreationType.YOUR)
                  && OAuth2Identity.Instance.LoggedIn);
            }
            if (noLikedModelsMenu != null)
            {
                // Tell the user that they have no liked models if: The creations manager has tried to load liked models
                // and it's invalid but the user is logged in.
                noLikedModelsMenu.SetActive(!modelsMenuActive && type == CreationType.LIKED
                  && !creationsManager.HasValidLoad(CreationType.LIKED)
                  && OAuth2Identity.Instance.LoggedIn);
            }
            if (signedOutYourModelsMenu != null)
            {
                // Tell the user to log in if they are not logged in.
                signedOutYourModelsMenu.SetActive(!modelsMenuActive && type == CreationType.YOUR
                  && !OAuth2Identity.Instance.LoggedIn);
            }
            if (signedOutLikedModelsMenu != null)
            {
                // Tell the user to log in if the are not logged in.
                signedOutLikedModelsMenu.SetActive(!modelsMenuActive && type == CreationType.LIKED
                  && !OAuth2Identity.Instance.LoggedIn);
            }
            if (offlineModelsMenu != null)
            {
                // Tell the user to check their internet connection if we have no featured models.
                offlineModelsMenu.SetActive(!modelsMenuActive && type == CreationType.FEATURED &&
                  !creationsManager.HasPendingOrValidLoad(CreationType.FEATURED));
            }
        }

        /// <summary>
        /// Refreshes the PolyMenu if the menu is already open in the hierachy.
        /// </summary>
        public void RefreshPolyMenu()
        {
            if (modelsMenu != null)
            {
                if (CurrentMenuSection() == PolyMenuSection.CREATION)
                {
                    // Update the pagination icons.
                    ChangePaginationButtons();

                    // Populate the menu with Zandria creations.
                    PopulateZandriaMenu(CurrentCreationType());
                }
            }
        }

        /// <summary>
        ///   Opens the Details section of the PolyMenu by setting the UI element active in the scene and loading the
        ///   creation onto the palette.
        /// </summary>
        /// <param name="creation">The creation to be opened.</param>
        public void OpenDetailsSection(Creation creation)
        {
            SetActiveMenu(Menu.DETAILS_MENU);
            // First close and remove the information for an already open Details panel.
            // The user is able to click on a new creation by clicking under the Details panel before closing.

            if (creation != null)
            {
                currentCreationHandler = creation.handler;
                StartCoroutine(AttachPreviewToDetailsHolder(creation));

                // Activate/Deactivate the correct buttons and UI elements for each creation type.
                creationTitle.SetActive(CurrentCreationType() == CreationType.YOUR);
                creationDate.SetActive(CurrentCreationType() == CreationType.FEATURED
                  || CurrentCreationType() == CreationType.LIKED);
                creatorName.SetActive(CurrentCreationType() == CreationType.FEATURED
                  || CurrentCreationType() == CreationType.LIKED);

                // Activate or deactivate the Open/Import buttons if the model is loaded.
                ActivateOpenImportButtons(creation.entry.loadStatus == ZandriaCreationsManager.LoadStatus.SUCCESSFUL);
                deleteButton.SetActive(CurrentCreationType() == CreationType.YOUR);
                yourModelsMenuSpacer.SetActive(CurrentCreationType() == CreationType.YOUR);
                likedOrFeaturedModelsMenuSpacer.SetActive(
                  CurrentCreationType() == CreationType.FEATURED || CurrentCreationType() == CreationType.LIKED);

                if (CurrentCreationType() == CreationType.YOUR)
                {
                    // Reset the creation title and creator name UI elements.
                    creationTitle.SetActive(false);
                    creationTitle.GetComponent<TextMeshPro>().text = "";
                    creatorName.SetActive(false);
                    creatorName.GetComponent<TextMeshPro>().text = "";
                }
                else if (CurrentCreationType() == CreationType.FEATURED || CurrentCreationType() == CreationType.LIKED)
                {
                    // Reset the creation date UI element.
                    creationDate.SetActive(false);
                    creationDate.GetComponent<TextMeshPro>().text = "";

                    // Activate and populate the creation title and creator name UI elements.
                    creationTitle.SetActive(true);
                    creationTitle.GetComponent<TextMeshPro>().text = currentCreationHandler.creationTitle;
                    creatorName.SetActive(true);
                    creatorName.GetComponent<TextMeshPro>().text =
                      new StringBuilder().Append(BASE_CREATOR).Append(currentCreationHandler.creatorName).ToString();
                }
            }
        }

        /// <summary>
        ///  Activates or deactivates the Open and Import detail buttons by changing the icon, text and stopping the button
        ///  action by setting the script inactive.
        /// </summary>
        /// <param name="active">Whether the buttons should be active.</param>
        private void ActivateOpenImportButtons(bool active)
        {
            openButtonIcon.color = active ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            openButtonText.color = active ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            openButtonScript.isActive = active;

            importButtonIcon.color = active ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            importButtonText.color = active ? SELECTED_ICON_COLOR : UNSELECTED_ICON_COLOR;
            importButtonScript.isActive = active;
        }

        /// <summary>
        ///   Takes a creation and loads a scaled up version of its preview onto the details menu. If the preview isn't
        ///   loaded yet it will wait for it to be loaded.
        /// </summary>
        /// <param name="creation">The creation to attach to the menu.</param>
        public IEnumerator AttachPreviewToDetailsHolder(Creation creation)
        {
            // Make sure the details thumbnail is active and then set the thumbnail to the creation's thumbnail.
            detailsThumbnail.SetActive(true);
            detailsThumbnail.GetComponent<SpriteRenderer>().sprite = creation.thumbnailSprite;

            // Wait until the creation is loaded to do anything else. During this time the thumbnail is displayed and the
            // Open/Import buttons are inactive.
            while (creation.entry.loadStatus != ZandriaCreationsManager.LoadStatus.SUCCESSFUL)
            {
                yield return null;
            }

            // The creation has loaded, scale the meshes for the details menu.
            List<MMesh> detailSizedMeshes;

            // Check if detailSizedMeshes already exist. We don't want to replicate them again from the originals if the
            // model has been open in the scene since they will reference the same MMesh instance.
            if (creation.handler.detailSizedMeshes.Count > 0)
            {
                detailSizedMeshes = creation.handler.detailSizedMeshes;
            }
            else
            {
                detailSizedMeshes = Scaler.ScaleMeshes(creation.handler.originalMeshes, DETAIL_TILE_SIZE);
                creation.handler.detailSizedMeshes = detailSizedMeshes;
            }

            // Get a preview from the MMeshes on a background thread. When it's done it will call back with the preview
            // and attach it to the details menu.
            MeshHelper.GameObjectFromMMeshesForMenu(new WorldSpace(PeltzerMain.DEFAULT_BOUNDS), detailSizedMeshes,
              delegate (GameObject meshPreview)
              {
                  // We have successfully loaded the creation as a preview so we attach it to the menu.
                  if (meshPreview != null)
                  {
                      // Zero the transform so we're only being transformed by the parent.
                      meshPreview.GetComponent<MeshWithMaterialRenderer>().ResetTransform();
                      //  Parent the mesh preview to the details menu.
                      meshPreview.transform.parent = detailsPreviewHolder.transform;
                      meshPreview.transform.localPosition = Vector3.zero;
                      meshPreview.transform.localRotation = Quaternion.Euler(
                  new Vector3(0, creation.handler.recommendedRotation, 0));

                      detailsPreviewHolder.GetComponent<SelectZandriaCreationMenuItem>().meshes = detailSizedMeshes;

                      // Deactivate the thumbnail now that the meshes are displaying and activate the Open/Import buttons.
                      detailsThumbnail.SetActive(false);
                      ActivateOpenImportButtons(/*active*/ true);
                  }
              });
        }

        private PolyMenuSection CurrentMenuSection()
        {
            return menuModes[menuIndex].menuSection;
        }

        private CreationType CurrentCreationType()
        {
            return menuModes[menuIndex].creationType;
        }

        private int CurrentPage()
        {
            return menuModes[menuIndex].page;
        }

        /// <summary>
        ///   Called after the user has successfully signed in and we need to update the PolyMenu.
        /// </summary>
        /// <param name="avatarIcon">The user's avatar to display on the PolyMenu.</param>
        /// <param name="displayName">The user's name to display on the PolyMenu.</param>
        public void SignIn(Sprite avatarIcon, string displayName)
        {
            // Set the signIn button false and reset the prompt to read "Sign In" instead of "Take headset off..."
            signInButton.SetActive(false);
            signInText.text = defaultSignInMessage;
            signOutButton.SetActive(true);

            // Change the UI elements to display the user's icon and name.
            optionsIcon.sprite = avatarIcon != null ? avatarIcon : signedOutIcon;
            optionsIcon.transform.localScale = avatarIcon != null ? USER_AVATAR_SCALE : DEFAULT_AVATAR_SCALE;
            this.displayName.text = displayName != null ? displayName : defaultDisplayName;

            RefreshPolyMenu();
        }

        /// <summary>
        ///   Called after the user has signed out and we need to update the PolyMenu.
        /// </summary>
        public void SignOut()
        {
            signInButton.SetActive(true);
            signOutButton.SetActive(false);

            optionsIcon.sprite = signedOutIcon;
            optionsIcon.transform.localScale = DEFAULT_AVATAR_SCALE;
            displayName.text = defaultDisplayName;

            signInText.text = defaultSignInMessage;

            RefreshPolyMenu();
        }

        /// <summary>
        ///   Called when the user has started the authentication process and needs to take off their headset to sign in
        ///   using their web browser.
        /// </summary>
        public void PromptUserToSignIn()
        {
            signInText.text = TAKE_HEADSET_OFF_FOR_SIGN_IN_PROMPT;
        }

        /// <summary>
        ///   Selects a Zandria creation represented by a list of MMeshes and adds them to the model then passes them to the
        ///   move tool to be moved and placed in the scene.
        /// </summary>
        /// <param name="meshes">The MMeshes to be selected and then moved.</param>
        public void SelectCreation(List<MMesh> meshes, string selectedAssetId)
        {
            if (meshes == null)
            {
                return;
            }

            Model model = PeltzerMain.Instance.GetModel();

            // We ignore the 'bool' output of the below: it it fails, we'll continue with the mesh in its current scale.
            Scaler.TryScalingMeshes(meshes, 1f / PeltzerMain.Instance.worldSpace.scale);

            // We give them new IDs at this point so they won't collide with anything already in the scene or 
            // (much more likely) with a previous import of this same creation. We need to store a local list of usedIds
            // to avoid some rare, but potential, cases where the same ID is generated twice during this import.
            List<int> usedIds = new List<int>(meshes.Count);

            // We group every mesh together in an import, overwriting previous groupings, under the assumption that users
            // are much more likely to want to move and place the entire import than they are to subtly edit it and thereby
            // depend on its original groupings.
            int groupId = model.GenerateGroupId();
            for (int i = 0; i < meshes.Count; i++)
            {
                MMesh mesh = meshes[i];
                int newId = model.GenerateMeshId(usedIds);
                usedIds.Add(newId);
                mesh.ChangeId(newId);
                mesh.ChangeGroupId(groupId);
                if (CurrentCreationType() != CreationType.YOUR)
                {
                    // Mark this mesh as being remixed from the selected asset.
                    mesh.ChangeRemixId(selectedAssetId);
                }
                mesh.offset = PeltzerMain.Instance.peltzerController.LastPositionModel + mesh.offset;
            }

            // Switch to the 'move' tool in 'create' mode and start the 'move-create' operation.
            PeltzerMain.Instance.peltzerController
              .ChangeMode(ControllerMode.move, ObjectFinder.ObjectById("ID_ToolGrab"));
            PeltzerMain.Instance.GetMover().currentMoveType = tools.Mover.MoveType.CREATE;
            PeltzerMain.Instance.GetMover().StartMove(meshes);
            PeltzerMain.Instance.peltzerController.ResetUnhoveredItem();
            PeltzerMain.Instance.peltzerController.ResetMenu();

        }

        public bool ToolMenuIsActive()
        {
            return activeMenu == Menu.TOOLS_MENU;
        }

        public bool PolyMenuIsActive()
        {
            return activeMenu == Menu.POLY_MENU;
        }

        public bool DetailsMenuIsActive()
        {
            return activeMenu == Menu.DETAILS_MENU;
        }
    }
}
