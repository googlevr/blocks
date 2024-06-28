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
using System;
using System.Collections.Generic;

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.core;
using TMPro;

namespace com.google.apps.peltzer.client.tutorial
{
    public class Glow
    {
        /// <summary>
        /// The period of the "glow" animation (the time taken by each repetition of it).
        /// Shorter values means a faster animation. Given in seconds.
        /// </summary>
        private const float GLOW_PERIOD = 0.5f;

        /// <summary>
        /// The maximum emission factor when glowing. More is brighter.
        /// </summary>
        private const float GLOW_MAX_EMISSION = 0.9f;

        /// <summary>
        /// The period of the "glow" animation (the time taken by each repetition of it).
        /// Shorter values means a faster animation. Given in seconds.
        /// </summary>
        public float period { get; private set; }

        /// <summary>
        /// The maximum emission factor when glowing. More is brighter.
        /// </summary>
        public float maxEmission { get; private set; }

        /// <summary>
        /// The time to start this glow.
        /// </summary>
        public float startTime { get; private set; }

        /// <summary>
        /// The time to end this glow.
        /// </summary>
        public float endTime { get; private set; }

        /// <summary>
        /// A glow. This holds all the information to glow a gameObject a certain amount at a certain rate during a certain
        /// time period.
        /// </summary>
        /// <param name="period">The time for each glow repetition.</param>
        /// <param name="maxEmission">The brightest the glow can be.</param>
        /// <param name="delay">How long we should wait from when the glow is created until we start it.</param>
        /// <param name="duration">How long the glow should last.</param>
        public Glow(float period = GLOW_PERIOD, float maxEmission = GLOW_MAX_EMISSION, float delay = 0f, float duration = Mathf.Infinity)
        {
            this.period = period;
            this.maxEmission = maxEmission;
            startTime = Time.time + delay;
            endTime = startTime + duration;
        }
    }

    /// <summary>
    /// Responsible for calling attention to several parts of the UI during tutorial.
    /// </summary>
    public class AttentionCaller : MonoBehaviour, IMeshRenderOwner
    {
        /// <summary>
        /// Possible elements that we can call attention to.
        /// </summary>
        public enum Element
        {
            PELTZER_TOUCHPAD_LEFT,
            PELTZER_TOUCHPAD_RIGHT,
            PELTZER_TOUCHPAD_UP,
            PELTZER_TOUCHPAD_DOWN,
            PALETTE_TOUCHPAD_LEFT,
            PALETTE_TOUCHPAD_RIGHT,
            PALETTE_TOUCHPAD_UP,
            PALETTE_TOUCHPAD_DOWN,
            PELTZER_TRIGGER,
            PALETTE_TRIGGER,
            PELTZER_GRIP_LEFT,
            PELTZER_GRIP_RIGHT,
            PALETTE_GRIP_LEFT,
            PALETTE_GRIP_RIGHT,
            PELTZER_MENU_BUTTON,
            PALETTE_MENU_BUTTON,
            PELTZER_SYSTEM_BUTTON,
            PALETTE_SYSTEM_BUTTON,
            RED_PAINT_SWATCH,
            NEW_BUTTON,
            SAVE_BUTTON_ICON,
            GRID_BUTTON,
            TUTORIAL_BUTTON,
            TAKE_A_TUTORIAL_BUTTON,
            SIREN,
            PELTZER_SECONDARY_BUTTON,
            PALETTE_SECONDARY_BUTTON,
            PELTZER_THUMBSTICK,
            PALETTE_THUMBSTICK,
            SAVE_SELECTED_BUTTON,
        }

        private List<ControllerMode> supportedModes = new List<ControllerMode>() {
      ControllerMode.insertVolume,
      ControllerMode.insertStroke,
      ControllerMode.paintMesh,
      ControllerMode.move,
      ControllerMode.reshape,
      ControllerMode.delete
    };

        /// <summary>
        /// The period of the "glow" animation (the time taken by each repetition of it).
        /// Shorter values means a faster animation. Given in seconds.
        /// </summary>
        private const float GLOW_PERIOD = 1.0f;

