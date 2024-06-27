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

#define STEAMVRBUILD
using System.Collections.Generic;
using UnityEngine;

using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools;
using com.google.apps.peltzer.client.menu;
using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.tutorial;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Delegate method for controller clients to implement.
  /// </summary>
  public delegate void PaletteControllerActionHandler(object sender, ControllerEventArgs args);

  /// <summary>
  ///   Delegate method for undo listeners.
  /// </summary>
  public delegate void UndoActionHandler();

  /// <summary>
  ///   Script for the palette controller.
  /// </summary>
  public class PaletteController : MonoBehaviour {
    // The physical controller responsible for input & pose.
    public ControllerDevice controller;
    public ControllerGeometry controllerGeometry;

    // The toolheads
    [Header("Toolheads")]
    public GameObject shapeToolhead;
    public GameObject freeformToolhead;
    public GameObject paintToolhead;
    public GameObject grabToolhead;
    public GameObject modifyToolhead;
    public GameObject eraseToolhead;

    public GameObject steamRiftHolder;
    public GameObject oculusRiftHolder;

    /// <summary>
    /// Occasionally, the controller is not set when our app starts. This method
    /// will find the controller if it's null, and will return false if the
    /// controller is not found.
    /// </summary>
    /// <param name="paletteController">A reference to the controller that will be set if it's null.</param>
    /// <returns>Whether or not the controller was acquired and set.</returns>
    public static bool AcquireIfNecessary(ref PaletteController paletteController) {
      if (paletteController == null) {
        paletteController = FindObjectOfType<PaletteController>();
        if (paletteController == null) {
          return false;
        }
      }
      return true;
    }

    /// <summary>
    ///   Clients must register themselves on this handler.
    /// </summary>
    public event PaletteControllerActionHandler PaletteControllerActionHandler;

    /// <summary>
    ///   Clients must register themselves on this handler.
    /// </summary>
    public event UndoActionHandler UndoActionHandler;

    public GameObject tutorialButton;
    public GameObject UIPanels;
    public float speed;

    /// <summary>
    ///   List of objects that will change color when the selected color changes.
    /// </summary>
    private List<ColorChanger> colorChangers = new List<ColorChanger>();

    private static readonly float TWO_PI = Mathf.PI * 2f;
    private static readonly float PI_OVER_4 = Mathf.PI * .25f;
    private readonly Color BASE_COLOR = new Color(0.9927992f, 1f, 0.4779411f); // Yellowish highlight.
    private Transform peltzerControllerTouchpad;
    private AudioLibrary audioLibrary;

    /// <summary>
    ///   Red color for the active state of the app menu button.
    /// </summary>
    private readonly Color ACTIVE_BUTTON_COLOR = new Color(244f / 255f, 67f / 255f, 54f / 255f, 1);
    /// <summary>
    ///   Grey color for the inactive state of the app menu button.
    /// </summary>
    private readonly Color INACTIVE_BUTTON_COLOR = new Color(114f / 255f, 115f / 255f, 118f / 255f, 1f);

    /// <summary>
    ///   Reference to the menu root node GameObject.
    /// </summary>
    public GameObject menuPanel;
    /// <summary>
    ///   Reference to the Zandria menu root node GameObject.
    /// </summary>
    private GameObject polyMenuPanel;
    /// <summary>
    ///   Reference to the Details menu root node GameObject.
    /// </summary>
    private GameObject detailsMenuPanel;

    /// <summary>
    ///   Position reference for the menu panel when in the right hand.
    /// </summary>
    private readonly Vector3 menuPanelRightPos = new Vector3(-0.29f, 0f, 0f);
    /// <summary>
    ///   Position reference for the menu panel when in the right hand.
    /// </summary>
    private readonly Vector3 menuPanelZandriaRightPos = new Vector3(-0.34f, 0f, 0f);
    /// <summary>
    ///   Position reference for the details panel when in the left hand.
    /// </summary>
    private readonly Vector3 detailsPanelZandriaLeftPos = new Vector3(0.18f, 0f, -0.085f);
    /// <summary>
    ///   Position reference for the details panel when in the right hand.
    /// </summary>
    private readonly Vector3 detailsPanelZandriaRightPos = new Vector3(-0.18f, 0f, -0.085f);
    /// <summary>
    ///   Position reference for the menu panel when in the left hand and using Oculus.
    /// </summary>
    private readonly Vector3 menuPanelLeftPosOculus = new Vector3(0f, 0.025f, 0.025f);
    /// <summary>
    ///   Position reference for the menu panel when in the right hand and using Oculus.
    /// </summary>
    private readonly Vector3 menuPanelRightPosOculus = new Vector3(-0.29f, 0.025f, 0.025f);
    /// <summary>
    ///   Position reference for the Zandria menu panel when in the left hand and using Oculus.
    /// </summary>
    private readonly Vector3 menuPanelZandriaLeftPosOculus = new Vector3(0f, 0.025f, 0.025f);
    /// <summary>
    ///   Position reference for the Zandria menu panel when in the right hand and using Oculus.
    /// </summary>
    private readonly Vector3 menuPanelZandriaRightPosOculus = new Vector3(-0.35f, 0.025f, 0.025f);
    /// <summary>
    ///   Position reference for the details panel when in the left hand and using Oculus.
    /// </summary>
    private readonly Vector3 detailsPanelZandriaLeftPosOculus = new Vector3(0.18f, -0.03f, -0.03f);
    /// <summary>
    ///   Position reference for the details panel when in the right hand and using Oculus.
    /// </summary>
    private readonly Vector3 detailsPanelZandriaRightPosOculus = new Vector3(-0.18f, -0.03f, -0.03f);
    /// <summary>
    /// Tilt angle of the palette for controllers when using the Oculus SDK.
    /// </summary>
    private readonly Quaternion menuRotationOculus = Quaternion.Euler(-45, 0, 0);

    // Snap tooltip gameObjects and text
    private const int OPERATIONS_BEFORE_DISABLING_SNAP_TOOLTIPS = 3;

    public Handedness handedness = Handedness.LEFT;

    // Pop-up dialogs.
    public GameObject newModelPrompt;
    public GameObject publishedTakeOffHeadsetPrompt;
    public GameObject tutorialBeginPrompt;
    public GameObject tutorialSavePrompt;
    public GameObject publishAfterSavePrompt;
    public GameObject publishSignInPrompt;
    public GameObject tutorialExitPrompt;
    public GameObject saveLocallyPrompt;

    /// <summary>
    ///   Library to generate haptic feedback.
    /// </summary>
    private HapticFeedback hapticsLibrary;

    private Objectionary objectionary;

    private TouchpadOverlay currentOverlay;
    private Overlay overlay;

    private static readonly float DELAY_UNTIL_EVENT_REPEATS = 0.5f;
    private static readonly float DELAY_BETWEEN_REPEATING_EVENTS = 0.2f;
    private float? touchpadPressDownTime;
    private bool touchpadRepeating;
    private bool touchpadTouched = false;
    private float lastTouchpadRepeatTime;
    private ControllerEventArgs eventToRepeat;
    private TouchpadHoverState lastTouchpadHoverState = TouchpadHoverState.NONE;

    // Undo/Redo logic.
    private static readonly int MIN_REPEAT_SCALE = 1;
    private static readonly int MAX_REPEAT_SCALE = 8;
    private static readonly int REPEAT_SCALE_MULTIPLIER = 2;
    private int repeatScale = MIN_REPEAT_SCALE;

    /// <summary>
    ///   Local position for overlay icon - default - VIVE.
    /// </summary>
    Vector3 LEFT_OVERLAY_ICON_DEFAULT_POSITION_VIVE = new Vector3(-2.5f, 0f, 0f);
    Vector3 RIGHT_OVERLAY_ICON_DEFAULT_POSITION_VIVE = new Vector3(2.5f, 0f, 0f);
    Vector3 UP_OVERLAY_ICON_DEFAULT_POSITION_VIVE = new Vector3(0f, 2.5f, 0f);
    Vector3 DOWN_OVERLAY_ICON_DEFAULT_POSITION_VIVE = new Vector3(0f, -2.5f, 0f);

    /// <summary>
    ///   Local position for overlay icon - hover - VIVE.
    /// </summary>
    Vector3 LEFT_OVERLAY_ICON_HOVER_POSITION = new Vector3(-2.5f, 0f, -0.6f);
    Vector3 RIGHT_OVERLAY_ICON_HOVER_POSITION = new Vector3(2.5f, 0f, -0.6f);
    Vector3 UP_OVERLAY_ICON_HOVER_POSITION = new Vector3(0f, 2.5f, -0.6f);
    Vector3 DOWN_OVERLAY_ICON_HOVER_POSITION = new Vector3(0f, -2.5f, -0.6f);

    /// <summary>
    /// Records the time the publish dialog is opened so it can be expired after a set time.
    /// </summary>
    private float publishDialogStartTime = 0f;

    private static readonly float PUBLISH_DIALOG_LIFETIME = 10f;

    private bool setupDone;

    void Start() {
      PaletteControllerActionHandler += ControllerEventHandler;
    }

    public void Setup() {
      if (Config.Instance.sdkMode == SdkMode.SteamVR) {
#if STEAMVRBUILD
        controller = new ControllerDeviceSteam(transform);
#endif
      } else {
        ControllerDeviceOculus oculusController = new ControllerDeviceOculus(transform);
        oculusController.controllerType = OVRInput.Controller.LTouch;
        controller = oculusController;
      }
      controllerGeometry.baseControllerAnimation.SetControllerDevice(controller);

      hapticsLibrary = GetComponent<HapticFeedback>();
      audioLibrary = FindObjectOfType<AudioLibrary>();

      menuPanel = transform.Find("ID_PanelTools").gameObject;
      polyMenuPanel = transform.Find("Panel-Menu").gameObject;
      detailsMenuPanel = transform.Find("Model-Details").gameObject;
      tutorialButton = transform.Find("ID_PanelTools/ToolSide/Actions/Tutorial").gameObject;

      if (Config.Instance.sdkMode == SdkMode.Oculus) {
        menuPanel.transform.localPosition = menuPanelLeftPosOculus;
        menuPanel.transform.localRotation = menuRotationOculus;
        polyMenuPanel.transform.localRotation = menuRotationOculus;
        detailsMenuPanel.transform.localRotation = menuRotationOculus;
      }

      HideTooltips();

      newModelPrompt = transform.Find("ID_PanelTools/ToolSide/NewModelPrompt").gameObject;
      publishedTakeOffHeadsetPrompt = transform.Find("ID_PanelTools/ToolSide/TakeOffHeadsetPrompt").gameObject;
      tutorialSavePrompt = transform.Find("ID_PanelTools/ToolSide/TutorialSavePrompt").gameObject;
      tutorialBeginPrompt = transform.Find("ID_PanelTools/ToolSide/TutorialPrompt").gameObject;
      tutorialExitPrompt = transform.Find("ID_PanelTools/ToolSide/TutorialExitPrompt").gameObject;
      publishAfterSavePrompt = transform.Find("ID_PanelTools/ToolSide/PublishAfterSavePrompt").gameObject;
      publishSignInPrompt = transform.Find("ID_PanelTools/ToolSide/PublishSignInPrompt").gameObject;
      saveLocallyPrompt = transform.Find("ID_PanelTools/ToolSide/SaveLocallyPrompt").gameObject;

      bool shouldNagForTutorial = !PlayerPrefs.HasKey(TutorialManager.HAS_EVER_STARTED_TUTORIAL_KEY);
      if (shouldNagForTutorial) {
        tutorialButton.GetComponent<Renderer>().material.color = PeltzerController.MENU_BUTTON_GREEN;
        tutorialBeginPrompt.SetActive(true);
      } else {
        tutorialBeginPrompt.SetActive(false);
      }

      colorChangers.AddRange(GetComponentsInChildren<ColorChanger>(/* includeInactive */ true));

      // Turn on the paletteController menu overlay.
      controllerGeometry.menuOverlay.SetActive(true);
      if (controllerGeometry.OnMenuOverlay != null) controllerGeometry.OnMenuOverlay.SetActive(true);

      ResetTouchpadOverlay();

      // Put everything in the default handedness position.
      ControllerHandednessChanged();
      ResetTouchpadOverlay();

      setupDone = true;
    }

    void Update() {
      if (!setupDone) return;

      controller.Update();

      // If the publish dialog has been opened for more than 10 seconds, close it.
      if (publishedTakeOffHeadsetPrompt.activeSelf && Time.time - publishDialogStartTime > PUBLISH_DIALOG_LIFETIME) {
        publishedTakeOffHeadsetPrompt.SetActive(false);
      }

      menuPanel.SetActive(PeltzerMain.Instance.GetPolyMenuMain().ToolMenuIsActive()
        && PeltzerMain.Instance.restrictionManager.paletteAllowed);

      if (PeltzerMain.Instance.introChoreographer.introIsComplete) {
        ProcessButtonEvents();
      }
    }

    // Process user interaction.
    private void ProcessButtonEvents() {
      if (controller.IsTrackedObjectValid) {
        hapticsLibrary.controller = controller;
        SetGripTooltip();

        if (controller.WasJustPressed(ButtonId.Touchpad)) {
          if (PaletteControllerActionHandler != null) {
            Vector2 axis = controller.GetDirectionalAxis();
            TouchpadLocation location = controller.GetTouchpadLocation();

            ControllerEventArgs eventArgs = new ControllerEventArgs(ControllerType.PALETTE,
              ButtonId.Touchpad, ButtonAction.DOWN, location, TouchpadOverlay.NONE);
            PaletteControllerActionHandler(this, eventArgs);

            // Queue up this event to repeat in the case that the touchpad is not released
            if (location != TouchpadLocation.CENTER) {
              touchpadPressDownTime = Time.time;
              eventToRepeat = eventArgs;
              repeatScale = MIN_REPEAT_SCALE;
            }
          }
          // If the touchpad was released, then stop repeating events.
        } else if (controller.WasJustReleased(ButtonId.Touchpad)) {
          touchpadPressDownTime = null;
          touchpadRepeating = false;
          repeatScale = MIN_REPEAT_SCALE;
          // If the user stops touching the touchpad, sent out a 'cancel' event.
          PaletteControllerActionHandler(this, new ControllerEventArgs(ControllerType.PALETTE, ButtonId.Touchpad,
            ButtonAction.NONE, TouchpadLocation.NONE, currentOverlay));
        }

        if (controller.WasJustPressed(ButtonId.ApplicationMenu)) {
          if (PaletteControllerActionHandler != null) {
            ControllerEventArgs eventArgs = new ControllerEventArgs(ControllerType.PALETTE,
              ButtonId.ApplicationMenu, ButtonAction.DOWN, TouchpadLocation.NONE,
              TouchpadOverlay.NONE);
            PaletteControllerActionHandler(this, eventArgs);
          }
        }

        if (controller.WasJustPressed(ButtonId.SecondaryButton)) {
          if (PaletteControllerActionHandler != null) {
            ControllerEventArgs eventArgs = new ControllerEventArgs(ControllerType.PALETTE,
              ButtonId.SecondaryButton, ButtonAction.DOWN, TouchpadLocation.NONE,
              TouchpadOverlay.NONE);
            PaletteControllerActionHandler(this, eventArgs);
          }
        }

        if (Features.useContinuousSnapDetection && controller.IsTriggerHalfPressed()) {
          if (PaletteControllerActionHandler != null) {
            PaletteControllerActionHandler(this, new ControllerEventArgs(ControllerType.PALETTE,
            ButtonId.Trigger,
            ButtonAction.LIGHT_DOWN,
            TouchpadLocation.NONE,
            currentOverlay));
          }
        }

        if (Features.useContinuousSnapDetection && controller.WasTriggerJustReleasedFromHalfPress()) {
          if (PaletteControllerActionHandler != null) {
            PaletteControllerActionHandler(this, new ControllerEventArgs(ControllerType.PALETTE,
            ButtonId.Trigger,
            ButtonAction.LIGHT_UP,
            TouchpadLocation.NONE,
            currentOverlay));
          }
        }

        if (controller.WasJustPressed(ButtonId.Trigger)) {
          if (PaletteControllerActionHandler != null) {
            ControllerEventArgs eventArgs = new ControllerEventArgs(ControllerType.PALETTE,
              ButtonId.Trigger, ButtonAction.DOWN, TouchpadLocation.NONE,
              TouchpadOverlay.NONE);
            PaletteControllerActionHandler(this, eventArgs);
          }
        }

        if (controller.WasJustReleased(ButtonId.Trigger)) {
          if (PaletteControllerActionHandler != null) {
            ControllerEventArgs eventArgs = new ControllerEventArgs(ControllerType.PALETTE,
              ButtonId.Trigger, ButtonAction.UP, TouchpadLocation.NONE,
              TouchpadOverlay.NONE);
            PaletteControllerActionHandler(this, eventArgs);
          }
        }

        if (controller.IsPressed(ButtonId.Touchpad)) {
          // If the touchpad is held down, repeat events if appropriate.
          // Start repeating if it's been long enough since the initial press-down.
          SetHoverTooltips(isTouched: true);
          if (touchpadPressDownTime.HasValue && !touchpadRepeating &&
              (Time.time - touchpadPressDownTime.Value) > DELAY_UNTIL_EVENT_REPEATS) {
            touchpadRepeating = true;
            PaletteControllerActionHandler(this, eventToRepeat);
            lastTouchpadRepeatTime = Time.time;
            // Keep repeating if it's been long enough since the last
            // repeated event was sent.
          } else if (touchpadRepeating &&
                     (Time.time - lastTouchpadRepeatTime) >
                      DELAY_BETWEEN_REPEATING_EVENTS) {
            if (repeatScale < MAX_REPEAT_SCALE) {
              repeatScale *= REPEAT_SCALE_MULTIPLIER;
            }
            for (int i = 1; i <= repeatScale; i++) {
              PaletteControllerActionHandler(this, eventToRepeat);
            }
            lastTouchpadRepeatTime = Time.time;
          }
        } else if (controller.IsTouched(ButtonId.Touchpad)) {
          SetHoverTooltips(isTouched: true);
        } else {
          SetHoverTooltips(isTouched: false);
        }
      }
    }

    /// <summary>
    ///   Sets the hover tooltips on the touchpad.
    /// </summary>
    /// <param name="isTouched">Whether the touchpad is currently touched (hovered over)</param>
    private void SetHoverTooltips(bool isTouched) {
      // Show the menu tooltips if the user is touching, but not if we're zooming or all tooltips are disabled.
      bool touchedAndEnabled = !PeltzerMain.Instance.HasDisabledTooltips
        && !PeltzerMain.Instance.Zoomer.Zooming
        && isTouched;
      bool showPolyMenu = Features.showModelsMenuTooltips
        && touchedAndEnabled && PeltzerMain.Instance.polyMenuMain.polyMenu.activeInHierarchy;
      bool showToolMenu = touchedAndEnabled && PeltzerMain.Instance.polyMenuMain.toolMenu.activeInHierarchy;

      // Get the correct references to tooltips.
      GameObject pageLeftRightTooltip = PeltzerMain.Instance.paletteController.handedness == Handedness.RIGHT ?
        PeltzerMain.Instance.paletteController.controllerGeometry.menuRightTooltip :
        PeltzerMain.Instance.paletteController.controllerGeometry.menuLeftTooltip;
      GameObject undoRedoTooltip = PeltzerMain.Instance.paletteController.handedness == Handedness.RIGHT ?
        PeltzerMain.Instance.paletteController.controllerGeometry.undoRedoRightTooltip :
        PeltzerMain.Instance.paletteController.controllerGeometry.undoRedoLeftTooltip;

      PeltzerMain.Instance.paletteController.controllerGeometry.menuDownTooltip.SetActive(showPolyMenu);
      PeltzerMain.Instance.paletteController.controllerGeometry.menuUpTooltip.SetActive(showPolyMenu);
      pageLeftRightTooltip.SetActive(showPolyMenu);
      // Reset both instances to false before setting the state of shown tooltip.
      PeltzerMain.Instance.paletteController.controllerGeometry.undoRedoRightTooltip.SetActive(false);
      PeltzerMain.Instance.paletteController.controllerGeometry.undoRedoLeftTooltip.SetActive(false);
      undoRedoTooltip.SetActive(showToolMenu);
      if (touchedAndEnabled) {
        TouchpadHoverState touchpadHoverState;
        switch (controller.GetTouchpadLocation()) {
          case TouchpadLocation.TOP:
            touchpadHoverState = TouchpadHoverState.UP;
            break;
          case TouchpadLocation.BOTTOM:
            touchpadHoverState = TouchpadHoverState.DOWN;
            break;
          case TouchpadLocation.LEFT:
            touchpadHoverState = TouchpadHoverState.LEFT;
            break;
          case TouchpadLocation.RIGHT:
            touchpadHoverState = TouchpadHoverState.RIGHT;
            break;
          default:
            touchpadHoverState = TouchpadHoverState.NONE;
            break;
        }
        SetTouchpadHoverTexture(touchpadHoverState);
      } else {
        SetTouchpadHoverTexture(TouchpadHoverState.NONE);
      }
    }

    public GameObject GetToolheadForMode(ControllerMode mode) {
      switch (mode) {
        case ControllerMode.insertVolume:
        case ControllerMode.subtract:
          return shapeToolhead;
        case ControllerMode.insertStroke:
          return freeformToolhead;
        case ControllerMode.paintFace:
        case ControllerMode.paintMesh:
          return paintToolhead;
        case ControllerMode.move:
          return grabToolhead;
        case ControllerMode.subdivideFace:
        case ControllerMode.reshape:
        case ControllerMode.extrude:
          return modifyToolhead;
        case ControllerMode.delete:
        case ControllerMode.deletePart:
          return eraseToolhead;
      }
      return null;
    }

    public void SetPublishDialogActive() {
      publishedTakeOffHeadsetPrompt.SetActive(true);
      publishDialogStartTime = Time.time;
    }

    private bool IsUndoEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && args.TouchpadLocation == TouchpadLocation.LEFT;
    }

    private bool IsRedoEvent(ControllerEventArgs args) {
      return args.ControllerType == ControllerType.PALETTE
        && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN
        && args.TouchpadLocation == TouchpadLocation.RIGHT;
    }

    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if (!PeltzerMain.Instance.restrictionManager.undoRedoAllowed) {
        return;
      } else if (IsUndoEvent(args)) {
        Undo();
      } else if (IsRedoEvent(args)) {
        Redo();
      }
    }

    /// <summary>
    ///   The 'undo' command. Special-cased for stroke and subdivide modes, and selected items.
    /// </summary>
    private void Undo() {
      bool undid = false;
      bool canUndoInModel = false;
      Selector selector = PeltzerMain.Instance.GetSelector();

      if (Features.clickToSelectWithUndoRedoEnabled && selector.selectedMeshes.Count > 0) {
        // Divert to local undo for multi-selection
        undid = selector.UndoMultiSelect();
      } else if (Features.localUndoRedoEnabled && selector.isMultiSelecting) {
        // Divert to local undo for multi-selection
        undid = selector.UndoMultiSelect();
      } else if (PeltzerMain.Instance.GetFreeform().IsStroking()) {
        // In stroke mode, we try and remove a checkpoint.
        undid = PeltzerMain.Instance.GetFreeform().Undo();
      } else if (selector.AnythingSelected()) {
        // If anything was selected in any mode, we clear the selection.
        undid = true;
        selector.DeselectAll();
      } else if (PeltzerMain.Instance.GetDeleter().isDeleting) {
        // If we are currently deleting, anything that has been deleted so far is restored.
        undid = PeltzerMain.Instance.GetDeleter().CancelDeletionsSoFar();
      } else {
        // Else, we actually try and perform 'undo' on the model. We defer the actual 'undo' call until after the
        // handler fires, in case tools try and do something with a mesh which is about to be deleted.
        canUndoInModel = !PeltzerMain.Instance.OperationInProgress() && PeltzerMain.Instance.GetModel().CanUndo();
        // As soon as the user undoes something that is not multi-selection, clear all the redo stacks (the undo stacks would
        // already be empty at this point).
        selector.ClearMultiSelectRedoState();
      }

      audioLibrary.PlayClip(undid || canUndoInModel ? audioLibrary.undoSound : audioLibrary.errorSound);
      TriggerHapticFeedback();

      if (undid || canUndoInModel) {
        if (UndoActionHandler != null) {
          UndoActionHandler();
        }
      }

      if (canUndoInModel) {
        PeltzerMain.Instance.GetModel().Undo();
      }
    }

    /// <summary>
    ///   The 'redo' command. No special-cases.
    /// </summary>
    private void Redo() {
      bool redid;
      Selector selector = PeltzerMain.Instance.GetSelector();

      if (Features.clickToSelectWithUndoRedoEnabled
        && selector.redoMeshMultiSelect.Count > 0) {
        // Divert to local redo for multi-selection
        redid = selector.RedoMultiSelect();
      } else if (Features.localUndoRedoEnabled && selector.isMultiSelecting) {
        // Divert to local redo for multi-selection
        redid = selector.RedoMultiSelect();
      } else if (PeltzerMain.Instance.OperationInProgress()) {
        redid = false;
      } else {
        // We always remove selections when re-doing.
        if (selector.AnythingSelected()) {
          selector.DeselectAll();
        }
        redid = PeltzerMain.Instance.GetModel().Redo();
      }

      audioLibrary.PlayClip(redid ? audioLibrary.redoSound : audioLibrary.errorSound);
      TriggerHapticFeedback();
    }

    /// <summary>
    ///   Called when the handedness changes of the controller to accomodate necessary changes.
    /// </summary>
    public void ControllerHandednessChanged() {
      if (handedness == Handedness.LEFT) {
        if (Config.Instance.sdkMode == SdkMode.Oculus) {
          menuPanel.transform.localPosition = menuPanelLeftPosOculus;
          polyMenuPanel.transform.localPosition = menuPanelZandriaLeftPosOculus;
          detailsMenuPanel.transform.localPosition = detailsPanelZandriaLeftPosOculus;
        } else {
          menuPanel.transform.localPosition = Vector3.zero;
          polyMenuPanel.transform.localPosition = Vector3.zero;
          detailsMenuPanel.transform.localPosition = detailsPanelZandriaLeftPos;
        }

        controllerGeometry.zoomRightTooltip.SetActive(false);
        controllerGeometry.moveRightTooltip.SetActive(false);
        controllerGeometry.snapRightTooltip.SetActive(false);
        controllerGeometry.straightenRightTooltip.SetActive(false);
        controllerGeometry.menuRightTooltip.SetActive(false);

        controllerGeometry.snapGrabAssistRightTooltip.SetActive(false);
        controllerGeometry.snapGrabHoldRightTooltip.SetActive(false);
        controllerGeometry.snapStrokeRightTooltip.SetActive(false);
        controllerGeometry.snapShapeInsertRightTooltip.SetActive(false);
        controllerGeometry.snapModifyRightTooltip.SetActive(false);
        controllerGeometry.snapPaintOrEraseRightTooltip.SetActive(false);

        controllerGeometry.applicationButtonTooltipRight.SetActive(false);
      } else if (handedness == Handedness.RIGHT) {
        if (Config.Instance.sdkMode == SdkMode.Oculus) {
          menuPanel.transform.localPosition = menuPanelRightPosOculus;
          polyMenuPanel.transform.localPosition = menuPanelZandriaRightPosOculus;
          detailsMenuPanel.transform.localPosition = detailsPanelZandriaRightPosOculus;
        } else {
          menuPanel.transform.localPosition = menuPanelRightPos;
          polyMenuPanel.transform.localPosition = menuPanelZandriaRightPos;
          detailsMenuPanel.transform.localPosition = detailsPanelZandriaRightPos;
        }

        controllerGeometry.zoomLeftTooltip.SetActive(false);
        controllerGeometry.moveLeftTooltip.SetActive(false);
        controllerGeometry.snapLeftTooltip.SetActive(false);
        controllerGeometry.straightenLeftTooltip.SetActive(false);
        controllerGeometry.menuLeftTooltip.SetActive(false);

        controllerGeometry.snapGrabAssistLeftTooltip.SetActive(false);
        controllerGeometry.snapGrabHoldLeftTooltip.SetActive(false);
        controllerGeometry.snapStrokeLeftTooltip.SetActive(false);
        controllerGeometry.snapShapeInsertLeftTooltip.SetActive(false);
        controllerGeometry.snapModifyLeftTooltip.SetActive(false);
        controllerGeometry.snapPaintOrEraseLeftTooltip.SetActive(false);

        controllerGeometry.applicationButtonTooltipLeft.SetActive(false);
      }
    }

    /// <summary>
    ///   Triggers controller vibration.
    ///
    ///   Better haptic feedback will come with https://bug
    /// </summary>
    public void TriggerHapticFeedback(
        HapticFeedback.HapticFeedbackType type = HapticFeedback.HapticFeedbackType.FEEDBACK_1,
        float durationSeconds = 0.01f, float strength = 0.3f) {

      if (hapticsLibrary != null) {
        hapticsLibrary.PlayHapticFeedback(type, durationSeconds, strength);
      }
    }

    /// <summary>
    ///   Triggers controller vibrations to get the user to look at the controller.
    /// </summary>
    public void LookAtMe() {
      TriggerHapticFeedback(
          HapticFeedback.HapticFeedbackType.FEEDBACK_1,
          0.2f,
          0.5f
        );
    }

    /// <summary>
    ///   Triggers controller vibrations to let the user know they've done a good job.
    /// </summary>
    public void YouDidIt() {
      TriggerHapticFeedback(
          HapticFeedback.HapticFeedbackType.FEEDBACK_1,
          0.1f,
          0.2f
        );
    }

    /// <summary>
    ///   Update the colors on the palette to match the selected color.
    /// </summary>
    /// <param name="currentMaterial">The new color.</param>
    public void UpdateColors(int currentMaterial) {
      if (!PeltzerMain.Instance.restrictionManager.toolheadColorChangeAllowed) {
        return;
      }

      // Change the tools on the palette.
      foreach (ColorChanger colorChanger in colorChangers) {
        colorChanger.ChangeMaterial(currentMaterial);
      }
    }

    /// <summary>
    /// Depending on the current controller mode, will show the relevant snapping tooltips.
    /// </summary>
    public void ShowSnapAssistanceTooltip() {
      if (!TooltipsAllowed()) return;

      switch (PeltzerMain.Instance.peltzerController.mode) {
        case ControllerMode.insertVolume:
          GameObject shapeInsertSnapTooltip = handedness == Handedness.LEFT ?
            controllerGeometry.snapShapeInsertLeftTooltip : controllerGeometry.snapShapeInsertRightTooltip;
          shapeInsertSnapTooltip.SetActive(true);
          break;
        case ControllerMode.subdivideFace:
        case ControllerMode.reshape:
        case ControllerMode.extrude:
          GameObject modifySnapTooltip = handedness == Handedness.LEFT ?
            controllerGeometry.snapModifyLeftTooltip : controllerGeometry.snapModifyRightTooltip;
          modifySnapTooltip.SetActive(true);
          break;
        case ControllerMode.paintFace:
        case ControllerMode.paintMesh:
        case ControllerMode.delete:
          GameObject paintOrEraseSnapTooltip = handedness == Handedness.LEFT ?
            controllerGeometry.snapPaintOrEraseLeftTooltip : controllerGeometry.snapPaintOrEraseRightTooltip;
          paintOrEraseSnapTooltip.SetActive(true);
          break;
        case ControllerMode.insertStroke:
          GameObject strokeSnapTooltip = handedness == Handedness.LEFT ?
            controllerGeometry.snapStrokeLeftTooltip : controllerGeometry.snapStrokeRightTooltip;
          strokeSnapTooltip.SetActive(true);
          break;
        case ControllerMode.move:
          // Give priority to half trigger tool tip over the more general one, because it has an expiration time.
          GameObject holdTriggerHalfwaySnapTooltip = handedness == Handedness.LEFT ?
            controllerGeometry.snapGrabHoldLeftTooltip : controllerGeometry.snapGrabHoldRightTooltip;
          if (holdTriggerHalfwaySnapTooltip.activeSelf) break;

          GameObject grabAssistSnapTooltip = handedness == Handedness.LEFT ?
            controllerGeometry.snapGrabAssistLeftTooltip : controllerGeometry.snapGrabAssistRightTooltip;
          grabAssistSnapTooltip.SetActive(true);
          break;
        default:
          return;
      }
    }

    /// <summary>
    /// Show the tool tip that instructs the user on how to hold the half trigger to preview a snap.
    /// In a separate function because it has different behavior from other snap tooltips.
    /// </summary>
    public void ShowHoldTriggerHalfwaySnapTooltip() {
      if (!TooltipsAllowed()) return;

      GameObject holdTriggerHalfwaySnapTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.snapGrabHoldLeftTooltip : controllerGeometry.snapGrabHoldRightTooltip;
      holdTriggerHalfwaySnapTooltip.SetActive(true);
    }

    /// <summary>
    /// Returns whether tooltips are currently allowed.
    /// </summary>
    private bool TooltipsAllowed() {
      return PeltzerMain.Instance.restrictionManager.tooltipsAllowed
        && !PeltzerMain.Instance.tutorialManager.TutorialOccurring()
        && !PeltzerMain.Instance.HasDisabledTooltips;
    }

    public void DisableSnapTooltips() {
      GameObject snapTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.snapLeftTooltip : controllerGeometry.snapRightTooltip;
      snapTooltip.SetActive(false);
      GameObject straightenTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.straightenLeftTooltip : controllerGeometry.straightenRightTooltip;
      straightenTooltip.SetActive(false);
    }

    public void HideSnapAssistanceTooltips() {
      GameObject snapGrabAssistTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.snapGrabAssistLeftTooltip : controllerGeometry.snapGrabAssistRightTooltip;
      snapGrabAssistTooltip.SetActive(false);
      GameObject snapGrabHoldTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.snapGrabHoldLeftTooltip : controllerGeometry.snapGrabHoldRightTooltip;
      snapGrabHoldTooltip.SetActive(false);
      GameObject snapStrokeTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.snapStrokeLeftTooltip : controllerGeometry.snapStrokeRightTooltip;
      snapStrokeTooltip.SetActive(false);
      GameObject snapShapeInsertTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.snapShapeInsertLeftTooltip : controllerGeometry.snapShapeInsertRightTooltip;
      snapShapeInsertTooltip.SetActive(false);
      GameObject snapModifyTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.snapModifyLeftTooltip : controllerGeometry.snapModifyRightTooltip;
      snapModifyTooltip.SetActive(false);
      GameObject snapPaintOrEraseTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.snapPaintOrEraseLeftTooltip : controllerGeometry.snapPaintOrEraseRightTooltip;
      snapPaintOrEraseTooltip.SetActive(false);
    }

    /// <summary>
    ///   Determines which tooltip and where to show it when called. These are the grip tooltips to
    ///   advise a user how to move/zoom the world.
    ///   We only show these tooltips until the user has successfully moved or zoomed the world.
    ///   We do not show these tooltips until at least one object is in the scene.
    /// </summary>
    public void SetGripTooltip() {
      if (!PeltzerController.AcquireIfNecessary(ref PeltzerMain.Instance.peltzerController)) return;

      if (PeltzerMain.Instance.Zoomer.userHasEverZoomed
        || !PeltzerMain.Instance.restrictionManager.tooltipsAllowed
        || PeltzerMain.Instance.HasDisabledTooltips
        || PeltzerMain.Instance.tutorialManager.TutorialOccurring()) {
        DisableGripTooltips();
        return;
      }

      GameObject zoomTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.zoomLeftTooltip : controllerGeometry.zoomRightTooltip;
      GameObject moveTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.moveLeftTooltip : controllerGeometry.moveRightTooltip;
      if (controller.IsPressed(ButtonId.Grip)
        && PeltzerMain.Instance.peltzerController.controller.IsPressed(ButtonId.Grip)) {
        zoomTooltip.SetActive(false);
        // Stop pulsating glow highlight on grips.
        float emission = 0;
        Color highlightColor = BASE_COLOR * Mathf.LinearToGammaSpace(emission);
        if (controllerGeometry.gripLeft != null) {
          controllerGeometry.gripLeft.GetComponent<Renderer>().material.SetColor("_EmissionColor", highlightColor);
        }
        if (controllerGeometry.gripRight != null) {
          controllerGeometry.gripRight.GetComponent<Renderer>().material.SetColor("_EmissionColor", highlightColor);
        }
        // hide the hold to move tooltip
        moveTooltip.SetActive(false);
      } else if (controller.IsPressed(ButtonId.Grip)
        && !PeltzerMain.Instance.peltzerController.controller.IsPressed(ButtonId.Grip)) {
        zoomTooltip.SetActive(true);
        moveTooltip.SetActive(false);
      } else if (!controller.IsPressed(ButtonId.Grip)
        && PeltzerMain.Instance.peltzerController.controller.IsPressed(ButtonId.Grip)) {
        zoomTooltip.SetActive(true);
        // Pulsating glow highlight on grips.
        float emission = Mathf.PingPong(Time.time / 2F, 0.4f);
        Color highlightColor = BASE_COLOR * Mathf.LinearToGammaSpace(emission);
        if (controllerGeometry.gripLeft != null) {
          controllerGeometry.gripLeft.GetComponent<Renderer>().material.SetColor("_EmissionColor", highlightColor);
        }
        if (controllerGeometry.gripRight != null) {
          controllerGeometry.gripRight.GetComponent<Renderer>().material.SetColor("_EmissionColor", highlightColor);
        }
        moveTooltip.SetActive(false);
      } else {
        DisableGripTooltips();
      }
    }

    /// <summary>
    ///   Disables the 'hold to move' and 'hold to zoom' tooltips, and grip-button pulsing.
    /// </summary>
    public void DisableGripTooltips() {
      GameObject zoomTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.zoomLeftTooltip : controllerGeometry.zoomRightTooltip;
      GameObject moveTooltip = handedness == Handedness.LEFT ?
        controllerGeometry.moveLeftTooltip : controllerGeometry.moveRightTooltip;

      zoomTooltip.SetActive(false);
      moveTooltip.SetActive(false);
      // Stop pulsating glow highlight on grips.
      float emission = 0;
      Color highlightColor = BASE_COLOR * Mathf.LinearToGammaSpace(emission);

      if (controllerGeometry.gripLeft != null) {
        controllerGeometry.gripLeft.GetComponent<Renderer>().material.SetColor("_EmissionColor", highlightColor);
      }
      if (controllerGeometry.gripRight != null) {
        controllerGeometry.gripRight.GetComponent<Renderer>().material.SetColor("_EmissionColor", highlightColor);
      }
    }

    /// <summary>
    ///   Make the app menu button red or grey depending on whether or not its active.
    /// </summary>
    /// <param name="active"></param>
    public void SetApplicationButtonOverlay(bool active) {
      if (controllerGeometry.appMenuButton == null) return;

      Material material = controllerGeometry.appMenuButton.GetComponent<Renderer>().material;
      Color highlightEmmissionColor;
      Color highlightColor;
      if (active) {
        highlightEmmissionColor = ACTIVE_BUTTON_COLOR * Mathf.LinearToGammaSpace(0.6f); // Set emission to 60%.
        highlightColor = ACTIVE_BUTTON_COLOR;
      } else {
        highlightEmmissionColor = INACTIVE_BUTTON_COLOR * Mathf.LinearToGammaSpace(0f);
        highlightColor = INACTIVE_BUTTON_COLOR;
      }

      material.SetColor("_EmissionColor", highlightEmmissionColor);
      material.color = highlightColor;
    }

    /// <summary>
    ///   Make the secondary button red or grey depending on whether or not its active.
    /// </summary>
    /// <param name="active"></param>
    public void SetSecondaryButtonOverlay(bool active) {
      if (controllerGeometry.secondaryButton == null) return;

      Material material = controllerGeometry.secondaryButton.GetComponent<Renderer>().material;
      Color highlightEmmissionColor;
      Color highlightColor;
      if (active) {
        highlightEmmissionColor = ACTIVE_BUTTON_COLOR * Mathf.LinearToGammaSpace(0.6f); // Set emission to 60%.
        highlightColor = ACTIVE_BUTTON_COLOR;
      } else {
        highlightEmmissionColor = INACTIVE_BUTTON_COLOR * Mathf.LinearToGammaSpace(0f);
        highlightColor = INACTIVE_BUTTON_COLOR;
      }

      material.SetColor("_EmissionColor", highlightEmmissionColor);
      material.color = highlightColor;
    }

    /// <summary>
    ///   Takes a controller and registers any event handlers with the specified controller in order to facilitate
    ///   event handling between controllers.
    /// </summary>
    /// <param name="controller">The controller containing the handlers to register against.</param>
    public void RegisterCrossControllerHandlers(PeltzerController controller) {
      // Register the ShapeToolheadAnimation component for shape and material changes.
      ShapeToolheadAnimation sta = gameObject.GetComponent<ShapeToolheadAnimation>();
      controller.shapesMenu.ShapeMenuItemChangedHandler += sta.ShapeChangedHandler;
    }

    public void ResetTouchpadOverlay() {
      PolyMenuMain polyMenuMain = PeltzerMain.Instance.GetPolyMenuMain();
      Zoomer zoomer = PeltzerMain.Instance.Zoomer;
      if (zoomer != null && zoomer.isMovingWithPaletteController) {
        ChangeTouchpadOverlay(TouchpadOverlay.RESET_ZOOM);
      } else if (polyMenuMain != null && (polyMenuMain.DetailsMenuIsActive() || polyMenuMain.PolyMenuIsActive())) {
        ChangeTouchpadOverlay(TouchpadOverlay.MENU);
      } else {
        ChangeTouchpadOverlay(TouchpadOverlay.UNDO_REDO);
      }
    }

    /// <summary>
    ///   Change the touchpad overlay to the given type.  Will automatically highlight selected
    ///   modes where appropriate.
    /// </summary>
    /// <param name="newOverlay"></param>
    public void ChangeTouchpadOverlay(TouchpadOverlay newOverlay) {
      currentOverlay = newOverlay;

      // Set the correct parent overlay active based on the passed overlay. Some parent overlays have sub-overlays that
      // will be set based on ControllerMode.
      controllerGeometry.volumeInserterOverlay.SetActive(currentOverlay == TouchpadOverlay.VOLUME_INSERTER);
      controllerGeometry.freeformOverlay.SetActive(currentOverlay == TouchpadOverlay.FREEFORM);
      controllerGeometry.paintOverlay.SetActive(currentOverlay == TouchpadOverlay.PAINT);
      controllerGeometry.modifyOverlay.SetActive(currentOverlay == TouchpadOverlay.MODIFY);

      controllerGeometry.moveOverlay.SetActive(currentOverlay == TouchpadOverlay.MOVE);
      if (controllerGeometry.OnMoveOverlay != null) controllerGeometry.OnMoveOverlay.SetActive(currentOverlay == TouchpadOverlay.MOVE);

      controllerGeometry.deleteOverlay.SetActive(currentOverlay == TouchpadOverlay.DELETE);

      controllerGeometry.menuOverlay.SetActive(currentOverlay == TouchpadOverlay.MENU);
      if (controllerGeometry.OnMenuOverlay != null) controllerGeometry.OnMenuOverlay.SetActive(currentOverlay == TouchpadOverlay.MENU);

      controllerGeometry.undoRedoOverlay.SetActive(currentOverlay == TouchpadOverlay.UNDO_REDO);
      if (controllerGeometry.OnUndoRedoOverlay != null) controllerGeometry.OnUndoRedoOverlay.SetActive(currentOverlay == TouchpadOverlay.UNDO_REDO);

      controllerGeometry.resizeOverlay.SetActive(currentOverlay == TouchpadOverlay.RESIZE);
      controllerGeometry.resetZoomOverlay.SetActive(currentOverlay == TouchpadOverlay.RESET_ZOOM);

      // Set the secondary button active if we need to show the reset zoom overlay.
      SetSecondaryButtonOverlay(/*active*/ currentOverlay == TouchpadOverlay.RESET_ZOOM);
      // The user can't open the menu while zooming.
      SetApplicationButtonOverlay(/*active*/ currentOverlay != TouchpadOverlay.RESET_ZOOM);

      // The palette controller overlays have no sub-overlays so we don't need to worry about setting them. But if they
      // did we would copy what we do in peltzer controller and do it here.

      // Get reference to current overlay.
      GameObject currentOverlayGO;
      switch (currentOverlay) {
        case TouchpadOverlay.VOLUME_INSERTER:
          currentOverlayGO = controllerGeometry.volumeInserterOverlay;
          break;
        case TouchpadOverlay.FREEFORM:
          currentOverlayGO = controllerGeometry.freeformOverlay;
          break;
        case TouchpadOverlay.PAINT:
          currentOverlayGO = controllerGeometry.paintOverlay;
          break;
        case TouchpadOverlay.MODIFY:
          currentOverlayGO = controllerGeometry.modifyOverlay;
          break;
        case TouchpadOverlay.MOVE:
          currentOverlayGO = controllerGeometry.moveOverlay;
          break;
        case TouchpadOverlay.DELETE:
          currentOverlayGO = controllerGeometry.deleteOverlay;
          break;
        case TouchpadOverlay.MENU:
          currentOverlayGO = controllerGeometry.menuOverlay;
          break;
        case TouchpadOverlay.UNDO_REDO:
          currentOverlayGO = controllerGeometry.undoRedoOverlay;
          break;
        case TouchpadOverlay.RESIZE:
          currentOverlayGO = controllerGeometry.resizeOverlay;
          break;
        case TouchpadOverlay.RESET_ZOOM:
          currentOverlayGO = controllerGeometry.resetZoomOverlay;
          break;
        default:
          currentOverlayGO = null;
          break;
      }

      // Update cache reference to current Overlay component.
      if (currentOverlayGO != null) {
        overlay = currentOverlayGO.GetComponent<Overlay>();
      }
    }

    public TouchpadOverlay TouchpadOverlay { get { return currentOverlay; } }

    public void HideTooltips() {
      controllerGeometry.snapLeftTooltip.SetActive(false);
      controllerGeometry.snapRightTooltip.SetActive(false);
      controllerGeometry.straightenLeftTooltip.SetActive(false);
      controllerGeometry.straightenRightTooltip.SetActive(false);
      controllerGeometry.zoomLeftTooltip.SetActive(false);
      controllerGeometry.zoomRightTooltip.SetActive(false);
      controllerGeometry.moveLeftTooltip.SetActive(false);
      controllerGeometry.moveRightTooltip.SetActive(false);
      controllerGeometry.applicationButtonTooltipLeft.SetActive(false);
      controllerGeometry.applicationButtonTooltipRight.SetActive(false);

      controllerGeometry.snapGrabAssistLeftTooltip.SetActive(false);
      controllerGeometry.snapGrabAssistRightTooltip.SetActive(false);
      controllerGeometry.snapGrabHoldLeftTooltip.SetActive(false);
      controllerGeometry.snapGrabHoldRightTooltip.SetActive(false);
      controllerGeometry.snapStrokeLeftTooltip.SetActive(false);
      controllerGeometry.snapStrokeRightTooltip.SetActive(false);
      controllerGeometry.snapShapeInsertLeftTooltip.SetActive(false);
      controllerGeometry.snapShapeInsertRightTooltip.SetActive(false);
      controllerGeometry.snapModifyLeftTooltip.SetActive(false);
      controllerGeometry.snapModifyRightTooltip.SetActive(false);
      controllerGeometry.snapPaintOrEraseLeftTooltip.SetActive(false);
      controllerGeometry.snapPaintOrEraseRightTooltip.SetActive(false);
      SetTouchpadHoverTexture(TouchpadHoverState.NONE);
    }

    /// <summary>
    ///   Set the hover state material on the controller.
    /// </summary>
    /// <param name="state">State of the hover state to match.</param>
    public void SetTouchpadHoverTexture(TouchpadHoverState state) {
      // Only for VIVE currently.
      if (Config.Instance.VrHardware == VrHardware.Vive) {
        // Set state of hover icon for the current overlay.
        // Reset scale to default.
        if (overlay != null) {
          Vector3 resetScale = new Vector3(1.0f, 1.0f, 1.0f);
          overlay.upIcon.transform.localScale = resetScale;
          overlay.downIcon.transform.localScale = resetScale;
          overlay.leftIcon.transform.localScale = resetScale;
          overlay.rightIcon.transform.localScale = resetScale;
          // Reset positions to default.
          overlay.upIcon.transform.localPosition = UP_OVERLAY_ICON_DEFAULT_POSITION_VIVE;
          overlay.downIcon.transform.localPosition = DOWN_OVERLAY_ICON_DEFAULT_POSITION_VIVE;
          overlay.leftIcon.transform.localPosition = LEFT_OVERLAY_ICON_DEFAULT_POSITION_VIVE;
          overlay.rightIcon.transform.localPosition = RIGHT_OVERLAY_ICON_DEFAULT_POSITION_VIVE;
          // }
          SpriteRenderer icon = null;
          switch (state) {
            case TouchpadHoverState.UP:
              icon = overlay.upIcon;
              icon.transform.localScale *= 1.25f;
              icon.transform.localPosition = UP_OVERLAY_ICON_HOVER_POSITION;

              break;
            case TouchpadHoverState.DOWN:
              icon = overlay.downIcon;
              icon.transform.localScale *= 1.25f;
              icon.transform.localPosition = DOWN_OVERLAY_ICON_HOVER_POSITION;
              break;
            case TouchpadHoverState.LEFT:
              icon = overlay.leftIcon;
              icon.transform.localScale *= 1.25f;
              icon.transform.localPosition = LEFT_OVERLAY_ICON_HOVER_POSITION;
              break;
            case TouchpadHoverState.RIGHT:
              icon = overlay.rightIcon;
              icon.transform.localScale *= 1.25f;
              icon.transform.localPosition = RIGHT_OVERLAY_ICON_HOVER_POSITION;
              break;
            case TouchpadHoverState.NONE:
              break;
          }
          if (state != lastTouchpadHoverState && icon && icon.gameObject.activeInHierarchy) {
            TriggerHapticFeedback(
              HapticFeedback.HapticFeedbackType.FEEDBACK_1,
              0.003f,
              0.15f
            );
          }
          lastTouchpadHoverState = state;
        }
      }
    }
  }
}
