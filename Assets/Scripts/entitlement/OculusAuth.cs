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

namespace com.google.apps.peltzer.client.entitlement {
  public class OculusAuth : MonoBehaviour {
    private bool userIsEntitled = false;
    // The App ID is a public identifier for the Blocks app on the Oculus platform. It is
    // analogous to Apple's App ID, which shows up in URLs related to the app.
    private const string OCULUS_APP_ID = "[Removed]]";

    private void Awake() {
      // TODO AB
      // Oculus.Platform.Core.Initialize(OCULUS_APP_ID);
      // Oculus.Platform.Entitlements.IsUserEntitledToApplication().OnComplete(EntitlementCallback);
    }

    public void Update() {
      // Oculus.Platform.Request.RunCallbacks();
    }

    // TODO AB
    // private void EntitlementCallback(Oculus.Platform.Message response) {
    //   string message;
    //   if (response.IsError) {
    //     if (response.GetError() != null) {
    //       message = response.GetError().Message;
    //     } else {
    //       message = "Authentication failed";
    //     }
    //   } else {
    //     userIsEntitled = true;
    //     message = "";
    //   }
    //
    //   if (message != string.Empty) {
    //     Debug.Log(message, this);
    //   }
    //
    //   if (!userIsEntitled) {
    //     Debug.Log("User not authenticated! You must be logged in to continue.");
    //     Application.Quit();
    //   }
    // }
  }
}
