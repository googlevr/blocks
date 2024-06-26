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

#define STEAMVRBUILD
using com.google.apps.peltzer.client.model.controller;
using System;
using UnityEngine;

/// <summary>
///   Holds app-level configuration information.
/// </summary>
namespace com.google.apps.peltzer.client.app {
  public enum VrHardware {
    Unset,
    None,
    Rift,
    Vive,
  }

  public enum SdkMode {
    Unset = -1,
    Oculus = 0,
    SteamVR,
  }

  public class Config : MonoBehaviour {
    private static Config instance;
    public static Config Instance {
      get {
        if (instance == null) {
          instance = GameObject.FindObjectOfType<Config>();
          Debug.Assert(instance != null, "No Config object found in scene!");
        }
        return instance;
      }
    }

    public OculusHandTrackingManager oculusHandTrackingManager;

    public string appName = "[Removed]";
    // The SDK being used -- Oculus or Steam. Set from the Editor.
    public SdkMode sdkMode;
    // The current version ID -- 'debug' or something more meaningful. Set from the Editor.
    public string version = "debug";

    [SerializeField] private GameObject cameraRigGameObject;
    [SerializeField] private GameObject controllerLeftGameObject;
    [SerializeField] private GameObject controllerRightGameObject;

    // The hardware being used -- Vive or Rift. Detected at runtime.
    private VrHardware vrHardware;

    // Find or fetch the hardware being used.
    public VrHardware VrHardware {
      // This is set lazily the first time VrHardware is accesssed. 
      get {
        if (vrHardware == VrHardware.Unset) {
          if (sdkMode == SdkMode.Oculus) {
            vrHardware = VrHardware.Rift;
          }
#if STEAMVRBUILD
          else if (sdkMode == SdkMode.SteamVR) {
            // If SteamVR fails for some reason we will discover it here.
            try {
              if (Valve.VR.OpenVR.System == null) {
                vrHardware = VrHardware.None;
                return vrHardware;
              }
            } catch (Exception) {
              vrHardware = VrHardware.None;
              return vrHardware;
            }

            // RiftUsedInSteamVr relies on headset detection, so controllers don't have to be on.
            if (RiftUsedInSteamVr()) {
              vrHardware = VrHardware.Rift;
            } else {
              vrHardware = VrHardware.Vive;
            }
          } 
#endif
          else {
            vrHardware = VrHardware.None;
          }
        }

        return vrHardware;
      }
    }

    void Start() {
      instance = this;
      if (sdkMode == SdkMode.Oculus) {
        oculusHandTrackingManager = cameraRigGameObject.AddComponent<OculusHandTrackingManager>();
        oculusHandTrackingManager.leftTransform = controllerLeftGameObject.transform;
        oculusHandTrackingManager.rightTransform = controllerRightGameObject.transform;
      } else if (sdkMode == SdkMode.SteamVR) {
#if STEAMVRBUILD
        var controllerLeftTracking = controllerLeftGameObject.AddComponent<SteamVR_TrackedObject>();
        controllerLeftTracking.SetDeviceIndex(1);
        var controllerRightTracking = controllerRightGameObject.AddComponent<SteamVR_TrackedObject>();
        controllerRightTracking.SetDeviceIndex(2);
        var manager = cameraRigGameObject.AddComponent<SteamVR_ControllerManager>();
        manager.left = controllerLeftGameObject;
        manager.right = controllerRightGameObject;
        manager.UpdateTargets();
#endif
      }
    }

#if STEAMVRBUILD
    // Check  if the Rift hardware is being used in SteamVR.
    private bool RiftUsedInSteamVr() {
      Valve.VR.CVRSystem system = Valve.VR.OpenVR.System;
      // If system == null, then somehow, the SteamVR SDK was not properly loaded in.
      Debug.Assert(system != null, "OpenVR System not found, check \"Virtual Reality Supported\"");

      // Index 0 is always the HMD.
      return CheckRiftTrackedInSteamVr(0);
    }

    // Check if the tracked object is Rift hardware.
    private bool CheckRiftTrackedInSteamVr(uint index) {
      var system = Valve.VR.OpenVR.System;
      var error = Valve.VR.ETrackedPropertyError.TrackedProp_Success;

      var capacity = system.GetStringTrackedDeviceProperty(
          index,
          Valve.VR.ETrackedDeviceProperty.Prop_ManufacturerName_String,
          null,
          0,
          ref error);
      System.Text.StringBuilder buffer = new System.Text.StringBuilder((int)capacity);
      system.GetStringTrackedDeviceProperty(
          index,
          Valve.VR.ETrackedDeviceProperty.Prop_ManufacturerName_String,
          buffer,
          capacity,
          ref error);
      string s = buffer.ToString();

      if (s.Contains("Oculus")) {
        return true;
      }
      return false;
    }
#endif

#if UNITY_EDITOR
    public void OnValidate() {
      bool useVrSdk = sdkMode == SdkMode.Oculus || sdkMode == SdkMode.SteamVR;

      // Writing to this sets the scene-dirty flag, so don't do it unless necessary
      if (UnityEditor.PlayerSettings.virtualRealitySupported != useVrSdk) {
        UnityEditor.PlayerSettings.virtualRealitySupported = useVrSdk;
      }

      // This hotswaps vr sdks based on selection.
      string[] newDevices;
      switch (sdkMode) {
        case SdkMode.Oculus:
          newDevices = new string[] { "Oculus" };
          UnityEditorInternal.VR.VREditor.SetVREnabledDevicesOnTargetGroup(UnityEditor.BuildTargetGroup.Standalone, newDevices);
          break;
        case SdkMode.SteamVR:
          newDevices = new string[] { "OpenVR" };
          UnityEditorInternal.VR.VREditor.SetVREnabledDevicesOnTargetGroup(UnityEditor.BuildTargetGroup.Standalone, newDevices);
          break;
        default:
          newDevices = new string[] { "" };
          UnityEditorInternal.VR.VREditor.SetVREnabledDevicesOnTargetGroup(UnityEditor.BuildTargetGroup.Standalone, newDevices);
          break;
      }
    }
#endif
  }
}
