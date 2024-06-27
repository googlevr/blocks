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
using System.Linq;
using System.Text;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    ///   A slerp (spherical linear interpolation). An instance of this class represents a slerp for a single
    ///   rotational transform - and maintains the state of that slerp (start orientation, target orientation,
    ///   start time, duration).
    ///   The users of this class are responsible for keeping track of Slerpee/transform correspondance, as well
    ///   as assigning the output of Slerpee to the corresponding transform.
    ///   It's a glorified calculator, essentially.
    /// </summary>
    class Slerpee
    {
        // The default duration of a slerp, in seconds.
        private const float DEFAULT_SLERP_DURATION_SECONDS = .05f;
        // Minimum angle difference for targetOrientation to start a new slerp.
        private const float MIN_SLERP_ANGLE_DEGREES = .1f;
        private Quaternion baseOrientation;
        private Quaternion targetOrientation;
        private Quaternion currentSlerpedOrientation;

        private float slerpStartTime = 0f;

        // This is to aid debugging and logging, and should not be used for any other purpose.
        private int meshTargetId;

        private bool isSlerping = false;

        /// <summary>
        ///   Do initial setup for the slerp, passing in the current base orientation to slerp from.
        ///   Passing an id does not change behavior in any way, but is helpful for debugging/logging so should be
        ///   done whenever possible.
        /// </summary>
        public Slerpee(Quaternion baseOrientation, int id = -1)
        {
            this.baseOrientation = baseOrientation;
            this.currentSlerpedOrientation = baseOrientation;
            this.targetOrientation = baseOrientation;
            this.meshTargetId = id;
        }

        /// <summary>
        ///   Starts a slerp from the current orientation to the targetOrientation.
        ///   Setting instant to true will apply the orientation instantly.
        ///   If a slerp is already in progress and startSlerp is called with a different targetOrientation,
        ///   that slerp will be cancelled, and a new one will be started using the currentSlerpedOrientation as the
        ///   new baseOrientation.
        /// </summary>
        private Quaternion StartOrUpdateSlerpInternal(Quaternion targetOrientation,
            bool instant = false)
        {
            if (instant)
            {
                currentSlerpedOrientation = targetOrientation;
                this.targetOrientation = targetOrientation;
                this.baseOrientation = targetOrientation;
                isSlerping = false;
                return currentSlerpedOrientation;
            }

            // If the new target is essentially the same as our current target, don't start a new slerp and just update.
            if (Quaternion.Angle(this.targetOrientation, targetOrientation) <= MIN_SLERP_ANGLE_DEGREES)
            {
                // Early return if a slerp isn't active.
                if (!isSlerping)
                {
                    return currentSlerpedOrientation;
                }
                return UpdateAndGetCurrentOrientation();
            }

            // Reset slerp parameters for a new slerp.
            slerpStartTime = Time.time;
            this.targetOrientation = targetOrientation;
            // If a slerp is in progress, use the in progress orientation as the base for the new slerp. Otherwise, 
            // us the final orientation of the last completed slerp. Either way, currentSlerpedOrientation holds this value.
            baseOrientation = currentSlerpedOrientation;
            isSlerping = true;

            return currentSlerpedOrientation;
        }

        /// <summary>
        ///   Starts a slerp from the current orientation to the targetOrientation.
        ///   If a slerp is already in progress and startSlerp is called with a different targetOrientation,
        ///   that slerp will be cancelled, and a new one will be started using the currentSlerpedOrientation as the
        ///   new baseOrientation.
        /// </summary>
        public Quaternion StartOrUpdateSlerp(Quaternion targetOrientation)
        {
            return StartOrUpdateSlerpInternal(targetOrientation);
        }

        /// <summary>
        /// Updates the slerp to the supplied orientation instantly without interpolation.
        /// </summary>
        /// <param name="targetOrientation">The orientation to update to.</param>
        /// <returns>The targetOrientation that was input.</returns>
        public Quaternion UpdateOrientationInstantly(Quaternion targetOrientation)
        {
            return StartOrUpdateSlerpInternal(targetOrientation, true);
        }

        /// <summary>
        ///   Updates the current state of the slerp, and returns the new interpolated orientation.
        /// </summary>
        public Quaternion UpdateAndGetCurrentOrientation()
        {
            // Calculate what percentage of the duration has elapsed.
            float elapsedTime = Time.time - slerpStartTime;
            if (elapsedTime > DEFAULT_SLERP_DURATION_SECONDS)
            {
                isSlerping = false;
                // Unity doesn't provide an inbuilt Quaternion normalization function, and slerping normalizes.
                currentSlerpedOrientation = Quaternion.Slerp(baseOrientation, targetOrientation, 1.0f);
                return currentSlerpedOrientation;
            }

            float pctDone = elapsedTime / DEFAULT_SLERP_DURATION_SECONDS;
            currentSlerpedOrientation = Quaternion.Slerp(baseOrientation, targetOrientation, pctDone);
            return currentSlerpedOrientation;
        }

        public override String ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Current Slerp: inSlerp " + isSlerping + "\n");
            builder.Append("    " + "meshId: " + meshTargetId + "\n");
            builder.Append("    " + "baseOrientation: " + baseOrientation + "\n");
            builder.Append("    " + "targetOrientation: " + targetOrientation + "\n");
            builder.Append("    " + "currentSlerpedOrientation: " + currentSlerpedOrientation + "\n");
            builder.Append("    " + "slerpStartTime: " + slerpStartTime + "\n");
            float elapsedTime = Time.time - slerpStartTime;
            builder.Append("    " + "elapsedTime: " + elapsedTime + "\n");
            float pctDone = Math.Max(0f, Math.Min(1f, elapsedTime / DEFAULT_SLERP_DURATION_SECONDS));
            builder.Append("    " + "pctDone: " + pctDone.ToString("0.00%") + "\n");
            return builder.ToString();
        }
    }
}
