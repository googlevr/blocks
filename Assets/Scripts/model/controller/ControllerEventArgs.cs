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

namespace com.google.apps.peltzer.client.model.controller
{

    /// <summary>
    ///   Payload for controller events. Contains relevant information to describe the event.
    /// </summary>
    public class ControllerEventArgs : EventArgs
    {
        private readonly ControllerType controllerType;
        private readonly ButtonId buttonId;
        private readonly ButtonAction buttonAction;
        private readonly TouchpadLocation touchpadLocation;
        private readonly TouchpadOverlay overlay;

        public ControllerEventArgs(ControllerType controllerType, ButtonId buttonId,
            ButtonAction buttonAction, TouchpadLocation touchpadLocation,
            TouchpadOverlay overlay)
        {
            this.controllerType = controllerType;
            this.buttonId = buttonId;
            this.buttonAction = buttonAction;
            this.touchpadLocation = touchpadLocation;
            this.overlay = overlay;
        }

        public ControllerType ControllerType { get { return controllerType; } }
        public ButtonId ButtonId { get { return buttonId; } }
        public ButtonAction Action { get { return buttonAction; } }
        public TouchpadLocation TouchpadLocation { get { return touchpadLocation; } }
        public TouchpadOverlay TouchpadOverlay { get { return overlay; } }
    }
}
