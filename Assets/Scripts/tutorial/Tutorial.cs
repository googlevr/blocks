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

namespace com.google.apps.peltzer.client.tutorial
{
    /// <summary>
    /// Represents a tutorial. A tutorial is a single lesson in user education (for example, a lesson
    /// that teaches the user how to place and move objects). It is made up of a list of steps,
    /// which the user must go through in sequence.
    /// </summary>
    public abstract class Tutorial
    {
        public List<ITutorialStep> steps { get; private set; }

        protected Tutorial()
        {
            steps = new List<ITutorialStep>();
        }

        /// <summary>
        /// Called to prepare the tutorial. This is called before the first step.
        /// </summary>
        public virtual void OnPrepare() { }

        /// <summary>
        /// Adds a new tutorial step.
        /// </summary>
        /// <param name="step"></param>
        protected void AddStep(ITutorialStep step)
        {
            steps.Add(step);
        }
    }
}