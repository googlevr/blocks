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
using System.Collections.Generic;
using System.Linq;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.alignment;
using System.Collections.ObjectModel;

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  ///   Representation of an Axes which is made of three vectors. Each vector represents the right, up, and forward
  ///   orientation of space.
  /// </summary>
  public class Axes {
    public static Axes identity = new Axes(Vector3.right, Vector3.up, Vector3.forward);
    public enum Axis { RIGHT, UP, FORWARD };

    public Vector3 right;
    public Vector3 up;
    public Vector3 forward;

    public Axes(Vector3 right, Vector3 up, Vector3 forward) {
      this.right = right;
      this.up = up;
      this.forward = forward;
    }

    /// <summary>
    ///   Finds the rotation from one Axes to another.
    /// </summary>
    /// <param name="from">The starting Axes.</param>
    /// <param name="to">The final Axes.</param>
    /// <returns>The rotational difference between the two Axes'.</returns>
    public static Quaternion FromToRotation(Axes from, Axes to) {
      // To find the rotation between two Axes we only need to find the difference between two of the axes since a
      // pair of axes must move together to maintain the 90 degree angles between them. So we will start by moving
      // one arbitrary Axes into place. Then applying this change to a second Axes and finding the remaining
      // difference between this Axes and the universal grid axes.

      // Start by finding the rotation difference between the to.up axis and the from.up.
      Quaternion yRotation = Quaternion.FromToRotation(from.up, to.up);

      // Apply the yRotation to the from.right then find the difference between the partially rotated from.right axis
      // and the to.right axis.
      Quaternion residualRotation = Quaternion.FromToRotation(yRotation * from.right, to.right);

      // Combine the rotations to find the rotational difference.
      return residualRotation * yRotation;
    }

    /// <summary>
    /// Given the coplanarVertices of face determine the axes for the face.
    /// </summary>
    /// <param name="coplanarVertices">Vertices of the face.</param>
    /// <returns>The axes that define the face.</returns>
    public static Axes FindAxesForAFace(List<Vector3> coplanarVertices) {
      Vector3 forward = FindForwardAxis(coplanarVertices);
      Vector3 right = FindRightAxis(coplanarVertices);
      Vector3 up = FindUpAxis(forward, right);

      return new Axes(right, up, forward);
    }

    /// <summary>
    /// Finds the forward axis given the veritces of a face, which is just the normal out of the origin.
    /// </summary>
    /// <param name="coplanarVertices">The vertices of a face.</param>
    /// <returns>The forward axis.</returns>
    public static Vector3 FindForwardAxis(List<Vector3> coplanarVertices) {
      return MeshMath.CalculateNormal(coplanarVertices);
    }

    /// <summary>
    ///   Finds the right axis of a face by comparing all the edges in the face and choosing an edge for the right axis
    ///   that is the most representative of the other edges. Essentially we are trying to find an edge that is
    ///   perpendicular to as many edges as possible so that we can rotate the preview to align to the greatest number
    ///   of edges. Since the forward axis is the face normal any edge is guaranteed to be perpendicular to the normal.
    /// </summary>
    /// <param name="coplanarVertices">The vertices of a face.</param>
    /// <returns>The right axis.</returns>
    public static Vector3 FindRightAxis(List<Vector3> coplanarVertices) {
      EdgeInfo mostRepresentativeEdge = MeshMath.FindMostRepresentativeEdge(coplanarVertices);
      return mostRepresentativeEdge.edgeVector.normalized;
    }

    /// <summary>
    ///   Finds the up axis which is just the axis perpendicular to the right and forward axis. We use the left hand
    ///   rule to make sure the up axis points the right way so snapGrid.forward, up and right are related the same
    ///   as the universal Vector3.forward, up and right.
    /// </summary>
    /// <param name="right">The right axis.</param>
    /// <param name="forward">The forward axis.</param>
    /// <returns>The cross product of the right and forward axis.</returns>
    public static Vector3 FindUpAxis(Vector3 right, Vector3 forward) {
      return Vector3.Cross(forward, right).normalized;
    }
  }

  /// <summary>
  ///   Holds a snaptransform as well as any additional information needed to render UX effects related to Snapping.
  /// </summary>
  public struct SnapInfo {
    // The transform needed to snap the object to the grid
    internal SnapTransform transform;
    // The point on the surface that we're snapping to in face snap
    internal Vector3 snapPoint;
    // The normal of that point
    internal Vector3 snapNormal;
    // The position of the face that is being snapped
    internal Vector3 snappingFacePosition;
    // Whether we're within the threshhold to snap directly to the surface
    internal bool inSurfaceThreshhold;
  }

  public class SnapGrid {
    public enum SnapType { NONE, VERTEX, FACE, MESH, UNIVERSAL };
    // TODO (bug) These thresholds are not optimized and should be calibrated following more testing.
    /// <summary>
    ///   Floating point error threshold for comparing angles.
    /// </summary>
    private const float angleThreshold = 0.01f;
    /// <summary>
    ///   A threshold above which a face is considered too far away to select.
    ///   This is in Unity units, where 1.0f = 1 meter by default and were chosen by testing and iterating.
    /// </summary>
    private const float FACE_CLOSENESS_THRESHOLD_DEFAULT = 0.1f;
    /// <summary>
    ///   A threshold above which a vertex is considered too far away to select.
    ///   This is in Unity units, where 1.0f = 1 meter by default and were chosen by testing and iterating.
    /// </summary>
    private const float VERTEX_CLOSENESS_THRESHOLD_DEFAULT = 0.005f;
    /// <summary>
    ///   A threshold above which the angle (in degrees) between two faces is too great to snap them together. The
    ///   angle between two faces is defined as the rotational difference to make them flush.
    ///   Chosen to be smaller than the angle between any two faces on a primitive.
    /// </summary>
    private const float FACE_ANGLE_THRESHOLD_DEFAULT = 25.0f;
    /// <summary>
    ///   Threshold for surface snapping.
    /// </summary>
    private float surfaceThreshold;
    /// <summary>
    ///   Threshold for center snapping.
    /// </summary>
    private float centerThreshold;
    /// <summary>
    ///   Threshold for edge snapping.
    /// </summary>
    private float edgeThreshold;
    /// <summary>
    ///   The anchor type that generated this snapGrid.
    /// </summary>
    public SnapType snapType;
    /// <summary>
    ///   The origin of the snapGrid.
    /// </summary>
    private Vector3 origin;
    /// <summary>
    ///   The positional difference between the snapGrid and the universal grid.
    /// </summary>
    private Vector3 offset;
    /// <summary>
    ///   The rotation of the snapGrid.
    /// </summary>
    private Quaternion rotation = Quaternion.identity;
    /// <summary>
    ///   The SnapGrid normal. This is either the normal of the snapFace, the vertexNormal or zero if
    ///   snapType = MESH or UNIVERSAL.
    /// </summary>
    private Vector3 normal;
    /// <summary>
    ///   The three vectors that represent the Axes of the snapGrid.
    /// </summary>
    private Axes snapAxes;
    /// <summary>
    ///   A position used as the center for snapping. Not necessarily the same as the origin.
    /// </summary>
    public Vector3 snapCenter;
    /// <summary>
    ///   The clockwise, coplanar vertices that make up the snapFace.
    /// </summary>
    private List<Vector3> coplanarSnapFaceVertices;
    private static EdgeInfo mostRepresentativeEdge;
    private bool hasComparableFace;

    public SnapGrid(MMesh previewMesh, Vector3 previewMeshOffset, Quaternion previewMeshRotation, Model model,
      SpatialIndex spatialIndex, WorldSpace worldSpace, bool isSubtract, out Quaternion finalPreviewMeshRotation,
      out Face previewFace, out List<Vector3> coplanarPreviewFaceVerticesAtOrigin, out int targetMeshId) {
      // Setup the thresholds for surface, center and edge snapping relative to the previewMesh size.
      // TODO (bug): These thresholds are still being tested and iterated on so the repetition is being left for
      // clarity.
      surfaceThreshold =
        Mathf.Min(previewMesh.bounds.size.x, previewMesh.bounds.size.y, previewMesh.bounds.size.z) * 2.0f;
      centerThreshold =
        Mathf.Min(previewMesh.bounds.size.x, previewMesh.bounds.size.y, previewMesh.bounds.size.z) * 0.75f;
      edgeThreshold =
        Mathf.Min(previewMesh.bounds.extents.x, previewMesh.bounds.extents.y, previewMesh.bounds.extents.z);

      // We decide whether to MeshSnap, FaceSnap, VertexSnap or UniversalSnap using the following method.
      // First find all nearbyFaces, nearbyVertices, closestFace and closestVertex and cache the results then:

      // 1) If there are more than six faces on the same mesh in nearbyFaces: MeshSnap
      // 2) If there are any nearbyFaces and the closestFace is closer than the closestVertex: FaceSnap
      // 3) If we didn't face or mesh snap because there were no faces or closestVertex is closer than closestFace and
      //    there are nearbyVertices: VertexSnap
      // 4) Default to the universalGrid.

      // Set a radius for selecting a face to the radius of the previewMesh plus a small default threshold. Using
      // this radius we can call directly to the spatialIndex to find all nearbyFaces within the selection radius.
      float faceClosenessThreshold = FACE_CLOSENESS_THRESHOLD_DEFAULT
        + Mathf.Max(previewMesh.bounds.extents.x, previewMesh.bounds.extents.y, previewMesh.bounds.extents.z);
      List<DistancePair<FaceKey>> nearbyFacePairs;
      spatialIndex.FindFacesClosestTo(previewMeshOffset, faceClosenessThreshold, /*ignoreInFace*/ false,
        out nearbyFacePairs);

      // Set a radius for selecting a vertex to the radius of the previewMesh plus a small default threshold. Using
      // this radius we can call directly to the spatialIndex to find all nearbyVertices within the selection radius.
      float vertexClosenessThreshold = VERTEX_CLOSENESS_THRESHOLD_DEFAULT
        + Mathf.Max(previewMesh.bounds.extents.x, previewMesh.bounds.extents.y, previewMesh.bounds.extents.z);
      List<DistancePair<VertexKey>> nearbyVertexPairs;
      spatialIndex.FindVerticesClosestTo(previewMeshOffset, vertexClosenessThreshold, out nearbyVertexPairs);

      FacePair closestFace = new FacePair();
      if (nearbyFacePairs.Count > 0) {
        hasComparableFace = MeshMath.FindClosestFace(nearbyFacePairs, previewMesh, previewMeshOffset, previewMeshRotation,
          model, FACE_ANGLE_THRESHOLD_DEFAULT, out closestFace);
      }

      FaceVertexPair closestVertex = new FaceVertexPair();
      if (nearbyVertexPairs.Count > 0) {
        closestVertex = MeshMath.FindClosestVertex(nearbyVertexPairs, previewMesh, previewMeshOffset, previewMeshRotation,
          model);
      }

      // First try snapping to a mesh. If there are more than 3 nearby faces belonging to the same mesh the user
      // probably wants to mesh snap instead of face snapping.
      if (nearbyFacePairs.Count > 0) {
        // Try finding a mesh to snap to.
        int nearestMeshId;
        if (MeshMath.TryFindingNearestMeshGivenNearbyFaces(nearbyFacePairs, 3, out nearestMeshId) || isSubtract) {
          // Snap to a mesh.
          MMesh nearestMesh = model.GetMesh(nearestMeshId);

          // Setup the snapGrid for mesh snapping.
          SetupMeshSnapGrid(previewMeshOffset, nearestMesh);

          // Snapping to a mesh doesn't require any knowledge of the previewFace so we can pass back null.
          previewFace = null;
          coplanarPreviewFaceVerticesAtOrigin = null;

          // The previewMesh should copy the snapMeshes rotation to the nearest 90 degrees.
          finalPreviewMeshRotation = GridUtils.SnapToNearest(previewMeshRotation, nearestMesh.rotation, 90f);
          targetMeshId = nearestMeshId;
          return;
        }

        // If we didn't mesh snap there are still faces we could snap to. We will snap to the closetFace calculated
        // early unless the closetVertex is closer than the closestFace or there are no nearby vertices.
        if (hasComparableFace && (nearbyVertexPairs.Count == 0 || closestVertex.separation > closestFace.separation)) {
          // First, calculate the center of the face being snapped to in model space, for comparison across meshes.
          FaceKey snapFaceKey = closestFace.toFaceKey;
          MMesh snapMesh = model.GetMesh(snapFaceKey.meshId);
          Face snapFace = snapMesh.GetFace(snapFaceKey.faceId);
          List<Vector3> coplanarSnapFaceVertices = new List<Vector3>(snapFace.vertexIds.Count);
          foreach (int vertexId in snapFace.vertexIds) {
            coplanarSnapFaceVertices.Add(snapMesh.VertexPositionInModelCoords(vertexId));
          }
          Vector3 snapFaceCenter = MeshMath.CalculateGeometricCenter(coplanarSnapFaceVertices);

          // Next, calculate properties of the held mesh that is being snapped.
          // We obtain the vertex positions at 'origin' -- ignoring the mesh's transform -- such that this 
          // information can be re-used without recaulcation as the mesh's transform is modified.
          // We further obtain the vertex positions including the mesh's transform, to find a snap target.
          FaceKey previewFaceKey = closestFace.fromFaceKey;
          previewFace = previewMesh.GetFace(previewFaceKey.faceId);
          coplanarPreviewFaceVerticesAtOrigin = new List<Vector3>(previewFace.vertexIds.Count);
          List<Vector3> coplanarPreviewFaceVertices = new List<Vector3>(coplanarPreviewFaceVerticesAtOrigin.Count);
          for (int i = 0; i < previewFace.vertexIds.Count; i++) {
            Vector3 positionModelSpaceBeforeTransform =
              previewMesh.VertexPositionInModelCoords(previewFace.vertexIds[i]);
            coplanarPreviewFaceVerticesAtOrigin.Add(positionModelSpaceBeforeTransform);
            coplanarPreviewFaceVertices.Add(
              (previewMeshRotation * positionModelSpaceBeforeTransform)
              + previewMeshOffset);
          }

          // Setup the snapGrid for face snapping.
          SetupFaceSnapGrid(snapFace, snapMesh, MeshMath.CalculateGeometricCenter(coplanarPreviewFaceVertices));

          // Calculate the new rotation for the previewMesh so that the previewFace and snapFace are flush.
          finalPreviewMeshRotation =
            FindPreviewMeshRotationForFaceSnap(coplanarPreviewFaceVertices, previewMeshRotation);
          targetMeshId = snapMesh.id;
          return;
        }
      }

      // We haven't mesh or face snapped so either there were no faces to snap to or the closestVertex was closer
      // than the closestFace and we should be vertex snapping.
      if (nearbyVertexPairs.Count > 0) {
        // Find the nearest vertex called the snapVertex and the mesh it belongs to called the snapMesh.
        VertexKey snapVertexKey = closestVertex.vertexKey;
        MMesh snapMesh = model.GetMesh(snapVertexKey.meshId);

        // Find the position of the vertex being snapped to and setup the snapGrid for vertex snapping.
        Vector3 snapVertex = snapMesh.VertexPositionInModelCoords(snapVertexKey.vertexId);
        SetupVertexSnapGrid(snapVertex, snapMesh);

        // Find the face on the previewMesh closest to the snapVertex. This will be returned to the tool and used on
        // update for snapping.
        previewFace = previewMesh.GetFace(closestVertex.faceKey.faceId);

        // Find the list of coplanar vertices on the previewFace. Finding them each update loop is expensive so we
        // cache them. The position of the previewMesh is never updated which is why these vertices are marked as
        // "at origin" to use them in calculations their positions will have to be updated.
        coplanarPreviewFaceVerticesAtOrigin = new List<Vector3>(previewFace.vertexIds.Count);
        for (int i = 0; i < previewFace.vertexIds.Count; i++) {
          int id = previewFace.vertexIds[i];
          coplanarPreviewFaceVerticesAtOrigin.Add(previewMesh.VertexPositionInModelCoords(id));
        }

        // We need the actual position of the coplanarPreviewFaceVerticesAtOrigin so we apply the previewMesh
        // rotation and offset to each Vector3.
        List<Vector3> coplanarPreviewFaceVertices = new List<Vector3>(coplanarPreviewFaceVerticesAtOrigin.Count);
        for (int i = 0; i < coplanarPreviewFaceVerticesAtOrigin.Count; i++) {
          Vector3 vertex = coplanarPreviewFaceVerticesAtOrigin[i];
          coplanarPreviewFaceVertices.Add((previewMeshRotation * vertex) + previewMeshOffset);
        }

        // Calculate the new rotation for the previewMesh so that it is balanced on the vertex.
        finalPreviewMeshRotation = FindPreviewMeshRotationForVertexSnap(
          MeshMath.CalculateNormal(coplanarPreviewFaceVertices), previewMeshRotation);
        targetMeshId = snapMesh.id;
        return;
      }
      targetMeshId = -1;
      // If there was no vertex, face or mesh to snap to, snap to the universal grid.
      SetupUniversalSnapGrid();

      // Snapping to the universal grid doesn't require any knowledge of the previewFace so we can pass back null.
      previewFace = null;
      coplanarPreviewFaceVerticesAtOrigin = null;

      // The previewMesh should rotate to the nearest 90 degrees on the universal grid which has the identity
      // rotation.
      finalPreviewMeshRotation = GridUtils.SnapToNearest(previewMeshRotation, Quaternion.identity, 90f);
    }

    /// <summary>
    ///   Creates a snapGrid anchored on a vertex.
    /// </summary>
    /// <param name="snapVertex">The vertex being snapped to.</param>
    /// <param name="mesh">The mesh the vertex is on.</param>
    private void SetupVertexSnapGrid(Vector3 snapVertex, MMesh mesh) {
      snapType = SnapType.VERTEX;

      origin = snapVertex;
      snapCenter = origin;

      // The vertex normal can be imagined as a vector from the mesh center pointing out through the vertex.
      normal = snapVertex - mesh.offset;

      // The offset and rotation aren't important for snapType = VERTEX. The user can only positionally snap along
      // the normal.
      offset = Vector3.zero;
      rotation = Quaternion.identity;
    }

    /// <summary>
    ///   Creates a snapGrid anchored on a face.
    /// </summary>
    /// <param name="snapFace">The face being snapped to.</param>
    /// <param name="mesh">The mesh the face belongs to.</param>
    /// <param name="previewFacePosition">The position of the snap.</param>
    private void SetupFaceSnapGrid(Face snapFace, MMesh mesh, Vector3 previewFacePosition) {
      snapType = SnapType.FACE;

      // Find the clockwise positions of the vertices that make up the face.
      coplanarSnapFaceVertices = new List<Vector3>(snapFace.vertexIds.Count);
      for (int i = 0; i < snapFace.vertexIds.Count; i++) {
        int id = snapFace.vertexIds[i];
        coplanarSnapFaceVertices.Add(mesh.VertexPositionInModelCoords(id));
      }

      // Find the two vertices that make up the closest edge. The grid will be anchored on this edge.
      KeyValuePair<Vector3, Vector3> closestEdgeEndPoints =
        MeshMath.FindClosestEdgeInFace(previewFacePosition, coplanarSnapFaceVertices);

      origin = FindFaceOrigin(previewFacePosition, closestEdgeEndPoints);
      offset = FindOffset(origin);

      // Find the three face axes that will represent the snapGrid.
      Vector3 forward = FindForwardAxis(coplanarSnapFaceVertices);
      Vector3 right = FindBestRightAxis(coplanarSnapFaceVertices);
      Vector3 up = FindUpAxis(right, forward);
      snapAxes = new Axes(right, up, forward);

      // Find the rotational difference from the universal axes to the axes of the face.
      rotation = FromToRotation(new Axes(Vector3.right, Vector3.up, Vector3.forward), snapAxes);
      normal = forward;
      snapCenter = MeshMath.CalculateGeometricCenter(coplanarSnapFaceVertices);
    }

    /// <summary>
    ///   Creates a snapGrid anchored to a mesh.
    /// </summary>
    /// <param name="positionToSnap">The position of the preview at start of snap.</param>
    /// <param name="mesh">The mesh being snapped to.</param>
    private void SetupMeshSnapGrid(Vector3 positionToSnap, MMesh mesh) {
      snapType = SnapType.MESH;

      origin = mesh.offset;
      offset = FindOffset(origin);
      rotation = mesh.rotation;
      snapCenter = origin;
      normal = GridUtils.FindNearestLocalMeshAxis(positionToSnap - snapCenter, rotation);
    }

    /// <summary>
    ///   Creates a snapGrid representation of the universal grid.
    /// </summary>
    private void SetupUniversalSnapGrid() {
      snapType = SnapType.UNIVERSAL;

      origin = Vector3.zero;
      offset = Vector3.zero;
      rotation = Quaternion.identity;
      // The normal isn't important for universal snapping. We can do snapping calculations without it.
      normal = Vector3.zero;
    }

    /// <summary>
    ///   Finds the rotation of the previewMesh given the vertex being snapped to. It does this by creating a vertex
    ///   normal which is a vector from the mesh center to the vertex and finding the rotational difference between
    ///   the vertex normal and the previewFace normal.
    /// </summary>
    /// <param name="previewFaceNormal">The normal of the previewFace.</param>
    /// <param name="previewMeshRotation">The rotation of the previewMesh.</param>
    /// <returns>The new rotation for the previewMesh such that it is balanced on the snapVertex.</returns>
    public Quaternion FindPreviewMeshRotationForVertexSnap(Vector3 previewFaceNormal, Quaternion previewMeshRotation) {
      Quaternion normalRotDelta = Quaternion.FromToRotation(previewFaceNormal, -normal);

      return normalRotDelta * previewMeshRotation;
    }

    /// <summary>
    ///   Finds the rotation of the previewMesh given the snapGrid properties. It does this by finding the Axes
    ///   representing the previewFace, then the rotation from the previewFace to the snapFace and applying the
    ///   rotational delta to the rotation of the previewMesh.
    /// </summary>
    /// <param name="coplanarPreviewFaceVertices">
    ///   The vertices representing the previewFace which is being rotated to be flush with the snapFace.
    /// </param>
    /// <param name="previewMeshRotation">The rotation of the previewMesh the previewFace belongs to.</param>
    /// <returns>The new rotation for the previewMesh such that the previewFace and snapFace are flush.</returns>
    public Quaternion FindPreviewMeshRotationForFaceSnap(List<Vector3> coplanarPreviewFaceVertices,
      Quaternion previewMeshRotation) {
      // We want to rotate to line up with the snapFaceAxes, except we want the forwards to point in different
      // directions. So what we really want is to flip around the snapFaceAxes and rotate to match up with that.
      Axes invertedFaceAxes = new Axes(snapAxes.right, -snapAxes.up, snapAxes.forward);

      // Find the axes that represent the previewFace.
      Vector3 previewForward = FindForwardAxis(coplanarPreviewFaceVertices);

      // Choose the right axes that is closest to the right axes of the snapFace to minimize the effect of the
      // rotation. We will find the edge that is closest to the edge we used to define snapAxes.right but we also
      // want to invert this edge so that it goes in the same direction as snapAxes.right. They point in different
      // directions to start with because the winding order of the faces are in opposite directions since the normals
      // of the face point toward each other.
      Vector3 previewRight =
        -MeshMath.ClosestEdgeToEdge(coplanarPreviewFaceVertices, mostRepresentativeEdge).normalized;
      Vector3 previewUp = FindUpAxis(previewRight, previewForward);
      Axes previewFaceAxes = new Axes(previewRight, previewUp, previewForward);

      Quaternion previewRotDelta = FromToRotation(previewFaceAxes, invertedFaceAxes);

      return previewRotDelta * previewMeshRotation;
    }

    /// <summary>
    ///   Takes a position on a mesh and snaps it to the grid then updates the mesh position.
    /// </summary>
    /// <param name="positionToSnap">The position being snapped to the grid.</param>
    /// <param name="previewFaceId">The id of the face being snapped if there is a face being snapped.</param>
    /// <param name="previewMeshOffset">The offset of the mesh being snapped.</param>
    /// <param name="previewMeshRotation">The rotation of the mesh being snapped.</param>
    /// <param name="previewMesh">The mesh being snapped.</param>
    /// <returns>The snap transform for the previewMesh after snapping, as well as other data about the snap necessary
    ///   for UX shading.</returns>
    public SnapInfo snapToGrid(Vector3 positionToSnap, int previewFaceId, Vector3 previewMeshOffset,
      Quaternion previewMeshRotation, MMesh previewMesh) {
      // Depending on the snapType use different methods to positionally snap.
      // Note that we could write all these functions into one generalized function but to avoid unneccesary calls and
      // to increase readability they have been split into separate functions.
      SnapInfo snapInfo = new SnapInfo();

      bool relationalSnapped = false;
      if (snapType == SnapType.VERTEX) {
        Vector3 snappedPosition = previewMeshOffset + (SnapPositionToVertexSnapGrid(positionToSnap) - positionToSnap);
        Quaternion snappedRotation = previewMeshRotation;
        snapInfo.transform = new SnapTransform(snappedPosition, snappedRotation);
        relationalSnapped = true;
      } else if (snapType == SnapType.FACE) {
        snapInfo = SnapPositionToFaceSnapGrid(positionToSnap, previewFaceId, previewMeshOffset,
          previewMeshRotation, previewMesh);
        relationalSnapped = true;
      } else if (snapType == SnapType.MESH) {
        Vector3 snappedPosition = SnapPositionToMeshSnapGrid(previewMeshOffset);
        Quaternion snappedRotation = previewMeshRotation;
        snapInfo.transform = new SnapTransform(snappedPosition, snappedRotation);
        relationalSnapped = true;
      } else if (snapType == SnapType.UNIVERSAL) {
        Vector3 snappedPosition = SnapPositionToUniversalSnapGrid(previewMeshOffset, previewMesh.bounds);
        Quaternion snappedRotation = previewMeshRotation;
        snapInfo.transform = new SnapTransform(snappedPosition, snappedRotation);
      }

      if (relationalSnapped) {
        if (PeltzerMain.Instance.peltzerController.mode == controller.ControllerMode.insertVolume) {
          PeltzerMain.Instance.snappedInVolumeInserter = true;
        } else if (PeltzerMain.Instance.peltzerController.mode == controller.ControllerMode.move) {
          PeltzerMain.Instance.snappedInMover = true;
        }
      }
      return snapInfo;
    }

    /// <summary>
    ///   Snaps a position along the vertexNormal.
    /// </summary>
    /// <param name="positionToSnap">Position to snap along the normal.</param>
    /// <returns>The snapped position.</returns>
    private Vector3 SnapPositionToVertexSnapGrid(Vector3 positionToSnap) {
      if (WithinSurfaceThreshold(positionToSnap)) {
        return origin;
      } else {
        // Project the position onto the normal then snap it to the nearest position on the normal.
        return GridUtils.ProjectPointOntoLine(positionToSnap, normal, origin);
      }
    }

    /// <summary>
    ///   Snaps a position to a grid defined by a face.
    /// </summary>
    /// <param name="positionToSnap">The position being snapped to the grid.</param>
    /// <param name="previewFaceId">The idea of the face being snapped if there is a face being snapped.</param>
    /// <param name="previewMeshOffset">The offset of the mesh being snapped.</param>
    /// <param name="previewMeshRotation">The rotation of the mesh being snapped.</param>
    /// <param name="previewMesh">The mesh being snapped.</param>
    /// <returns>The new position and rotation for the previewMesh after snapping.</returns>
    private SnapInfo SnapPositionToFaceSnapGrid(Vector3 positionToSnap, int previewFaceId,
      Vector3 previewMeshOffset, Quaternion previewMeshRotation, MMesh previewMesh) {
      SnapInfo snapInfo = new SnapInfo();
      Vector3 snappedPosition;
      Quaternion snappedRotation;
      bool snappedToCenter = false;

      // A point can center and/or surface snap or neither. So first we either centerSnap or snap to the grid.
      // This can be thought of as snapping in the X and Y plane (we also snap in the Z but we can override the Z later
      // without affecting the X and Y).
      if (WithinCenterThreshold(positionToSnap)) {
        // If center snapping, snap onto a line represented by the normal centered on the snapCenter.
        snappedPosition = GridUtils.ProjectPointOntoLine(positionToSnap, normal, snapCenter);
        snappedToCenter = true;
      } else {
        // Just snap to the nearest snapGrid point.
        snappedPosition = SnapPositionToSnapGrid(positionToSnap);
      }

      // Position of point projected onto the surface plane. The point has already been snapped in the X and Y planes 
      // so just project it down onto the surface (snap on the Z plane).
      // We need this for shader effects even if we're not snapping to the surface.
      Vector3 surfaceSnappedPosition = Math3d.ProjectPointOnPlane(normal, origin, snappedPosition);

      // Now check to see if we should be snapping to the surface in addition to the above snap.
      if (WithinSurfaceThreshold(positionToSnap)) {
        snapInfo.inSurfaceThreshhold = true;
        // The point has already been snapped in the X and Y planes so just project it down onto the surface (snap on
        // the Z plane).
        snappedPosition = surfaceSnappedPosition;

        // Recalculate the position of the previewFaceVertices.
        Vector3 delta = snappedPosition - positionToSnap;
        ReadOnlyCollection<int> vertexIds = previewMesh.GetFace(previewFaceId).vertexIds;
        List<Vector3> coplanarPreviewFaceVertices = new List<Vector3>(vertexIds.Count);
        foreach (int vertexId in vertexIds) {
          coplanarPreviewFaceVertices.Add(
            previewMeshRotation * previewMesh.VertexPositionInMeshCoords(vertexId) + (previewMeshOffset + delta));
        }

        // Check to see if we are close enough to edge snap but only if we aren't center snapped. Center snap supercedes
        // edge snapping.
        EdgeInfo previewFaceEdge;
        EdgeInfo snapFaceEdge;
        bool withinCornerThreshold;
        Vector3 corner;

        if (!snappedToCenter && WithinEdgeThreshold(coplanarPreviewFaceVertices, out previewFaceEdge, out snapFaceEdge,
          out withinCornerThreshold, out corner)) {
          return SnapToEdgeOrCorner(
            previewMeshOffset + delta,
            previewMeshRotation,
            previewFaceEdge,
            snapFaceEdge,
            withinCornerThreshold,
            corner,
            positionToSnap);
        } else {
          snappedPosition = previewMeshOffset + (snappedPosition - positionToSnap);
          snappedRotation = previewMeshRotation;
        }
      } else {
        snappedPosition = previewMeshOffset + (snappedPosition - positionToSnap);
        snappedRotation = previewMeshRotation;
      }
      snapInfo.transform = new SnapTransform(snappedPosition, snappedRotation);
      snapInfo.snapPoint = surfaceSnappedPosition;
      snapInfo.snapNormal = normal;
      snapInfo.snappingFacePosition = positionToSnap;
      return snapInfo;
    }

    /// <summary>
    ///   Snaps a position to a grid defined by a mesh.
    /// </summary>
    /// <param name="positionToSnap">Position being snapped to the snapGrid.</param>
    /// <returns>The snapped position.</returns>
    private Vector3 SnapPositionToMeshSnapGrid(Vector3 positionToSnap) {
      if (WithinCenterThreshold(positionToSnap)) {
        // If center snapping, snap onto a line represented by the normal centered on the snapCenter.
        return GridUtils.ProjectPointOntoLine(positionToSnap, normal, snapCenter);
      } else {
        // Just snap to the nearest grid point.
        return SnapPositionToSnapGrid(positionToSnap);
      }
    }

    /// <summary>
    ///   Snaps a position to a snapGrid.
    /// </summary>
    /// <param name="positionToSnap">Position being snapped to the snapGrid.</param>
    /// <returns>The snapped position.</returns>
    private Vector3 SnapPositionToSnapGrid(Vector3 positionToSnap) {
      // Draw a vector from the origin of the snapGrid to the position being snapped.
      Vector3 positionalVector = positionToSnap - origin;

      // Unrotate the positionalVector so we are working on the universal grid.
      Vector3 universalVector = Quaternion.Inverse(rotation) * positionalVector;

      // Get the unrotated position from the end point of the universalVector.
      Vector3 universalPosition = origin + (universalVector.normalized * Vector3.Distance(origin, positionToSnap));

      // Snap the unrotatedPosition to UniversalGrid + offset.
      Vector3 universalSnappedPosition = GridUtils.SnapToGrid(universalPosition, offset);

      // Draw a new vector from the origin to the unrotatedSnappedPosition.
      Vector3 universalPositionalSnappedVector = universalSnappedPosition - origin;

      // Rotate back.
      Vector3 positionalSnappedVector = rotation * universalPositionalSnappedVector;

      // Get snapped position - which is the end point from the positionalSnappedVector.
      return origin + (positionalSnappedVector.normalized * Vector3.Distance(origin, universalSnappedPosition));
    }

    /// <summary>
    ///   Snaps a mesh to an edge by rotating the mesh so its nearest edge aligns with the edge and moves it
    ///   positionally to align with the edge.
    /// </summary>
    /// <param name="previewMeshOffset">The offset of the mesh being snapped.</param>
    /// <param name="previewMeshRotation">The rotation of the mesh being snapped.</param>
    /// <param name="previewFaceEdge">The edgeInfo for the edge on the previewMesh being snapped.</param>
    /// <param name="snapFaceEdge">The edgeInfo for the edge on the snapMesh being snapped to.</param>
    /// <param name="withinCornerThreshold">Whether there is a corner to snap to.</param>
    /// <param name="corner">The position of the corner or Vector3.zero if there isn't a close enough corner.</param>
    /// <returns>The new position and rotation for the previewMesh after snapping.</returns>
    private SnapInfo SnapToEdgeOrCorner(Vector3 previewMeshOffset, Quaternion previewMeshRotation,
      EdgeInfo previewFaceEdge, EdgeInfo snapFaceEdge, bool withinCornerThreshold, Vector3 corner,
      Vector3 positionToSnap) {
      // These were found in WithinEdgeThreshold and recorded to avoid looping through both faces again.
      Vector3 previewEdge = previewFaceEdge.edgeVector;
      Vector3 snapEdge = snapFaceEdge.edgeVector;
      SnapInfo snapInfo = new SnapInfo();

      // Switch the direction of snapEdge to minimize the angle between previewFaceEdge and snapFaceEdge.
      // The angle between snapEdge and -snapEdge is 180 degrees. So if the angle between previewEdge and snapEdge is
      // greater than 90 the angle between previewEdge and -snapEdge will be less than 90 and therefore the minimized
      // angle.
      if (90.0f - Vector3.Angle(previewEdge, snapEdge) < angleThreshold)
        snapEdge = -snapEdge;

      // Find the rotational difference between the two edges.
      Quaternion edgeRotDelta = Quaternion.FromToRotation(previewEdge, snapEdge);

      // Find the position of an edge point if snapped onto the line.
      Vector3 snappedEdgeStartPoint = GridUtils.ProjectPointOntoLine(previewFaceEdge.edgeStart, snapEdge,
        snapFaceEdge.edgeStart);
      
      // Find the difference and apply it to positionToSnap.
      Vector3 snappedPosition = previewMeshOffset + (snappedEdgeStartPoint - previewFaceEdge.edgeStart);

      // Slide the position over so it snaps to the corner.
      if (withinCornerThreshold) {
        Vector3 snappedEdgeEndPoint = snappedEdgeStartPoint + previewFaceEdge.edgeVector;
        Vector3 distanceToCorner =
          Vector3.Distance(corner, snappedEdgeEndPoint) < Vector3.Distance(corner, snappedEdgeStartPoint) ?
          corner - snappedEdgeEndPoint : corner - snappedEdgeStartPoint;
        snappedPosition = snappedPosition + distanceToCorner;
      }

      // Find the rotational difference to align the edges.
      Quaternion snappedRotation = edgeRotDelta * previewMeshRotation;
      snapInfo.transform = new SnapTransform(snappedPosition, snappedRotation);
      snapInfo.inSurfaceThreshhold = true;
      snapInfo.snapPoint = snappedPosition;
      snapInfo.snapNormal = normal;
      snapInfo.snappingFacePosition = positionToSnap;
      return snapInfo;
    }

    /// <summary>
    ///   Snaps a position to the universal grid.
    /// </summary>
    /// <param name="positionToSnap">Position to snap.</param>
    /// <param name="bounds">The bounds of the mesh being snapped to the universal grid.</param>
    /// <returns>The snapped position.</returns>
    private Vector3 SnapPositionToUniversalSnapGrid(Vector3 positionToSnap, Bounds bounds) {
      // We don't have to do anything fancy to snap to the universal grid.
      return GridUtils.SnapToGrid(positionToSnap, bounds);
    }

    /// <summary>
    ///   Checks to see if a position is within the threshold for snapping to the surface.
    /// </summary>
    /// <param name="positionToSnap">The position.</param>
    /// <returns>Whether the position is in the threshold.</returns>
    private bool WithinSurfaceThreshold(Vector3 positionToSnap) {
      return Mathf.Abs(Math3d.SignedDistancePlanePoint(normal, origin, positionToSnap)) < surfaceThreshold;
    }

    /// <summary>
    ///   Checks to see if a position is within the threshold for snapping to the center.
    /// </summary>
    /// <param name="positionToSnap">The position.</param>
    /// <returns>Whether the position is in the threhold.</returns>
    private bool WithinCenterThreshold(Vector3 positionToSnap) {
      // Project the position onto a plane defined by the normal and the snapCenter.
      Vector3 projectedPosition = Math3d.ProjectPointOnPlane(normal, snapCenter, positionToSnap);
      // Check to see if the planar distance is within the threshold.
      return Mathf.Abs(Vector3.Distance(projectedPosition, snapCenter)) < centerThreshold;
    }

    /// <summary>
    ///   Checks to see if any edge in the previewFace is within the threshold to snap to any edge in the snapFace.
    /// </summary>
    /// <param name="coplanarPreviewFaceVertices">The coplanar vertices representing the previewFace.</param>
    /// <param name="previewFaceEdge">The edgeInfo for the previewFaceEdge.</param>
    /// <param name="snapFaceEdge">The edgeInfo for the snapFaceEdge.</param>
    /// <param name="withinCornerThreshold">Whether the position is close enough to a corner to snap to.</param>
    /// <param name="corner">The position of the corner or Vector3.zero if there isn't a close enough corner.</param>
    /// <returns>Whether there is a pair of edges within the edge threshold of each other.</returns>
    private bool WithinEdgeThreshold(IEnumerable<Vector3> coplanarPreviewFaceVertices, out EdgeInfo previewFaceEdge,
      out EdgeInfo snapFaceEdge, out bool withinCornerThreshold, out Vector3 corner) {
      IEnumerable<EdgePair> closestEdgePairs =
        MeshMath.FindClosestEdgePairs(coplanarPreviewFaceVertices, coplanarSnapFaceVertices);

      // Set defaults for out parameters.
      previewFaceEdge = new EdgeInfo();
      snapFaceEdge = new EdgeInfo();
      withinCornerThreshold = false;
      corner = Vector3.zero;

      // Find if there is at least one edge to be snapped to.
      if (closestEdgePairs.First().separation < edgeThreshold) {
        previewFaceEdge = closestEdgePairs.First().fromEdge;
        snapFaceEdge = closestEdgePairs.First().toEdge;

        // Check to see if there are two perpendicular intersecting nearbyEdges in which case we should corner snap.
        if (closestEdgePairs.Count() > 1) {
          EdgePair secondEdgePair = closestEdgePairs.ElementAt(1);
          EdgeInfo secondEdge = secondEdgePair.toEdge;
          if (secondEdgePair.separation < edgeThreshold
            && Mathf.Abs(90.0f - Vector3.Angle(snapFaceEdge.edgeVector, secondEdge.edgeVector)) < 0.01f) {
            withinCornerThreshold = true;
            corner =
              Math3d.ProjectPointOntoLine(snapFaceEdge.edgeStart, secondEdge.edgeVector, secondEdge.edgeStart);
          }
        }

        return true;
      }

      return false;
    }

    /// <summary>
    ///   Finds the origin of the snapGrid by choosing the closest vertex on the closest edge.
    /// </summary>
    /// <param name="previewFacePosition">The position at the start of the snap.</param>
    /// <param name="closestEdgeEndPoints">The vertices of the closest edge.</param>
    /// <returns>The closest vertex from the closest edge.</returns>
    private static Vector3 FindFaceOrigin(Vector3 previewFacePosition,
      KeyValuePair<Vector3, Vector3> closestEdgeEndPoints) {
      Vector3 v1 = closestEdgeEndPoints.Key;
      Vector3 v2 = closestEdgeEndPoints.Value;

      // Let the origin be which ever vertex in the edge is closest.
      return Vector3.Distance(v1, previewFacePosition) < Vector3.Distance(v2, previewFacePosition) ? v1 : v2;
    }

    /// <summary>
    ///   Finds the snapGrid offset which is the distance from the universal grid.
    /// </summary>
    private static Vector3 FindOffset(Vector3 origin) {
      return origin - GridUtils.SnapToGrid(origin);
    }

    /// <summary>
    ///   Finds the forward axis which is just the normal out of the origin.
    /// </summary>
    private static Vector3 FindForwardAxis(List<Vector3> coplanarVertices) {
      return MeshMath.CalculateNormal(coplanarVertices);
    }

    /// <summary>
    ///   Finds the right axis by comparing all the edges in the face and choosing an edge for the right axis that is
    ///   the most representative of the other edges. Essentially we are trying to find an edge that is perpendicular
    ///   to as many edges as possible so that we can rotate the preview to align to the greatest number of edges.
    /// </summary>
    /// <param name="coplanarSnapFaceVertices">The vertices representing the snapFace.</param>
    /// <returns>The right axis as a normalized vector.</returns>
    private static Vector3 FindBestRightAxis(List<Vector3> coplanarSnapFaceVertices) {
      mostRepresentativeEdge = MeshMath.FindMostRepresentativeEdge(coplanarSnapFaceVertices);
      return mostRepresentativeEdge.edgeVector.normalized;
    }

    /// <summary>
    ///   Finds the right axis of the snapGrid by using the closest edge in the face. Since the forward axis is the
    ///   face normal any edge is guaranteed to be perpendicular to the normal.
    /// </summary>
    /// <param name="closestEdge">The closest edge.</param>
    /// <returns>A normalized vector representing the right axis of the snapGrid.</returns>
    private static Vector3 FindRightAxis(KeyValuePair<Vector3, Vector3> closestEdge) {
      return (closestEdge.Key - closestEdge.Value).normalized;
    }

    /// <summary>
    ///   Finds the up axis which is just the axis perpendicular to the right and forward axis. We use the left hand
    ///   rule to make sure the up axis points the right way so snapGrid.forward, up and right are related the same
    ///   as the universal Vector3.forward, up and right.
    /// </summary>
    /// <param name="right">The right axis.</param>
    /// <param name="forward">The forward axis.</param>
    /// <returns>The cross product of the right and forward axis.</returns>
    private static Vector3 FindUpAxis(Vector3 right, Vector3 forward) {
      return Vector3.Cross(forward, right).normalized;
    }

    /// <summary>
    ///   Finds the rotation from one Axes to another.
    /// </summary>
    /// <param name="from">The starting Axes.</param>
    /// <param name="to">The final Axes.</param>
    /// <returns>The rotational difference between the two Axes'.</returns>
    private static Quaternion FromToRotation(Axes from, Axes to) {
      // To find the rotation between two Axes we only need to find the difference between two of the axes since a
      // pair of axes must move together to maintain the 90 degree angles between them. So we will start by moving
      // one arbitrary Axes into place. Then applying this change to a second Axes and finding the remaining
      // difference between this Axes and the universal grid axes.

      // Start by finding the rotation difference between the to.up axis and the from.up.
      Quaternion yRotation = Quaternion.FromToRotation(from.up, to.up);

      // Apply the yRotation to the from.right then find the difference between the partially rotated from.right axis
      // and the to.right axis.
      Quaternion residualRotation = Quaternion.FromToRotation(yRotation * from.right, to.right);

      // Combine the rotations to find the rotational difference.
      return residualRotation * yRotation;
    }

    // Public for testing.
    public static Vector3 FindForwardAxisForTest(List<Vector3> coplanarVertices) {
      return FindForwardAxis(coplanarVertices);
    }

    // Public for testing.
    public static Vector3 FindRightAxisForTest(KeyValuePair<Vector3, Vector3> closestEdge) {
      return FindRightAxis(closestEdge);
    }

    // Public for testing.
    public static Vector3 FindUpAxisForTest(Vector3 right, Vector3 forward) {
      return FindUpAxis(right, forward);
    }

    // Public for testing.
    public static Quaternion FindFaceRotationForTest(Vector3 right, Vector3 up, Vector3 forward) {
      return FromToRotation(new Axes(Vector3.right, Vector3.up, Vector3.forward), new Axes(right, up, forward));
    }
  }
}
