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
using System.Linq;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.tools.utils {
  /// <summary>
  /// These enums help to specify a style in which an effect for a vertex, edge, or face is rendered.  This is
  /// orthogonal to whether the element is turned on or off (though once an element finishes animating off its style
  /// will be cleared).
  /// </summary>
  public enum VertexStyles : int {
    VERTEX_SELECT=0,
    VERTEX_INACTIVE
  };

  public enum EdgeStyles : int {
    EDGE_SELECT=0,
    EDGE_INACTIVE,
  };

  public enum FaceStyles : int {
    FACE_SELECT=0,
    PAINT, // Uses vertex colors for highlight color
    EXTRUDE
  };

  public enum MeshStyles : int {
    MESH_SELECT=0,
    MESH_DELETE,
    MESH_PAINT,
    TUTORIAL_HIGHLIGHT
  };

  /// <summary>
  /// Manages the presentation (rendering and animation) of highlighted geometry elements based on highlight state
  /// supplied by tools that need highlight UI.
  /// </summary>
  public class HighlightUtils : MonoBehaviour {

    public static readonly float DEFAULT_EDGE_DIMENSION = 0.001f;

    /// <summary>
    /// Internal class for tracking highlight information common to highlighting edges, faces, and vertices.
    /// Mostly, this manages in and out animation timing.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TrackedHighlightSet<T> {
      // Pairs of element keys and times of their animation events.  For fade-in, the time is the time in seconds at
      // which the element should start fading in.  For fade-out, the time is the negative of the time at which the
      // element should start fading out.
      private Dictionary<T, float> selectTimes;
      private Dictionary<T, int> styles;
      private Dictionary<T, float> animationInDurations;
      // While we can derive this from the above, we'd need to do so every frame so it's cheaper to maintain as we go.
      private Dictionary<int, HashSet<T>> elementsByStyle;
      private float animationDurationIn;
      private float animationDurationOut;

      //Exposed for renderers to optimize against.
      public HashSet<T> newlyAdded;
      public HashSet<T> newlyRemoved;
      // Custom channels which may need to be used for various effects, containing data such as effect origin point and
      // color.
      // A given effect should use these in order - ie, customChannel1 should not be used unless customChannel0 is
      // already being used.
      // A given style id should be tied to a specific usage of the data in these channels, and there's probably an
      // elegant way to enforce that. But for now we use the honor system.
      private Dictionary<T, Vector4> customChannel0;
      private Dictionary<T, Vector4> customChannel1;

      private void ClearCustomChannels(T key) {
        if (customChannel0.ContainsKey(key)) {
          customChannel0.Remove(key);
          // Should only need to check channel 1 if there was data in channel 0.
          if (customChannel1.ContainsKey(key)) {
            customChannel1.Remove(key);
          }
        }
      }

      /// <param name="animationDuration">The duration of a fade-in or a fade-out</param>
      public TrackedHighlightSet(float animationDurationIn,
        float animationDurationOut,
        IEnumerable<int> permittedStyles) {
        selectTimes = new Dictionary<T, float>();
        styles = new Dictionary<T, int>();
        animationInDurations = new Dictionary<T, float>();
        elementsByStyle = new Dictionary<int, HashSet<T>>();
        this.animationDurationIn = animationDurationIn;
        this.animationDurationOut = animationDurationOut;
        foreach (int styleEnumId in permittedStyles) {
          elementsByStyle[styleEnumId] = new HashSet<T>();
        }
        this.customChannel0 = new Dictionary<T, Vector4>();
        this.customChannel1 = new Dictionary<T, Vector4>();
        this.newlyAdded = new HashSet<T>();
        this.newlyRemoved = new HashSet<T>();
      }

      /// <summary>
      /// Returns the number of renderable elements (distinct from the number of selected elements.
      /// </summary>
      /// <returns></returns>
      public int RenderableCount() {
        return selectTimes.Count;
      }

      // Turns highlighting for a given element on (it will animate in over animationDuration seconds)
      // Optionally accepts an animation duration to override the default.
      public void TurnOn(T key, float? durationIn = null) {
        float thisAnimationDuration;
        if (!animationInDurations.TryGetValue(key, out thisAnimationDuration)) {
          thisAnimationDuration = durationIn == null ? animationDurationIn : durationIn.Value;
          animationInDurations[key] = thisAnimationDuration;
        }

        float selectTime;
        if (!selectTimes.TryGetValue(key, out selectTime)) {
          selectTimes[key] = Time.time;
          newlyAdded.Add(key);
        } else {
          if (selectTime < 0) {
            float curPct = Mathf.Min(1.0f, (Time.time + selectTime) / animationDurationOut);
            selectTimes[key] = 
              Mathf.Min(Time.time, Time.time + thisAnimationDuration - curPct * thisAnimationDuration);
          }
        }
        if (!styles.ContainsKey(key)) {
          styles.Add(key, 0);
          elementsByStyle[0].Add(key);
          customChannel0.Add(key, Vector4.zero);
          customChannel1.Add(key, Vector4.zero);
        }
      }

      public void SetStyle(T key, int styleId) {
        SetStyle(key, styleId, Vector4.zero, Vector4.zero);
      }

      public void SetStyle(T key, int styleId, Vector4 channelData0) {
        SetStyle(key, styleId, channelData0, Vector4.zero);
      }

      public void SetStyle(T key, int styleId, Vector4 channelData0, Vector4 channelData1) {
        int oldStyle;
        if (styles.TryGetValue(key, out oldStyle)) {
          elementsByStyle[oldStyle].Remove(key);
        }
        styles[key] = styleId;
        elementsByStyle[styleId].Add(key);
        customChannel0[key] = channelData0;
        customChannel1[key] = channelData1;
      }

      // Turns highlighting for a given element on immediately
      public void TurnOnImmediate(T key) {
        selectTimes[key] = Time.time - animationInDurations[key];
        if (!styles.ContainsKey(key)) {
          styles.Add(key, 0);
        }
      }

      // Turns highlighting for a given element off (it will still display until it finishes animating out)
      public void TurnOff(T key) {
        if (selectTimes.ContainsKey(key)) {
          if (selectTimes[key] > 0) {
            float curPct = Mathf.Min(1.0f, (Time.time - selectTimes[key]) / animationInDurations[key]);
            selectTimes[key] = Mathf.Max(-Time.time, -(Time.time +
                                                       (animationDurationOut - curPct * animationDurationOut)));
          }
        }
      }

      // Turns highlighting for a given element off (it will still display until it finishes animating out)
      public void TurnOffImmediate(T key) {
        if (selectTimes.ContainsKey(key)) {
          if (selectTimes[key] > 0) {
            selectTimes[key] = -Time.time + animationDurationOut;
          }
        }
      }

      public IEnumerable<T> Keys() {
        return selectTimes.Keys;
      }

      /// <summary>
      /// Returns the percentage of the way through the animation the given element is, with 100% being fully visible
      /// and 0% being hidden.
      /// </summary>
      /// <param name="key"></param>
      /// <returns></returns>
      public float GetAnimPct(T key) {
        float curPct = 0f;
        if (!selectTimes.ContainsKey(key)) return curPct;
        if (selectTimes[key] > 0) return Mathf.Min(1.0f, (Time.time - selectTimes[key]) / animationInDurations[key]);
        return 1.0f - Mathf.Min(1.0f, (Time.time + selectTimes[key]) / animationDurationOut);
      }

      public HashSet<T> getKeysForStyle(int styleId) {
        return elementsByStyle[styleId];
      }

      public void ClearAll() {
        newlyRemoved.UnionWith(selectTimes.Keys);
        selectTimes.Clear();
        styles.Clear();
        foreach (int styleId in elementsByStyle.Keys) {
          elementsByStyle[styleId].Clear();
        }
        customChannel0.Clear();
        customChannel1.Clear();
        newlyAdded.Clear();
      }

      public Vector4 GetCustomChannel0(T key) {
        return customChannel0[key];
      }

      public Vector4 GetCustomChannel1(T key) {
        return customChannel1[key];
      }

      // Removes all animations that have completed fading out
      public void ClearExpired() {
        newlyAdded.Clear();
        newlyRemoved.Clear();
        List<T> keysToRemove = new List<T>();
        foreach (T key in selectTimes.Keys) {
          if (selectTimes[key] < 0) {
            if (Time.time + selectTimes[key] > animationDurationOut) {
              keysToRemove.Add(key);
            }
          }
        }
        foreach (T key in keysToRemove) {
          selectTimes.Remove(key);
          elementsByStyle[styles[key]].Remove(key);
          styles.Remove(key);
          ClearCustomChannels(key);
          newlyRemoved.Add(key);
        }
      }
    }

    private WorldSpace worldSpace;
    private Model model;

    private TrackedHighlightSet<VertexKey> vertexHighlights;
    private TrackedHighlightSet<EdgeKey> edgeHighlights;
    private TrackedHighlightSet<EdgeTemporaryStyle.TemporaryEdge> temporaryEdgeHighlights;
    private TrackedHighlightSet<FaceKey> faceHighlights;
    private TrackedHighlightSet<int> meshHighlights;
    public InactiveRenderer inactiveRenderer;
    private bool isSetup = false;

    public const float VERT_EDGE_ANIMATION_DURATION_IN = 0.08f;
    public const float VERT_EDGE_ANIMATION_DURATION_OUT = 0.08f;
    public const float FACE_ANIMATION_DURATION_IN = 0.4f;
    public const float FACE_ANIMATION_DURATION_OUT = 0.15f;
    public const float MESH_ANIMATION_DURATION_IN = 0.4f;
    public const float MESH_ANIMATION_DURATION_OUT = 0.15f;
    // Face highlight animation durations are (inversely) correlated with face size.
    // This is the base for that calculation.
    private const float BASE_FACE_HIGHLIGHT_DURATION = 0.1125f;
    // Mesh highlight animation durations are (inversely) correlated with face size.
    // This is the base for that calculation.
    private const float MESH_FACE_HIGHLIGHT_DURATION = 0.225f;
    /// <summary>
    /// Sets up materials and data structures for managing highlights.
    /// </summary>
    public void Setup(WorldSpace worldSpace, Model model, MaterialLibrary materialLibrary) {
      this.worldSpace = worldSpace;
      this.model = model;
      EdgeSelectStyle.material = new Material(materialLibrary.edgeHighlightMaterial);
      EdgeInactiveStyle.material = new Material(materialLibrary.edgeInactiveMaterial);
      EdgeTemporaryStyle.material = new Material(materialLibrary.edgeHighlightMaterial);
      FaceSelectStyle.material = new Material(materialLibrary.faceHighlightMaterial);
      FacePaintStyle.material = new Material(materialLibrary.facePaintMaterial);
      FaceExtrudeStyle.material = new Material(materialLibrary.faceExtrudeMaterial);
      MeshSelectStyle.material = new Material(materialLibrary.meshSelectMaterial);
      MeshSelectStyle.silhouetteMaterial = new Material(materialLibrary.highlightSilhouetteMaterial);
      MeshPaintStyle.material = new Material(materialLibrary.meshSelectMaterial);
      VertexSelectStyle.material = new Material(materialLibrary.pointHighlightMaterial);
      VertexInactiveStyle.material = new Material(materialLibrary.pointInactiveMaterial);
      TutorialHighlightStyle.material = materialLibrary.meshSelectMaterial;

      
      MeshSelectStyle.Setup();
      MeshPaintStyle.Setup();
      MeshDeleteStyle.Setup();
      TutorialHighlightStyle.Setup();

      inactiveRenderer = new InactiveRenderer(model, worldSpace, materialLibrary);
      vertexHighlights = new TrackedHighlightSet<VertexKey>(VERT_EDGE_ANIMATION_DURATION_IN,
        VERT_EDGE_ANIMATION_DURATION_OUT,
        new [] {(int)VertexStyles.VERTEX_SELECT, (int)VertexStyles.VERTEX_INACTIVE});

      edgeHighlights = new TrackedHighlightSet<EdgeKey>(VERT_EDGE_ANIMATION_DURATION_IN,
        VERT_EDGE_ANIMATION_DURATION_OUT,
        new [] {(int)EdgeStyles.EDGE_SELECT, (int)EdgeStyles.EDGE_INACTIVE});

      temporaryEdgeHighlights = new TrackedHighlightSet<EdgeTemporaryStyle.TemporaryEdge>(VERT_EDGE_ANIMATION_DURATION_IN,
        VERT_EDGE_ANIMATION_DURATION_OUT,
        new[] { (int)EdgeStyles.EDGE_SELECT});

      faceHighlights = new TrackedHighlightSet<FaceKey>(FACE_ANIMATION_DURATION_IN,
       FACE_ANIMATION_DURATION_OUT,
         new [] {
           (int)FaceStyles.FACE_SELECT,
           (int)FaceStyles.PAINT,
           (int)FaceStyles.EXTRUDE
         });

      meshHighlights = new TrackedHighlightSet<int>(MESH_ANIMATION_DURATION_IN,
        MESH_ANIMATION_DURATION_OUT,
        new [] {(int)MeshStyles.MESH_SELECT, (int)MeshStyles.MESH_DELETE, (int)MeshStyles.MESH_PAINT,
        (int)MeshStyles.TUTORIAL_HIGHLIGHT});
    }

    // Turns the highlight on for the edge with the supplied key, optionally over the given duration.
    public void TurnOn(EdgeKey key, float? durationIn = null) {
      edgeHighlights.TurnOn(key, durationIn);
    }

    // Turns the highlight off for the edge with the supplied key.
    public void TurnOff(EdgeKey key) {
      edgeHighlights.TurnOff(key);
    }

    // Turns highlights off immediately for all edges (no fade-out).
    public void ClearEdges() {
      edgeHighlights.ClearAll();
    }

    // Turns the highlight on for the temporary edge with the supplied key, optionally over the given duration.
    public void TurnOn(EdgeTemporaryStyle.TemporaryEdge key, float? durationIn = null) {
      temporaryEdgeHighlights.TurnOn(key, durationIn);
    }

    // Turns the highlight off for the temporary edge with the supplied key.
    public void TurnOff(EdgeTemporaryStyle.TemporaryEdge key) {
      temporaryEdgeHighlights.TurnOff(key);
    }

    // Turns highlights off immediately for all temporary edges (no fade-out).
    public void ClearTemporaryEdges() {
      temporaryEdgeHighlights.ClearAll();
    }


    // Turns the highlight on for the vertex with the supplied key.
    public void TurnOn(VertexKey key) {
      vertexHighlights.TurnOn(key);
    }

    // Turns the highlight off for the vertex with the supplied key.
    public void TurnOff(VertexKey key) {
      vertexHighlights.TurnOff(key);
    }

    // Turns highlighting off immediately for all vertices (no fade-out).
    public void ClearVertices() {
      vertexHighlights.ClearAll();
    }

    // Sets the style for the specified vertex highlight to Select.
    public void SetVertexStyleToSelect(VertexKey key) {
      vertexHighlights.SetStyle(key, (int) VertexStyles.VERTEX_SELECT);
    }

    // Sets the style for the specified vertex highlight to inactive.
    public void SetVertexStyleToInactive(VertexKey key) {
      vertexHighlights.SetStyle(key, (int) VertexStyles.VERTEX_INACTIVE);
    }
    
    // Sets the style for the specified edge highlight to Select.
    public void SetEdgeStyleToSelect(EdgeKey key) {
      edgeHighlights.SetStyle(key, (int) EdgeStyles.EDGE_SELECT);
    }

    // Sets the style for the specified edge highlight to inactive.
    public void SetEdgeStyleToInactive(EdgeKey key) {
      edgeHighlights.SetStyle(key, (int) EdgeStyles.EDGE_INACTIVE);
    }

    // Sets the style for the specified edge highlight to Select.
    public void SetTemporaryEdgeStyleToSelect(EdgeTemporaryStyle.TemporaryEdge key) {
      temporaryEdgeHighlights.SetStyle(key, (int)EdgeStyles.EDGE_SELECT);
    }

    // Sets the style for the specified face highlight to Paint, using the specified position as the origin of the
    // effect, and the color as the paint color.
    public void SetFaceStyleToPaint(FaceKey key, Vector3 position, Color color) {
      Vector3 worldSelectPosition = worldSpace.ModelToWorld(position);
      faceHighlights.SetStyle(key,
        (int)FaceStyles.PAINT,
        new Vector4(worldSelectPosition.x, worldSelectPosition.y, worldSelectPosition.z, 1f),
        new Vector4(color.r, color.g, color.b, color.a));
    }

    // Sets the style for the specified face highlight to Extrude, using the specified position as the origin of the
    // effect, and the color as the paint color. (Extrude effect is WIP, so these args may change)
    public void SetFaceStyleToExtrude(FaceKey key, Vector3 positionModel, Color color) {
      faceHighlights.SetStyle(key,
        (int)FaceStyles.EXTRUDE,
        new Vector4(positionModel.x, positionModel.y, positionModel.z, 1f),
        new Vector4(color.r, color.g, color.b, color.a));
    }

    // Sets the style for the specified face highlight to Select, using the specified position as the origin of the
    // selection animation.
    public void SetFaceStyleToSelect(FaceKey key, Vector3 position) {
      Vector3 worldSelectPosition = worldSpace.ModelToWorld(position);
      faceHighlights.SetStyle(key,
        (int) FaceStyles.FACE_SELECT,
        new Vector4(worldSelectPosition.x, worldSelectPosition.y, worldSelectPosition.z, 1f));
    }

    // Turns the highlight on for the vertex with the supplied key.
    public void TurnOn(FaceKey key, Vector3 selectionPos) {
      MMesh mesh = model.GetMesh(key.meshId);
      Face face = mesh.GetFace(key.faceId);

      // Assuming most faces have only a handful of verts, this is pretty cheap.
      Bounds bounds = new Bounds(mesh.VertexPositionInModelCoords(face.vertexIds[0]), Vector3.zero);
      for (int i = 1; i < face.vertexIds.Count; i++) {
        bounds.Encapsulate(mesh.VertexPositionInModelCoords(face.vertexIds[i]));
      }

      // On our standard cube in grid mode with default zoom, each scale-up operation
      // increases this magnitude by ~0.0145. The magnitude of the default cube is ~0.0565
      float magnitude = Mathf.Max(0.05f, bounds.size.magnitude * worldSpace.scale);

      faceHighlights.TurnOn(key, BASE_FACE_HIGHLIGHT_DURATION * magnitude);
    }

    // Turns the highlight on for the vertex with the supplied key.
    public void TurnOn(FaceKey key) {
      TurnOn(key, Vector4.zero);
    }

    // Turns the highlight off for the vertex with the supplied key.
    public void TurnOff(FaceKey key) {
      faceHighlights.TurnOff(key);
    }

    // Turns highlighting off immediately for all vertices (no fade-out).
    public void ClearFaces() {
      faceHighlights.ClearAll();
    }

    // Turns the highlight on for the mesh with the supplied key.
    public void TurnOnMesh(int meshId, Vector3 selectionPos) {
      MMesh mesh = model.GetMesh(meshId);

      // On our standard cube in grid mode with default zoom, each scale-up operation
      // increases this magnitude by ~0.0145. The magnitude of the default cube is ~0.0565
      float magnitude = Mathf.Max(0.5f, mesh.bounds.size.magnitude * worldSpace.scale);

      meshHighlights.TurnOn(meshId, MESH_FACE_HIGHLIGHT_DURATION * magnitude);
    }

    // Turns the highlight on for the mesh with the supplied key.
    public void TurnOnMesh(int meshId) {
      TurnOnMesh(meshId, Vector4.zero);
    }

    // Turns the highlight off for the mesh with the supplied key.
    public void TurnOffMesh(int meshId) {
      meshHighlights.TurnOffImmediate(meshId);
    }

    // Turns highlighting off immediately for all meshes (no fade-out).
    public void ClearMeshes() {
      meshHighlights.ClearAll();
    }

    // Sets the style for the specified mesh highlight to Delete.
    public void SetMeshStyleToDelete(int meshId) {
      meshHighlights.SetStyle(meshId, (int) MeshStyles.MESH_DELETE);
    }

    // Sets the style for the specified mesh highlight to Delete.
    public void SetMeshStyleToPaint(int meshId) {
      meshHighlights.SetStyle(meshId, (int)MeshStyles.MESH_PAINT);
    }

    // Sets the style for the specified mesh highlight to Tutorial.
    public void SetMeshStyleToTutorial(int meshId) {
      meshHighlights.SetStyle(meshId, (int)MeshStyles.TUTORIAL_HIGHLIGHT);
    }

    // Clears all highlight state managed here.
    public void ClearAll() {
      vertexHighlights.ClearAll();
      edgeHighlights.ClearAll();
      ClearFaces();
      ClearMeshes();
      MeshCycler.ResetCycler();
    }

    // Renders highlight meshes for all selected geometric elements.
    public void LateUpdate() {
      RenderVertices();
      RenderEdges();
      RenderFaces();
      RenderMeshes();
      inactiveRenderer.RenderEdges();
      inactiveRenderer.RenderPoints();
    }

    // Renders vertex highlights.
    private void RenderVertices() {
      VertexSelectStyle.RenderVertices(model, vertexHighlights, worldSpace);
      VertexInactiveStyle.RenderVertices(model, vertexHighlights, worldSpace);
      vertexHighlights.ClearExpired();
    }

    // Renders edge highlights.
    private void RenderEdges() {
      EdgeSelectStyle.RenderEdges(model, edgeHighlights, worldSpace);
      EdgeInactiveStyle.RenderEdges(model, edgeHighlights, worldSpace);
      EdgeTemporaryStyle.RenderEdges(model, temporaryEdgeHighlights, worldSpace);
      edgeHighlights.ClearExpired();
    }

    // Renders face highlights.
    // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
    // face geometry frame to frame)
    private void RenderFaces() {
      FaceSelectStyle.RenderFaces(model, faceHighlights, worldSpace);
      FacePaintStyle.RenderFaces(model, faceHighlights, worldSpace);
      FaceExtrudeStyle.RenderFaces(model, faceHighlights, worldSpace);
      faceHighlights.ClearExpired();
    }

    // Renders mesh highlights.
    private void RenderMeshes() {
      MeshSelectStyle.RenderMeshes(model, meshHighlights, worldSpace);
      MeshDeleteStyle.RenderMeshes(model, meshHighlights, worldSpace);
      MeshPaintStyle.RenderMeshes(model, meshHighlights, worldSpace);
      if (PeltzerMain.Instance.tutorialManager.TutorialOccurring()) {
        TutorialHighlightStyle.RenderMeshes(model, meshHighlights, worldSpace);
      }
      meshHighlights.ClearExpired();
    }
  }
}
