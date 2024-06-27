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
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.tutorial;

namespace com.google.apps.peltzer.client.menu {
  /// <summary>
  /// Handles the choreography of the intro animation.
  ///
  /// The intro animation starts in darkness with the Blocks logo assembling from shards as we play the intro
  /// sound. After the logo assembles, the lighting changes to show the environment and keep the Blocks logo
  /// on the screen for a few seconds.
  /// </summary>
  public class IntroChoreographer : MonoBehaviour {
    /// <summary>
    /// Duration of introduction animation (Blocks logo assembling together).
    /// </summary>
    private const float INTRO_ANIMATION_DURATION = 10.0f;

    /// <summary>
    /// Speed scale of the intro animation (tweaked to match it with the intro sound).
    /// </summary>
    private const float INTRO_ANIMATION_SPEED_SCALE = 0.75f;

    /// <summary>
    /// How long to display the Blocks logo after the intro animation.
    /// </summary>
    private const float INTRO_LOGO_DURATION = 0.7f;

    /// <summary>
    /// How long the lighting change takes (from dark to light).
    /// </summary>
    private const float LIGHTING_CHANGE_DURATION = 1.0f;

    /// <summary>
    /// How long the terrain animation takes to complete.
    /// </summary>
    private const float TERRAIN_ANIMATION_DURATION = 2.0f;

    /// <summary>
    /// Minimum amount of time user should hold down the triggers to skip intro.
    /// </summary>
    private const float SKIP_INTRO_TRIGGER_HOLD_DURATION = 0.25f;

    /// <summary>
    ///   Library for playing sounds.
    /// </summary>
    private AudioLibrary audioLibrary;
    /// <summary>
    ///   Peltzer controller, to allow user to skip intro.
    /// </summary>
    private PeltzerController peltzerController;
    /// <summary>
    ///   Palette controller, to allow user to skip intro.
    /// </summary>
    private PaletteController paletteController;
    /// <summary>
    /// Time user has spent holding the triggers down.
    /// </summary>
    private float triggerTime;

    public Color StartSkyColor = new Color32(22, 23, 33, 255);
    public Color StartGroundColor = new Color32(23, 23, 33, 255);

    // GameObjects that choreograph (obtained during Setup()):
    private GameObject introAnim;
    private GameObject introLogo;
    private GameObject introLogoLine1;
    private GameObject environment;
    private GameObject terrainLift;
    private GameObject terrainFloor;
    private GameObject dust;

    public enum State {
      // Playing the intro animation (Blocks logo floating and assembling).
      INTRO_ANIMATION,
      // Animating lighting change,
      INTRO_LIGHTING,
      // Showing the intro logo ("Blocks by Google")
      INTRO_LOGO,
      // Done with intro choreography.
      DONE,
    }

    public State state {get; private set;}

    /// <summary>
    /// Countdown to next state transtiion.
    /// </summary>
    private float countdown = INTRO_ANIMATION_DURATION;

    /// <summary>
    /// True if Setup() was called.
    /// </summary>
    private bool setupDone;

    /// <summary>
    /// True when intro animation is complete.
    /// </summary>
    public bool introIsComplete = false;


    /// <summary>
    /// True if the user's Zandria creations should be loaded once the start up animation finishes.
    /// </summary>
    public bool loadCreationsWhenDone;

    /// <summary>
    /// Initial setup. Must be called before anything else.
    /// </summary>
    public void Setup(AudioLibrary audioLibrary, PeltzerController peltzerController,
      PaletteController paletteController) {
      this.audioLibrary = audioLibrary;
      this.peltzerController = peltzerController;
      this.paletteController = paletteController;
      triggerTime = 0;
      introAnim = ObjectFinder.ObjectById("ID_IntroAnim");
      introLogo = ObjectFinder.ObjectById("ID_IntroLogo");
      introLogoLine1 = ObjectFinder.ObjectById("ID_Logo_Line_1");
      environment = ObjectFinder.ObjectById("ID_Environment");
      terrainLift = ObjectFinder.ObjectById("ID_TerrainLift");
      terrainFloor = ObjectFinder.ObjectById("ID_TerrainNoMountains");
      dust = ObjectFinder.ObjectById("ID_Dust");
      dust.SetActive(false);

      // Forbid everything initially (until the user selects an item from the menu).
      PeltzerMain.Instance.restrictionManager.ForbidAll();

      ChangeState(State.INTRO_ANIMATION);
      setupDone = true;

      // Initially false until the user is authenticated by PeltzerMain.
      loadCreationsWhenDone = false;
    }

