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

using System.Collections.Generic;
using UnityEngine;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.tutorial
{
    /// <summary>
    /// Manages the execution of tutorials.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static string HAS_EVER_STARTED_TUTORIAL_KEY = "blocks_has_started_tutorial";

        /// <summary>
        /// Mesh ID of the camera alignment marker in the tutorial file.
        /// This must be a CONE. The position of the cone will indicate where the camera should be located,
        /// and the direction of the tip of the cone will indicate where it should be pointing.
        /// </summary>
        private const int CAMERA_ALIGNMENT_MARKER_MESH_ID = 100;

        private const float DISTANCE_FROM_USER = 1.5f;

        /// <summary>
        /// Delay before first step, in seconds.
        /// </summary>
        private const float DELAY_BEFORE_FIRST_STEP = 2;

        /// <summary>
        /// Delay between successive tutorial steps, in seconds.
        /// </summary>
        private const float DELAY_BETWEEN_STEPS = 2.5f;

        /// <summary>
        /// Delay after last step, in seconds.
        /// </summary>
        private const float DELAY_AFTER_LAST_STEP = 1;

        // All available tutorials, ordered.
        private List<Tutorial> tutorials = new List<Tutorial> {
      new IceCreamTutorial()
    };

        // The current tutorial being taken.
        private Tutorial currentTutorial;
        private int currentTutorialIndex;

        /// <summary>
        /// The tutorial step we're currently running. This is null if there is no current step
        /// (for example, when we're in the delay between steps).
        /// </summary>
        private ITutorialStep currentStep;

        // The index of the tutorial step we're currently running.
        private int currentStepIndex = 0;

        /// <summary>
        /// If currentStep == null, then we are counting down to advance to the next step
        /// or to end the tutorial. When this reaches 0, we advance to the next step
        /// (or finish the tutorial).
        /// </summary>
        private float countdownToAdvance;

        // Parameters to animate the scene lighting to be different for tutorials.
        // Red-Blue max value to make the sky brighter (does not use a Color because Colors are capped at
        // 1 and we want the tutorial sky brighter).
        private const float TUTORIAL_SKY_RB_VAL = 1.06f;

        // Parameters to control the duration of in and out animations.
        private const float ANIMATION_DURATION = 1.0f;
        private const float ANIMATION_DURATION_IN = 1.0f;
        private const float MOUNTAIN_ANIMATION_DURATION = 1.5f;
        private const float ANIMATION_DURATION_OUT = 3.0f;
        private const float ANIMATION_QUADRATIC = 4.0f;

        // Scale of the model when the out-animation begins.
        private float startingAnimationScale = 0f;

        // Parameters to animate the ground coloring (and fog and fresnel of distant mountains).
        private static readonly Color TUTORIAL_GROUND_DIFFUSE_A =
          new Color(255f / 255f, 235f / 255f, 219f / 255f, 255f / 255f);
        private static readonly Color TUTORIAL_GROUND_DIFFUSE_B =
          new Color(233f / 255f, 205f / 255f, 200f / 255f, 255f / 255f);
        private static readonly Color TUTORIAL_GROUND_FOG =
          new Color(255f / 255f, 230f / 255f, 219f / 255f, 255f / 255f);
        private static readonly Color TUTORIAL_GROUND_FRESNEL =
          new Color(255f / 255f, 240f / 255f, 219f / 255f, 255f / 255f);

        private static readonly Color ORIGINAL_GROUND_DIFFUSE_A =
          new Color(255f / 255f, 233f / 255f, 190f / 255f, 255f / 255f);
        private static readonly Color ORIGINAL_GROUND_DIFFUSE_B =
          new Color(233f / 255f, 205f / 255f, 176f / 255f, 255f / 255f);
        private static readonly Color ORIGINAL_GROUND_FOG =
          new Color(255f / 255f, 230f / 255f, 195f / 255f, 255f / 255f);
        private static readonly Color ORIGINAL_GROUND_FRESNEL =
          new Color(255f / 255f, 228f / 255f, 190f / 255f, 255f / 255f);

        // Height of the flat terrain (without mountains).
        private const float TERRAIN_FLAT_HEIGHT = 0.0378f;

        // Parameters to adjust the final confetti effect animation.
        private const float PITCH_MIN = 0.9f;
        private const float PITCH_MAX = 1.3f;
        private const float RADIUS_MIN = 0.15f;
        private const float RADIUS_MAX = 0.3f;
        private const float STAGGER_TIME = 0.0008f;

        private Lighting polyLighting;
        private bool animatingIn;
        private bool animatingOut;
        private float timeStartedAnimating;
        private bool animatingInMesh;
        private float timeStartedAnimatingMesh;
        private Vector3 startingAnimationOffset;
        private Vector3 finalAnimationOffset;

        /// <summary>
        /// Queue of meshes to be destroyed for the end tutorial animation, paired with a float that holds
        /// the time they should be detonated.
        /// </summary>
        private Queue<KeyValuePair<MMesh, float>> meshesToBeShattered = new Queue<KeyValuePair<MMesh, float>>();
        private ParticleSystem finishEffectPrefab;

        public void Start()
        {
            polyLighting = ObjectFinder.ComponentById<Lighting>("ID_Lighting");
            finishEffectPrefab = Resources.Load<ParticleSystem>("Tutorial/FinishEffect");
        }

        /// <summary>
        /// Returns whether or not a tutorial is currently occurring.
        /// </summary>
        /// <returns></returns>
        public bool TutorialOccurring()
        {
            return currentTutorial != null;
        }

        /// <summary>
        ///   Start a given tutorial.
        /// </summary>
        /// <param name="tutorialIndex">The index of the tutorial, 0-indexed</param>
        public void StartTutorial(int tutorialIndex)
        {
            if (tutorialIndex < 0 || tutorialIndex >= tutorials.Count)
            {
                Debug.LogError("Only " + tutorials.Count + " tutorials exist");
                return;
            }

            PlayerPrefs.SetString(HAS_EVER_STARTED_TUTORIAL_KEY, "true");
            PlayerPrefs.Save();

            PeltzerMain.Instance.CreateNewModel();

            animatingIn = true;
            timeStartedAnimating = Time.time;

            currentTutorialIndex = tutorialIndex;
            currentTutorial = tutorials[currentTutorialIndex];
            currentStep = null;

            // Prepare tutorial.
            currentTutorial.OnPrepare();

            // Install a command validator in the model so the tutorial can validate commands.
            PeltzerMain.Instance.model.OnValidateCommand += ValidateCommand;
            PeltzerMain.Instance.controllerMain.ControllerActionHandler += OnControllerEvent;

            // TODO(bug): implement other tutorials.
            currentStepIndex = 0;
            countdownToAdvance = DELAY_BEFORE_FIRST_STEP;

            // Don't show move/zoom tooltips during the tutorial.
            PeltzerMain.Instance.restrictionManager.tooltipsAllowed = false;
            PeltzerMain.Instance.paletteController.DisableGripTooltips();
            PeltzerMain.Instance.peltzerController.DisableGripTooltips();
            PeltzerMain.Instance.paletteController.DisableSnapTooltips();

            // Play a start tutorial sound effect.
            AudioLibrary audioLibrary = PeltzerMain.Instance.audioLibrary;
            audioLibrary.PlayClip(audioLibrary.tutorialIntroSound);

        }

        /// <summary>
        /// Indicates if the given command is allowed or not in the current tutorial state.
        /// In particular, if there is no tutorial currently active, this returns true.
        /// </summary>
        /// <param name="command">The command to validate</param>
        /// <returns>True if the command is allowed, false if not.</returns>
        public bool ValidateCommand(Command command)
        {
            // If no tutorial is currently active, all commands are valid.
            if (currentTutorial == null)
            {
                return true;
            }
            // Otherwise, a command is only valid if there is an active step and that step approves it.
            return currentStep != null && currentStep.OnCommand(command);
        }

        /// <summary>
        /// Exits the tutorial. Can only be called if a tutorial is in progress.
        /// </summary>
        public void ExitTutorial(bool isForceExit = false)
        {
            PeltzerMain.Instance.GetFloatingMessage().ShowProgressBar(/*show*/ false);

            AssertOrThrow.NotNull(currentTutorial, "Can't exit tutorial: not in a tutorial.");
            float curPct = 1f;
            if (animatingIn)
            {
                curPct = (Time.time - timeStartedAnimating) / ANIMATION_DURATION_IN;
                animatingIn = false;
            }

            if (isForceExit)
            {
                PeltzerMain.Instance.GetFloatingMessage().Show("Come back any time!", TextPosition.CENTER);
                PeltzerMain.Instance.GetFloatingMessage().ShowHeader("");
                PeltzerMain.Instance.GetFloatingMessage().HideAllGIFs();
            }

            animatingOut = true;
            IceCreamFinishEffect();
            startingAnimationScale = PeltzerMain.Instance.worldSpace.scale;
            startingAnimationOffset = PeltzerMain.Instance.worldSpace.offset;
            finalAnimationOffset = new Vector3(startingAnimationOffset.x, -2f, startingAnimationOffset.z);

            // Reset the state of the current step, in case the user wants to complete the tutorial again later.
            if (isForceExit && currentStep != null)
            {
                currentStep.ResetState();
            }

            timeStartedAnimating = Time.time;
            currentStep = null;
            currentTutorial = null;
            PeltzerMain.Instance.model.OnValidateCommand -= ValidateCommand;
            PeltzerMain.Instance.attentionCaller.ResetAll();

            PeltzerMain.Instance.GetSelector().DeselectAll();
            PeltzerMain.Instance.attentionCaller.StopGlowingAll();

            if (!PeltzerMain.Instance.HasEverShownFeaturedTooltip
                && !PeltzerMain.Instance.applicationButtonToolTips.IsActive())
            {
                PeltzerMain.Instance.applicationButtonToolTips.TurnOn("ViewFeatured");
                PeltzerMain.Instance.polyMenuMain.SwitchToFeaturedSection();
                PeltzerMain.Instance.HasEverShownFeaturedTooltip = true;

            }
        }

        /// <summary>
        /// Make the ice cream explode into sprinkles upon completion.
        /// </summary>
        public void IceCreamFinishEffect()
        {
            float startTime = Time.time;

            // Manually reverse the order of the meshes.
            ICollection<MMesh> meshCollection = PeltzerMain.Instance.model.GetAllMeshes();
            List<MMesh> meshes = new List<MMesh>(meshCollection.Count);

            foreach (MMesh mesh in meshCollection)
            {
                meshes.Add(mesh);
            }
            for (int i = meshes.Count - 1; i >= 0; i--)
            {
                // Push each mesh onto the particle system effect queue.
                startTime += STAGGER_TIME;
                meshesToBeShattered.Enqueue(new KeyValuePair<MMesh, float>(meshes[i], startTime));
            }
        }

        /// <summary>
        /// Process the finish effect queue; when the time has come for the mesh to be shattered, dequeue
        /// it and instantiate a relevant particle system effect.
        /// </summary>
        public void ProcessFinishEffectQueue()
        {
            if (meshesToBeShattered.Peek().Value <= Time.time)
            {
                KeyValuePair<MMesh, float> pair = meshesToBeShattered.Dequeue();
                MMesh mesh = pair.Key;

                // Only play an effect for a third of the meshes, to reduce the visual & performance overload, per bug
                if (meshesToBeShattered.Count % 3 == 0)
                {
                    // Instantiate a shatter effect.
                    ParticleSystem shatterEffect = Instantiate(finishEffectPrefab);
                    // Remove the shatter effect from the scene when it is over.
                    Destroy(shatterEffect, shatterEffect.main.duration);

                    // Play a confetti sound effect.
                    AudioLibrary audioLibrary = PeltzerMain.Instance.audioLibrary;
                    float pitch = Random.Range(PITCH_MIN, PITCH_MAX);
                    audioLibrary.PlayClip(audioLibrary.confettiSound, pitch);

                    shatterEffect.transform.position = PeltzerMain.Instance.worldSpace.ModelToWorld(mesh.offset);
                    ParticleSystem.ShapeModule shapeModule = shatterEffect.shape;
                    shapeModule.radius = Random.Range(RADIUS_MIN, RADIUS_MAX);
                    int materialId = mesh.GetFace(0).properties.materialId;
                    if (materialId == MaterialRegistry.PINK_WIREFRAME_ID
                      || materialId == MaterialRegistry.GREEN_WIREFRAME_ID)
                    {
                        materialId = MaterialRegistry.RED_ID;
                    }
                    shatterEffect.GetComponent<ParticleSystemRenderer>().material =
                      MaterialRegistry.GetMaterialWithAlbedoById(materialId);
                }

                // Hide the mesh- safe because we will soon call CreateNewModel() when the tutorial is finished exitting,
                // deleting any remaining meshes.
                PeltzerMain.Instance.model.HideMeshForTestOrTutorial(mesh.id);
            }
        }

        private void AdvanceToNextStep()
        {
            if (currentStepIndex >= currentTutorial.steps.Count)
            {
                // End of tutorial.
                ExitTutorial();
            }
            else
            {
                // Advance to next step.
                currentStep = currentTutorial.steps[currentStepIndex];
                currentStepIndex++;
                // Prepare the step for display.
                currentStep.OnPrepare();
            }
        }

        /// <summary>
        /// Exits the tutorial if there is a peltzer controller trigger pull while the user
        /// is hovering over the tutorial exit button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnControllerEvent(object sender, ControllerEventArgs args)
        {

        }

        private bool IsGrabEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.DOWN;
        }

        // Fade the terrain colors to those of the tutorial environment.
        private void AnimateTerrain(GameObject terrain, float pct)
        {
            if (animatingIn)
            {
                terrain.GetComponent<Renderer>().material.SetVector("_DiffuseColorA",
                  Color.Lerp(ORIGINAL_GROUND_DIFFUSE_A, TUTORIAL_GROUND_DIFFUSE_A, pct));
                terrain.GetComponent<Renderer>().material.SetVector("_DiffuseColorB",
                  Color.Lerp(ORIGINAL_GROUND_DIFFUSE_B, TUTORIAL_GROUND_DIFFUSE_B, pct));
                terrain.GetComponent<Renderer>().material.SetVector("_FogColor",
                  Color.Lerp(ORIGINAL_GROUND_FOG, TUTORIAL_GROUND_FOG, pct));
                terrain.GetComponent<Renderer>().material.SetVector("_FresnelColor",
                  Color.Lerp(ORIGINAL_GROUND_FRESNEL, TUTORIAL_GROUND_FRESNEL, pct));
            }
            else
            {
                terrain.GetComponent<Renderer>().material.SetVector("_DiffuseColorA",
                  Color.Lerp(TUTORIAL_GROUND_DIFFUSE_A, ORIGINAL_GROUND_DIFFUSE_A, pct));
                terrain.GetComponent<Renderer>().material.SetVector("_DiffuseColorB",
                  Color.Lerp(TUTORIAL_GROUND_DIFFUSE_B, ORIGINAL_GROUND_DIFFUSE_B, pct));
                terrain.GetComponent<Renderer>().material.SetVector("_FogColor",
                  Color.Lerp(TUTORIAL_GROUND_FOG, ORIGINAL_GROUND_FOG, pct));
                terrain.GetComponent<Renderer>().material.SetVector("_FresnelColor",
                  Color.Lerp(TUTORIAL_GROUND_FRESNEL, ORIGINAL_GROUND_FRESNEL, pct));
            }
        }

        private void Update()
        {
            if (animatingIn)
            {
                float pct = (Time.time - timeStartedAnimating) / ANIMATION_DURATION_IN;
                if (pct >= 1)
                {
                    pct = 1;
                    animatingIn = false;
                }
                else
                {
                    float r = Mathf.Lerp(1f, TUTORIAL_SKY_RB_VAL, pct);
                    float b = Mathf.Lerp(1f, TUTORIAL_SKY_RB_VAL, pct);
                    RenderSettings.skybox.SetVector("_Tint", new Vector4(r, 1f, b, 1f));

                    // Fade ground in.
                    GameObject terrain = ObjectFinder.ObjectById("ID_TerrainLift").transform.Find("Terrain2-Best").gameObject;
                    GameObject terrainWithoutMountains = ObjectFinder.ObjectById("ID_TerrainNoMountains").gameObject;
                    AnimateTerrain(terrain, pct);
                    AnimateTerrain(terrainWithoutMountains, pct);

                    // Shrink mountains with a Bezier curve ease.
                    float mountainPct =
                      Math3d.CubicBezierEasing(0f, 0f, 0.2f, 1f, (Time.time - timeStartedAnimating) / ANIMATION_DURATION_IN);
                    float y = Mathf.Lerp(1f, TERRAIN_FLAT_HEIGHT, mountainPct);
                    ObjectFinder.ObjectById("ID_TerrainLift").transform.localScale = new Vector3(1f, y, 1f);

                    // Don't do anything else while animating in.
                    return;
                }
            }
            else if (meshesToBeShattered.Count > 0)
            {
                // Process any meshes that are queued to be shattered in the tutorial ending animation.
                ProcessFinishEffectQueue();
            }
            else if (animatingOut)
            {
                float pct = (Time.time - timeStartedAnimating) / ANIMATION_DURATION_OUT;
                if (pct >= 1)
                {
                    pct = 1;
                    animatingOut = false;

                    // Remove all restrictions so the user is back to the fully functional mode.
                    PeltzerMain.Instance.restrictionManager.AllowAll();
                    // Once the exit animations are done start a new model.
                    PeltzerMain.Instance.CreateNewModel();
                    PeltzerMain.Instance.GetFloatingMessage().FadeOutBillboard();
                }
                else
                {
                    // Return the sky to the correct color.
                    float r = Mathf.Lerp(TUTORIAL_SKY_RB_VAL, 1f, pct);
                    float b = Mathf.Lerp(TUTORIAL_SKY_RB_VAL, 1f, pct);
                    RenderSettings.skybox.SetVector("_Tint", new Vector4(r, 1f, b, 1f));

                    // Fade ground out.
                    GameObject terrain = ObjectFinder.ObjectById("ID_TerrainLift").transform.Find("Terrain2-Best").gameObject;
                    GameObject terrainWithoutMountains = ObjectFinder.ObjectById("ID_TerrainNoMountains").gameObject;
                    AnimateTerrain(terrain, pct);
                    AnimateTerrain(terrainWithoutMountains, pct);

                    // Raise mountains with a Bezier curve ease.
                    float mountainPct =
                      Math3d.CubicBezierEasing(0f, 0f, 0.2f, 1f, (Time.time - timeStartedAnimating) / ANIMATION_DURATION_OUT);
                    float y = Mathf.Lerp(TERRAIN_FLAT_HEIGHT, 1f, mountainPct);
                    ObjectFinder.ObjectById("ID_TerrainLift").transform.localScale = new Vector3(1f, y, 1f);
                }
            }

            if (animatingInMesh)
            {
                float pct = Math3d.CubicBezierEasing(0f, 0f, 0.2f, 1f, (Time.time - timeStartedAnimatingMesh) / ANIMATION_DURATION_IN);
                if (pct >= 1)
                {
                    pct = 1;
                    animatingInMesh = false;
                }
                PeltzerMain.Instance.worldSpace.scale = Mathf.Lerp(0.5f, 1, pct);
            }

            if (currentTutorial == null)
            {
                // No tutorial is currently active, so there's nothing to do.
                return;
            }

            if (currentStep == null)
            {
                // We are counting down to the next step or to the end of the tutorial.
                if ((countdownToAdvance -= Time.deltaTime) <= 0)
                {
                    // Time to advance.
                    AdvanceToNextStep();
                }
            }
            else if (currentStep.OnValidate())
            {
                // User completed step. Start counting down to the next step.
                currentStep.OnFinish();
                currentStep = null;
                countdownToAdvance = (currentStepIndex >= currentTutorial.steps.Count) ?
                    DELAY_AFTER_LAST_STEP : DELAY_BETWEEN_STEPS;
            }
        }

        /// <summary>
        /// Loads a tutorial file name from the resources folder and rotates/translates the world so that model
        /// appears in a static position in front of the billboard.
        /// </summary>
        /// <param name="fileName">The resources path of the tutorial file name to load.</param>
        public void LoadAndAlignTutorialModel(string path, Vector3 anchorDirection, Vector3 anchorPosition)
        {
            PeltzerMain.Instance.LoadPeltzerFileFromResources(path, /*resetAttentionCaller*/ false, /*resetRestrictions*/ false);
            animatingInMesh = true;
            // Play a sound effect for the model animating in.
            AudioLibrary audioLibrary = PeltzerMain.Instance.audioLibrary;
            audioLibrary.PlayClip(audioLibrary.tutorialMeshAnimateInSound);

            timeStartedAnimatingMesh = Time.time;

            Quaternion rotationalOffset = Quaternion.AngleAxis(45f, Vector3.up);

            // Look for the camera marker mesh.
            MMesh marker = PeltzerMain.Instance.model.GetMesh(CAMERA_ALIGNMENT_MARKER_MESH_ID);
            Vector3 markerPos = marker.offset;

            // Figure out where the tip of the cone is pointing. This will give us the direction the camera has to point.
            Vector3 tipPos = marker.VertexPositionInModelCoords(FindTipOfCone(marker));

            // The "forward" vector we want is the vector that goes from the marker position to the tip position.
            Vector3 desiredForward = tipPos - markerPos;

            Quaternion fullRotation = Quaternion.FromToRotation(desiredForward, anchorDirection);

            // Set the world rotation such that desiredForward is rotated into cameraForward.
            PeltzerMain.Instance.worldSpace.rotation = Quaternion.Euler(0f, fullRotation.eulerAngles.y, 0f);

            // Delete the marker.
            PeltzerMain.Instance.model.DeleteMesh(CAMERA_ALIGNMENT_MARKER_MESH_ID);

            // Also adjust the world's translation such that the camera coincides with the marker.
            Vector3 markerWorldPos = PeltzerMain.Instance.worldSpace.ModelToWorld(markerPos);
            PeltzerMain.Instance.worldSpace.offset = anchorPosition - markerWorldPos;
            PeltzerMain.Instance.worldSpace.scale = 0.3f;
        }

        /// <summary>
        /// Find the tip of the given cone mesh.
        /// The tip is defined as the vertex with the highest degree (vertex that belongs to most faces).
        /// </summary>
        /// <param name="cone">The cone whose tip is to be found.</param>
        /// <returns>The vertex ID of the tip of the cone.</returns>
        private static int FindTipOfCone(MMesh cone)
        {
            // The tip of the cone is the vertex with highest degree (belongs to the most faces).
            Dictionary<int, int> degreeOf = new Dictionary<int, int>();
            int winner = -1;
            foreach (Face face in cone.GetFaces())
            {
                foreach (int vertexId in face.vertexIds)
                {
                    int currentValue;
                    degreeOf[vertexId] = degreeOf.TryGetValue(vertexId, out currentValue) ?
                      currentValue + 1 : 1;
                    if (winner < 0 || degreeOf[vertexId] > degreeOf[winner])
                    {
                        winner = vertexId;
                    }
                }
            }
            AssertOrThrow.True(winner >= 0, "Could not find tip of cone in mesh.");
            return winner;
        }
    }
}