        /// <summary>
        /// The maximum emission factor when glowing. More is brighter.
        /// </summary>
        private const float GLOW_MAX_EMISSION = 0.8f;

        /// <summary>
        /// The maximum percent we can grey something.
        /// </summary>
        private const float GREY_MAX = 0.9f;

        private const float DEFAULT_BULB_PERIOD = 0.25f;
        private const float DEFAULT_BULB_MAX_EMISSION = 0.9f;
        private const float DEFAULT_BULB_DURATION = 1.5f;

        private const float DEFAULT_SIREN_PERIOD = 0.4f;
        private const float DEFAULT_SIREN_MAX_EMISSION = 1f;
        private const float DEFAULT_SIREN_DELAY = 0f;
        private const float DEFAULT_SIREN_DURATION = 2f;

        /// <summary>
        /// Base color of the glow animation.
        /// </summary>
        private readonly Color GLOW_BASE_COLOR = Color.black;

        /// <summary>
        /// Override color.
        /// </summary>
        private readonly Color OVERRIDE_COLOR = new Color(128f / 255f, 128f / 255f, 128f / 255f);

        /// <summary>
        /// Base color of the glowing sphere animations.
        /// </summary>
        private readonly Color SPHERE_BASE_COLOR = new Color(0.5f, 1.0f, 0.5f);

        /// <summary>
        /// Color for an icon if it is inactive or "greyed out".
        /// </summary>
        private readonly Color INACTIVE_ICON_COLOR = new Color(1f, 1f, 1f, 0.117f);

        /// <summary>
        /// Color for an icon if it is active or "coloured".
        /// </summary>
        private readonly Color ACTIVE_ICON_COLOR = new Color(1f, 1f, 1f, 1f);

        /// <summary>
        /// Name of the emission color variable in the shader.
        /// </summary>
        private const string EMISSIVE_COLOR_VAR_NAME = "_EmissiveColor";

        /// <summary>
        /// Elements to highlight with a large glowing green sphere.
        /// </summary>
        private readonly List<Element> SPHERE_ELEMENTS = new List<Element> { };

        private const string NEW_BUTTON_PATH = "ID_PanelTools/ToolSide/Actions/New";
        private const string SAVE_BUTTON_ICON_PATH = "ID_PanelTools/ToolSide/Actions/Save";
        private const string SAVE_SELECTED_BUTTON_PATH = "ID_PanelTools/ToolSide/Menu-Save/Save-Selected";
        private const string GRID_BUTTON_PATH = "ID_PanelTools/ToolSide/Actions/Blockmode";
        private const string TUTORIAL_BUTTON_PATH = "ID_PanelTools/ToolSide/Actions/Tutorial";
        private const string TAKE_A_TUTORIAL_BUTTON_PATH = "ID_PanelTools/ToolSide/TutorialPrompt/Btns/YesTutorial";

        private const int RED_MATERIAL_ID = 8;

        private PeltzerController peltzerController;
        private PaletteController paletteController;

        /// <summary>
        /// Maps an Element to the GameObject it represents.
        /// </summary>
        private Dictionary<Element, GameObject> elements = new Dictionary<Element, GameObject>();

        ChangeMaterialMenuItem[] allColourSwatches;

        List<GameObject> lightBulbs;

        /// <summary>
        /// Maps Elements to spheres that are currently in the scene.
        /// </summary>
        private Dictionary<Element, GameObject> currentSpheres = new Dictionary<Element, GameObject>();

        /// <summary>
        /// Offsets that adjust attention-calling-spheres to align with their element, because
        /// most controller elements are offcenter.
        /// </summary>
        private readonly Vector3 TRIGGER_OFFSET = new Vector3(0.0f, -0.04f, -0.03f);
        private readonly Vector3 LEFT_GRIP_OFFSET = new Vector3(.02f, -.08f, 0.01f);
        private readonly Vector3 RIGHT_GRIP_OFFSET = new Vector3(-.02f, -.08f, 0.01f);

        /// <summary>
        /// Sizes of the different attention-calling-spheres.
        /// </summary>
        const float TRIGGER_SPHERE_SIZE_MAX = 0.1f;
        const float TRIGGER_SPHERE_SIZE_MIN = 0.03f;
        const float GRIP_SPHERE_SIZE_MAX = 0.03f;
        const float GRIP_SPHERE_SIZE_MIN = 0.01f;