    private void Update() {
      if (!setupDone) return;

      // Hold the triggers for the set duration to skip the intro.
      bool paletteTriggerDown = PaletteController.AcquireIfNecessary(ref paletteController)
        && paletteController.controller.IsPressed(ButtonId.Trigger);
      bool peltzerTriggerDown = PeltzerController.AcquireIfNecessary(ref peltzerController)
        && peltzerController.controller.IsPressed(ButtonId.Trigger);
      if (peltzerTriggerDown && paletteTriggerDown) {
        if (triggerTime == 0) {
          // Start tracking the time spent with the triggers held.
          triggerTime = Time.time;
        } else if (Time.time - triggerTime > SKIP_INTRO_TRIGGER_HOLD_DURATION) {
          // Skip the rest of the intro.
          ChangeState(State.DONE);
        }
      } else if (triggerTime > 0) {
        // One or both of the triggers were released, reset the trigger time to 0.
        triggerTime = 0;
      }

      // Note: Update() stops being called once we go into the DONE state because we set MonoBehavior.enabled = false
      // when we go into that state.

      countdown -= Time.deltaTime;
      switch (state) {
        case State.INTRO_ANIMATION:
          // Check to see if it's time to advance.
          if (countdown <= 0) ChangeState(State.INTRO_LOGO);
          break;
        case State.INTRO_LOGO:
          // Check to see if it's time to advance.
          introLogo.transform.LookAt(PeltzerMain.Instance.hmd.transform);
          float fadeInPct = Mathf.Max(0.0f, Mathf.Min(1.0f, 1 - countdown/INTRO_LOGO_DURATION));
          TextMesh textLine1 = introLogoLine1.GetComponent<TextMesh>();
          textLine1.color = new Color(textLine1.color.r, textLine1.color.g, textLine1.color.b, fadeInPct);
          if (countdown <= 0) ChangeState(State.INTRO_LIGHTING);
          break;
        case State.INTRO_LIGHTING:
          // Set the skybox lighting to animate from darkness to light.
          SetSkyboxLightFactor(Mathf.Clamp01(1.0f - (countdown / LIGHTING_CHANGE_DURATION)));
          // Check to see if it's time to advance.
          if ((countdown + TERRAIN_ANIMATION_DURATION) <= 0) ChangeState(State.DONE);
          break;
        default:
          break;
      }
    }

    private void ChangeState(State newState) {
      state = newState;
      switch (newState) {
        case State.INTRO_ANIMATION:
          PeltzerMain.Instance.GetMover().currentMoveType = tools.Mover.MoveType.NONE;
          PeltzerMain.Instance.paletteController.ChangeTouchpadOverlay(TouchpadOverlay.NONE);
          terrainLift.SetActive(false);
          terrainFloor.SetActive(false);
          introLogo.SetActive(false);
          introAnim.SetActive(true);
          introAnim.GetComponentInChildren<Animator>().speed = INTRO_ANIMATION_SPEED_SCALE;
          audioLibrary.PlayClip(audioLibrary.startupSound);
          countdown = INTRO_ANIMATION_DURATION;
          SetSkyboxLightFactor(0f);
          break;
        case State.INTRO_LOGO:
          introLogo.SetActive(true);
          countdown = INTRO_LOGO_DURATION;
          break;
        case State.INTRO_LIGHTING:
          environment.SetActive(true);
          countdown = LIGHTING_CHANGE_DURATION;
          break;
        case State.DONE:          
          // This transition has some redundant work below because we want to be able to shortcut directly
          // to State.DONE from any state if the user presses a key (in debug mode).
          SetSkyboxLightFactor(1);
          introAnim.SetActive(false);
          introLogo.SetActive(false);
          environment.SetActive(true);
          dust.SetActive(true);
          audioLibrary.FadeClip(audioLibrary.startupSound);
          // We're now ready to either show the startup menu if the user has ever used Blocks before;
          // or skip straight to the tutorial if they haven't.
          if (!PeltzerMain.Instance.HasEverStartedPoly && Features.forceFirstTimeUsersIntoTutorial) {
            PeltzerMain.Instance.tutorialManager.StartTutorial(0);
            PeltzerMain.Instance.GetMover().currentMoveType = tools.Mover.MoveType.MOVE;
            PeltzerMain.Instance.paletteController.ChangeTouchpadOverlay(TouchpadOverlay.NONE);
          } else {
            PeltzerMain.Instance.GetMover().currentMoveType = tools.Mover.MoveType.MOVE;
            PeltzerMain.Instance.paletteController.ChangeTouchpadOverlay(TouchpadOverlay.UNDO_REDO);
            // Don't clear reference images added by the user while in the menu (because they typically do that before
            // putting on the headset).
            PeltzerMain.Instance.CreateNewModel(/* clearReferenceImages */ false);
          }

          introIsComplete = true;
          // Prompt the user to take a tutorial.
          PeltzerMain.Instance.attentionCaller.StartGlowing(AttentionCaller.Element.TAKE_A_TUTORIAL_BUTTON);
          PeltzerMain.Instance.peltzerController.LookAtMe();
          PeltzerMain.Instance.CheckLeftHandedPlayerPreference();
          PeltzerMain.Instance.menuHint.SetTimer();
          PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();


          // If the user is already signed in, load their zandria creations after the startup animation finishes.
          if (loadCreationsWhenDone) {
            PeltzerMain.Instance.LoadCreations();
          }
          // We no longer need to Update(), as our work is done.
          enabled = false;  // This is the MonoBehaviour.enabled property.
          GameObject visualBoundingBox = ObjectFinder.ObjectById("ID_PolyWorldBounds");
          visualBoundingBox.SetActive(true);

          break;
      }
    }

    private void SetSkyboxLightFactor(float factor) {
      Color skyColorToSet = Color.white * factor + StartSkyColor * (1f - factor);
      RenderSettings.skybox.SetColor("_Tint", skyColorToSet);//new Color(factor, factor, factor, 1f));
      Color groundColorToSet = Color.white * factor + StartGroundColor * (1f - factor);
    }
  }
}
