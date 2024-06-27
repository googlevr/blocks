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
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.tutorial
{
    public enum TextPosition
    {
        NONE, CENTER, HALF_SIDE, FULL_SIDE, BOTTOM, CENTER_NO_TITLE
    };

    /// <summary>
    /// Floating message that gets shown in front of the user to give tutorial instructions.
    /// </summary>
    public class FloatingMessage : MonoBehaviour
    {
        /// <summary>
        /// Fixed distance to keep from origin.
        /// </summary>
        private const float DISTANCE_FROM_USER = 2.5f;

        /// <summary>
        /// Maximum angle between message and camera. If the angle becomes bigger, the message moves to follow
        /// the camera's heading.
        /// </summary>
        private const float MAX_ANGLE_FROM_CAMERA = 45.0f;

        /// <summary>
        /// Duration of the fade-in animation when showing a new message.
        /// </summary>
        private const float FADE_IN_DURATION = 0.5f;

        /// <summary>
        /// Starting alpha value for the fade in animation.
        /// </summary>
        private const float FADE_START_ALPHA = 0.7f;

        /// <summary>
        /// String prefix for source of the progress bar images. Intermediate images are named "bar_1", "bar_2",
        /// etc. and are bookended by "bar_empty" and "bar_full".
        /// </summary>
        private const string PROGRESS_BAR_IMAGE_PATH_PREFIX = "Tutorial/Textures/bar_";

        /// <summary>
        /// Max number of progress bar images; used to check that we are not over-incrementing and attempting
        /// to find an image that does not actually exist.
        /// </summary>
        private const int PROGRESS_BAR_MAX = 13;

        /// <summary>
        /// Current fill index of the progress bar.
        /// </summary>
        private int currentProgressBarCount = 0;

        private static readonly Color TEXT_COLOR_NORMAL = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        private static readonly Color TEXT_COLOR_SUCCESS = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        private static readonly Color BACKGROUND_NORMAL = new Color(1.0f, 1.0f, 1.0f, 0.8f);
        private static readonly Color BACKGROUND_SUCCESS = new Color(86f / 255f, 196f / 255f, 22f / 255f, 1.0f);

        public GameObject billboardHolder;
        public GameObject billboard;
        public GameObject rocks;
        private TextMeshPro fullSideMessageText;
        private TextMeshPro halfSideMessageText;
        private TextMeshPro centerMessageText;
        private TextMeshPro centerNoTitleMessageText;
        private TextMeshPro bottomMessageText;
        private TextMeshPro headerText;
        private GameObject progressBarHolder;
        private RawImage messageProgressBar;

        // Particle systems that make up the confetti effect.
        private ParticleSystem confetti1;
        private ParticleSystem confetti2;
        private ParticleSystem confetti3;
        private ParticleSystem confetti4;
        private ParticleSystem finalConfetti1;
        private ParticleSystem finalConfetti2;
        private ParticleSystem finalConfetti3;
        private ParticleSystem finalConfetti4;
        private float CONFETTI_RADIUS_MIN = 0.25f;
        private float CONFETTI_RADIUS_MAX = 0.65f;
        private float CONFETTI_RADIUS_FINAL = 1f;

        // Animations associated with various messages.
        private GameObject moveGIF;
        private GameObject zoomGIF;
        private GameObject selectSphereGIF;
        private GameObject insertSphereGIF;
        private GameObject smallerSphereGIF;
        private GameObject insertAnotherSphereGIF;
        private GameObject choosePaintbrushGIF;
        private GameObject chooseColorGIF;
        private GameObject paintColorGIF;
        private GameObject chooseGrabGIF;
        private GameObject multiselectGIF;
        private GameObject copyGIF;
        private GameObject chooseEraserGIF;
        private GameObject eraseGIF;

        // Animating the message's position and rotation.
        private const float ANIMATION_DURATION = 1.5f;
        // Animating the message appearing/disappearing.
        private const float ANIMATION_SCALE_DURATION = 0.3f;
        // Time to wait before hiding the post.
        private const float WAIT_TO_HIDE_DURATION = 0.25f;
        // The power of the quadratic function used to animate.
        private const float ANIMATION_QUADRATIC = 5f;

        private bool animating;
        private float timeStartedAnimating;
        private Vector3 positionAtAnimationStart;
        private Quaternion rotationAtAnimationStart;
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        /// <summary>
        /// Indicates at what rotation (from forward) we are currently showing the message.
        /// </summary>
        private Quaternion messageRotation = Quaternion.identity;

        /// <summary>
        /// Time (as given by Time.realtimeSinceStartup) when the last message was shown.
        /// Used to compute animation.
        /// </summary>
        private float messageShownTime;

        /// <summary>
        /// The time that the post should be hidden.
        /// </summary>
        private float timeToHide;
        /// <summary>
        /// Whether we are animating the post out by scaling it down.
        /// </summary>
        private bool isScaleOutAnimation;
        /// <summary>
        /// Whether we are animating the post in by scaling it up.
        /// </summary>
        private bool isScaleInAnimation;
        /// <summary>
        /// Whether we are animating the post by rotating it either up or down.
        /// </summary>
        private bool isRotationAnimation;
        /// <summary>
        /// Whether the post is waiting to hide.
        /// </summary>
        private bool waitingToHide;

        /// <summary>
        /// Advances the FloatingMessage progress bar by incrementing the state index and fetching the
        /// new image.
        /// </summary>
        public void IncrementProgressBar()
        {
            currentProgressBarCount += 1;
            // Make sure we do not look for a image that does not exist.
            if (currentProgressBarCount > PROGRESS_BAR_MAX)
            {
                messageProgressBar.texture = Resources.Load<Texture>(PROGRESS_BAR_IMAGE_PATH_PREFIX + "full");
                return;
            }
            string progressBarImagePath = PROGRESS_BAR_IMAGE_PATH_PREFIX + currentProgressBarCount.ToString();
            messageProgressBar.texture = Resources.Load<Texture>(progressBarImagePath);
        }

        /// <summary>
        /// Stops all confetti particle systems.
        /// </summary>
        private void StopConfetti()
        {
            confetti1.Stop();
            confetti2.Stop();
            confetti3.Stop();
            confetti4.Stop();
            finalConfetti1.Stop();
            finalConfetti2.Stop();
            finalConfetti3.Stop();
            finalConfetti4.Stop();
        }

        private void PlayAndSizeConfetti(ParticleSystem confetti, float size)
        {
            ParticleSystem.ShapeModule shape = confetti.shape;
            shape.radius = size;
            confetti.Play();
        }

        /// <summary>
        /// Plays a celebratory confetti effect.
        /// </summary>
        public void PlayConfetti()
        {
            PlayAndSizeConfetti(confetti1, UnityEngine.Random.Range(CONFETTI_RADIUS_MIN, CONFETTI_RADIUS_MAX));
            PlayAndSizeConfetti(confetti2, UnityEngine.Random.Range(CONFETTI_RADIUS_MIN, CONFETTI_RADIUS_MAX));
            PlayAndSizeConfetti(confetti3, UnityEngine.Random.Range(CONFETTI_RADIUS_MIN, CONFETTI_RADIUS_MAX));
            PlayAndSizeConfetti(confetti4, UnityEngine.Random.Range(CONFETTI_RADIUS_MIN, CONFETTI_RADIUS_MAX));
        }

        public void PlayFinalConfetti()
        {
            PlayAndSizeConfetti(finalConfetti1, CONFETTI_RADIUS_FINAL);
            PlayAndSizeConfetti(finalConfetti2, CONFETTI_RADIUS_FINAL);
            PlayAndSizeConfetti(finalConfetti3, CONFETTI_RADIUS_FINAL);
            PlayAndSizeConfetti(finalConfetti4, CONFETTI_RADIUS_FINAL);
        }

        public void PositionBillboard()
        {
            timeStartedAnimating = Time.time;
            animating = true;
            isScaleInAnimation = true;
            isScaleOutAnimation = false;
            waitingToHide = false;

            // Move the post so it's in front of the user.
            Quaternion yRotation = Quaternion.Euler(0f, Camera.main.transform.rotation.eulerAngles.y, 0f);
            Vector3 forwardPosition = Camera.main.transform.position + (yRotation * Vector3.forward * DISTANCE_FROM_USER);
            billboardHolder.transform.position =
              new Vector3(forwardPosition.x, billboardHolder.transform.position.y, forwardPosition.z);

            // Rotate the post so its pointing towards the user but laying on the ground.
            float yAngle = Camera.main.transform.eulerAngles.y;
            billboard.transform.rotation = Quaternion.Euler(90f, yAngle, 0);

            rotationAtAnimationStart = billboard.transform.rotation;
            targetRotation = Quaternion.Euler(0f, yAngle, 0f);

            billboard.transform.localScale = Vector3.zero;
            rocks.transform.localScale = Vector3.zero;
            billboardHolder.SetActive(true);
            StopConfetti();
        }

        public void FadeOutBillboard()
        {
            timeStartedAnimating = Time.time;
            animating = true;
            waitingToHide = true;

            float yAngle = Camera.main.transform.eulerAngles.y;

            rotationAtAnimationStart = billboard.transform.rotation;
            targetRotation = Quaternion.Euler(90f, yAngle, 0f);
        }

        /// <summary>
        /// Reset the progress bar to an empty state.
        /// </summary>
        public void ResetProgressBar()
        {
            messageProgressBar.texture = Resources.Load<Texture>(PROGRESS_BAR_IMAGE_PATH_PREFIX + "empty");
            currentProgressBarCount = 0;
        }

        public void ShowHeader(string header)
        {
            headerText.SetText(header);
        }

        /// <summary>
        /// Shows a message in the given style.
        /// </summary>
        /// <param name="message">The message to show.</param>
        /// <param name="playConfetti">Whether or not to play a confetti effect.</param>
        /// <param name="showHeader">Whether or not to show the header phrase.</param>
        public void Show(string message, TextPosition textPosition, bool playConfetti = false,
          bool showHeader = false)
        {
            bool wasActive = billboardHolder.activeSelf;
            billboardHolder.SetActive(true);

            // Show the message and set up the background color for the box.
            if (textPosition == TextPosition.CENTER)
            {
                centerMessageText.SetText(message);
                centerNoTitleMessageText.SetText("");
                fullSideMessageText.SetText("");
                halfSideMessageText.SetText("");
                bottomMessageText.SetText("");
            }
            else if (textPosition == TextPosition.HALF_SIDE)
            {
                centerMessageText.SetText("");
                centerNoTitleMessageText.SetText("");
                fullSideMessageText.SetText("");
                halfSideMessageText.SetText(message);
                bottomMessageText.SetText("");
            }
            else if (textPosition == TextPosition.FULL_SIDE)
            {
                centerMessageText.SetText("");
                centerNoTitleMessageText.SetText("");
                fullSideMessageText.SetText(message);
                halfSideMessageText.SetText("");
                bottomMessageText.SetText("");
            }
            else if (textPosition == TextPosition.BOTTOM)
            {
                centerMessageText.SetText("");
                centerNoTitleMessageText.SetText("");
                fullSideMessageText.SetText("");
                halfSideMessageText.SetText("");
                bottomMessageText.SetText(message);
            }
            else if (textPosition == TextPosition.CENTER_NO_TITLE)
            {
                centerMessageText.SetText("");
                centerNoTitleMessageText.SetText(message);
                fullSideMessageText.SetText("");
                halfSideMessageText.SetText("");
                bottomMessageText.SetText("");
            }

            messageShownTime = Time.realtimeSinceStartup;

            if (playConfetti)
            {
                PlayConfetti();
            }

            if (!wasActive)
            {
                StopConfetti();
            }
        }

        /// <summary>
        /// Hides the message that's currently being shown.
        /// </summary>
        public void Hide()
        {
            billboardHolder.SetActive(false);
        }

        public void ShowProgressBar(bool show)
        {
            progressBarHolder.SetActive(show);
        }

        public void ShowGIF(String name)
        {
            // turn off other GIF instances.
            HideAllGIFs();

            // Turn on specified instance
            switch (name)
            {
                case "MOVE":
                    moveGIF.SetActive(true);
                    break;
                case "ZOOM":
                    zoomGIF.SetActive(true);
                    break;
                case "SELECT_SPHERE":
                    selectSphereGIF.SetActive(true);
                    break;
                case "INSERT_SPHERE":
                    insertSphereGIF.SetActive(true);
                    break;
                case "SMALLER_SPHERE":
                    smallerSphereGIF.SetActive(true);
                    break;
                case "INSERT_ANOTHER_SPHERE":
                    insertAnotherSphereGIF.SetActive(true);
                    break;
                case "CHOOSE_PAINTBRUSH":
                    choosePaintbrushGIF.SetActive(true);
                    break;
                case "CHOOSE_COLOR":
                    chooseColorGIF.SetActive(true);
                    break;
                case "PAINT_COLOR":
                    paintColorGIF.SetActive(true);
                    break;
                case "CHOOSE_GRAB":
                    chooseGrabGIF.SetActive(true);
                    break;
                case "MULTISELECT":
                    multiselectGIF.SetActive(true);
                    break;
                case "COPY":
                    copyGIF.SetActive(true);
                    break;
                case "CHOOSE_ERASER":
                    chooseEraserGIF.SetActive(true);
                    break;
                case "ERASE":
                    eraseGIF.SetActive(true);
                    break;
            }
        }

        /// <summary>
        ///   Hide all instances of the GIF objects.
        /// </summary>
        public void HideAllGIFs()
        {
            moveGIF.SetActive(false);
            zoomGIF.SetActive(false);
            selectSphereGIF.SetActive(false);
            insertSphereGIF.SetActive(false);
            smallerSphereGIF.SetActive(false);
            insertAnotherSphereGIF.SetActive(false);
            choosePaintbrushGIF.SetActive(false);
            chooseColorGIF.SetActive(false);
            paintColorGIF.SetActive(false);
            chooseGrabGIF.SetActive(false);
            multiselectGIF.SetActive(false);
            copyGIF.SetActive(false);
            chooseEraserGIF.SetActive(false);
            eraseGIF.SetActive(false);
        }

        public void Setup()
        {
            billboard = ObjectFinder.ObjectById("ID_Billboard");
            billboardHolder = ObjectFinder.ObjectById("ID_BillboardHolder");
            rocks = ObjectFinder.ObjectById("ID_Rocks");
            fullSideMessageText = ObjectFinder.ComponentById<TextMeshPro>("ID_BillboardFullSideText");
            halfSideMessageText = ObjectFinder.ComponentById<TextMeshPro>("ID_BillboardHalfSideText");
            centerMessageText = ObjectFinder.ComponentById<TextMeshPro>("ID_BillboardCenterText");
            centerNoTitleMessageText = ObjectFinder.ComponentById<TextMeshPro>("ID_BillboardCenterNoTitleText");
            bottomMessageText = ObjectFinder.ComponentById<TextMeshPro>("ID_BillboardBottomText");
            headerText = ObjectFinder.ComponentById<TextMeshPro>("ID_BillboardHeaderText");
            messageProgressBar = ObjectFinder.ComponentById<RawImage>("ID_TutorialProgressBar");
            progressBarHolder = ObjectFinder.ObjectById("ID_ProgressBarCanvas");

            confetti1 = ObjectFinder.ComponentById<ParticleSystem>("ID_Confetti1");
            confetti2 = ObjectFinder.ComponentById<ParticleSystem>("ID_Confetti2");
            confetti3 = ObjectFinder.ComponentById<ParticleSystem>("ID_Confetti3");
            confetti4 = ObjectFinder.ComponentById<ParticleSystem>("ID_Confetti4");

            finalConfetti1 = ObjectFinder.ComponentById<ParticleSystem>("ID_FinalConfetti1");
            finalConfetti2 = ObjectFinder.ComponentById<ParticleSystem>("ID_FinalConfetti2");
            finalConfetti3 = ObjectFinder.ComponentById<ParticleSystem>("ID_FinalConfetti3");
            finalConfetti4 = ObjectFinder.ComponentById<ParticleSystem>("ID_FinalConfetti4");

            moveGIF = ObjectFinder.ComponentById<Transform>("ID_MOVE_GIF").gameObject;
            zoomGIF = ObjectFinder.ComponentById<Transform>("ID_ZOOM_GIF").gameObject;
            selectSphereGIF = ObjectFinder.ComponentById<Transform>("ID_SELECT_SPHERE_GIF").gameObject;
            insertSphereGIF = ObjectFinder.ComponentById<Transform>("ID_INSERT_SPHERE_GIF").gameObject;
            smallerSphereGIF = ObjectFinder.ComponentById<Transform>("ID_SMALLER_SPHERE_GIF").gameObject;
            insertAnotherSphereGIF = ObjectFinder.ComponentById<Transform>("ID_INSERT_ANOTHER_SPHERE_GIF").gameObject;
            choosePaintbrushGIF = ObjectFinder.ComponentById<Transform>("ID_CHOOSE_PAINTBRUSH_GIF").gameObject;
            chooseColorGIF = ObjectFinder.ComponentById<Transform>("ID_CHOOSE_COLOR_GIF").gameObject;
            paintColorGIF = ObjectFinder.ComponentById<Transform>("ID_PAINT_COLOR_GIF").gameObject;
            chooseGrabGIF = ObjectFinder.ComponentById<Transform>("ID_CHOOSE_GRAB_GIF").gameObject;
            multiselectGIF = ObjectFinder.ComponentById<Transform>("ID_MULTISELECT_GIF").gameObject;
            copyGIF = ObjectFinder.ComponentById<Transform>("ID_COPY_GIF").gameObject;
            chooseEraserGIF = ObjectFinder.ComponentById<Transform>("ID_CHOOSE_ERASER_GIF").gameObject;
            eraseGIF = ObjectFinder.ComponentById<Transform>("ID_ERASE_GIF").gameObject;
            // Hide the GIFs
            HideAllGIFs();

            // Make sure the confetti is not playing.
            StopConfetti();

            // Always start with an empty progress bar.
            messageProgressBar.texture = Resources.Load<Texture>(PROGRESS_BAR_IMAGE_PATH_PREFIX + "empty");

            // Start inactive.
            billboardHolder.SetActive(false);
        }

        private void Update()
        {
            if (!billboard.activeInHierarchy) return;

            if (animating)
            {
                if (isScaleInAnimation)
                {
                    float pctDone =
                      Math3d.CubicBezierEasing(0f, 0f, 0.2f, 1f, (Time.time - timeStartedAnimating) / ANIMATION_SCALE_DURATION);
                    if (pctDone > 1)
                    {
                        billboard.transform.localScale = Vector3.one;
                        rocks.transform.localScale = Vector3.one;
                        isScaleInAnimation = false;
                        isRotationAnimation = true;
                        timeStartedAnimating = Time.time;
                    }
                    else
                    {
                        billboard.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, pctDone);
                        rocks.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, pctDone);
                    }
                }
                else if (isRotationAnimation)
                {
                    float pctDone =
                      Math3d.CubicBezierEasing(0f, 0f, 0f, 1f, (Time.time - timeStartedAnimating) / ANIMATION_DURATION);
                    if (pctDone > 1)
                    {
                        billboard.transform.rotation = targetRotation;
                        AudioLibrary audioLibrary = PeltzerMain.Instance.audioLibrary;
                        audioLibrary.PlayClip(audioLibrary.genericSelectSound);
                        if (waitingToHide)
                        {
                            isScaleOutAnimation = true;
                            isRotationAnimation = false;
                            timeStartedAnimating = Time.time;
                        }
                        else
                        {
                            animating = false;
                        }
                    }
                    else
                    {
                        billboard.transform.rotation = Quaternion.Lerp(rotationAtAnimationStart, targetRotation, pctDone);
                    }
                }
                else if (isScaleOutAnimation)
                {
                    float pctDone =
                      Math3d.CubicBezierEasing(0f, 0f, 0.2f, 1f, (Time.time - timeStartedAnimating) / ANIMATION_SCALE_DURATION);

                    if (pctDone > 1)
                    {
                        animating = false;
                        Hide();
                    }
                    else
                    {
                        billboard.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, pctDone);
                        rocks.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, pctDone);
                    }
                }
            }
        }
    }
}
