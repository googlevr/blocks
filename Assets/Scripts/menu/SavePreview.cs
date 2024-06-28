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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.tutorial;
using UnityEngine;

namespace com.google.apps.peltzer.client.menu
{
    /// <summary>
    /// Handles creating and animating a preview of the model a user just saved. This preview is used to
    /// show that a save was successful but also shows the user where the model is saved in the poly menu.
    /// </summary>
    public class SavePreview : MonoBehaviour
    {
        /// <summary>
        /// Represents what state the savePreview is in.
        /// </summary>
        public enum State
        {
            // Default.
            NONE,
            // Not trying to show any preview.
            INACTIVE,
            // The preview is ready and waiting to be activated.
            WAITING,
            // The preview is active on the controller and is not currently being animated. This represents the
            // time in between scaling up and then getting sucked into the controller.
            ACTIVE,
            // The preview is being scaled into existence.
            SCALE_ANIMATING,
            // The preview is being suctioned into the menu button.
            SUCTION_ANIMATING,
            // The button is expanding out in response to the preview hitting it.
            BUTTON_EXPAND_ANIMATING,
            // The button is collapsing back to its original size.
            BUTTON_COLLAPSE_ANIMATING
        };

        public static float PREVIEW_DURATION = 0.5f;
        public static float SUCTION_ANIMATION_DURATION = 0.75f;
        private const float SCALE_IN_ANIMATION_DURATION = 0.2f;
        private const float BUTTON_EXPAND_ANIMATION_DURATION = 0.2f;
        private const float BUTTON_COLLAPSE_ANIMATION_DURATION = 0.1f;
        private const float BUTTON_SCALE_FACTOR = 1.4f;

        public State state { get; private set; }

        /// <summary>
        /// The save preview that replaces the saving indicator and then gets sucked into the controller.
        /// </summary>
        private GameObject preview;
        /// <summary>
        /// The worldspace that the preview exists in. We use a unique worldspace for the preview that we
        /// can scale, this is easier than actually changing the mesh.
        /// </summary>
        private WorldSpace previewSpace;
        /// <summary>
        /// The scale of the preview before any animating.
        /// </summary>
        private float previewScale;
        /// <summary>
        /// The position of the preview before any animating.
        /// </summary>
        private Vector3 previewPosAtStart;

        /// <summary>
        /// The scale of the button before any animating.
        /// </summary>
        private Vector3 buttonStartScale;
        /// <summary>
        /// The max scale the button should reach while animating.
        /// </summary>
        private Vector3 buttonMaxScale;

        /// <summary>
        /// The time that we started the current operation.
        /// </summary>
        private float operationStartTime;

        public void Setup()
        {
            previewSpace = new WorldSpace(PeltzerMain.DEFAULT_BOUNDS, /* isLimited */ false);
            previewScale = previewSpace.scale;
        }

        void Update()
        {
            if (state == State.SCALE_ANIMATING)
            {
                // Scale the preview into scene by lerping the worldSpace for the preview.

                // Scale animation is linear relative to passed time.
                float pctDone = (Time.time - operationStartTime) / SCALE_IN_ANIMATION_DURATION;

                if (pctDone > 1f)
                {
                    // Set the scale to its final value.
                    previewSpace.scale = previewScale;
                    // Make the preview active. This is the state where it previews on the controller before being
                    // sucked into the button.
                    ChangeState(State.ACTIVE);
                }
                else
                {
                    previewSpace.scale = Mathf.Lerp(0f, previewScale, pctDone);
                }
            }
            else if (state == State.ACTIVE && Time.time > operationStartTime + PREVIEW_DURATION)
            {
                // Show the preview on the controller before starting the suction animation.
                ChangeState(State.SUCTION_ANIMATING);
            }
            else if (state == State.SUCTION_ANIMATING)
            {
                // Make the preview look like its being sucked into the controller by slerping its position
                // and lerping its scale down. We use a Cubic Bezier curve to make the animation speed up
                // over time to get the suction effect.

                float pctDone = Math3d.CubicBezierEasing(0f, 0f, 0.1f, 1f,
                  (Time.time - operationStartTime) / SUCTION_ANIMATION_DURATION);

                if (pctDone > 1f)
                {
                    // Make a thud when the preview hits the button.
                    PeltzerMain.Instance.paletteController
                      .TriggerHapticFeedback(HapticFeedback.HapticFeedbackType.FEEDBACK_1, 0.02f, 1f);

                    // State expanding the button in response to the preview hitting it.
                    ChangeState(State.BUTTON_EXPAND_ANIMATING);
                }
                else
                {
                    // Animate the scale.
                    previewSpace.scale = Mathf.Lerp(previewScale, 0f, pctDone);

                    // Animate the position.
                    preview.transform.localPosition = Vector3.Slerp(
                      previewPosAtStart,
                      PeltzerMain.Instance.paletteController.controllerGeometry.appMenuButtonHolder.transform.localPosition,
                      pctDone);

                    // Trigger feedback that gets stronger over the animation.
                    PeltzerMain.Instance.paletteController.TriggerHapticFeedback(
                      HapticFeedback.HapticFeedbackType.FEEDBACK_1, 0.001f, pctDone == 0f ? 0f : pctDone * 0.1f);
                }
            }
            else if (state == State.BUTTON_EXPAND_ANIMATING)
            {
                // Animate the button expanding. We want it to look like a ripple because the preview hit it so we
                // use a Cubic Bezier curve to lerp the scale quickly at first before slowing down.
                float pctDone = Math3d.CubicBezierEasing(0f, 1.0f, 1.0f, 1.0f,
                  (Time.time - operationStartTime) / BUTTON_EXPAND_ANIMATION_DURATION);

                // We need the holder which is centered over the button. The transform of the actual button is
                // incorrect because of a flaw in the controller UI.
                Transform buttonHolder =
                    PeltzerMain.Instance.paletteController.controllerGeometry.appMenuButtonHolder.transform;

                if (pctDone > 1.0f)
                {
                    buttonHolder.localScale = buttonMaxScale;
                    ChangeState(State.BUTTON_COLLAPSE_ANIMATING);
                }
                else
                {
                    // Animate the scale.
                    buttonHolder.localScale = Vector3.Lerp(buttonStartScale, buttonMaxScale, pctDone);

                    // Animate the glow on the actual button.
                    GameObject actualButton =
                      PeltzerMain.Instance.paletteController.controllerGeometry.appMenuButton;

                    AttentionCaller.SetEmissiveFactor(
                      actualButton,
                      (Time.time - operationStartTime) / BUTTON_EXPAND_ANIMATION_DURATION,
                      actualButton.GetComponent<Renderer>().material.color);
                }
            }
            else if (state == State.BUTTON_COLLAPSE_ANIMATING)
            {
                // No need to do anything fancy here. We scale down the button linearly and quickly.
                float pctDone = (Time.time - operationStartTime) / BUTTON_COLLAPSE_ANIMATION_DURATION;

                Transform buttonHolder =
                    PeltzerMain.Instance.paletteController.controllerGeometry.appMenuButtonHolder.transform;

                if (pctDone > 1.0f)
                {
                    buttonHolder.localScale = buttonStartScale;

                    // We don't animate the glow away. We want it to be as luminous for as long as possible.
                    GameObject actualButton =
                      PeltzerMain.Instance.paletteController.controllerGeometry.appMenuButton;
                    AttentionCaller.SetEmissiveFactor(actualButton, 0f, actualButton.GetComponent<Renderer>().material.color);
                    ChangeState(State.INACTIVE);
                }
                else
                {
                    buttonHolder.localScale = Vector3.Lerp(buttonMaxScale, buttonStartScale, pctDone);
                }
            }
        }