        private Dictionary<GameObject, Glow> currentlyGlowing = new Dictionary<GameObject, Glow>();

        /// <summary>
        /// Prefab that holds the glowing sphere.
        /// </summary>
        private GameObject spherePrefab;

        /// <summary>
        /// List of Mesh IDs of meshes that are currently being highlighted with a glow effect.
        /// </summary>
        private List<int> claimedMeshes = new List<int>();

        /// <summary>
        /// Starting value for the interpolation param between sphere sizes.
        /// </summary>
        static float sizePct = 0.0f;

        public Glow defaultSirenGlow;

        /// <summary>
        /// One-time setup. Call once before using this object.
        /// </summary>
        /// <param name="peltzerController">The PeltzerController to use.</param>
        /// <param name="paletteController">The PaletteController to use.</param>
        public void Setup(PeltzerController peltzerController, PaletteController paletteController)
        {
            this.peltzerController = peltzerController;
            this.paletteController = paletteController;

            // This only handles the red material as a special case. Ideally we would write this to be robust and allow
            // the greying/recoloring of any swatch given materialId. But that is overkill for what we need right now.
            allColourSwatches = ObjectFinder.ObjectById("ID_PanelTools").GetComponentsInChildren<ChangeMaterialMenuItem>(true);

            for (int i = 0; i < allColourSwatches.Length; i++)
            {
                if (allColourSwatches[i].materialId == RED_MATERIAL_ID)
                {
                    elements[Element.RED_PAINT_SWATCH] = allColourSwatches[i].gameObject;
                }
            }

            elements[Element.PELTZER_TOUCHPAD_LEFT] =
              peltzerController.controllerGeometry.touchpadLeft;
            elements[Element.PELTZER_TOUCHPAD_RIGHT] =
              peltzerController.controllerGeometry.touchpadRight;
            elements[Element.PELTZER_TOUCHPAD_UP] =
              peltzerController.controllerGeometry.touchpadUp;
            elements[Element.PELTZER_TOUCHPAD_DOWN] =
              peltzerController.controllerGeometry.touchpadDown;
            elements[Element.PALETTE_TOUCHPAD_LEFT] =
              paletteController.controllerGeometry.touchpadLeft;
            elements[Element.PALETTE_TOUCHPAD_RIGHT] =
              paletteController.controllerGeometry.touchpadRight;
            elements[Element.PALETTE_TOUCHPAD_UP] =
              paletteController.controllerGeometry.touchpadUp;
            elements[Element.PALETTE_TOUCHPAD_DOWN] =
              paletteController.controllerGeometry.touchpadDown;
            elements[Element.PALETTE_THUMBSTICK] =
              paletteController.controllerGeometry.thumbstick;
            elements[Element.PELTZER_THUMBSTICK] =
              peltzerController.controllerGeometry.thumbstick;
            elements[Element.PELTZER_TRIGGER] = peltzerController.controllerGeometry.trigger;
            elements[Element.PALETTE_TRIGGER] = paletteController.controllerGeometry.trigger;
            elements[Element.PELTZER_GRIP_LEFT] = peltzerController.controllerGeometry.gripLeft;
            elements[Element.PELTZER_GRIP_RIGHT] = peltzerController.controllerGeometry.gripRight;
            elements[Element.PALETTE_GRIP_LEFT] = paletteController.controllerGeometry.gripLeft;
            elements[Element.PALETTE_GRIP_RIGHT] = paletteController.controllerGeometry.gripRight;
            elements[Element.PELTZER_MENU_BUTTON] = peltzerController.controllerGeometry.appMenuButton;
            elements[Element.PELTZER_SYSTEM_BUTTON] = peltzerController.controllerGeometry.systemButton;
            elements[Element.PELTZER_SECONDARY_BUTTON] = peltzerController.controllerGeometry.secondaryButton;
            elements[Element.PALETTE_MENU_BUTTON] = paletteController.controllerGeometry.appMenuButton;
            elements[Element.PALETTE_SYSTEM_BUTTON] = paletteController.controllerGeometry.systemButton;
            elements[Element.PALETTE_SECONDARY_BUTTON] = paletteController.controllerGeometry.secondaryButton;
            elements[Element.NEW_BUTTON] = FindOrDie(paletteController.gameObject, NEW_BUTTON_PATH);
            elements[Element.SAVE_BUTTON_ICON] = FindOrDie(paletteController.gameObject, SAVE_BUTTON_ICON_PATH);
            elements[Element.GRID_BUTTON] = FindOrDie(paletteController.gameObject, GRID_BUTTON_PATH);
            elements[Element.TUTORIAL_BUTTON] = FindOrDie(paletteController.gameObject, TUTORIAL_BUTTON_PATH);
            elements[Element.TAKE_A_TUTORIAL_BUTTON] = FindOrDie(paletteController.gameObject, TAKE_A_TUTORIAL_BUTTON_PATH);
            elements[Element.SIREN] = ObjectFinder.ObjectById("ID_Siren");
            elements[Element.SAVE_SELECTED_BUTTON] = FindOrDie(paletteController.gameObject, SAVE_SELECTED_BUTTON_PATH);

            this.spherePrefab = Resources.Load<GameObject>("Prefabs/GlowOrb");

            lightBulbs = new List<GameObject>{
        ObjectFinder.ObjectById("ID_Light_1"),
        ObjectFinder.ObjectById("ID_Light_2"),
        ObjectFinder.ObjectById("ID_Light_3"),
        ObjectFinder.ObjectById("ID_Light_4"),
        ObjectFinder.ObjectById("ID_Light_5"),
        ObjectFinder.ObjectById("ID_Light_6"),
        ObjectFinder.ObjectById("ID_Light_7"),
        ObjectFinder.ObjectById("ID_Light_8"),
        ObjectFinder.ObjectById("ID_Light_9")};
        }

