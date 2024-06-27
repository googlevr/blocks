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
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.tools;
using com.google.apps.peltzer.client.zandria;
using com.google.apps.peltzer.client.app;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Delegate method for controller clients to implement.
  /// </summary>
  public delegate void PeltzerControllerActionHandler(object sender, ControllerEventArgs args);

  /// <summary>
  ///   Delegate method called when the selected material has changed.
  /// </summary>
  /// <param name="newMaterialId"></param>
  public delegate void MaterialChangedHandler(int newMaterialId);

  /// <summary>
  ///   Delegate called when the controller mode changes.
  /// </summary>
  public delegate void ModeChangedHandler(ControllerMode oldMode, ControllerMode newMode);

  /// <summary>
  ///   Delegate called when the block mode changes.
  /// </summary>
  /// <param name="isBlockMode">Whether block mode is enabled.</param>
  public delegate void BlockModeChangedHandler(bool isBlockMode);

  /// <summary>
  ///   Type of overlay to show on the controller's touchpad.
  /// </summary>
  public enum TouchpadOverlay {
    NONE, VOLUME_INSERTER, FREEFORM, PAINT, MODIFY, MOVE, DELETE, MENU, UNDO_REDO, RESET_ZOOM, RESIZE
  };

  public enum TouchpadHoverState {
    NONE, UP, DOWN, LEFT, RIGHT, RESIZE_UP, RESIZE_DOWN
  }

  /// <summary>
  ///   A 6DOF controller. Must be attached to the controllers at the outset of the app's runtime.
  ///   Manages controller state and communicates events to clients.
  /// </summary>
  public class PeltzerController : MonoBehaviour {
    // The physical controller responsible for input & pose.
    public ControllerDevice controller;
    public ControllerGeometry controllerGeometry;

    public GameObject steamRiftHolder;
    public GameObject oculusRiftHolder;

    // Some tools intelligently choose between a 'click and hold' operation and a 'click to begin, click to end'
    // operation based on how long the trigger is held after its initial depression. This is the threshold
    // beyond which the user is forced into a 'click and hold' behaviour. This is in seconds.
    public static readonly float SINGLE_CLICK_THRESHOLD = 0.175f;

    /// <summary>
    /// Minimum interval, in seconds, between two successive haptic feedback events of the same type.
    /// </summary>
    public const float MIN_HAPTIC_FEEDBACK_INTERVAL = 0.1f;

    /// <summary>
    /// Time when haptic feedback was last triggered (for each feedback type), as given by Time.time.
    /// </summary>
    private Dictionary<HapticFeedback.HapticFeedbackType, float> lastHapticFeedbackTime =
      new Dictionary<HapticFeedback.HapticFeedbackType, float>();

    private readonly List<ControllerMode> controllerModes
        = new List<ControllerMode>(Enum.GetValues(typeof(ControllerMode)).Cast<ControllerMode>());

    /// <summary>
    /// Occasionally, the controller is not set when our app starts. This method
    /// will find the controller if it's null, and will return false if the
    /// controller is not found.
    /// </summary>
    /// <param name="peltzerController">A reference to the controller that will be set if it's null.</param>
    /// <returns>Whether or not the controller was acquired and set.</returns>
    public static bool AcquireIfNecessary(ref PeltzerController peltzerController) {
      if (peltzerController == null) {
        peltzerController = FindObjectOfType<PeltzerController>();
        if (peltzerController == null) {
          return false;
        }
      }
      return true;
    }

    public GameObject wandTip;
    public TextMesh wandTipLabel;
    public HeldMeshes heldMeshes;

    // Touchpad Textures
    public Material touchpadUpMaterial;
    public Material touchpadDownMaterial;
    public Material touchpadLeftMaterial;
    public Material touchpadRightMaterial;
    private Material touchpadDefaultMaterial;
    private readonly Color BASE_COLOR = new Color(0.9927992f, 1f, 0.4779411f); // Yellowish highlight.

    // Create the WaitForSeconds once to avoid leaking:
    // https://forum.unity3d.com/threads/c-coroutine-waitforseconds-garbage-collection-tip.224878/
    private static WaitForSeconds SHORT_WAIT = new WaitForSeconds(0.02f);

    private static readonly float DELAY_UNTIL_EVENT_REPEATS = 0.5f;
    private static readonly float DELAY_BETWEEN_REPEATING_EVENTS = 0.2f;
    private static readonly float DETACHED_TOOLHEAD_TIME_TO_LIVE = 0.125f;

    /// <summary>
    ///   Distance threshold in metric units for how close hands have to be to 'point' at the palette.
    /// </summary>
    private readonly float PALETTE_DISTANCE_THRESHOLD = 0.33f;
    /// <summary>
    ///   The default material (color) for painting/insertion.
    /// </summary>
    public static readonly int DEFAULT_MATERIAL = MaterialRegistry.WHITE_ID;

    private ControllerMode previousMode;
    public SelectableMenuItem currentSelectableMenuItem;
    public TouchpadOverlay currentOverlay;
    private Overlay overlay;
    private AudioLibrary audioLibrary;
    private WorldSpace worldSpace;
    private MeshRepresentationCache meshRepresentationCache;

    private Dictionary<ControllerMode, GameObject> tooltips;

    private int _currentMaterial = DEFAULT_MATERIAL;

    /// <summary>
    ///   Red color for the active state of the app menu button.
    /// </summary>
    private readonly Color ACTIVE_BUTTON_COLOR = new Color(244f / 255f, 67f / 255f, 54f / 255f, 1f);
    /// <summary>
    ///   Grey color for the inactive state of the app menu button.
    /// </summary>
    private readonly Color INACTIVE_BUTTON_COLOR = new Color(114f / 255f, 115f / 255f, 118f / 255f, 1f);
    /// <summary>
    ///   Almost white color for the waiting state of the app menu button.
    /// </summary>
    private readonly Color WAITING_BUTTON_COLOR = new Color(0.99f, 0.99f, 0.99f, 1f);

    private RaycastHit menuHit;
    private Transform defaultTipPointer;
    private Vector3 defaultTipPointerDefaultLocation;
    public bool isPointingAtMenu = false;
    private GameObject currentHoveredObject;
    private float currentHoveredMenuItemStartTime;
    private Vector3 currentHoveredMenuItemDefaultPos;
    private Vector3 currentHoveredMenuItemDefaultScale;
    private readonly float MENU_HOVER_ANIMATION_SPEED = .004f;
    private readonly float BUTTON_HOVER_ANIMATION_SPEED = 0.150f;

    public static readonly Color MENU_BUTTON_LIGHT = new Color(97f / 255f, 97f / 255f, 97f / 255f);
    public static readonly Color MENU_BUTTON_DARK = new Color(51f / 255f, 51f / 255f, 51f / 255f);
    public static readonly Color MENU_BUTTON_GREEN = new Color(76f / 255f, 175f / 255f, 80f / 255f);
    public static readonly Color MENU_BUTTON_RED = new Color(244f / 255f, 67f / 255f, 54f / 255f);
    private bool menuIsInDefaultState;

    /// <summary>
    ///   Local position of the wand tip / selector when using RIFT.
    /// </summary>
    Vector3 WAND_TIP_POSITION_RIFT = new Vector3(0.0073f, -.0668f, .0022f);
    Vector3 WAND_TIP_ROTATION_OFFSET_RIFT = new Vector3(-45, 0, 0);

    /// <summary>
    ///   Local position of the wand tip / selector when using RIFT on OCULUS.
    /// </summary>
    Vector3 WAND_TIP_POSITION_OCULUS = new Vector3(0, -.001f, -.079f);
    Vector3 WAND_TIP_ROTATION_OFFSET_OCULUS = new Vector3(-82.238f, 7.095f, -1.992f);

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

    public PeltzerController() {
      PeltzerControllerActionHandler += ControllerEventHandler;
    }

    /// <summary>
    ///   What mode the controller is currently in.
    /// </summary>
    public ControllerMode mode { get; set; }

    /// <summary>
    ///   Whether block mode is enabled.
    /// </summary>
    public bool isBlockMode { get; private set; }

    /// <summary>
    ///   Last selected paint mode.  So when we use teh dropper to pick a color, we can come back to this mode.
    /// </summary>
    public ControllerMode lastSelectedPaintMode { get; set; }

    /// <summary>
    ///   Get or set the current material by id.
    /// </summary>
    public int currentMaterial {
      get { return _currentMaterial; }
      set { _currentMaterial = value; if (MaterialChangedHandler != null) { MaterialChangedHandler(currentMaterial); } }
    }

    /// <summary>
    ///   Clients must register themselves on this handler.
    /// </summary>
    public event PeltzerControllerActionHandler PeltzerControllerActionHandler;

    /// <summary>
    ///   Clients that care about material selection should register themselves
    /// </summary>
    public event MaterialChangedHandler MaterialChangedHandler;

    /// <summary>
    ///   Register for mode change events.
    /// </summary>
    public event ModeChangedHandler ModeChangedHandler;

    /// <summary>
    ///   Register for block mode change events.
    /// </summary>
    public event BlockModeChangedHandler BlockModeChangedHandler;

    /// <summary>
    ///   Library to generate haptic feedback.
    /// </summary>
    public HapticFeedback haptics { get; set; }

    /// <summary>
    ///   Represents the time when haptic feedback ceases. Continually spam
    ///   haptic feedback until this time. This is done because SteamVR_Controller
    ///   doesn't seem to respond well to haptic pulses over 1000ms.
    /// </summary>
    private float hapticFeedbackUntilTime = 0f;

    private Vector3 lastPositionModel;
    private Quaternion lastRotationWorld = Quaternion.identity;

    private List<ButtonId> buttons = new List<ButtonId> {
      ButtonId.Trigger,
      ButtonId.Grip,
      ButtonId.Touchpad,
      ButtonId.ApplicationMenu,
      ButtonId.SecondaryButton
    };

    private float? touchpadPressDownTime;
    private bool touchpadRepeating;
    private bool touchpadTouched = false;
    private float lastTouchpadRepeatTime;
    private ControllerEventArgs eventToRepeat;

    public GameObject attachedToolHead;
    private GameObject blockModeButton;
    private GameObject saveMenuBtn;
    private GameObject saveSubMenu;
    private GameObject tutorialSubMenu;
    private GameObject grabToolOnPalette;

    private VolumeInserter volumeInserterInstance;
    private Freeform freeformInstance;

    public Handedness handedness = Handedness.LEFT;

    /// <summary>
    /// The shapes menu is the floating tray that shows the available primitives for insertion.
    /// The menu is anchored to the controller (so it moves around with it). This behavior
    /// (added at runtime) contains the logic that controls how it displays and updates.
    /// </summary>
    public ShapesMenu shapesMenu { get; private set; }

    private bool setupDone;
    private bool triggerIsDown;

    /// <summary>
    /// Performs one-time setup. This must be called before anything else.
    /// </summary>
    public void Setup(VolumeInserter volumeInserter, Freeform freeform) {
      if (Config.Instance.sdkMode == SdkMode.SteamVR) {
#if STEAMVRBUILD
        controller = new ControllerDeviceSteam(transform);
#endif
      } else {
        ControllerDeviceOculus oculusController = new ControllerDeviceOculus(transform);
        oculusController.controllerType = OVRInput.Controller.RTouch;
        controller = oculusController;
      }
      controllerGeometry.baseControllerAnimation.SetControllerDevice(controller);

      audioLibrary = FindObjectOfType<AudioLibrary>();
      haptics = GetComponent<HapticFeedback>();
      lastSelectedPaintMode = ControllerMode.paintMesh;
      worldSpace = PeltzerMain.Instance.worldSpace;
      Transform wandTipXform = gameObject.transform.Find("UI-Tool/TipHead/Sphere");
      wandTip = wandTipXform != null ? wandTipXform.gameObject : gameObject;  //  Fall back to controller obj.

      if (Config.Instance.VrHardware == VrHardware.Rift) {
        // Adjust the placement of the selector position for Rift.
        if (Config.Instance.sdkMode == SdkMode.SteamVR) {
          wandTip.transform.parent.transform.localPosition = WAND_TIP_POSITION_RIFT;
        } else // Oculus SDK
          {
          wandTip.transform.parent.transform.localPosition = WAND_TIP_POSITION_OCULUS;
        }
      }
      wandTipLabel = gameObject.transform.Find("UI-Tool/TipHead/Label/txt").GetComponent<TextMesh>();
      blockModeButton = transform.parent.Find("Controller (Palette)/ID_PanelTools/ToolSide/Actions/Blockmode").gameObject;
      saveMenuBtn = transform.parent.Find("Controller (Palette)/ID_PanelTools/ToolSide/Actions/Save").gameObject;
      saveSubMenu = transform.parent.Find("Controller (Palette)/ID_PanelTools/ToolSide/Menu-Save").gameObject;
      tutorialSubMenu = transform.parent.Find("Controller (Palette)/ID_PanelTools/ToolSide/Menu-Tutorial").gameObject;
      grabToolOnPalette = ObjectFinder.ObjectById("ID_ToolGrab");

      // Grip tooltips.
      controllerGeometry.zoomLeftTooltip.SetActive(false);
      controllerGeometry.zoomRightTooltip.SetActive(false);
      controllerGeometry.moveLeftTooltip.SetActive(false);
      controllerGeometry.moveRightTooltip.SetActive(false);
      controllerGeometry.groupLeftTooltip.SetActive(false);
      controllerGeometry.groupRightTooltip.SetActive(false);
      controllerGeometry.ungroupLeftTooltip.SetActive(false);
      controllerGeometry.ungroupRightTooltip.SetActive(false);

      defaultTipPointer = transform.Find("UI-Tool/TipHead");
      defaultTipPointerDefaultLocation = new Vector3(defaultTipPointer.localPosition.x,
          defaultTipPointer.localPosition.y, defaultTipPointer.localPosition.z);

      touchpadDefaultMaterial = controllerGeometry.touchpad.GetComponent<Renderer>().material;

      volumeInserterInstance = volumeInserter;
      freeformInstance = freeform;

      ResetTouchpadOverlay();
      ShowTooltips();

      shapesMenu = gameObject.AddComponent<ShapesMenu>();
      shapesMenu.Setup(worldSpace, wandTip, _currentMaterial, meshRepresentationCache);

      // Put everything in the default handedness position.
      ControllerHandednessChanged();

      // This is a hack: we need the mode to be different to insertVolume to begin with such that the
      // toolhead-changed logic is triggered and the toolhead is set up appropriately.
      mode = ControllerMode.move;
      ChangeMode(ControllerMode.insertVolume, ObjectFinder.ObjectById("ID_ToolShapes"));

      setupDone = true;
    }

    /// <summary>
    ///   Cache the GameObjects for tooltips to be shown over this controller.
    /// </summary>
    public void CacheTooltips() {
      tooltips = new Dictionary<ControllerMode, GameObject>();
      tooltips.Add(ControllerMode.insertVolume, controllerGeometry.shapeTooltips);
      tooltips.Add(ControllerMode.insertStroke, controllerGeometry.freeformTooltips);
      tooltips.Add(ControllerMode.extrude, controllerGeometry.modifyTooltips);
      tooltips.Add(ControllerMode.reshape, controllerGeometry.modifyTooltips);
      tooltips.Add(ControllerMode.subdivideMesh, controllerGeometry.modifyTooltips);
      tooltips.Add(ControllerMode.subdivideFace, controllerGeometry.modifyTooltips);
      tooltips.Add(ControllerMode.paintFace, controllerGeometry.paintTooltips);
      tooltips.Add(ControllerMode.paintMesh, controllerGeometry.paintTooltips);
      tooltips.Add(ControllerMode.move, controllerGeometry.grabTooltips);
      // Currently no tooltips for delete mode.
    }

    public void SetDefaultMode() {
      // Invoke a change mode to our default tool (volume inserter, also known as "shape tool") so that our
      // animation and menu interaction states, and analytics, are correct and in sync. This seemed to be
      // the least invasive code to support that.
      ChangeMode(ControllerMode.insertVolume, ObjectFinder.ObjectById("ID_ToolShapes"));
    }

    void Update() {
      if (!setupDone) return;

      // First, update the ControllerDevice that can actually tell us the state of the physical controller.
      controller.Update();

      if (!PeltzerMain.Instance.restrictionManager.tooltipsAllowed) {
        HideTooltips();
      }

      // Then find out if the user is pointing at the menu and update accordingly.
      UpdateMenuItemPoint();

      if (controller.IsTrackedObjectValid) {
        // Update some variables.
        haptics.controller = controller;

        lastPositionModel = worldSpace.WorldToModel(wandTip.transform.position);
        if (Config.Instance.VrHardware == VrHardware.Vive) {
          lastRotationWorld = transform.rotation;
        } else {
          if (Config.Instance.sdkMode == SdkMode.SteamVR) {
            wandTip.transform.localRotation = Quaternion.Euler(WAND_TIP_ROTATION_OFFSET_RIFT);
          } else {
            wandTip.transform.localRotation = Quaternion.Euler(WAND_TIP_ROTATION_OFFSET_OCULUS);
          }
          lastRotationWorld = wandTip.transform.rotation;
        }

        // If we have no listeners (which would be very strange) then abort early.
        if (PeltzerControllerActionHandler == null) {
          return;
        }

        // Update the tooltips and process button events.
        if (PeltzerMain.Instance.introChoreographer.introIsComplete) {
          SetGripTooltip();
          ProcessButtonEvents();
        }
      }
    }

    private void UpdateMenuItemPoint() {
      bool isTriggerDown = controller.IsPressed(ButtonId.Trigger);
      bool isOperationInProgress = freeformInstance.IsStroking() ||
        PeltzerMain.Instance.GetMover().IsMoving() ||
        PeltzerMain.Instance.GetReshaper().IsReshaping() ||
        PeltzerMain.Instance.GetExtruder().IsExtrudingFace();

      if (isOperationInProgress) {
        // If an operation is in progress, the hover state needs to be reset, otherwise it might get stuck
        // if the user starts an operation while a hover state is active (bug).
        // It's cheap to call these methods every frame because they exit early if there is no work to be done.
        ResetUnhoveredItem();
        ResetMenu();
        return;
      }

      // Only update the hover state if the trigger is NOT down (if the trigger is down, don't change states because
      // the user is currently in the process of trying to click something that's already hovered).
      if (!isTriggerDown) {
        HandleMenuItemPoint();
      }
  }

    // Process user interaction.
    private void ProcessButtonEvents() {
      TouchpadLocation location = controller.GetTouchpadLocation();

      foreach (ButtonId buttonId in buttons) {
        if (buttonId == ButtonId.Trigger && currentSelectableMenuItem != null) {
          // If the user has pulled the trigger while pointing at a palette menu item, invoke the action handler.
          if (controller.WasJustPressed(buttonId)) {
            currentSelectableMenuItem.ApplyMenuOptions(PeltzerMain.Instance);
            TriggerHapticFeedback();
            // Swallow this event, so the other tools don't fire.
            continue;
          }
        } else if (buttonId == ButtonId.Trigger) {
          // Else, send out regular trigger events.
          DetectTrigger(controller);
        }

        // If the user has pressed any button other than the trigger, send out the event.
        if (controller.WasJustPressed(buttonId) && buttonId != ButtonId.Trigger) {
          if (buttonId == ButtonId.Touchpad) {

          }
          ControllerEventArgs eventArgs = new ControllerEventArgs(ControllerType.PELTZER, buttonId,
              ButtonAction.DOWN, buttonId == ButtonId.Touchpad ?
              location : TouchpadLocation.NONE, currentOverlay);

          PeltzerControllerActionHandler(this, eventArgs);

          // If this was a touchpad press-down, then queue up this event to repeat in the case that the touchpad
          // is not released.
          if (buttonId == ButtonId.Touchpad && location != TouchpadLocation.CENTER) {
            touchpadPressDownTime = Time.time;
            eventToRepeat = eventArgs;
          }
        }

        // If the user has released any button other than the trigger, send out the event.
        if (controller.WasJustReleased(buttonId) && buttonId != ButtonId.Trigger) {
          PeltzerControllerActionHandler(this, new ControllerEventArgs(ControllerType.PELTZER, buttonId,
              ButtonAction.UP, buttonId == ButtonId.Touchpad ?
              location : TouchpadLocation.NONE, currentOverlay));

          // If this was a touchpad release, then stop repeating the 'touchpad press-down' event.
          if (buttonId == ButtonId.Touchpad) {
            touchpadPressDownTime = null;
            touchpadRepeating = false;
          }
        }

        if (buttonId == ButtonId.Touchpad) {
          if (controller.IsTouched(buttonId)) {
            touchpadTouched = true;
            // If the touchpad is currently touched, and the touch isn't at 0,0 (which normally indicates
            // spurious input), send the touch event. This can result in an event sent every frame when
            // a user keeps their thumb on the touchpad.
            PeltzerControllerActionHandler(this, new ControllerEventArgs(ControllerType.PELTZER,
              ButtonId.Touchpad, ButtonAction.TOUCHPAD, location, currentOverlay));
          } else if (touchpadTouched) {
            // If the user stops touching the touchpad, sent out a 'cancel' event.
            PeltzerControllerActionHandler(this, new ControllerEventArgs(ControllerType.PELTZER, buttonId,
              ButtonAction.NONE, TouchpadLocation.NONE, currentOverlay));
            touchpadTouched = false;
          }

          // If the touchpad is held down, and we are past the delay for repeating this event,
          // send out the event again.
          if (touchpadPressDownTime.HasValue
              && !touchpadRepeating && (Time.time - touchpadPressDownTime.Value) > DELAY_UNTIL_EVENT_REPEATS) {
            touchpadRepeating = true;
            PeltzerControllerActionHandler(this, eventToRepeat);
            lastTouchpadRepeatTime = Time.time;
          } else if (touchpadRepeating && (Time.time - lastTouchpadRepeatTime) > DELAY_BETWEEN_REPEATING_EVENTS) {
            PeltzerControllerActionHandler(this, eventToRepeat);
            lastTouchpadRepeatTime = Time.time;
          }
        }
      }
    }

    /// <summary>
    ///   Make the app menu button red or grey depending on whether or not its active.
    /// </summary>
    /// <param name="active"></param>
    public void SetApplicationButtonOverlay(ButtonMode buttonMode) {
      if (controllerGeometry.appMenuButton == null) return;

      Material material = controllerGeometry.appMenuButton.GetComponent<Renderer>().material;
      Color highlightEmmissionColor;
      Color highlightColor;
      if (buttonMode == ButtonMode.ACTIVE) {
        highlightEmmissionColor = ACTIVE_BUTTON_COLOR * Mathf.LinearToGammaSpace(0.6f); // Set emission to 60%.
        highlightColor = ACTIVE_BUTTON_COLOR;
      } else if (buttonMode == ButtonMode.WAITING) {
        highlightEmmissionColor = WAITING_BUTTON_COLOR * Mathf.LinearToGammaSpace(0f);
        highlightColor = WAITING_BUTTON_COLOR;
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
    ///   Casts ray to detect user pointing at the palette menu with this GameObject. (Not to be confused
    ///   with item selection.) If so, the cursor is bound to the surface of the hit and associated menu interaction
    ///   UX is coordinated such as haptic feedback, and view alterations such as
    ///   showing or hiding aspects of the controllers.
    /// </summary>
    private void HandleMenuItemPoint() {
      // Cast a ray from the controller and see if it hits the menu.
      Vector3 controllerRayVector;
      Vector3 controllerRayOrigin;
      if (Config.Instance.VrHardware == VrHardware.Rift) {
        if (Config.Instance.sdkMode == SdkMode.SteamVR) {
          controllerRayVector = Quaternion.Euler(45, 0, 0) * Vector3.forward;
          controllerRayOrigin = transform.position + new Vector3(0, -0.045f, 0);
        } else {
          controllerRayVector = Vector3.forward;
          controllerRayOrigin = transform.position;// + new Vector3(0, -0.045f, 0);
        }
      } else {
        controllerRayVector = Vector3.forward;
        controllerRayOrigin = transform.position;
      }
      if (!Physics.Raycast(controllerRayOrigin, transform.TransformDirection(controllerRayVector),
        out menuHit, PALETTE_DISTANCE_THRESHOLD)) {
        // We didn't hit the menu with the ray, reset any existing state and exit early.
        if (mode == ControllerMode.reshape) {
          PeltzerMain.Instance.GetSelector().TurnOnSelectIndicator();
        }
        ResetUnhoveredItem();
        ResetMenu();
        return;
      }

      // Since we are pointing at the menu, be sure to hide the selector so we don't have any overlap.
      PeltzerMain.Instance.GetSelector().TurnOffSelectIndicator();
      // If a button is inactive or we're hovering over empty palette space, we want to show the
      // pointer and take no further action.
      SelectableMenuItem selectableMenuItem = menuHit.transform.GetComponent<SelectableMenuItem>();
      bool hitIsActive = true;
      if (selectableMenuItem != null) {
        hitIsActive = selectableMenuItem.isActive;
      }

      // Check for the various types of things we could be hovering.
      ChangeMaterialMenuItem changeMaterialMenuItem = menuHit.transform.GetComponent<ChangeMaterialMenuItem>();
      bool hoveringColor = changeMaterialMenuItem != null;
      ChangeModeMenuItem changeModeMenuItem = menuHit.transform.GetComponent<ChangeModeMenuItem>();
      bool hoveringToolhead = changeModeMenuItem != null;
      MenuActionItem menuActionItem = menuHit.transform.GetComponent<MenuActionItem>();
      bool hoveringButton = menuActionItem != null;
      SelectZandriaCreationMenuItem zandriaCreationMenuItem = menuHit.transform.GetComponent<SelectZandriaCreationMenuItem>();
      bool hoveringZandriaCreation = zandriaCreationMenuItem != null;
      PolyMenuButton polyMenuButton = menuHit.transform.GetComponent<PolyMenuButton>();
      bool hoveringPolyMenuOption = polyMenuButton != null;
      SelectableDetailsMenuItem selectableDetailsMenuItem = menuHit.transform.GetComponent<SelectableDetailsMenuItem>();
      bool hoveringZandriaDetails = selectableDetailsMenuItem != null;
      if (hoveringZandriaDetails) {
        hitIsActive &= selectableDetailsMenuItem.creation != null
        && selectableDetailsMenuItem.creation.entry != null
        && selectableDetailsMenuItem.creation.entry.loadStatus != ZandriaCreationsManager.LoadStatus.FAILED;
      }
      EmptyMenuItem emptyMenuItem = menuHit.transform.GetComponent<EmptyMenuItem>();
      bool isHoveringEmptyPaletteSpace = emptyMenuItem != null;
      EnvironmentMenuItem environmentMenuItem = menuHit.transform.GetComponent<EnvironmentMenuItem>();
      bool isHoveringEnvironmentMenuItem = environmentMenuItem != null;

      // Reset hovered items if we are not hovering anything.
      if (!(hoveringColor || hoveringToolhead || hoveringButton || hoveringZandriaCreation || hoveringPolyMenuOption
        || hoveringZandriaDetails || isHoveringEmptyPaletteSpace || isHoveringEnvironmentMenuItem)) {
        ResetUnhoveredItem();
        ResetMenu();
        return;
      }

      // At this point, we know something on the menu has been hit, so we update variables accordingly and 
      // hide the shapes menu.
      menuIsInDefaultState = false;
      isPointingAtMenu = true;
      shapesMenu.Hide();

      // We set the position of the pointer dot to be just in front of the item being pointed at.
      defaultTipPointer.transform.gameObject.SetActive(true);
      if (hoveringToolhead) {
        defaultTipPointer.position = menuHit.point + menuHit.transform.up * .015f;
      } else {
        defaultTipPointer.position = menuHit.point;
      }

      if (!hitIsActive || isHoveringEmptyPaletteSpace) {
        ResetUnhoveredItem();
        currentHoveredObject = null;
        currentSelectableMenuItem = null;
        wandTipLabel.text = "";
        ResetTutorialMenu();
        return;
      }

      // We set a label on the pointer dot to show what is being pointed at.
      if (selectableMenuItem != null && hitIsActive) {
        wandTipLabel.text = selectableMenuItem.hoverName;
        wandTipLabel.transform.parent.transform.LookAt(PeltzerMain.Instance.hmd.transform);
      } else {
        wandTipLabel.text = "";
      }

      // Reset the hover animation and start time if this is a new item or not a save submenu.
      if ((currentHoveredObject != menuHit.transform.gameObject)
          && (!menuHit.transform.name.Equals("Save-Copy")
          && !menuHit.transform.name.Equals("Save-Confirm")
          && !menuHit.transform.name.Equals("Save-Selected")
          && !menuHit.transform.name.Equals("Publish")
          && !menuHit.transform.name.Equals("Intro")
          && !menuHit.transform.name.Equals("SelectingMoving")
          && !menuHit.transform.name.Equals("ModifyingModels")
          && !menuHit.transform.name.Equals("SnappingAlignment"))) {
        currentHoveredMenuItemStartTime = Time.time;
        currentHoveredMenuItemDefaultPos = menuHit.transform.localPosition;
        currentHoveredMenuItemDefaultScale = menuHit.transform.localScale;
        ResetSaveMenu();
      }

      // Begin, or continue any animations.
      if (hoveringButton) {
        float delta = (Time.time - currentHoveredMenuItemStartTime);
        float pct = (delta / BUTTON_HOVER_ANIMATION_SPEED);
        float targetYPos;
        float targetYScale;
        if (pct > 1f) {
          pct = 1f;
          // Show sub-menu if available at the end of the animation.
          if (menuHit.transform.name.Equals("Save")) {
            if (PeltzerMain.Instance.model.GetNumberOfMeshes() > 0) {
              saveSubMenu.SetActive(true);
            }
          } else if (menuHit.transform.name.Equals("Tutorial")) {
            tutorialSubMenu.SetActive(true);
          }
        }
        if (!menuHit.transform.name.Equals("Save-Copy")
            && !menuHit.transform.name.Equals("Save-Confirm")
            && !menuHit.transform.name.Equals("Save-Selected")
            && !menuHit.transform.name.Equals("Publish")) {
          targetYPos = pct * 0.00250f;
          targetYScale = pct * (0.01f - currentHoveredMenuItemDefaultScale.y);

          menuHit.transform.localPosition = new Vector3(menuHit.transform.localPosition.x,
            targetYPos,
            menuHit.transform.localPosition.z);

          menuHit.transform.localScale = new Vector3(menuHit.transform.localScale.x,
            currentHoveredMenuItemDefaultScale.y + targetYScale,
            menuHit.transform.localScale.z);
        }
      } else if (hoveringToolhead) {
        // Record the starting collider position so we can move it back to its original position after the toolhead
        // is moved.
        Vector3 globalColliderStartPosition =
          menuHit.transform.TransformPoint(menuHit.transform.GetComponent<BoxCollider>().center);

        // Hovering over a tool head.
        menuHit.transform.localPosition = new Vector3(menuHit.transform.localPosition.x,
          (menuHit.transform.localPosition.y + MENU_HOVER_ANIMATION_SPEED) < .02f ?
          (menuHit.transform.localPosition.y + .002f) : .02f,
          menuHit.transform.localPosition.z);

        // Restore the position of the collider.
        menuHit.transform.GetComponent<BoxCollider>().center =
          menuHit.transform.InverseTransformPoint(globalColliderStartPosition);
      } else if (hoveringZandriaDetails) {
        // Hovering over a saved or featured Poly models menu.
        GameObject creationPreview = menuHit.transform.Find("CreationPreview").gameObject;

        creationPreview.transform.localPosition = new Vector3(creationPreview.transform.localPosition.x,
          creationPreview.transform.localPosition.y,
          (creationPreview.transform.localPosition.z) > -.2f ?
          (creationPreview.transform.localPosition.z - .02f) : -.2f);
      } else if (isHoveringEnvironmentMenuItem) {
        menuHit.transform.localPosition = new Vector3(menuHit.transform.localPosition.x,
          (menuHit.transform.localPosition.y + MENU_HOVER_ANIMATION_SPEED) < 0f ?
          (menuHit.transform.localPosition.y + .003f) : 0f,
          menuHit.transform.localPosition.z);
      } else if (!hoveringColor && !hoveringPolyMenuOption) {
        // Catch-all for other possible objects to hover.
        menuHit.transform.localPosition = new Vector3(menuHit.transform.localPosition.x,
          (menuHit.transform.localPosition.y + MENU_HOVER_ANIMATION_SPEED) < .02f ?
          (menuHit.transform.localPosition.y + .002f) : .02f,
          menuHit.transform.localPosition.z);
      }

      // If this hover is the same as last frame, nothing new to do.
      if (currentHoveredObject == menuHit.transform.gameObject) {
        return;
      }

      // Reset last hovered item if needed, special-casing the 'save' sub-menu.
      if (currentHoveredObject != null && !currentHoveredObject.name.Equals("Save")) {
        ResetUnhoveredItem();
      } else if (currentHoveredObject != null && currentHoveredObject.name.Equals("Save")
          && !(menuHit.transform.name.Equals("Save-Copy")
            || menuHit.transform.name.Equals("Save-Selected")
            || menuHit.transform.name.Equals("Save-Confirm")
            || menuHit.transform.name.Equals("Publish"))) {
        ResetUnhoveredItem();
      }

      // Trigger haptic feedback.
      TriggerHapticFeedback(
        HapticFeedback.HapticFeedbackType.FEEDBACK_1, /* durationSeconds */ 0.01f, /* strength */ 0.15f);

      // Animate Color swatches
      if (hoveringColor) {
        changeMaterialMenuItem.SetHovered(true);
      }

      // Animate Poly Menu buttons
      if (hoveringPolyMenuOption) {
        polyMenuButton.SetHovered(true);
      }

      // Set the current menu item so that we can use this value on Trigger pull, i.e. mode selection.
      currentSelectableMenuItem = menuHit.transform.GetComponent<SelectableMenuItem>();
      currentHoveredObject = menuHit.transform.gameObject;

      // Set the material / color for a hovered button.
      menuActionItem = currentHoveredObject.GetComponent<MenuActionItem>();
      if (menuActionItem != null) {
        if (IsHoveredButtonThatShouldChangeColour(menuActionItem)) {
          menuHit.transform.gameObject.GetComponent<Renderer>().material.color = MENU_BUTTON_LIGHT;
        }
      }
    }

    /// <summary>
    ///   Hide and reset any floating save UI and associated state.
    /// </summary>
    private void ResetSaveMenu() {
      saveSubMenu.SetActive(false);
      saveMenuBtn.GetComponent<Renderer>().material.color = MENU_BUTTON_DARK;
      saveMenuBtn.transform.localScale = new Vector3(saveMenuBtn.transform.localScale.x,
        0.005f, saveMenuBtn.transform.localScale.z);
      saveMenuBtn.transform.localPosition = new Vector3(saveMenuBtn.transform.localPosition.x,
      0f, saveMenuBtn.transform.localPosition.z);
    }

    /// <summary>
    ///   Hide and reset any floating save UI and associated state.
    /// </summary>
    private void ResetTutorialMenu() {
      tutorialSubMenu.SetActive(false);
    }

    /// <summary>
    ///   If nothing on the menu is being pointed at, reset the whole menu state.
    /// </summary>
    public void ResetMenu() {
      if (menuIsInDefaultState) {
        return;
      }

      // Hide and reset any floating save UI and associated state.
      ResetSaveMenu();
      ResetTutorialMenu();

      // Set the "cursor/pointer" back to the default location.
      defaultTipPointer.transform.gameObject.SetActive(false);
      defaultTipPointer.localPosition = defaultTipPointerDefaultLocation;
      lastPositionModel = worldSpace.WorldToModel(wandTip.transform.position);
      isPointingAtMenu = false;

      // Reset currentMenuItem and hover
      currentSelectableMenuItem = null;
      currentHoveredObject = null;

      if (attachedToolHead != null) {
        attachedToolHead.SetActive(true);
      }
      menuIsInDefaultState = true;
    }

    /// <summary>
    ///   True if the given item is a button that should change color when hovered.
    /// </summary>
    private bool IsHoveredButtonThatShouldChangeColour(MenuActionItem menuActionItem) {
        return menuActionItem.action == MenuAction.CLEAR
        || menuActionItem.action == MenuAction.SAVE
        || menuActionItem.action == MenuAction.CANCEL_SAVE
        || menuActionItem.action == MenuAction.NOTHING
        || menuActionItem.action == MenuAction.SHOW_SAVE_CONFIRM
        || menuActionItem.action == MenuAction.SAVE_COPY
        || menuActionItem.action == MenuAction.SAVE_SELECTED
        || menuActionItem.action == MenuAction.PUBLISH
        || menuActionItem.action == MenuAction.NEW_WITH_SAVE
        || (menuActionItem.action == MenuAction.TUTORIAL_PROMPT && !(
          PeltzerMain.Instance.paletteController.tutorialBeginPrompt.activeInHierarchy ||
          PeltzerMain.Instance.paletteController.tutorialSavePrompt.activeInHierarchy ||
          PeltzerMain.Instance.paletteController.tutorialExitPrompt.activeInHierarchy))
        || (menuActionItem.action == MenuAction.BLOCKMODE && !isBlockMode);
    }

    /// <summary>
    ///   Resets an unhovered item.
    /// </summary>
    public void ResetUnhoveredItem() {
      if (currentHoveredObject == null) {
        return;
      }

      // Some menu buttons turn light gray when hovered, here we reset them.
      MenuActionItem menuActionItem = currentHoveredObject.GetComponent<MenuActionItem>();
      if (menuActionItem != null) {
        if (IsHoveredButtonThatShouldChangeColour(menuActionItem)) {
          currentHoveredObject.gameObject.GetComponent<Renderer>().material.color = MENU_BUTTON_DARK;
        }
      }

      ChangeMaterialMenuItem changeMaterialMenuItem = currentHoveredObject.GetComponent<ChangeMaterialMenuItem>();
      PolyMenuButton polyMenuButton = currentHoveredObject.GetComponent<PolyMenuButton>();
      if (changeMaterialMenuItem != null) {
        // If we were hovering a colour swatch, return it to its unhovered state.
        changeMaterialMenuItem.SetHovered(false);
      } else if (polyMenuButton != null) {
        // If we were hovering a button on the PolyMenu, return it to its unhovered state.
        polyMenuButton.SetHovered(false);
      } else if (menuActionItem != null) {
        // If we were hovering a button in the save sub-menu, reset its hovered state.
        if (menuActionItem.action != MenuAction.SAVE_COPY
            && menuActionItem.action != MenuAction.SAVE_SELECTED
            && menuActionItem.action != MenuAction.SHOW_SAVE_CONFIRM
            && menuActionItem.action != MenuAction.PUBLISH) {
          currentHoveredObject.transform.localScale = new Vector3(currentHoveredObject.transform.localScale.x,
          0.005f, currentHoveredObject.transform.localScale.z);
          currentHoveredObject.transform.localPosition = new Vector3(currentHoveredObject.transform.localPosition.x,
          currentHoveredMenuItemDefaultPos.y, currentHoveredObject.transform.localPosition.z);
        }
      } else if (currentHoveredObject.GetComponent<ChangeModeMenuItem>() != null) {
        // If we were hovering a toolhead, reset its hovered state.
        // We need to change the position of the tool heads without moving the colliders position in global space.
        Vector3 globalColliderStartPosition = currentHoveredObject.transform.TransformPoint(
          currentHoveredObject.transform.GetComponent<BoxCollider>().center);

        // Move the toolhead back to it's original position.
        currentHoveredObject.transform.localPosition = new Vector3(currentHoveredObject.transform.localPosition.x,
          0f, currentHoveredObject.transform.localPosition.z);

        // Restore the position of the collider before we moved the toolhead.
        currentHoveredObject.transform.GetComponent<BoxCollider>().center =
          currentHoveredObject.transform.InverseTransformPoint(globalColliderStartPosition);
      } else if (currentHoveredObject.GetComponent<EnvironmentMenuItem>() != null) {
        currentHoveredObject.transform.localPosition = new Vector3(currentHoveredObject.transform.localPosition.x,
          -0.02f, currentHoveredObject.transform.localPosition.z);
      } else {
        // If none of the above special-cases were hit, just reset the default position of whatever else was hovered.
        currentHoveredObject.transform.localPosition = new Vector3(currentHoveredObject.transform.localPosition.x,
          0f, currentHoveredObject.transform.localPosition.z);

        // If a poly menu item was being hovered, reset it.
        Transform creationPreview = currentHoveredObject.transform.Find("CreationPreview");
        if (creationPreview != null) {
          creationPreview.gameObject.transform.localPosition = new Vector3(0f, 0f, 0f);
        }
      }
    }

    /// <summary>
    ///   Detect and distribute events for an abstracted Trigger input.
    /// </summary>
    /// <param name="controller">Instanec of SteamVR_Controller.Device</param>
    private void DetectTrigger(ControllerDevice controller) {
      // Note: for better precision, we detect trigger up/down based on a threshold rather than use the built-in
      // SteamVR down/up detection for the trigger button.

      if (controller.WasJustPressed(ButtonId.Trigger)) {
        PeltzerControllerActionHandler(this, new ControllerEventArgs(ControllerType.PELTZER,
        ButtonId.Trigger,
        ButtonAction.DOWN,
        controller.GetTouchpadLocation(),
        currentOverlay));
      } else if (controller.WasJustReleased(ButtonId.Trigger)) {
        PeltzerControllerActionHandler(this, new ControllerEventArgs(ControllerType.PELTZER,
          ButtonId.Trigger,
          ButtonAction.UP,
          controller.GetTouchpadLocation(),
          currentOverlay));
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

      float lastTime;
      if (lastHapticFeedbackTime.TryGetValue(type, out lastTime) &&
          Time.time - lastTime < MIN_HAPTIC_FEEDBACK_INTERVAL) {
        // We triggered haptic feedback of this type too recently. Throttle.
        return;
      }
      lastHapticFeedbackTime[type] = Time.time;

      if (haptics != null) {
        haptics.PlayHapticFeedback(type, durationSeconds, strength);
      }
    }

    /// <summary>
    ///   Triggers controller vibrations to get the user to look at the controller.
    /// </summary>
    public void LookAtMe() {
      TriggerHapticFeedback(
          HapticFeedback.HapticFeedbackType.FEEDBACK_1,
          0.2f,
          0.2f
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

    // Handle mode selection when appropriate.
    private void ControllerEventHandler(object sender, ControllerEventArgs args) {
      if (args.ControllerType == ControllerType.PELTZER && args.ButtonId == ButtonId.Touchpad
        && args.Action == ButtonAction.DOWN) {
        TouchpadLocation location = args.TouchpadLocation;

        // Don't allow mode switching while zooming.
        if (PeltzerMain.Instance.Zoomer.Zooming) {
          return;
        }

        if (IsModifyMode(mode)) {
          // Don't allow switching modes whilst in the middle of an operation.
          if ((mode == ControllerMode.extrude && PeltzerMain.Instance.GetExtruder().IsExtrudingFace()) ||
            mode == ControllerMode.reshape && PeltzerMain.Instance.GetReshaper().IsReshaping() ||
            mode == ControllerMode.reshape && PeltzerMain.Instance.GetSubdivider().IsSubdividing()) {
            return;
          }

          if (location == TouchpadLocation.TOP && mode != ControllerMode.reshape) {
            ChangeMode(ControllerMode.reshape);
          } else if (location == TouchpadLocation.LEFT && mode != ControllerMode.subdivideFace) {
            ChangeMode(ControllerMode.subdivideFace);
          } else if (location == TouchpadLocation.RIGHT && mode != ControllerMode.extrude) {
            ChangeMode(ControllerMode.extrude);
          }
        } else if (IsPaintMode(mode)) {
          if (location == TouchpadLocation.LEFT && mode != ControllerMode.paintMesh) {
            ChangeMode(ControllerMode.paintMesh);
            lastSelectedPaintMode = ControllerMode.paintMesh;
          } else if (location == TouchpadLocation.RIGHT && mode != ControllerMode.paintFace) {
            ChangeMode(ControllerMode.paintFace);
            lastSelectedPaintMode = ControllerMode.paintFace;
          }
        } else if (IsDeleteMode(mode)) {
          if (!Features.enablePartDeletion) {
            ChangeMode(ControllerMode.delete);
          }
          else {
            if (location == TouchpadLocation.RIGHT && mode != ControllerMode.deletePart) {
              ChangeMode(ControllerMode.deletePart);
            }
            else if (location == TouchpadLocation.LEFT && mode != ControllerMode.delete) {
              ChangeMode(ControllerMode.delete);
            }
          }
        }
      }
    }

    // Stop showing the overlay on the trackpad during volume 'fill' insertion.
    public void SetVolumeOverlayActive(bool active) {
      controllerGeometry.volumeInserterOverlay.gameObject.SetActive(active);
      controllerGeometry.shapeTooltips.SetActive(active);
    }

    private bool IsModifyMode(ControllerMode mode) {
      return mode == ControllerMode.reshape
        || mode == ControllerMode.extrude
        || mode == ControllerMode.subdivideFace
        || mode == ControllerMode.subdivideMesh;
    }

    private bool IsDeleteMode(ControllerMode mode) {
      return mode == ControllerMode.delete
        || mode == ControllerMode.deletePart;
    }

    private bool IsPaintMode(ControllerMode mode) {
      return mode == ControllerMode.paintFace
        || mode == ControllerMode.paintMesh;
    }

    /// <summary>
    ///   Toggles block mode by enabling or disabling the grid and sending out a BlockModeChanged event to all
    ///   listening tools.
    /// </summary>
    public void ToggleBlockMode(bool initiatedByUser) {
      isBlockMode = !isBlockMode;

      if (isBlockMode) {
        // Set status button to green for active.
        blockModeButton.GetComponent<Renderer>().material.color = MENU_BUTTON_GREEN;
      } else {
        // Set status button to dark default for inactive.
        blockModeButton.GetComponent<Renderer>().material.color = MENU_BUTTON_DARK;
      }

      if (BlockModeChangedHandler != null)
        BlockModeChangedHandler(isBlockMode);

    }

    public Vector3 LastPositionModel { get { return lastPositionModel; } }
    public Quaternion LastRotationWorld { get { return lastRotationWorld; } }
    public Quaternion LastRotationModel { get { return worldSpace.WorldOrientationToModel(lastRotationWorld); } }

    /// <summary>
    ///   Handles mode change requests.
    /// </summary>
    /// <param name="newMode">The mode we are enabling</param>
    /// <param name="toolHead">An optional reference to a gameobject representing a tool head for animation.</param>
    public void ChangeMode(ControllerMode newMode, GameObject toolHead = null) {
      if (!PeltzerMain.Instance.restrictionManager.IsControllerModeAllowed(newMode)) {
        return;
      }

      ControllerMode previousMode = this.mode;
      this.mode = newMode;

      if (toolHead != null && toolHead != attachedToolHead) {
        // We changed to a new tool, hide the necessary tooltips.
        PeltzerMain.Instance.paletteController.HideSnapAssistanceTooltips();
      }

      ShowTooltips();
      ResetTouchpadOverlay();


      // Set the application button and secondary button to be inactive by default. If a tool wants it active it will
      // set it itself.
      SetApplicationButtonOverlay(ButtonMode.INACTIVE);
      SetSecondaryButtonOverlay(/*active*/ false);

      if (ModeChangedHandler != null) {
        ModeChangedHandler(previousMode, newMode);
      }

      // Animate the tool mode change
      if (previousMode != newMode) {
        if (toolHead != null) {
          if (attachedToolHead != null) {
            // Deactivate any attached animations to instance of attachedToolHead.
            switch (previousMode) {
              case ControllerMode.extrude:
              case ControllerMode.reshape:
              case ControllerMode.subdivideFace:
              case ControllerMode.subdivideMesh:
                attachedToolHead.GetComponent<ModifyToolheadAnimation>().Deactivate();
                switch (newMode) {
                  case ControllerMode.reshape:
                    PeltzerMain.Instance.GetSelector().UpdateInactive(Selector.FACES_EDGES_AND_VERTICES);
                    break;
                  case ControllerMode.subdivideFace:
                  case ControllerMode.subdivideMesh:
                    PeltzerMain.Instance.GetSelector().UpdateInactive(Selector.EDGES_ONLY);
                    break;
                  default:
                    PeltzerMain.Instance.GetSelector().ResetInactive();
                    break;
                }
                break;
              case ControllerMode.move:
                attachedToolHead.GetComponent<GrabToolheadAnimation>().Deactivate();
                break;
              case ControllerMode.paintFace:
              case ControllerMode.paintMesh:
                attachedToolHead.GetComponent<PaintToolheadAnimation>().Deactivate();
                break;
              case ControllerMode.deletePart:
                PeltzerMain.Instance.GetSelector().ResetInactive();
                break;
            }

            // Detach the current toolhead.
            attachedToolHead.AddComponent<Rigidbody>();
            BoxCollider bc = attachedToolHead.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.05f, 0.035f, 0.1f);
            bc.center = new Vector3(0f, 0f, 0.02f);
            attachedToolHead.transform.parent = null;
            Destroy(attachedToolHead, DETACHED_TOOLHEAD_TIME_TO_LIVE);
          }

          // Create the new tool head.
          attachedToolHead = (GameObject)Instantiate(toolHead, gameObject.transform.localPosition, Quaternion.identity);
          attachedToolHead.SetActive(true);
          DestroyImmediate(attachedToolHead.GetComponent<BoxCollider>());

          attachedToolHead.transform.parent = gameObject.transform;
          attachedToolHead.transform.localScale = toolHead.transform.lossyScale;
          attachedToolHead.transform.position = toolHead.transform.position;
          attachedToolHead.transform.localEulerAngles = new Vector3(-6f, toolHead.transform.localEulerAngles.y,
              toolHead.transform.localEulerAngles.z);

          // Hide the 'mock shape' attached to the toolhead, if any.
          Transform attachedMockShape = attachedToolHead.transform.Find("mockShape");
          if (attachedMockShape != null) {
            attachedMockShape.gameObject.SetActive(false);
          }

          // Reshow any removed tool and hide the toolhead on the palette.
          GameObject removed = PeltzerMain.Instance.paletteController.GetToolheadForMode(previousMode);
          // Show the 'mock shape' attached to the toolhead, if any.
          Transform paletteMockShape = removed.transform.Find("mockShape");
          if (paletteMockShape != null) {
            paletteMockShape.gameObject.SetActive(true);
          }
          removed.SetActive(true);

          if(previousMode == ControllerMode.move && handedness == Handedness.LEFT) {
            removed.GetComponent<ChangeModeMenuItem>().ScaleFromNothing(new Vector3(-1f, 1f, 1f));
          } else {
            removed.GetComponent<ChangeModeMenuItem>().ScaleFromNothing(Vector3.one);
          }

          GameObject added = PeltzerMain.Instance.paletteController.GetToolheadForMode(newMode);
          GetComponentInChildren<BaseControllerAnimation>().UpdateTouchpadOverlay(toolHead);
          added.SetActive(false);
          StartToolHeadAnimation();
        }
      }

      // modify the registration point based on the tool.
      switch (newMode) {
        case ControllerMode.reshape:
          defaultTipPointerDefaultLocation = new Vector3(0f, 0f, -0.055f);
          break;
        case ControllerMode.insertStroke:
        case ControllerMode.insertVolume:
          if (Config.Instance.VrHardware == VrHardware.Vive) {
            defaultTipPointerDefaultLocation = new Vector3(0f, 0f, -0.015f);
          } else {
            defaultTipPointerDefaultLocation = new Vector3(0f, 0, -0.045f);
          }
          break;
        case ControllerMode.delete:
        case ControllerMode.deletePart:
          defaultTipPointerDefaultLocation = new Vector3(0f, 0f, -0.035f);
          break;
        default:
          defaultTipPointerDefaultLocation = new Vector3(0f, 0f, -0.045f);
          break;
      }

      if (Config.Instance.VrHardware == VrHardware.Rift) {
        if (Config.Instance.sdkMode == SdkMode.SteamVR) {
          defaultTipPointerDefaultLocation = defaultTipPointerDefaultLocation + WAND_TIP_POSITION_RIFT - new Vector3(0f, 0f, -0.045f);
        } else {
          defaultTipPointerDefaultLocation = defaultTipPointerDefaultLocation + WAND_TIP_POSITION_OCULUS - new Vector3(0f, 0f, -0.045f);
        }

      }

    }

    /// <summary>
    ///   Animates a toolhead from the palette menu onto the controller.
    /// </summary>
    /// <returns></returns>
    private void StartToolHeadAnimation() {
      BringAttachedToolheadToController();
      ChangeToolColor();
      TriggerHapticFeedback(
        HapticFeedback.HapticFeedbackType.FEEDBACK_4, /* durationSeconds */ 0.2f, /* strength */ 0.4f);
    }

    public void BringAttachedToolheadToController() {
      attachedToolHead.GetComponent<ChangeModeMenuItem>().BringToController();
    }

    /// <summary>
    ///   Hide images on the trackpad that show the various modify modes.
    /// </summary>
    public void HideModifyOverlays() {
      controllerGeometry.modifyOverlay.SetActive(false);
    }

    /// <summary>
    ///   Show images on the trackpad that show the various modify modes.
    /// </summary>
    public void ShowModifyOverlays() {
      ChangeTouchpadOverlay(currentOverlay);
    }

    /// <summary>
    ///   Hide textual overlay tooltips.
    /// </summary>
    public void HideTooltips() {
      controllerGeometry.shapeTooltips.SetActive(false);
      controllerGeometry.freeformTooltips.SetActive(false);
      controllerGeometry.modifyTooltips.SetActive(false);
      controllerGeometry.paintTooltips.SetActive(false);
      controllerGeometry.moverTooltips.SetActive(false);
      controllerGeometry.grabTooltips.SetActive(false);
      controllerGeometry.groupTooltipRoot.SetActive(false);
    }

    /// <summary>
    ///   Show textual overlay tooltips appropriate to the current tool.
    /// </summary>
    public void ShowTooltips() {
      if (!PeltzerMain.Instance.restrictionManager.tooltipsAllowed || PeltzerMain.Instance.HasDisabledTooltips) return;

      controllerGeometry.shapeTooltips.SetActive(true);
      controllerGeometry.freeformTooltips.SetActive(true);
      controllerGeometry.modifyTooltips.SetActive(true);
      controllerGeometry.paintTooltips.SetActive(true);
      controllerGeometry.moverTooltips.SetActive(true);
      controllerGeometry.grabTooltips.SetActive(true);
      controllerGeometry.groupTooltipRoot.SetActive(true);
    }

    public void ChangeToolColor() {
      if (PaletteController.AcquireIfNecessary(ref PeltzerMain.Instance.paletteController)) {
        PeltzerMain.Instance.paletteController.UpdateColors(currentMaterial);
      }

      // Change the preview shapes for Volume Inserter. We have to generate new GameObjects because the old
      // ones are being drawn with Graphics.DrawMesh which won't respond to changes in material.
      shapesMenu.ChangeShapesMenuMaterial(currentMaterial);

      if (attachedToolHead == null) {
        return;
      }

      // Change the attached tool.
      ColorChanger currentTool = attachedToolHead.GetComponentInChildren<ColorChanger>();

      if (currentTool != null) {
        currentTool.ChangeMaterial(currentMaterial);
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

      // Set the secondary button active if the reset zoom or checkpoint button should be shown.
      SetSecondaryButtonOverlay(/*active*/ currentOverlay == TouchpadOverlay.RESET_ZOOM
        || (mode == ControllerMode.insertStroke && freeformInstance.IsStroking() && freeformInstance.IsManualStroking()));

      // If the restrictions manager is allowing touchpadHighlighting don't let the peltzerController change any
      // icons.
      if (PeltzerMain.Instance.restrictionManager.touchpadHighlightingAllowed) {
        return;
      }

      Color fullWhite = new Color(1f, 1f, 1f, 1f);
      Color halfWhite = new Color(1f, 1f, 1f, 0.196f);

      // Color the sub-overlays depending on mode.
      switch (mode) {
        case ControllerMode.paintMesh:
          // The mesh.
          controllerGeometry.paintOverlay.GetComponent<Overlay>().leftIcon.color = fullWhite;
          // The face.
          controllerGeometry.paintOverlay.GetComponent<Overlay>().rightIcon.color = halfWhite;
          break;
        case ControllerMode.paintFace:
          // The mesh.
          controllerGeometry.paintOverlay.GetComponent<Overlay>().leftIcon.color = halfWhite;
          // The face.
          controllerGeometry.paintOverlay.GetComponent<Overlay>().rightIcon.color = fullWhite;
          break;
        case ControllerMode.subdivideFace:
          // Subdivide.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().leftIcon.color = fullWhite;
          // Reshape.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().upIcon.color = halfWhite;
          // Extrude.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().rightIcon.color = halfWhite;
          break;
        case ControllerMode.reshape:
          // Subdivide.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().leftIcon.color = halfWhite;
          // Reshape.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().upIcon.color = fullWhite;
          // Extrude.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().rightIcon.color = halfWhite;
          break;
        case ControllerMode.extrude:
          // Subdivide.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().leftIcon.color = halfWhite;
          // Reshape.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().upIcon.color = halfWhite;
          // Extrude.
          controllerGeometry.modifyOverlay.GetComponent<Overlay>().rightIcon.color = fullWhite;
          break;
        case ControllerMode.delete:
          controllerGeometry.deleteOverlay.GetComponent<Overlay>().leftIcon.gameObject
            .SetActive(Features.enablePartDeletion);
          controllerGeometry.deleteOverlay.GetComponent<Overlay>().rightIcon.gameObject
            .SetActive(Features.enablePartDeletion);
          // Delete part.
          controllerGeometry.deleteOverlay.GetComponent<Overlay>().rightIcon.color = halfWhite;
          // Delete mesh.
          controllerGeometry.deleteOverlay.GetComponent<Overlay>().leftIcon.color = fullWhite;
          break;
        case ControllerMode.deletePart:
          controllerGeometry.deleteOverlay.GetComponent<Overlay>().leftIcon.gameObject
            .SetActive(Features.enablePartDeletion);
          controllerGeometry.deleteOverlay.GetComponent<Overlay>().rightIcon.gameObject
            .SetActive(Features.enablePartDeletion);
          // Delete part.
          controllerGeometry.deleteOverlay.GetComponent<Overlay>().rightIcon.color = fullWhite;
          // Delete mesh.
          controllerGeometry.deleteOverlay.GetComponent<Overlay>().leftIcon.color = halfWhite;
          break;
        case ControllerMode.insertStroke:
          if (freeformInstance.IsStroking()) {
            // Set the change vertices of the face overlay to be inactive. You can't change verts in the middle of
            // a stroke.
            controllerGeometry.freeformChangeFaceOverlay.SetActive(false);
            if (freeformInstance.IsManualStroking()) {
              // Activate the checkpoint button.
              controllerGeometry.freeformOverlay.GetComponent<Overlay>().center.SetActive(true);
            }
          } else {
            // Make sure the changeFaceOverlay is on.
            controllerGeometry.freeformChangeFaceOverlay.SetActive(true);
            controllerGeometry.freeformOverlay.GetComponent<Overlay>().center.SetActive(false);
          }
          break;
      }

      GameObject currentOverlayGO;
      // Get reference to current overlay.
      switch (mode) {
        case ControllerMode.insertVolume:
        case ControllerMode.subtract:
          currentOverlayGO = controllerGeometry.volumeInserterOverlay;
          break;
        case ControllerMode.insertStroke:
          currentOverlayGO = controllerGeometry.freeformOverlay;
          break;
        case ControllerMode.paintFace:
        case ControllerMode.paintMesh:
          currentOverlayGO = controllerGeometry.paintOverlay;
          break;
        case ControllerMode.move:
          currentOverlayGO = controllerGeometry.moveOverlay;
          break;
        case ControllerMode.reshape:
        case ControllerMode.extrude:
        case ControllerMode.subdivideFace:
        case ControllerMode.subdivideMesh:
          currentOverlayGO = controllerGeometry.modifyOverlay;
          break;
        case ControllerMode.delete:
        case ControllerMode.deletePart:
          currentOverlayGO = controllerGeometry.deleteOverlay;
          break;
        default:
          currentOverlayGO = null;
          break;
      }

      // Set state of hover icon for the current overlay.
      overlay = currentOverlayGO.GetComponent<Overlay>();
    }

    /// <summary>
    ///   Reset the touchpad to its default state.  This should probably be used by Actions that want
    ///   to temporarily set the overlay (i.e. to resize) and are done with their operation.
    /// </summary>
    public void ResetTouchpadOverlay() {
      Zoomer zoomer = PeltzerMain.Instance.Zoomer;
      if (zoomer != null && zoomer.isMovingWithPeltzerController) {
        ChangeTouchpadOverlay(TouchpadOverlay.RESET_ZOOM);
        return;
      }

      switch (mode) {
        case ControllerMode.insertVolume:
        case ControllerMode.subtract:
          ChangeTouchpadOverlay(TouchpadOverlay.VOLUME_INSERTER);
          break;
        case ControllerMode.insertStroke:
          ChangeTouchpadOverlay(TouchpadOverlay.FREEFORM);
          break;
        case ControllerMode.reshape:
          ChangeTouchpadOverlay(TouchpadOverlay.MODIFY);
          break;
        case ControllerMode.extrude:
          ChangeTouchpadOverlay(TouchpadOverlay.MODIFY);
          break;
        case ControllerMode.subdivideFace:
          ChangeTouchpadOverlay(TouchpadOverlay.MODIFY);
          break;
        case ControllerMode.subdivideMesh:
          ChangeTouchpadOverlay(TouchpadOverlay.MODIFY);
          break;
        case ControllerMode.deletePart:
          ChangeTouchpadOverlay(TouchpadOverlay.DELETE);
          break;
        case ControllerMode.delete:
          ChangeTouchpadOverlay(TouchpadOverlay.DELETE);
          break;
        case ControllerMode.move:
          ChangeTouchpadOverlay(TouchpadOverlay.MOVE);
          break;
        case ControllerMode.paintMesh:
          ChangeTouchpadOverlay(TouchpadOverlay.PAINT);
          break;
        case ControllerMode.paintFace:
          ChangeTouchpadOverlay(TouchpadOverlay.PAINT);
          break;
        default:
          ChangeTouchpadOverlay(TouchpadOverlay.NONE);
          break;
      }
    }

    /// <summary>
    ///   Gets the current velocity of the controller.
    /// </summary>
    /// <returns>The current velocity of the controller.</returns>
    public Vector3 GetVelocity() {
      return controller.GetVelocity();
    }

    /// <summary>
    ///   Set the hover state material on the controller.
    /// </summary>
    /// <param name="state">State of the hover state to match.</param>
    public void SetTouchpadHoverTexture(TouchpadHoverState state) {
      // Only for VIVE currently.
      if (Config.Instance.VrHardware == VrHardware.Vive) {
        switch (state) {
          case TouchpadHoverState.UP:
            overlay.upIcon.transform.localScale *= 1.25f;
            overlay.upIcon.transform.localPosition = UP_OVERLAY_ICON_HOVER_POSITION;
            break;
          case TouchpadHoverState.DOWN:
            overlay.downIcon.transform.localScale *= 1.25f;
            overlay.downIcon.transform.localPosition = DOWN_OVERLAY_ICON_HOVER_POSITION;
            break;
          case TouchpadHoverState.LEFT:
            overlay.leftIcon.transform.localScale *= 1.25f;
            overlay.leftIcon.transform.localPosition = LEFT_OVERLAY_ICON_HOVER_POSITION;
            break;
          case TouchpadHoverState.RIGHT:
            overlay.rightIcon.transform.localScale *= 1.25f;
            overlay.rightIcon.transform.localPosition = RIGHT_OVERLAY_ICON_HOVER_POSITION;
            break;
          case TouchpadHoverState.NONE:
            // Reset scale to default.
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
            break;
        }
      }
    }

    /// <summary>
    ///   Called when the handedness changes of the controller to accomodate necessary changes.
    /// </summary>
    public void ControllerHandednessChanged() {
      if (handedness == Handedness.LEFT) {
        controllerGeometry.groupLeftTooltip.SetActive(false);
        controllerGeometry.ungroupLeftTooltip.SetActive(false);
        controllerGeometry.zoomLeftTooltip.SetActive(false);
        controllerGeometry.moveLeftTooltip.SetActive(false);
        // Set Move tool hand to LEFT.
        grabToolOnPalette.transform.localScale = new Vector3(-1f, 1f, 1f);
        if (mode == ControllerMode.move) {
          attachedToolHead.transform.localScale = grabToolOnPalette.transform.localScale;
        }
      } else if (handedness == Handedness.RIGHT) {
        controllerGeometry.groupRightTooltip.SetActive(false);
        controllerGeometry.ungroupRightTooltip.SetActive(false);
        controllerGeometry.zoomRightTooltip.SetActive(false);
        controllerGeometry.moveRightTooltip.SetActive(false);
        // Set Move tool hand to RIGHT.
        grabToolOnPalette.transform.localScale = new Vector3(1f, 1f, 1f);
        if (mode == ControllerMode.move) {
          attachedToolHead.transform.localScale = grabToolOnPalette.transform.localScale;
        }

      }
    }

    /// <summary>
    ///   Determines which tooltip and where to show it when called. These are the grip tooltips to
    ///   advise a user how to move/zoom the world.
    ///   We only show these tooltips until the user has successfully moved or zoomed the world. 
    ///   We do not show these tooltips until at least one object is in the scene.
    ///   We do not show these tooltips during tutorials.
    /// </summary>
    public void SetGripTooltip() {
      if (!PaletteController.AcquireIfNecessary(ref PeltzerMain.Instance.paletteController)) return;

      if (PeltzerMain.Instance.Zoomer.userHasEverZoomed
        || !PeltzerMain.Instance.restrictionManager.tooltipsAllowed
        || PeltzerMain.Instance.HasDisabledTooltips
        || PeltzerMain.Instance.tutorialManager.TutorialOccurring()) {
        DisableGripTooltips();
        return;
      }

      GameObject zoomTooltip = handedness == Handedness.RIGHT ?
        controllerGeometry.zoomLeftTooltip : controllerGeometry.zoomRightTooltip;
      GameObject moveTooltip = handedness == Handedness.RIGHT ?
        controllerGeometry.moveLeftTooltip : controllerGeometry.moveRightTooltip;

      if (controller.IsPressed(ButtonId.Grip)
        && PeltzerMain.Instance.paletteController.controller.IsPressed(ButtonId.Grip)) {
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
        && !PeltzerMain.Instance.paletteController.controller.IsPressed(ButtonId.Grip)) {
        zoomTooltip.SetActive(true);
        moveTooltip.SetActive(false);
      } else if (!controller.IsPressed(ButtonId.Grip)
        && PeltzerMain.Instance.paletteController.controller.IsPressed(ButtonId.Grip)) {
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
        zoomTooltip.SetActive(false);
        bool showMoveTooltip = !PeltzerMain.Instance.Zoomer.userHasEverMoved
          && PeltzerMain.Instance.model.GetNumberOfMeshes() > 0;
        moveTooltip.SetActive(showMoveTooltip);
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
    }

    /// <summary>
    ///   Disables the 'hold to move' and 'hold to zoom' tooltips, and grip-button pulsing.
    /// </summary>
    public void DisableGripTooltips() {
      GameObject zoomTooltip = handedness == Handedness.RIGHT ?
        controllerGeometry.zoomLeftTooltip : controllerGeometry.zoomRightTooltip;
      GameObject moveTooltip = handedness == Handedness.RIGHT ?
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

    public TouchpadOverlay TouchpadOverlay { get { return currentOverlay; } }
  }
}
