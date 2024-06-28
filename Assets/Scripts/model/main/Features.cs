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
using System.Collections.Generic;
using System.Reflection;

namespace com.google.apps.peltzer.client.model.main
{
    /// <summary>
    /// Peltzer features that may or may not be enabled.
    /// Flags that are not marked as readonly can be also set from the debug console at runtime
    /// (this is done by reflection).
    /// </summary>
    public class Features
    {
        // If true, CSG subtraction (subtracting one shape from another, also known as "carving") is enabled.
        public static bool csgSubtractEnabled = false;

        // If true, saves creations in the Mogwai object store.
        public static bool saveToMogwaiObjectStore = true;

        // If true, stamping (also known as "custom primitives") are enabled in the Volume Inserter.
        public static bool stampingEnabled = false;

        // If true, enable the debug console.
        public static readonly bool enableDebugConsole = true;

        // If true, enable controller swapping by bumping controllers together (like TiltBrush).
        public static bool enableControllerSwapping = true;

        // If true, enable deletion of parts (vertices, edges, and faces)
        public static bool enablePartDeletion = false;

        // If true, try to merge adjacent coplanar faces (to remove unnecessary face splits).
        public static bool mergeAdjacentCoplanarFaces = false;

        // If true, clicking the trigger far from the selected items during a move/copy/reshape/extrude operation will
        // just deselect them.
        public static bool clickAwayToDeselect = true;

        // If true, force first-time users into tutorial.
        public static bool forceFirstTimeUsersIntoTutorial = false;

        // If true, publish to Zandria prod (else autopush).
        public static bool useZandriaProd = false;

        // Show ruler for volume inserter
#if UNITY_EDITOR
    public static bool showVolumeInserterRuler = true;
#else
        public static bool showVolumeInserterRuler = false;
#endif

        // If true, use the new expanded radius for rendering wireframes.
        public static bool expandedWireframeRadius = false;

        // If true, adjust the world space for editing convenience after opening a creation.
        // If false, don't (start with the identity world space).
        public static bool adjustWorldSpaceOnOpen = true;

        // If true, trigger haptic feedback on hover. If false, don't (only in multiselect).
        public static bool vibrateOnHover = false;

        // If true, selectively divert global undo/redo stack to local undo/redo stacks.
        public static bool localUndoRedoEnabled = true;

        // If true, enable click to select functionality.
        public static bool clickToSelectEnabled = false;

        // If true, undo redo works with click to select. Only true if local undo/redo is enabled and if click to select is enabled.
        public static bool clickToSelectWithUndoRedoEnabled = clickToSelectEnabled && localUndoRedoEnabled;

        // If true, shows tooltips when the user touches the touchpad/thumbstick whilst on the Poly menu.
        public static bool showModelsMenuTooltips = false;

        // If true, the subdivide tool will turn into the experimental loop subdivide form.
        // Incompatible with planeSubdivideEnabled.
        public static bool loopSubdivideEnabled = false;

        // If true, the subdivide tool will turn into the experimental plane subdivide form.
        // Incompatible with loopSubdivideEnabled.
        public static bool planeSubdivideEnabled = false;

        // If true, allow noncoplanar faces to remain during mesh fixing.
        public static bool allowNoncoplanarFaces = false;

        // If true, show tooltips and ropes for multi-selecting.
        public static bool showMultiselectTooltip = false;

        // If true, show rope guides for snapping.
        public static bool showSnappingGuides = true;

        // If true, use the new continuous snap detection;
        public static bool useContinuousSnapDetection = true;

        // If true, enable world space grid planes.
        public static bool enableWorldSpaceGridPlanes = false;

        // If true, show previews of Poly models on the menu instead of thumbnails.
        public static bool showPolyMenuModelPreviews = false;

        /// <summary>
        /// This function takes a comma delimited string containing feature names prepended with a '+' or a '-' and turns
        /// those features on or off respectively.
        /// </summary>
        public static void ToggleFeatureString(String featureString)
        {
            Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
            foreach (FieldInfo fieldInfo in typeof(Features).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                // Only get fields that bool and not read-only.
                if (fieldInfo.FieldType == typeof(bool) && fieldInfo.MemberType == MemberTypes.Field &&
                  !fieldInfo.IsInitOnly)
                {
                    fields[fieldInfo.Name.ToLower()] = fieldInfo;
                }
            }

            string[] features = featureString.Split(',');
            foreach (string feature in features)
            {
                Char prefix = feature[0];
                string featureName = feature.Substring(1, feature.Length - 1);
                switch (prefix)
                {
                    case '+':
                        fields[featureName.ToLower()].SetValue(null, true);
                        break;
                    case '-':
                        fields[featureName.ToLower()].SetValue(null, false);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
