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
using System.Collections.Generic;
using com.google.apps.peltzer.client.app;

namespace com.google.apps.peltzer.client.menu
{
    /// <summary>
    /// Creates and positions a preview for the menuHint and maintains information about the preview.
    /// </summary>
    public class MenuPreview
    {
        public GameObject preview;
        public MeshWithMaterialRenderer renderer;
        public Vector3 positionAtStartOfOperation;

        public MenuPreview(MeshWithMaterialRenderer mwmRenderer, GameObject parent, WorldSpace worldSpace)
        {
            preview = new GameObject();
            renderer = preview.AddComponent<MeshWithMaterialRenderer>();
            renderer.SetupAsCopyOf(mwmRenderer);
            renderer.worldSpace = worldSpace;

            preview.transform.parent = parent.transform;
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.identity;

            // This is set at the beginning of an operation and can be local or global depending on what the
            // operation calls for.
            positionAtStartOfOperation = Vector3.zero;
        }
    }

    /// <summary>
    /// Handles creating and animating a menuHint which previews four featured models and hints to the user
    /// where the poly menu is.
    /// </summary>
    public class MenuHint : MonoBehaviour
    {
        /// <summary>
        /// Represents what state the menuHint is in.
        /// </summary>
        public enum State
        {
            // Default.
            NONE,
            // The preview is being populated from the menu.
            POPULATING,
            // The menuHint has been shown and is now inactive.
            INACTIVE,
            // The preview is loaded but we are waiting for the timer to run down.
            WAITING,
            // The preview is being scaled into existence.
            SCALE_ANIMATING,
            // The preview is rotating.
            ROTATION_ANIMATING,
            // The preview is being suctioned into the menu button.
            SUCTION_ANIMATING,
            // The button is expanding out in response to the preview hitting it.
            BUTTON_EXPAND_ANIMATING,
            // The button is collapsing back to its original size.
            BUTTON_COLLAPSE_ANIMATING
        };

        /// <summary>
        /// The time between the intro completing and the menuHint starting to show.
        /// </summary>
        public static float WAIT_DURATION = 5f;
        /// <summary>
        /// How long the scale in animation lasts.
        /// </summary>
        private const float SCALE_IN_ANIMATION_DURATION = 0.2f;
        /// <summary>
        /// How long the previews rotate for.
        /// </summary>
        public static float ROTATION_ANIMATION_DURATION = 3f;
        /// <summary>
        /// How long it takes to suck the preview into the button.
        /// </summary>
        public static float SUCTION_ANIMATION_DURATION = 0.75f;
        /// <summary>
        /// How long the buttons expands for after being hit.
        /// </summary>
        private const float BUTTON_EXPAND_ANIMATION_DURATION = 0.2f;
        /// <summary>
        /// How long it takes for the button to reset.
        /// </summary>
        private const float BUTTON_COLLAPSE_ANIMATION_DURATION = 0.1f;
        /// <summary>
        /// How much larger the button gets.
        /// </summary>
        private const float BUTTON_SCALE_FACTOR = 1.4f;
        /// <summary>
        /// How long we wait in between haptic pulses when getting the users attention.
        /// </summary>
        private const float HAPTICS_PAUSE = 0.5f;
        /// <summary>
        /// How many times we pulse the controller to get the users attention.
        /// </summary>
        private const int ATTENTION_PULSES = 3;
        /// <summary>
        /// The default scale for the model previews.
        /// </summary>
        public const float DEFAULT_SCALE = 0.45f;
        /// <summary>
        /// The minimum speed factor used at the start of rotating the previews.
        /// </summary>
        private const float MIN_ROTATION_SPEED = 20f;
        /// <summary>
        /// The rotation speed factor we hit before starting to suck the previews into the button.
        /// </summary>
        private const float MID_ROTATION_SPEED = 150f;
        /// <summary>
        /// The max rotation speed factor for the previews as they are sucked into the button.
        /// </summary>
        private const float MAX_ROTATION_SPEED = 2500f;

        /// <summary>
        /// The current state of the menuHint.
        /// </summary>
        private State state;
        /// <summary>
        /// The previews shown as a hint.
        /// </summary>
        private List<MenuPreview> menuPreviews;
        /// <summary>
        ///  The MeshWithMaterialRenderers passed from the ZandriaCreationsManager that will be used to make
        ///  preview meshes of the models used as hints.
        /// </summary>
        List<MeshWithMaterialRenderer> mwmRenderers;
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
        /// <summary>
        /// The position of the menuHint at the start of the current operation. This can be in global or local
        /// depending on the operation.
        /// </summary>
        private Vector3 operationStartPosition;
        /// <summary>
        /// The world space that the preview models exist in. This is used to manipulate their apparent size.
        /// </summary>
        private WorldSpace previewSpace;
        /// <summary>
        /// The number of haptic pulses that have been triggered to get the users attention.
        /// </summary>
        private int attentionPulseCount;

