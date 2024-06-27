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
using UnityEngine;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;

/// <summary>
///   For each item that is selcted, draws a stylized line between it and the destination position
///   while enforcing rules of connection such as distance dropoff thresholding.
/// </summary>
public class HintRope : MonoBehaviour {

  /// <summary>
  /// Where the gameobject the strings will point to for each mesh when the rope is drawn.
  /// </summary>
  public GameObject destination;
  /// <summary>
  ///   The root value for the distance threshold when determining whether to render the rope, or not.
  /// </summary>
  public float distThresh = .25f;
  /// <summary>
  ///   The width of the rope - this is fed to the LineRenderer.
  /// </summary>
  public float hintWidth = .001f;
  /// <summary>
  ///   Start color to interpolate for LineRenderer.
  /// </summary>
  public Color startColor = new Color(233f/255f, 249f/255f, 255f/255f, 50f/255f);
  /// <summary>
  ///   End color to interpolate for LineRenderer.
  /// </summary>
  public Color endColor = new Color(1f, 1f, 1f, 155f/255f);

  /// <summary>
  ///   The start time for the hint, used for animations.
  /// </summary>
  private float hintStartTime = 0.0f;
  /// <summary>
  ///   Lookup for our currently rendered rope and the associated meshId.
  /// </summary>
  private Dictionary<int,GameObject> ropes = new Dictionary<int, GameObject>();
  /// <summary>
  ///   Reference to the groupButton which we use to calculate the default destination, when no destination is given.
  /// </summary>
  private Transform groupButton;
  /// <summary>
  ///   Material for LineRenderer - White / Diffuse.
  /// </summary>
  private Material whiteDiffuseMat;

  void Start () {
    if (!Features.showMultiselectTooltip) {
      Destroy(this);
      return;
    }
    whiteDiffuseMat = new Material(Shader.Find("Particles/Additive"));
  }
	
  /// <summary>
  ///   - Draw the hint rope for each selected meash that is within the calculated distance threshold.
  ///   - Remove the hint ropes that do not meet the distance threshold.
  /// </summary>
  void Update () {
    if (PeltzerMain.Instance.GetMover().userHasPerformedGroupAction) {
      // destroy self
      DestroySelf();
      return;
    }

        // Get the reference to the center of the icon over the appMenuButton
        // as it has the correct registration point and appMenuButton does not.
        destination = PeltzerMain.Instance.peltzerController.controllerGeometry.groupButtonIcon;

    int selectedCount = PeltzerMain.Instance.GetSelector().selectedMeshes.Count;

    // Nothing to do, return.
    if (selectedCount == 0) {
      PruneRopes();
      return;
    }

    // Prune ropes to remove invalid hint ropes.
    PruneRopes();

    if (selectedCount > 1) {
      foreach (int meshId in PeltzerMain.Instance.GetSelector().selectedMeshes) {
        GameObject go;
        LineRenderer lineRenderer;
        MMesh mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        Vector3 sourcePos = PeltzerMain.Instance.worldSpace
            .ModelToWorld(mesh.bounds.center);

        float meshBoundsMaxEstimate = GetMaxElement(mesh.bounds.size);

        // If meshId not found, add entry.
        if (!ropes.TryGetValue(meshId, out go)) {

          // Calculate distance threshold.
          float dist = Vector3.Distance(sourcePos, destination.transform.position);

          if (dist > (distThresh + meshBoundsMaxEstimate) * PeltzerMain.Instance.worldSpace.scale) {
            continue;
          }

          // Create a GameObject instance to attach the line renderer.
          go = new GameObject("hint_rope");
          go.transform.position = destination.transform.position;

          // Create a Line Renderer and attach to the gameobject.
          lineRenderer = go.AddComponent<LineRenderer>();
          lineRenderer.startWidth = hintWidth;
          lineRenderer.endWidth = hintWidth;

          // Set the material on the line renderer. 
          lineRenderer.material = whiteDiffuseMat;
          lineRenderer.startColor = startColor;
          lineRenderer.endColor = endColor;

          // Add the gameobject reference to ropes.
          ropes.Add(meshId, go);
        } else {
          // Get reference to line renderer
          lineRenderer = go.GetComponent<LineRenderer>();
        }
        
        // Update source position - [0].
        lineRenderer.SetPosition(0, sourcePos);

        // Update destination position - [1].
        lineRenderer.SetPosition(1, destination.transform.position);
      }
    }
  }

  /// <summary>
  ///   Determine which ropes are not relevant and remove them.
  /// </summary>
  private void PruneRopes() {
    // Nothing to prune, return;
    if (ropes.Count == 0) return;

    List<int> pruneList = new List<int>();
    foreach (KeyValuePair<int, GameObject> rope in ropes) {
      // Mark rope for pruning if no longer selected.
      if ( !PeltzerMain.Instance.GetSelector().selectedMeshes.Contains(rope.Key)) {
        pruneList.Add(rope.Key);
      } else {
        // Calculate distance threshold.
        Vector3 sourcePos = rope.Value.GetComponent<LineRenderer>().GetPosition(0);
        float dist = Vector3.Distance(sourcePos, destination.transform.position);
        float meshBoundsMaxEstimate = GetMaxElement(PeltzerMain.Instance.model.GetMesh(rope.Key).bounds.size);

        // Mark rope for pruning if distance is too great.
        if (dist > (distThresh + meshBoundsMaxEstimate) * PeltzerMain.Instance.worldSpace.scale) {
          pruneList.Add(rope.Key);
        }
      }
    }

    // Prune!
    foreach (int key in pruneList) {
      DestroyHintRope(key);
    }
  }

  /// <summary>
  ///   Destroy the rope associated with meshId
  /// </summary>
  /// <param name="meshId">The mesh id of the assocated rope.</param>
  private void DestroyHintRope(int meshId) {
    // Destroy the GameObject holding the LineRenderer.
    Destroy(ropes[meshId]);
    ropes.Remove(meshId);
  }

  /// <summary>
  ///   Destroy the script.
  /// </summary>
  private void DestroySelf() {
    foreach (KeyValuePair<int, GameObject> rope in ropes) {
      Destroy(rope.Value);
    }
    Destroy(this);
  }

  /// <summary>
  ///   Convenience method for getting the max of three floats stored in a Vector3.
  /// </summary>
  /// <param name="v3">Vector3 holding the 3 values from which we want to find the max.</param>
  /// <returns></returns>
  private float GetMaxElement(Vector3 v3) {
    return Mathf.Max(Mathf.Max(v3.x, v3.y), v3.z);
  }
}