        public void ChangeState(State newState)
        {
            // Don't try to show the preview if the meshes haven't been passed from ZandriaCreationsManager yet.
            if (newState == State.SCALE_ANIMATING && state != State.WAITING)
            {
                return;
            }

            state = newState;

            switch (state)
            {
                case State.INACTIVE:
                    break;
                case State.WAITING:
                    // Do nothing. SetupBubble should be called to switch the state to WAITING. We need that method to be
                    // called externally by ZandriaCreationManager so that the meshes for the preview can be passed in.
                    break;
                case State.ACTIVE:
                    operationStartTime = Time.time;
                    break;
                case State.SCALE_ANIMATING:
                    // We can only switch to the SCALE_ANIMATING state when the last state was WAITING so that we know the
                    // mesh preview is ready to be shown.
                    preview.SetActive(true);
                    previewScale = previewSpace.scale;
                    previewSpace.scale = 0f;
                    operationStartTime = Time.time;
                    break;
                case State.SUCTION_ANIMATING:
                    previewPosAtStart = preview.transform.localPosition;
                    operationStartTime = Time.time;
                    break;
                case State.BUTTON_EXPAND_ANIMATING:
                    if (preview != null)
                    {
                        preview.SetActive(false);
                        Destroy(preview);
                        previewSpace.scale = previewScale;
                    }

                    operationStartTime = Time.time;
                    buttonStartScale =
                      PeltzerMain.Instance.paletteController.controllerGeometry.appMenuButton.transform.parent.localScale;
                    buttonMaxScale = new Vector3(
                      buttonStartScale.x * BUTTON_SCALE_FACTOR,
                      buttonStartScale.y,
                      buttonStartScale.z * BUTTON_SCALE_FACTOR);
                    break;
                case State.BUTTON_COLLAPSE_ANIMATING:
                    operationStartTime = Time.time;
                    break;
            }
        }

        public void SetupPreview(MeshWithMaterialRenderer mwmRenderer)
        {
            if (state != State.INACTIVE && state != State.NONE)
            {
                // If the save preview is currently active and in progress, skip the save preview for the new save.
                // This happens when saves are in quick succession (usually for save selected), so the user should already
                // know where the saved models are going from the in progress preview.
                return;
            }
            preview = new GameObject();
            MeshWithMaterialRenderer clonedRenderer = preview.AddComponent<MeshWithMaterialRenderer>();
            clonedRenderer.SetupAsCopyOf(mwmRenderer);
            clonedRenderer.worldSpace = previewSpace;

            preview.transform.parent =
              PeltzerMain.Instance.paletteController.controllerGeometry.appMenuButtonHolder.transform.parent;
            preview.transform.position = ObjectFinder.ObjectById("ID_ProgressIndicatorPanel").transform.position;

            // Hide it until the progressIndicator is done.
            preview.SetActive(false);

            ChangeState(State.WAITING);
        }
    }
}