        /// <summary>
        /// The game object that holds the four previews.
        /// </summary>
        private GameObject menuHint;
        /// <summary>
        /// The game object that holds the text displayed with the menu hint.
        /// </summary>
        private GameObject menuHintText;
        /// <summary>
        /// Holds the menuHint and the menuHintText.
        /// </summary>
        private GameObject menuHintRoot;
        /// <summary>
        /// Empty game objects that hold the positions that the previews can be attached.
        /// </summary>
        private GameObject[] menuPreviewHolders;
        /// <summary>
        /// The position of the button the previews are sucked into. This is in the same space as the menuHint
        /// so that we can animate without the motion of the controller having an effect.
        /// </summary>
        private Vector3 buttonPositionInPreviewSpace;

        /// <summary>
        /// Position of the menu hint when using the Oculus.
        /// </summary>
        private static readonly Vector3 ROOT_POSITION_OCULUS = new Vector3(0.002721673f, 0.05050485f, 0.06261638f);

        /// <summary>
        /// Rotation of the menu hint when using the Oculus.
        /// </summary>
        private static readonly Vector3 ROOT_ROTATION_OCULUS = new Vector3(-46.218f, 6.207f, -11.243f);

        public void Setup()
        {
            // Indicate to ZandriaCreationsManager that the MenuHint is waiting for previews.
            state = State.POPULATING;

            // Find all the relevant game objects.
            menuHint = ObjectFinder.ObjectById("ID_MenuHint");
            menuHintRoot = ObjectFinder.ObjectById("ID_MenuHintHolder");
            menuHintText = ObjectFinder.ObjectById("ID_MenuHintText");
            menuPreviewHolders = new GameObject[4] {
        ObjectFinder.ObjectById("ID_MenuPreview_1"),
        ObjectFinder.ObjectById("ID_MenuPreview_2"),
        ObjectFinder.ObjectById("ID_MenuPreview_3"),
        ObjectFinder.ObjectById("ID_MenuPreview_4")};

            // Make sure we position the menuHint and text correctly for Rift/Vive by copying the position of the save
            // indicator.
            menuHintRoot.transform.position = ObjectFinder.ObjectById("ID_ProgressIndicatorPanel").transform.position;

            // Find where the menu button is in the menuHints transform space so that we can animate the menuHint
            // locally so that movement of the controller doesn't affect the animation.
            buttonPositionInPreviewSpace = menuHint.transform.parent.InverseTransformPoint(
              PeltzerMain.Instance.paletteController.controllerGeometry.appMenuButtonHolder.transform.position);

            menuPreviews = new List<MenuPreview>(menuPreviewHolders.Length);
            mwmRenderers = new List<MeshWithMaterialRenderer>(menuPreviewHolders.Length);

            // Hide everything until the previews are ready and the timer has run down.
            menuHintRoot.SetActive(false);

            // We won't set the operation startTime until the intro choreography is complete. This way we can guarantee
            // the menu is active.
            operationStartTime = 0f;

            if (Config.Instance.sdkMode == SdkMode.Oculus)
            {
                menuHintRoot.transform.localPosition = ROOT_POSITION_OCULUS;
                menuHintRoot.transform.localRotation = Quaternion.Euler(ROOT_ROTATION_OCULUS);
            }
        }

