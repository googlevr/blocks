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

using UnityEngine;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.app;

namespace com.google.apps.peltzer.client.model.main
{
    /// <summary>
    /// Allow the user to control the world transform.
    /// This is misnamed: this doesn't only do zoom but also pan/rotate.
    /// </summary>
    public class Zoomer : MonoBehaviour
    {
        // The amount of time to forcefully show the world space bounding box in the beginning of the session.
        private const float FIRST_TIME_RUN_DURATION = 10.0f;

        private PeltzerController peltzerController;
        private PaletteController paletteController;
        private WorldSpace worldSpace;
        private GameObject visualBoundingBox;
        private GameObject visualBoundingBoxCube;
        private GameObject visualBoundingBoxLines;
        private GameObject gridPlanes;
        private AudioLibrary audioLibrary;

        // Whether the user is manipulating a grid plane to show the bounding box or not.
        public bool isManipulatingGridPlane = false;
        // Whether to hide tooltips for moving/zooming.
        public bool userHasEverMoved = false;
        public bool userHasEverZoomed = false;

        // Did we start moving or zooming last frame?  If so, lastXXXPos are valid.
        public bool Zooming { get; set; }
        public bool moving = false;
        public bool isMovingWithPaletteController;
        public bool isMovingWithPeltzerController;
        private Vector3 lastPalettePos;
        private Vector3 lastPeltzerPos;

        // The start time within the life cycle of the application session as to when the instance,
        // which controls the visibility of the world space, comes into play.
        public float firstRunStartTime = 0f;

        // Details of the world state at the beginning of a zoom operation.
        private float startDistance;
        private float startScale;
        private Quaternion worldRotationAtZoomStart = Quaternion.identity;
        private Vector3 controllerDiffAtZoomStart;
        private Vector3 centerAtZoomStartModel;

        // World space rendering.
        private Material wallMaterial;

        /// <summary>
        ///   Every tool is implemented as MonoBehaviour, which means it may do no work in its constructor.
        ///   As such, this setup method must be called before the tool is used for it to have a valid state.
        /// </summary>
        public void Setup(ControllerMain controllerMain, PeltzerController peltzerController,
          PaletteController paletteController, WorldSpace worldSpace, AudioLibrary audioLibrary)
        {
            this.peltzerController = peltzerController;
            this.paletteController = paletteController;
            this.worldSpace = worldSpace;
            this.audioLibrary = audioLibrary;
            controllerMain.ControllerActionHandler += ControllerActionHandler;

            // Get the reference to the bounding box GameObject.
            visualBoundingBox = ObjectFinder.ObjectById("ID_PolyWorldBounds");
            gridPlanes = visualBoundingBox.transform.Find("GridPlanes").gameObject;
            visualBoundingBoxCube = visualBoundingBox.transform.Find("Cube").gameObject;
            visualBoundingBoxLines = visualBoundingBox.transform.Find("Lines").gameObject;
            wallMaterial = visualBoundingBox.transform.Find("Cube").GetComponent<Renderer>().material;
        }

        private bool IsResetEvent(ControllerEventArgs args)
        {
            // Only consider this a reset event if the secondary button hit was on the same controller
            // which has a trigger down.
            if ((isMovingWithPaletteController && args.ControllerType == ControllerType.PALETTE)
                 || (isMovingWithPeltzerController && args.ControllerType == ControllerType.PELTZER))
            {
                // If the controller is a Rift, reset uses the secondary button; otherwise uses the touchpad.
                if (Config.Instance.VrHardware == VrHardware.Rift)
                {
                    return args.ButtonId == ButtonId.SecondaryButton
                      && args.Action == ButtonAction.DOWN;
                }
                return args.ButtonId == ButtonId.Touchpad
                  && args.Action == ButtonAction.DOWN;
            }
            else
            {
                return false;
            }
        }

        private void ControllerActionHandler(object sender, ControllerEventArgs args)
        {
            if ((Zooming || moving) && IsResetEvent(args))
            {
                ClearState();
                audioLibrary.PlayClip(audioLibrary.zoomResetSound);
            }
        }

        public void ClearState()
        {
            worldSpace.SetToDefault();
            InitZoomStartVars();
        }