        /// <summary>
        /// Starts glowing the given element.
        /// </summary>
        /// <param name="which">The element to start glowing.</param>
        public void StartGlowing(Element which, Glow glow = null)
        {
            if (elements[which] == null) return;
            if (SPHERE_ELEMENTS.Contains(which) && !currentSpheres.ContainsKey(which))
            {
                // This element should be highlighted with a glowing sphere.
                SetSphere(which);
            }
            else if (!currentlyGlowing.ContainsKey(elements[which]))
            {
                currentlyGlowing[elements[which]] = glow == null ? new Glow() : glow;
            }
        }

        public void StartGlowing(ControllerMode mode, Glow glow = null)
        {
            if (mode == PeltzerMain.Instance.peltzerController.mode)
            {
                StartGlowing(PeltzerMain.Instance.peltzerController.attachedToolHead
                  .GetComponent<ToolMaterialManager>().materialObjects, glow);
            }

            StartGlowing(PeltzerMain.Instance.paletteController.GetToolheadForMode(mode)
              .GetComponent<ToolMaterialManager>().materialObjects, glow);
        }

        public void StartGlowing(GameObject[] components, Glow glow = null)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (!currentlyGlowing.ContainsKey(components[i]))
                {
                    currentlyGlowing[components[i]] = glow == null ? new Glow() : glow;
                }
            }
        }

        public void StartGlowing(GameObject obj, Glow glow = null)
        {
            if (!currentlyGlowing.ContainsKey(obj))
            {
                currentlyGlowing[obj] = glow == null ? new Glow() : glow;
            }
        }

        public void GlowTheSiren()
        {
            StartGlowing(Element.SIREN,
              new Glow(DEFAULT_SIREN_PERIOD, DEFAULT_SIREN_MAX_EMISSION, DEFAULT_SIREN_DELAY, DEFAULT_SIREN_DURATION));
        }

        public void CascadeGlowAllLightbulbs(float duration = DEFAULT_BULB_DURATION)
        {
            for (int i = 0; i < lightBulbs.Count; i++)
            {
                float delay = i % 2 == 0 ? 0f : DEFAULT_BULB_PERIOD;
                Glow glow = new Glow(DEFAULT_BULB_PERIOD, DEFAULT_BULB_MAX_EMISSION, delay, duration);
                StartGlowing(lightBulbs[i], glow);
            }
        }

        public void GreyOut(ControllerMode mode)
        {
            // The toolheads are cloned when attached to the controller. We need to handle greying out the cloned version.
            if (mode == PeltzerMain.Instance.peltzerController.mode)
            {
                PeltzerMain.Instance.peltzerController.attachedToolHead.GetComponent<ToolMaterialManager>().ChangeToDisable();
            }

            PeltzerMain.Instance.paletteController.GetToolheadForMode(mode).GetComponent<ToolMaterialManager>()
              .ChangeToDisable();

            PeltzerMain.Instance.paletteController.GetToolheadForMode(mode).GetComponent<SelectableMenuItem>().isActive
              = false;
        }

        /// <summary>
        /// Greys out an element by setting its override.
        /// </summary>
        /// <param name="which">The element to grey.</param>
        public void GreyOut(Element which, float greyAmount = GREY_MAX)
        {
            GreyOut(elements[which], greyAmount);
        }

        public void GreyOut(GameObject obj, float greyAmount = GREY_MAX)
        {
            if (obj != null)
            {
                Material mat = obj.GetComponent<Renderer>().material;
                mat.SetFloat("_OverrideAmount", greyAmount);
                mat.SetColor("_OverrideColor", OVERRIDE_COLOR);

                if (obj.GetComponent<SelectableMenuItem>() != null)
                {
                    obj.GetComponent<SelectableMenuItem>().isActive = false;
                }

                // Grey out the icon on the buttons.
                if (obj.GetComponentInChildren<SpriteRenderer>() != null)
                {
                    obj.GetComponentInChildren<SpriteRenderer>().color = INACTIVE_ICON_COLOR;
                }

                // Grey out text on buttons.
                if (obj.GetComponentInChildren<TextMeshPro>() != null)
                {
                    obj.GetComponentInChildren<TextMeshPro>().color = INACTIVE_ICON_COLOR;
                }
            }
        }

        public void GreyOut(SpriteRenderer[] icons)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                GreyOut(icons[i]);
            }
        }

        public void GreyOut(SpriteRenderer icon)
        {
            icon.color = INACTIVE_ICON_COLOR;
        }

        /// <summary>
        /// Greys out every element.
        /// </summary>
        public void GreyOutAll()
        {
            foreach (Element element in elements.Keys)
            {
                GreyOut(element);
            }

            // The red swatch is in both lists, this will grey it out twice.
            GreyOutAllColorSwatches();
            GreyOutAllToolheads();
            GreyOutAllTouchpadIcons();
        }

        public void GreyOutAllColorSwatches()
        {
            for (int i = 0; i < allColourSwatches.Length; i++)
            {
                GreyOut(allColourSwatches[i].gameObject, 0.4f);
            }
        }

        public void GreyOutAllTouchpadIcons()
        {
            GameObject[] peltzerOverlays = PeltzerMain.Instance.peltzerController.controllerGeometry.overlays;
            for (int i = 0; i < peltzerOverlays.Length; i++)
            {
                GreyOut(peltzerOverlays[i].GetComponent<Overlay>().icons);
            }

            GameObject[] paletteOverlays = PeltzerMain.Instance.paletteController.controllerGeometry.overlays;
            for (int i = 0; i < paletteOverlays.Length; i++)
            {
                GreyOut(paletteOverlays[i].GetComponent<Overlay>().icons);
            }
        }

        public void GreyOutAllToolheads()
        {
            foreach (ControllerMode mode in supportedModes)
            {
                GreyOut(mode);
            }
        }

        public void Recolor(ControllerMode mode)
        {
            // The toolheads are cloned when attached to the controller. We need to handle recoloring the cloned version.
            if (mode == PeltzerMain.Instance.peltzerController.mode)
            {
                if (PeltzerMain.Instance.peltzerController.attachedToolHead != null)
                {
                    PeltzerMain.Instance.peltzerController.attachedToolHead.GetComponent<ToolMaterialManager>().ChangeToEnable();
                }
            }

            PeltzerMain.Instance.paletteController.GetToolheadForMode(mode).GetComponent<ToolMaterialManager>()
              .ChangeToEnable();

            PeltzerMain.Instance.paletteController.GetToolheadForMode(mode).GetComponent<SelectableMenuItem>().isActive
              = true;

            if (mode == ControllerMode.move)
            {
                PeltzerMain.Instance.GetMover().InvalidateCachedMaterial();
            }
        }

        /// <summary>
        /// Recolors an element by setting its override amount back to zero.
        /// </summary>
        /// <param name="which">The element to recolor.</param>
        public void Recolor(Element which)
        {
            Recolor(elements[which]);
        }

        public void Recolor(GameObject obj)
        {
            if (obj != null)
            {
                obj.GetComponent<Renderer>().material.SetFloat("_OverrideAmount", 0f);

                if (obj.GetComponent<SelectableMenuItem>() != null)
                {
                    obj.GetComponent<SelectableMenuItem>().isActive = true;
                }

                // Recolor the icons on the buttons.
                if (obj.GetComponentInChildren<SpriteRenderer>() != null)
                {
                    obj.GetComponentInChildren<SpriteRenderer>().color = ACTIVE_ICON_COLOR;
                }

                // Grey out text on buttons.
                if (obj.GetComponentInChildren<TextMeshPro>() != null)
                {
                    obj.GetComponentInChildren<TextMeshPro>().color = ACTIVE_ICON_COLOR;
                }
            }
        }

        public void Recolor(SpriteRenderer[] icons)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                Recolor(icons[i]);
            }
        }

        public void Recolor(SpriteRenderer icon)
        {
            icon.color = ACTIVE_ICON_COLOR;
        }

        /// <summary>
        /// Recolors every element.
        /// </summary>
        public void RecolorAll()
        {
            foreach (Element element in elements.Keys)
            {
                Recolor(element);
            }

            // The red swatch is in both lists, this will recolor it out twice.
            RecolorAllColorSwatches();
            RecolorAllToolheads();
            RecolorAllTouchpadIcons();
        }

        public void RecolorAllColorSwatches()
        {
            for (int i = 0; i < allColourSwatches.Length; i++)
            {
                Recolor(allColourSwatches[i].gameObject);
            }
        }

        public void RecolorAllToolheads()
        {
            foreach (ControllerMode mode in supportedModes)
            {
                Recolor(mode);
            }
        }

        public void RecolorAllTouchpadIcons()
        {
            GameObject[] peltzerOverlays = PeltzerMain.Instance.peltzerController.controllerGeometry.overlays;
            for (int i = 0; i < peltzerOverlays.Length; i++)
            {
                Recolor(peltzerOverlays[i].GetComponent<Overlay>().icons);
            }

            GameObject[] paletteOverlays = PeltzerMain.Instance.paletteController.controllerGeometry.overlays;
            for (int i = 0; i < paletteOverlays.Length; i++)
            {
                Recolor(paletteOverlays[i].GetComponent<Overlay>().icons);
            }
        }

        /// <summary>
        /// Positions and scales a glowing sphere effect in the scene on the trigger or grips.
        /// </summary>
        /// <param name="which"></param>
        private void SetSphere(Element which)
        {
            // This element should be highlighted with a glowing sphere.
            GameObject newSphere = Instantiate(spherePrefab);
            newSphere.transform.position = elements[which].transform.position;
            newSphere.transform.parent = elements[which].transform;
            switch (which)
            {
                case Element.PELTZER_TRIGGER:
                    newSphere.transform.localScale = new Vector3(TRIGGER_SPHERE_SIZE_MAX, TRIGGER_SPHERE_SIZE_MAX,
                      TRIGGER_SPHERE_SIZE_MAX);
                    newSphere.transform.localPosition += TRIGGER_OFFSET;
                    break;
                case Element.PELTZER_GRIP_LEFT:
                case Element.PALETTE_GRIP_LEFT:
                    newSphere.transform.localScale = new Vector3(GRIP_SPHERE_SIZE_MAX, GRIP_SPHERE_SIZE_MAX,
                      GRIP_SPHERE_SIZE_MAX);
                    newSphere.transform.localPosition += LEFT_GRIP_OFFSET;
                    break;
                case Element.PELTZER_GRIP_RIGHT:
                case Element.PALETTE_GRIP_RIGHT:
                    newSphere.transform.localScale = new Vector3(GRIP_SPHERE_SIZE_MAX, GRIP_SPHERE_SIZE_MAX,
                      GRIP_SPHERE_SIZE_MAX);
                    newSphere.transform.localPosition += RIGHT_GRIP_OFFSET;
                    break;
                default:
                    break;
            }
            currentSpheres[which] = newSphere;
        }

        public void StopGlowingAll()
        {
            foreach (Element element in elements.Keys)
            {
                StopGlowing(element);
            }

            foreach (ControllerMode mode in supportedModes)
            {
                StopGlowing(mode);
            }
        }

        /// <summary>
        /// Stops glowing the given element.
        /// </summary>
        /// <param name="which">The element to stop glowing.</param>
        public void StopGlowing(Element which)
        {
            if (currentSpheres != null && currentSpheres.ContainsKey(which))
            {
                if (currentSpheres[which] != null)
                {
                    Destroy(currentSpheres[which]);
                    currentSpheres.Remove(which);
                }
            }
            else if (currentlyGlowing != null && elements[which] != null &&
              currentlyGlowing.ContainsKey(elements[which]))
            {
                currentlyGlowing.Remove(elements[which]);
                SetEmissiveFactor(elements[which], 0, GLOW_BASE_COLOR);
            }
        }

        public void StopGlowing(ControllerMode mode)
        {
            if (mode == PeltzerMain.Instance.peltzerController.mode)
            {
                StopGlowing(PeltzerMain.Instance.peltzerController.attachedToolHead
                  .GetComponent<ToolMaterialManager>().materialObjects);
            }

            StopGlowing(PeltzerMain.Instance.paletteController.GetToolheadForMode(mode)
              .GetComponent<ToolMaterialManager>().materialObjects);
        }

        public void StopGlowing(GameObject[] components)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (currentlyGlowing.ContainsKey(components[i]))
                {
                    currentlyGlowing.Remove(components[i]);
                    SetEmissiveFactor(components[i], 0, GLOW_BASE_COLOR);
                }
            }
        }

        /// <summary>
        /// Claim ownership of rendering the mesh to add a glowing style effect ot it.
        /// </summary>
        /// <param name="meshId">The mesh to glow.</param>
        public void StartMeshGlowing(int meshId)
        {
            if (claimedMeshes.Contains(meshId))
            {
                return;
            }
            if (PeltzerMain.Instance.model.ClaimMesh(meshId, this) != -1)
            {
                claimedMeshes.Add(meshId);
                PeltzerMain.Instance.highlightUtils.TurnOnMesh(meshId);
                PeltzerMain.Instance.highlightUtils.SetMeshStyleToTutorial(meshId);
            }
            else
            {
                Debug.LogError("Mesh could not be claimed.");
            }
        }

        /// <summary>
        /// Implement the IRenderMeshOwner interface to allow Model to reclaim the mesh
        /// from this class if other tools need it.  Only Model should call this method.
        /// </summary>
        /// <param name="meshId">The mesh to claim.</param>
        /// <param name="fosterRenderer">The new owner.</param>
        /// <returns></returns>
        public int ClaimMesh(int meshId, IMeshRenderOwner fosterRenderer)
        {
            if (claimedMeshes.Contains(meshId))
            {
                claimedMeshes.Remove(meshId);
                PeltzerMain.Instance.highlightUtils.TurnOffMesh(meshId);
                return meshId;
            }
            // Didn't have the mesh.
            return -1;
        }

        /// <summary>
        /// Reclaims if need be the mesh that should have the glowing style effect ie in the case of another
        /// renderer taking and subsequently relinquishing ownership.
        /// </summary>
        /// <param name="meshId">The mesh to glow.</param>
        public void MakeSureMeshIsGlowing(int meshId)
        {
            if (!claimedMeshes.Contains(meshId) && PeltzerMain.Instance.model.ClaimMeshIfUnowned(meshId, this) != -1)
            {
                claimedMeshes.Add(meshId);
                PeltzerMain.Instance.highlightUtils.TurnOnMesh(meshId);
                PeltzerMain.Instance.highlightUtils.SetMeshStyleToTutorial(meshId);
            }
        }

        /// <summary>
        /// Stops a mesh from glowing.
        /// </summary>
        /// <param name="meshId"></param>
        public void StopMeshGlowing(int meshId)
        {
            if (claimedMeshes.Contains(meshId))
            {
                claimedMeshes.Remove(meshId);
                PeltzerMain.Instance.model.RelinquishMesh(meshId, this);
                PeltzerMain.Instance.highlightUtils.TurnOffMesh(meshId);
            }
        }

        /// <summary>
        /// Disables attention on all elements.
        /// </summary>
        public void ResetAll()
        {
            foreach (GameObject obj in currentlyGlowing.Keys)
            {
                SetEmissiveFactor(obj, 0, GLOW_BASE_COLOR);
            }
            currentlyGlowing.Clear();

            RecolorAll();
        }

        /// <summary>
        /// Update the interpolater for lerping between max and min sphere sizes.
        /// </summary>
        private void UpdateT()
        {
            sizePct += 0.8f * Time.deltaTime;
            if (sizePct > 1.0f)
            {
                sizePct = 0.0f;
            }
        }

        private void Update()
        {
            UpdateT();

            if (currentlyGlowing.Count > 0)
            {
                List<GameObject> elementsToStopGlowing = new List<GameObject>(currentlyGlowing.Count);

                // Update the glowing effect on all the currently glowing objects.
                foreach (KeyValuePair<GameObject, Glow> glow in currentlyGlowing)
                {
                    if (Time.time > glow.Value.startTime)
                    {
                        if (Time.time < glow.Value.endTime)
                        {
                            SetEmissiveFactor(glow.Key, Mathf.PingPong((Time.time + glow.Value.startTime) / glow.Value.period, glow.Value.maxEmission),
                              glow.Key.GetComponent<Renderer>().material.color);
                        }
                        else
                        {
                            elementsToStopGlowing.Add(glow.Key);
                        }
                    }
                }

                // Remove any glows that are complete.
                foreach (GameObject obj in elementsToStopGlowing)
                {
                    currentlyGlowing.Remove(obj);
                    SetEmissiveFactor(obj, 0, GLOW_BASE_COLOR);
                }
            }

            foreach (KeyValuePair<Element, GameObject> pair in currentSpheres)
            {
                SetEmissiveFactor(pair.Value, Mathf.PingPong(Time.time / GLOW_PERIOD, GLOW_MAX_EMISSION),
                  SPHERE_BASE_COLOR);

                // Lerp the size of the sphere so it pulses from large to small.
                float start_size = pair.Key == Element.PELTZER_TRIGGER ? TRIGGER_SPHERE_SIZE_MIN : GRIP_SPHERE_SIZE_MIN;
                float end_size = pair.Key == Element.PELTZER_TRIGGER ? TRIGGER_SPHERE_SIZE_MAX : GRIP_SPHERE_SIZE_MAX;

                float new_size = Mathf.Lerp(start_size, end_size, sizePct);
                pair.Value.transform.localScale = new Vector3(new_size, new_size, new_size);
            }
        }

        public static void SetEmissiveFactor(GameObject obj, float factor, Color base_color)
        {
            Color highlightColor = base_color * Mathf.LinearToGammaSpace(factor);
            obj.GetComponent<Renderer>().material.SetColor(EMISSIVE_COLOR_VAR_NAME, highlightColor);
        }

        /// <summary>
        /// Finds the given child of the given GameObject by name. Throws an exception if the object
        /// is not found.
        /// </summary>
        /// <param name="parent">The GameObject where to search for the object.</param>
        /// <param name="path">The path to the object.</param>
        /// <returns>The GameObject.</returns>
        private static GameObject FindOrDie(GameObject parent, string path)
        {
            Transform transform = parent.transform.Find(path);
            if (transform == null || transform.gameObject == null)
            {
                throw new Exception("Can't find object '" + path + "' in parent: " + parent.name);
            }
            return transform.gameObject;
        }
    }
}