        void Update()
        {
            if (state == State.WAITING)
            {
                // While operationStartTime == 0f we know the introchoreography hasn't finished so even if the
                // previews are ready we continue to wait. Otherwise we wait until HAPTICS_PAUSE time has passed.
                // trigger haptics to get the users attention and repeat for ATTENTION_PULSES number of pulses.
                if (operationStartTime != 0f && Time.time > operationStartTime + HAPTICS_PAUSE)
                {
                    PeltzerMain.Instance.paletteController.LookAtMe();
                    operationStartTime = Time.time;
                    attentionPulseCount++;

                    // ZandriaCreationsManager has given MenuHint all the previews, the introChoreography is done, the
                    // menuHint timer has run down and we have the users attention from the haptics so we setup the preview
                    // and start animating the menuHint.
                    if (attentionPulseCount >= ATTENTION_PULSES)
                    {
                        SetupPreviews();
                    }
                }
            }
            else if (state == State.SCALE_ANIMATING)
            {
                // Scale the preview into scene by lerping the worldSpace for the preview.

                // No need to be fancy scale animation is linear relative to passed time.
                float pctDone = (Time.time - operationStartTime) / SCALE_IN_ANIMATION_DURATION;

                if (pctDone > 1f)
                {
                    // Set the scale to its final value.
                    previewSpace.scale = DEFAULT_SCALE;
                    // Progress to the next state of the animation which is where the previews rotate around the controller.
                    ChangeState(State.ROTATION_ANIMATING);
                }
                else
                {
                    // Animate the previews into existance. 
                    previewSpace.scale = Mathf.Lerp(0f, DEFAULT_SCALE, pctDone);
                }
            }
            else if (state == State.ROTATION_ANIMATING)
            {
                // We increase the speed that the previews rotate at lineraly.
                float pctDone = (Time.time - operationStartTime) / ROTATION_ANIMATION_DURATION;

                if (pctDone > 1)
                {
                    ChangeState(State.SUCTION_ANIMATING);
                }
                else
                {
                    float speed = Mathf.Lerp(MIN_ROTATION_SPEED, MID_ROTATION_SPEED, pctDone);
                    // Rotate the menuHint about itself. The previews are children and rotate with it.
                    menuHint.transform.RotateAround(menuHint.transform.position, menuHint.transform.up, speed * Time.deltaTime);

                    // Rotate each preview individually in the opposite direction. This causes them to keep facing the
                    // user and the menuHint spins. Plus it looks better.
                    foreach (MenuPreview preview in menuPreviews)
                    {
                        preview.preview.transform.RotateAround(
                          preview.preview.transform.position,
                          preview.preview.transform.up,
                          -(speed * Time.deltaTime));
                    }
                }
            }
            else if (state == State.SUCTION_ANIMATING)
            {
                // Use an easy curve to suck in the preview.
                float pctDone = Math3d.CubicBezierEasing(0f, 0f, 0.1f, 1f,
                      (Time.time - operationStartTime) / SUCTION_ANIMATION_DURATION);

                if (pctDone > 1f)
                {
                    // Make a thud when the preview hits the button.
                    PeltzerMain.Instance.paletteController
                      .TriggerHapticFeedback(HapticFeedback.HapticFeedbackType.FEEDBACK_1, 0.04f, 1f);

                    // Expand the button in response to the preview hitting it.
                    ChangeState(State.BUTTON_EXPAND_ANIMATING);
                }
                else
                {
                    // Animate the scale.
                    previewSpace.scale = Mathf.Lerp(DEFAULT_SCALE, 0f, pctDone);

                    // Animate the position of the entire menuHint towards the button in local space.
                    menuHint.transform.localPosition = Vector3.Slerp(
                     operationStartPosition,
                     buttonPositionInPreviewSpace,
                     pctDone);

                    // Continue to rotate to give the menuHint a tornado effect.
                    float speed = Mathf.Lerp(MID_ROTATION_SPEED, MAX_ROTATION_SPEED, pctDone);
                    menuHint.transform.RotateAround(menuHint.transform.position, menuHint.transform.up, speed * Time.deltaTime);

                    foreach (MenuPreview preview in menuPreviews)
                    {
                        // Animate each individual preview towards the center of the whole menuHint converting the
                        // position of the menuHint to be a sibling of the preview so that we can animate the change locally.
                        preview.preview.transform.localPosition = Vector3.Slerp(
                          preview.positionAtStartOfOperation,
                          preview.preview.transform.parent.InverseTransformPoint(menuHint.transform.position),
                          pctDone);

                        // Continue to rotate each individual preview.
                        preview.preview.transform.RotateAround(
                         preview.preview.transform.position,
                         preview.preview.transform.up,
                         -(speed * Time.deltaTime));
                    }

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
            state = newState;

            switch (state)
            {
                // The animation is over.
                case State.INACTIVE:
                    foreach (MenuPreview preview in menuPreviews)
                    {
                        Destroy(preview.preview);
                    }

                    // Reset the active states of all the menu hint components.
                    menuHintText.SetActive(true);
                    menuHint.SetActive(true);
                    menuHintRoot.SetActive(false);

                    menuPreviews.Clear();
                    break;
                case State.WAITING:
                    break;
                case State.SCALE_ANIMATING:
                    menuHintRoot.SetActive(true);
                    menuHintText.SetActive(false);
                    previewSpace.scale = 0f;
                    operationStartTime = Time.time;
                    break;
                case State.ROTATION_ANIMATING:
                    operationStartTime = Time.time;
                    menuHintText.SetActive(true);
                    break;
                case State.SUCTION_ANIMATING:
                    menuHintText.SetActive(false);
                    foreach (MenuPreview preview in menuPreviews)
                    {
                        preview.positionAtStartOfOperation = preview.preview.transform.localPosition;
                    }
                    operationStartTime = Time.time;
                    operationStartPosition = menuHint.transform.localPosition;
                    break;
                case State.BUTTON_EXPAND_ANIMATING:
                    menuHintRoot.SetActive(false);
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

        private void SetupPreviews()
        {
            previewSpace = new WorldSpace(PeltzerMain.DEFAULT_BOUNDS, /* isLimited */ false);
            previewSpace.scale = DEFAULT_SCALE;

            for (int i = 0; i < menuPreviewHolders.Length; i++)
            {
                menuPreviews.Add(new MenuPreview(mwmRenderers[i], menuPreviewHolders[i], previewSpace));
            }

            ChangeState(State.SCALE_ANIMATING);
        }

        public void SetTimer()
        {
            operationStartTime = Time.time + WAIT_DURATION;
        }

        public void AddPreview(MeshWithMaterialRenderer mwmRenderer)
        {
            if (state != State.POPULATING)
            {
                return;
            }

            mwmRenderers.Add(mwmRenderer);

            if (mwmRenderers.Count == menuPreviewHolders.Length)
            {
                ChangeState(State.WAITING);
            }
        }

        public bool IsPopulating()
        {
            return state == State.POPULATING;
        }
    }
}