        /// <summary>
        ///   Keep track of the world-state at the start of a zoom operation, so we can calculate deltas.
        /// </summary>
        private void InitZoomStartVars()
        {
            worldRotationAtZoomStart = worldSpace.rotation;

            Vector3 paletteControllerPositionAtZoomStart = paletteController.transform.position;
            Vector3 peltzerControllerPositionAtZoomStart = peltzerController.transform.position;
            Vector3 paletteControllerModelPositionAtZoomStart = worldSpace.WorldToModel(paletteControllerPositionAtZoomStart);
            Vector3 peltzerControllerModelPositionAtZoomStart = worldSpace.WorldToModel(peltzerControllerPositionAtZoomStart);

            centerAtZoomStartModel = (paletteControllerModelPositionAtZoomStart + peltzerControllerModelPositionAtZoomStart) * 0.5f;
            controllerDiffAtZoomStart = Vector3.Normalize(paletteControllerPositionAtZoomStart - peltzerControllerPositionAtZoomStart);
            startScale = worldSpace.scale;
            startDistance = Vector3.Distance(paletteControllerPositionAtZoomStart, peltzerControllerPositionAtZoomStart);
        }

        void Update()
        {
            if (!PeltzerMain.Instance.restrictionManager.changeWorldTransformAllowed)
            {
                return;
            }

            if (firstRunStartTime == 0f)
            {
                firstRunStartTime = Time.time;
            }

            // Check for grips.
            bool paletteGripDown = false;
            bool peltzerGripDown = false;
            paletteGripDown = PaletteController.AcquireIfNecessary(ref paletteController)
              && paletteController.controller.IsPressed(ButtonId.Grip);
            peltzerGripDown = PeltzerController.AcquireIfNecessary(ref peltzerController)
              && peltzerController.controller.IsPressed(ButtonId.Grip);

            if (paletteGripDown && peltzerGripDown && !PeltzerMain.Instance.tutorialManager.TutorialOccurring())
            {
                // Where zooming is false, this means this is the first frame of the ZoomWorld operation, so all we'll do is
                // collect the controller positions so we can detect relative changes in distance between controllers.
                if (Zooming)
                {
                    ZoomWorld();
                }
                else
                {
                    StartZooming();
                }
            }
            else
            {
                if (Zooming)
                {
                    EndZooming();
                }
                if (paletteGripDown || peltzerGripDown)
                {
                    // Where moving is false, this means this is the first frame of the MoveWorld operation, so all we'll do is
                    // collect the controller positions so we can detect movement.
                    if (moving)
                    {
                        Vector3 offset = paletteGripDown ?
                          paletteController.transform.position - lastPalettePos :
                          peltzerController.transform.position - lastPeltzerPos;
                        worldSpace.offset += offset;
                    }
                    if (paletteGripDown)
                    {
                        StartMovingWithPaletteController();
                        StopMovingWithPeltzerController();
                    }
                    else
                    {
                        StartMovingWithPeltzerController();
                        StopMovingWithPaletteController();
                    }
                    userHasEverMoved = true;
                    moving = true;
                }
                else if (moving)
                {
                    // Check if we are currently moving before setting moving to false.
                    moving = false;
                    StopMovingWithPeltzerController();
                    StopMovingWithPaletteController();
                }
            }
            if (Zooming || moving)
            {
                if (paletteController != null)
                {
                    lastPalettePos = paletteController.transform.position;
                }
                if (peltzerController != null)
                {
                    lastPeltzerPos = peltzerController.transform.position;
                }
            }

            if (PeltzerMain.Instance.restrictionManager.showingWorldBoundingBoxAllowed)
            {
                MaybeShowBoundingBox();
            }
        }

        private void StartMovingWithPaletteController()
        {
            isMovingWithPaletteController = true;
            paletteController.ChangeTouchpadOverlay(TouchpadOverlay.RESET_ZOOM);
        }

        private void StopMovingWithPaletteController()
        {
            isMovingWithPaletteController = false;
            paletteController.ResetTouchpadOverlay();
        }

        private void StartMovingWithPeltzerController()
        {
            isMovingWithPeltzerController = true;
            peltzerController.ChangeTouchpadOverlay(TouchpadOverlay.RESET_ZOOM);
        }

        private void StopMovingWithPeltzerController()
        {
            isMovingWithPeltzerController = false;
            peltzerController.ResetTouchpadOverlay();
        }

