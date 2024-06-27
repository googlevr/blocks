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

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    ///   The scaling types used by various tools.
    /// </summary>
    public enum ScaleType
    {
        /// <summary>
        ///   Scaling type when no scaling is happening currently.
        /// </summary>
        NONE,
        /// <summary>
        ///   Scaling type when the user is scaling up.
        /// </summary>
        SCALE_UP,
        /// <summary>
        ///   Scaling type when the user is scaling down.
        /// </summary>
        SCALE_DOWN,
    }
}
