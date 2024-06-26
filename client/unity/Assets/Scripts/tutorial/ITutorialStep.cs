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

namespace com.google.apps.peltzer.client.tutorial {
  /// <summary>
  /// Defines a step in a tutorial.
  /// </summary>
  public interface ITutorialStep {
    /// <summary>
    /// Called when preparing the tutorial step for display. The implementation is expected to set up the app's state
    /// (load the appropriate models, place meshes, enable/disable tools, modes, etc) and display the appropriate
    /// user guidance (tooltips, animations, etc). The implementation should suppose that the previous state of the
    /// app is what it was at the end of the OnFinish() call for the previous step, as step[i].OnPrepare() is
    /// called after step[i - 1].OnFinish().
    /// </summary>
    void OnPrepare();

    /// <summary>
    /// Called when the user issues a model command while in this step. The step has the option to accept or reject
    /// the mutation.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>True if the command is accepted and should be executed, false if the command is rejected.</returns>
    bool OnCommand(Command command);

    /// <summary>
    /// Called once per frame while this step is active. The implementation should validate to check whether or not
    /// the user has completed the required action for this step.
    /// </summary>
    /// <returns>True if the user has completed the step. False if not.</returns>
    bool OnValidate();

    /// <summary>
    /// Called when the step finishes (after OnValidate() returns true). The implementation is supposed to do any
    /// necessary cleanup (typically the opposite of any state changes introduced on OnPrepare, unless those
    /// state changes should persist to the next step).
    /// </summary>
    void OnFinish();

    /// <summary>
    /// Should be called when the step finishes if the step had any state variables that should be reset, or if the
    /// tutorial is exitted in the middle of an ongoing step.
    /// </summary>
    void ResetState();
  }
}