        private void StartZooming()
        {
            Zooming = true;
            StartMovingWithPaletteController();
            StartMovingWithPeltzerController();
            if (!userHasEverZoomed)
            {
                userHasEverZoomed = true;
                peltzerController.DisableGripTooltips();
                paletteController.DisableGripTooltips();
            }
            peltzerController.ChangeTouchpadOverlay(TouchpadOverlay.RESET_ZOOM);
            paletteController.ChangeTouchpadOverlay(TouchpadOverlay.RESET_ZOOM);
            InitZoomStartVars();
            PeltzerMain.Instance.restrictionManager.undoRedoAllowed = false;
        }

        private void EndZooming()
        {
            Zooming = false;
            peltzerController.ResetTouchpadOverlay();
            paletteController.ResetTouchpadOverlay();
            StopMovingWithPaletteController();
            StopMovingWithPeltzerController();
            // Re-enable zooming, if the tool menu is active.
            PeltzerMain.Instance.restrictionManager.undoRedoAllowed = PeltzerMain.Instance.GetPolyMenuMain().ToolMenuIsActive();
        }

        /// <summary>
        ///   Zooms the entire world, based on the relative position of the controllers.
        /// </summary>
        private void ZoomWorld()
        {
            Vector3 curPalettePos = paletteController.transform.position;
            Vector3 curPeltzerPos = peltzerController.transform.position;
            Vector3 curDiffVector = Vector3.Normalize(curPalettePos - curPeltzerPos);

            // We only allow rotation around the Y axis.
            Quaternion curRotation = Quaternion.FromToRotation(controllerDiffAtZoomStart, curDiffVector);
            curRotation = Quaternion.Euler(new Vector3(0, curRotation.eulerAngles.y, 0));

            // Change the scale by the ratio of the old distance to the new one.
            float curDist = Vector3.Distance(curPalettePos, curPeltzerPos);
            worldSpace.scale = startScale / (startDistance / curDist);
            worldSpace.rotation = curRotation * worldRotationAtZoomStart;


            Vector3 targetWorldCenter = (curPalettePos + curPeltzerPos) * 0.5f;
            // We also need to change the offset so that the model-space position we were centered on at start remains
            // in the same point in world space.
            Vector3 toTargetWorldPos = targetWorldCenter - worldSpace.ModelToWorld(centerAtZoomStartModel);
            worldSpace.offset = worldSpace.offset + toTargetWorldPos;
        }

        /// <summary>
        ///   Updates the position, rotation and scale of the visual bounding box.
        /// </summary>
        private void UpdateVisualBoundingBox()
        {
            // Get world position of selector position.
            Vector4 selectorWorldPosition = PeltzerMain.Instance.worldSpace
              .ModelToWorld(PeltzerMain.Instance.peltzerController.LastPositionModel);
            selectorWorldPosition.w = 0;
            wallMaterial.SetVector("_SelectorPosition", selectorWorldPosition);
            visualBoundingBox.transform.position = worldSpace.offset;
            visualBoundingBox.transform.localScale = worldSpace.scale * PeltzerMain.DEFAULT_BOUNDS.size;
            visualBoundingBox.transform.localRotation = worldSpace.rotation;
        }

        /// <summary>
        ///   Makes the bounding box visible, if required.
        /// </summary>
        private void MaybeShowBoundingBox()
        {
            bool userInBounds = worldSpace.bounds.Contains(peltzerController.LastPositionModel);
            // Measure the controller positions distance from the world space bounds using delta of bounds extents.
            Bounds tempBounds = new Bounds();
            tempBounds.Encapsulate(worldSpace.bounds);
            tempBounds.Expand(-1.0f);
            userInBounds = tempBounds.Contains(peltzerController.LastPositionModel);
            bool showVisualBoundingBox = IsFirstTimeShow() || Zooming
              || moving
              || !userInBounds
              || peltzerController.heldMeshes
              || isManipulatingGridPlane ? true : false;
            visualBoundingBoxLines.SetActive(showVisualBoundingBox);
            visualBoundingBoxCube.SetActive(showVisualBoundingBox);
            if (showVisualBoundingBox)
            {
                UpdateVisualBoundingBox();
            }
        }

        /// <summary>
        ///   First time show is an override to subsequent logic that allows us to "present" the bounding box
        ///   as a first time / onboarding segment. This function maintains the initial presentation of the
        ///   worldspace bounds and persists it for the duration specified by FIRST_TIME_RUN_DURATION.
        /// </summary>
        private bool IsFirstTimeShow()
        {
            // If duration is met, set firstTimeRun = false
            float timeDelta = Time.time - firstRunStartTime;
            return timeDelta < FIRST_TIME_RUN_DURATION;
        }
    }
}
