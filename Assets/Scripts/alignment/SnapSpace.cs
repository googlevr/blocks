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
using com.google.apps.peltzer.client.tools.utils;
using UnityEngine;

namespace com.google.apps.peltzer.client.alignment
{
    /// <summary>
    /// Defines a SnapSpace: a special kind of coordinate system that an MMesh can be orientated in and snapped to.
    /// 
    /// Each method in ISnapSpace represents a step in the snapping flow. When we 'DETECT' what snap should occur we
    /// construct the space. This is a partial setup, every class that inherits from this class should set required
    /// metadata and maintain any data that was already calculated while detecting the snap. This helps us avoid any
    /// duplicate work when the snap is executed, and extraneous calculations in every detection frame. When the user
    /// actually pulls the alt-trigger we 'EXECUTE' the snap; finishing any remaining work to setup the SnapSpace.
    /// SnapTo() is then called continuously to 'MODIFY' the snap.
    /// </summary>
    public abstract class SnapSpace : CoordinateSystem
    {
        /// <summary>
        ///   The type of snapping that generated this SnapSpace. The type of snapping determines the properities of the
        ///   space and how the held mesh is translated by SnapTo().
        /// </summary>
        public abstract SnapType SnapType
        {
            get;
        }

        /// <summary>
        ///   Called once on alt-trigger when a user wants to execute the detected snap. This is expected to do all the
        ///   heavy lifting calculations avoided on construction to maximize performance.
        /// </summary>
        public abstract void Execute();

        /// <summary>
        ///   Checks if the state of a snap is still valid.
        /// </summary>
        /// <returns>Whether the snap is still valid.</returns>
        public abstract bool IsValid();

        /// <summary>
        ///   Called every frame while the user is holding down alt-trigger to calculate the snapped transform of the
        ///   held mesh. The method should take in the unsnapped position and rotation of the held mesh, translate it into
        ///   the snap space and return the new snapped position and rotation for this frame. The tool calling SnapTo() is
        ///   then responsible for actually updating the transform of the held mesh.
        /// </summary>
        /// <param name="position">The position of the mesh being snapped.</param>
        /// <param name="rotation">The rotation of the mesh being snapped.</param>
        /// <returns>The snapped position and rotation as a SnapTransform.</returns>
        public abstract SnapTransform Snap(Vector3 position, Quaternion rotation);

        /// <summary>
        ///   Called when a snap has been started and then stopped. Useful for clearing any UI elements.
        /// </summary>
        public abstract void StopSnap();

        /// <summary>
        ///   Checks whether a SnapSpace is equivalent to another SnapSpace.
        /// </summary>
        /// <param name="otherSpace">The other SnapSpace.</param>
        /// <returns>Whether they are equal.</returns>
        public abstract bool Equals(SnapSpace otherSpace);
    }
}
