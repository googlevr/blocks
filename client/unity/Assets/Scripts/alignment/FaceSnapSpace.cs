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
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace com.google.apps.peltzer.client.alignment {
  /// <summary>
  ///   A FaceSnapSpace is a coordinate system that an MMesh can be orientated in and snapped to.
  ///   
  ///   A FaceSnapSpace is a 2D coordinate system overlayed on a face. A FaceSnapSpace is used to 'snap a face from a
  ///   source mesh onto a face of a target mesh' so its properties are defined by the target face. The axes are: the
  ///   most representative edge of the face, the face's normal and their cross product. The most representative edge
  ///   of a face is the one that is perpendicular that greatest number of other edges. This allows the FaceSnapSpace
  ///   to naturally align with as many edges as possible. Its rotation can then be defined as the rotational
  ///   difference from the unit axes to its axes and its origin is the center of the face.
  ///   
  ///   When an MMesh is snapped to a FaceSnapSpace its rotation is changed so that the source face is parallel with
  ///   the target face and its moved so that the faces are flush. The source face will automatically stick to
  ///   important points on the target face. Corners will stick to corners, edges to edges and centers to centers
  ///   if the distance between them is within a threshold. If GridMode is on the MMesh will also move in grid units
  ///   along the surface of the face.
  /// </summary>
  public class FaceSnapSpace : SnapSpace {
    private const SnapType snapType = SnapType.FACE;

    /// <summary>
    ///   Floating point error threshold for comparing angles.
    /// </summary>
    private static readonly float DEGREE_ANGLE_ERROR_THRESHOLD = 0.01f;
    /// <summary>
    /// The point on the targetFace that the reference point on the sourceFace will snap to initially. This is
    /// important for displaying the correct UI snap hints.
    /// </summary>
    public Vector3 initialSnapPoint;

    public Vector3 snapPoint;
    /// <summary>
    /// The face key of the face being snapped.
    /// </summary>
    public FaceKey sourceFaceKey { get; private set; }
    /// <summary>
    /// The center of the face being snapped.
    /// </summary>
    public Vector3 sourceFaceCenter { get; private set; }

    // We keep a reference to these variables to avoid extraneously re-calculating anything while defining a
    // FaceSnapSpace and snapping to the space.

    /// <summary>
    /// The face key of the face being snapped to. This face defines the properties of the FaceSnapSpace.
    /// </summary>
    public FaceKey targetFaceKey { get; private set; }
    /// <summary>
    /// The center of the face being snapped to, calculated as the geometric center of the target faces verts.
    /// </summary>
    public Vector3 targetFaceCenter { get; private set; }
    /// <summary>
    /// The mesh whose face is being snapped to this FaceSnapSpace. Any mesh should be able to be snapped to a
    /// FaceSnapSpace because its an arbritrary coordinate system but we hold a reference to the specific mesh being
    /// snapped to increase performance. We need this reference from the start because the sourceMesh is a preview and
    /// its id is not in the spatialIndex.
    /// </summary>
    public MMesh sourceMesh;
    /// <summary>
    /// The vertices of the face being snapped to this FaceSnapSpace. Any face should be able to be snapped to a
    /// FaceSnapSpace because its an arbritrary coordinate system but we hold a reference to the specific face being
    /// snapped to increase performance.
    /// </summary>
    private ReadOnlyCollection<int> sourceFaceVertexIds;
    /// <summary>
    /// The vertices of the face being snapped to. We hold a reference to these vertices to increase performance when
    /// sticking to edges.
    /// </summary>
    private List<Vector3> targetFaceVertices;
    /// <summary>
    /// The edge from the target face used to define the rotation of this FaceSnapSpace. We keep a reference to this
    /// edge to increase performance while determining the rotation of a mesh being snapped to this FaceSnapSpace.
    /// </summary>
    private EdgeInfo mostRepresentativeEdge;
    /// <summary>
    /// The effect for sticking to an edge.
    /// </summary>
    private ContinuousEdgeStickEffect continuousEdgeStickEffect;
    /// <summary>
    /// Whether we are currently edge sticking.
    /// </summary>
    private bool isEdgeSticking;
    /// <summary>
    /// The effect for sticking to the origin or a vertex;
    /// </summary>
    private ContinuousPointStickEffect continuousPointStickEffect;
    /// <summary>
    /// Whether we are currently sticking to the origin.
    /// </summary>
    private bool isPointSticking;

    public Vector3 sourceMeshOffset;
    public Quaternion sourceMeshRotation;

    public FaceSnapSpace(MMesh sourceMesh, FaceKey sourceFaceKey, FaceKey targetFaceKey, Vector3 sourceFaceCenter,
      Vector3 targetFaceCenter, Vector3 initialSnapPoint) {
      this.sourceMesh = sourceMesh;
      this.sourceFaceKey = sourceFaceKey;
      this.targetFaceKey = targetFaceKey;
      this.sourceFaceCenter = sourceFaceCenter;
      this.targetFaceCenter = targetFaceCenter;
      this.initialSnapPoint = initialSnapPoint;
      this.snapPoint = initialSnapPoint;
      continuousEdgeStickEffect = new ContinuousEdgeStickEffect();
      continuousPointStickEffect = new ContinuousPointStickEffect();
    }

    /// <summary>
    /// Calculates the origin, rotation and axes of the FaceSnapSpace and any other information we can calculate once
    /// and hold onto.
    /// </summary>
    public override void Execute() {
      MMesh targetMesh = PeltzerMain.Instance.model.GetMesh(targetFaceKey.meshId);
      Face targetFace = targetMesh.GetFace(targetFaceKey.faceId);

      targetFaceVertices = new List<Vector3>(targetFace.vertexIds.Count);
      for (int i = 0; i < targetFace.vertexIds.Count; i++) {
        targetFaceVertices.Add(targetMesh.VertexPositionInModelCoords(targetFace.vertexIds[i]));
      }

      mostRepresentativeEdge = MeshMath.FindMostRepresentativeEdge(targetFaceVertices);
      Axes axes = Axes.FindAxesForAFace(targetFaceVertices);
      Setup(targetFaceCenter, Axes.FromToRotation(Axes.identity, axes), axes);

      Face sourceFace = sourceMesh.GetFace(sourceFaceKey.faceId);
      sourceFaceVertexIds = sourceFace.vertexIds;
    }

    /// <summary>
    /// Checks if the targetMeshId still exists in the model. If it does the snap is still valid.
    /// </summary>
    /// <returns>Whether the targetMeshId exists still.</returns>
    public override bool IsValid() {
      return PeltzerMain.Instance.model.HasMesh(targetFaceKey.meshId);
    }

    /// <summary>
    /// Translates a transform into the SnapSpace.
    /// 
    /// When snapping a position of a mesh to a FaceSnapSpace we don't actually want the position to be snapped to the
    /// FaceSnapSpace but a reference position on a face of the mesh. So we determine where the reference point is
    /// before the snap starts, find where the reference point should be snapped to and then move the position by the
    /// reference delta.
    /// 
    /// The reference point is snapped to a FaceSnapSpace by:
    ///   1) Projecting the reference onto the target face.
    ///   2) Sticking the reference to the origin if its close enough.
    ///   3) Otherwise, snapping to a grid defined by the coordinate system if grid mode is on.
    /// 
    /// After we've found the snapped reference point and if we didn't snap to the origin we check to see if any
    /// edge/corner on the source face is close enough to an edge/corner on the target face to stick them together.
    /// If they are we determine the delta to move the mesh so the edge/corner stick and update the reference point.
    /// 
    /// A rotation is snapped to a FaceSnapSpace by finding the rotation needed to align the source face with
    /// the FaceSnapSpace and then applying that delta to the rotation. When we edge/corner stick the source face
    /// might need to be rotated to align correctly in which case that delta will be added to the rotation as well.
    /// </summary>
    /// <param name="position">The position of the mesh being snapped.</param>
    /// <param name="rotation">The rotation of the mesh being snapped.</param>
    /// <returns>The snapped position and rotation.</returns>
    public override SnapTransform Snap(Vector3 position, Quaternion rotation) {
      sourceMeshOffset = position;
      sourceMeshRotation = rotation;
      List<Vector3> sourceFaceVerticesBeforeSnap =
        MeshMath.CalculateVertexPositions(sourceFaceVertexIds, sourceMesh, position, rotation);

      // The reference point on the sourceFace.
      Vector3 reference = MeshMath.CalculateGeometricCenter(sourceFaceVerticesBeforeSnap);

      // In face snapping we always snap to the surface of the face. So start snapping off by projecting the reference
      // point onto a plane along the sourceFace. We can define this plane easily by using axes.foward which is the
      // face's normal and the origin which is a point on the plane.
      Vector3 snappedReference = Math3d.ProjectPointOnPlane(axes.forward, origin, reference);

      // Determine what the rotation of the mesh needs to be if we just wanted to align the sourceFace with the
      // targetFace plane. This will be the final rotation if we don't edge/corner stick. Rotation is applied around
      // the offset of the mesh which is position.
      Quaternion rotationAfterAlignmentWithTargetFacePlane =
        FindSourceMeshRotationForFaceSnap(sourceFaceVerticesBeforeSnap, rotation);

      // Rotations are applied to meshes first. So apply our tempRotation to the reference point to see where it would
      // be when we rotate the mesh to align with the projected reference.
      Vector3 meshSpaceReference = Quaternion.Inverse(rotation) * (reference - position);
      Vector3 referenceRotatedToAlignWithTargetFace =
        (rotationAfterAlignmentWithTargetFacePlane * meshSpaceReference) + position;

      // Check if we are close enough to the origin (the center of the target face in FaceSnapSpaces) to snap the
      // reference there. If we are we won't do anything else.
      if (SnapToOrigin(snappedReference, out snappedReference)) {
        // We don't do any further rotational snapping so we can just use the tempRotation we already found.
        Quaternion snappedRotation = rotationAfterAlignmentWithTargetFacePlane;
        // Find the reference delta which is the the vector from the reference point once its rotated for the snap. To
        // the position it should be snapped to. Then apply this delta to the position.
        Vector3 referenceDelta = (snappedReference - referenceRotatedToAlignWithTargetFace);
        Vector3 snappedPosition = position + referenceDelta;
        isEdgeSticking = false;
        UXEffectManager.GetEffectManager().EndEffect(continuousEdgeStickEffect);

        if (!isPointSticking) {
          isPointSticking = true;
          UXEffectManager.GetEffectManager().StartEffect(continuousPointStickEffect);
        }
        continuousPointStickEffect.UpdateFromPoint(origin);

        return new SnapTransform(snappedPosition, snappedRotation);
      } else {
        // If we are in grid mode snap the reference to the nearest grid point for this coordinate system. This is
        // guaranteed to be on the surface of the face.
        if (PeltzerMain.Instance.peltzerController.isBlockMode) {
          SnapToGrid(snappedReference, out snappedReference);
        }

        // Determine where the position would be at this point for where the reference point is. We want to
        // mathematically perform the snap and then see if we should stick any edges/corners together.

        // At this point we've projected the reference onto the target face plane and potentially snapped the
        // projected reference onto the nearest grid point. Now we want to check if after the projection and grid snap
        // if any edges or corners on the source face are close enough to any edges/corners on the target face to
        // stick them together. So we need to find the reference delta at this point and then update position to where
        // it temporarily should be after the previous translations.
        Vector3 referenceDelta = (snappedReference - referenceRotatedToAlignWithTargetFace);
        Vector3 positionAfterProjectionRotationMaybeGridSnap = position + referenceDelta;

        // Now that we have the temporary position and rotation, at this point in the snap, we can calculate
        // where the positions of the vertices of the sourceFace should be and then check if we should edge/corner
        // stick.
        List<Vector3> sourceFaceVerticesAfterProjectionRotationMaybeGridSnap =
          MeshMath.CalculateVertexPositions(sourceFaceVertexIds, sourceMesh,
          positionAfterProjectionRotationMaybeGridSnap, rotationAfterAlignmentWithTargetFacePlane);

        // Find the snappedPosition and snappedRotation if we were to edge/corner stick. If we don't stick our current
        // temp position and rotation will be returned as the final snaps.
        Vector3 snappedPosition;
        Quaternion snappedRotation;
        bool edgeSnapped;
        EdgeInfo targetEdge;
        bool vertexSnapped;
        Vector3 targetVertex;
        if (StickToEdgeOrCorner(positionAfterProjectionRotationMaybeGridSnap,
          rotationAfterAlignmentWithTargetFacePlane, sourceFaceVerticesAfterProjectionRotationMaybeGridSnap,
          snappedReference, out snappedPosition, out snappedRotation, out edgeSnapped, out targetEdge, out vertexSnapped,
          out targetVertex)) {
          if (edgeSnapped) {
            if (!isEdgeSticking) {
              isEdgeSticking = true;
              UXEffectManager.GetEffectManager().StartEffect(continuousEdgeStickEffect);
            }
            continuousEdgeStickEffect.UpdateFromEdge(targetEdge);
          } else if (isEdgeSticking) {
            isEdgeSticking = false;
            UXEffectManager.GetEffectManager().EndEffect(continuousEdgeStickEffect);
          }

          if (vertexSnapped) {
            if (!isPointSticking) {
              isPointSticking = true;
              UXEffectManager.GetEffectManager().StartEffect(continuousPointStickEffect);
            }
            continuousPointStickEffect.UpdateFromPoint(targetVertex);
          } else if (isPointSticking) {
            isPointSticking = false;
            UXEffectManager.GetEffectManager().EndEffect(continuousPointStickEffect);
          }

          return new SnapTransform(snappedPosition, snappedRotation);
        } else {
          isEdgeSticking = false;
          UXEffectManager.GetEffectManager().EndEffect(continuousEdgeStickEffect);
          isPointSticking = false;
          UXEffectManager.GetEffectManager().EndEffect(continuousPointStickEffect);

          // We didn't corner or edge snap so we just use the rotation used to align with the target face plane and
          // the position needed to move the mesh so that the reference is on the target face plane.
          return new SnapTransform(
            positionAfterProjectionRotationMaybeGridSnap,
            rotationAfterAlignmentWithTargetFacePlane);
        }
      }
    }

    /// <summary>
    ///   Handles stopping snap logic maintained by the FaceSnapSpace such as hints for edge and vertex sticking.
    /// </summary>
    public override void StopSnap() {
      if (isEdgeSticking) {
        UXEffectManager.GetEffectManager().EndEffect(continuousEdgeStickEffect);
      }

      if (isPointSticking) {
        UXEffectManager.GetEffectManager().EndEffect(continuousPointStickEffect);
      }
    }

    /// <summary>
    /// Sticks an edge/corner on the source face to an edge/corner on the target face if they are close enough.
    /// </summary>
    /// <param name="position">The position being snapped.</param>
    /// <param name="rotation">The rotation being snapped.</param>
    /// <param name="sourceFaceVertices">
    /// The vertices of the face being snapped at the given position and rotation.
    /// </param>
    /// <param name="reference">The position of the reference point at the given position and rotation.</param>
    /// <param name="snappedPosition">The updated position that sticks a corner/edge.</param>
    /// <param name="snappedRotation">The udpated rotation that sticks a corner/edge.</param>
    /// <returns>Whether a corner/edge pair were close enough to stick.</returns>
    private bool StickToEdgeOrCorner(Vector3 position, Quaternion rotation, IEnumerable<Vector3> sourceFaceVertices,
      Vector3 reference, out Vector3 snappedPosition, out Quaternion snappedRotation, out bool edgeSnapped,
      out EdgeInfo targetEdge, out bool vertexSnapped, out Vector3 targetVertex) {
      EdgePair closestEdgePair;
      bool shouldEdgeSnap =
        MeshMath.MaybeFindClosestEdgePair(sourceFaceVertices, targetFaceVertices, out closestEdgePair);

      targetVertex = new Vector3();
      vertexSnapped = false;

      if (!shouldEdgeSnap ||
        closestEdgePair.separation > STICK_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale) {
        snappedPosition = position;
        snappedRotation = rotation;
        targetEdge = new EdgeInfo();
        edgeSnapped = false;
        return false;
      }

      EdgeInfo sourceEdge = closestEdgePair.fromEdge;
      targetEdge = closestEdgePair.toEdge;
      edgeSnapped = true;

      // Switch the direction of snapEdge to minimize the angle between sourceFaceEdge and targetFaceEdge.
      // The angle between snapEdge and -snapEdge is 180 degrees. So if the angle between sourceEdge and snapEdge is
      // greater than 90 the angle between sourceEdge and -snapEdge will be less than 90 and therefore the minimized
      // angle.
      if (90.0f - Vector3.Angle(sourceEdge.edgeVector, targetEdge.edgeVector) < DEGREE_ANGLE_ERROR_THRESHOLD) {
        targetEdge.edgeStart = targetEdge.edgeStart + targetEdge.edgeVector;
        targetEdge.edgeVector = -targetEdge.edgeVector;
      }

      // Find the rotational difference between the two edges.
      Quaternion edgeRotDelta = Quaternion.FromToRotation(sourceEdge.edgeVector, targetEdge.edgeVector);
      Vector3 rotatedSourceEdgeStart = (edgeRotDelta * (sourceEdge.edgeStart - position)) + position;

      // Find the position of an edge point if snapped onto the line.
      Vector3 snappedSourceEdgeStart = Math3d.ProjectPointOntoLine(rotatedSourceEdgeStart, targetEdge.edgeVector,
        targetEdge.edgeStart);

      // Find the difference and apply it to positionToSnap.
      Vector3 rotatedReference = (edgeRotDelta * (reference - position)) + position;
      Vector3 snappedReference = rotatedReference + (snappedSourceEdgeStart - rotatedSourceEdgeStart);

      // Check to see how far apart the vertices of the edges are from each other. If any set of vertices are close
      // enough we'll snap them together to corner snap.
      Vector3 targetEdgeStart = targetEdge.edgeStart;
      Vector3 targetEdgeEnd = targetEdgeStart + targetEdge.edgeVector;

      // Find the source edge points after they have been snapped onto the target edge.
      Vector3 sourceEdgeStart = snappedSourceEdgeStart;
      Vector3 sourceEdgeEnd = sourceEdgeStart + (edgeRotDelta * sourceEdge.edgeVector);

      float startToStartDelta = Vector3.Distance(sourceEdgeStart, targetEdgeStart);
      float endToEndDelta = Vector3.Distance(sourceEdgeEnd, targetEdgeEnd);
      float sourceStartToTargetEndDelta = Vector3.Distance(sourceEdgeStart, targetEdgeEnd);
      float sourceEndToTargetStartDelta = Vector3.Distance(sourceEdgeEnd, targetEdgeStart);

      float minDelta =
        Mathf.Min(startToStartDelta, endToEndDelta, sourceStartToTargetEndDelta, sourceEndToTargetStartDelta);

      // Slide the position over so it snaps to the corner.
      if (minDelta < STICK_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale) {
        if (minDelta == startToStartDelta) {
          snappedReference += (targetEdgeStart - sourceEdgeStart);
          targetVertex = targetEdgeStart;
        } else if (minDelta == endToEndDelta) {
          snappedReference += (targetEdgeEnd - sourceEdgeEnd);
          targetVertex = targetEdgeEnd;
        } else if (minDelta == sourceStartToTargetEndDelta) {
          snappedReference += (targetEdgeEnd - sourceEdgeStart);
          targetVertex = targetEdgeEnd;
        } else {
          snappedReference += (targetEdgeStart - sourceEdgeEnd);
          targetVertex = targetEdgeStart;
        }

        vertexSnapped = true;
        edgeSnapped = false;
      }

      snappedPosition = position + (snappedReference - rotatedReference);
      snappedRotation = Math3d.Normalize(edgeRotDelta * rotation);
      return true;
    }

    /// <summary>
    ///   Finds the rotation of the sourceMesh given the snapGrid properties. It does this by finding the Axes
    ///   representing the sourceFace, then the rotation from the sourceFace to the snapFace and applying the
    ///   rotational delta to the rotation of the sourceMesh.
    /// </summary>
    /// <param name="coplanarSourceFaceVertices">
    ///   The vertices representing the sourceFace which is being rotated to be flush with the snapFace.451
    /// </param>
    /// <param name="sourceMeshRotation">The rotation of the sourceMesh the sourceFace belongs to.</param>
    /// <returns>The new rotation for the sourceMesh such that the sourceFace and snapFace are flush.</returns>
    public Quaternion FindSourceMeshRotationForFaceSnap(List<Vector3> coplanarSourceFaceVertices,
      Quaternion sourceMeshRotation) {
      // To face snap we need to rotate the sourceMesh so that the sourceFace is flush with the targetFace.
      // To start we find the rotational difference between the faces and then we apply that difference to the
      // rotation of the sourceMesh. To find the rotational difference between the faces we can find the rotational
      // difference between the axes of the sourceFace at the start and what the final sourceFace.axes need to be to
      // be flush with the targetFace. The finalSourceFaceAxes is the axes of the targetFace but inverted so that the
      // forward (targerFace normal) points in the opposite direction because faces that are flush/aligned have
      // inverted normals.
      Axes finalSourceFaceAxes = new Axes(-axes.right, -axes.up, -axes.forward);

      // Find the axes that represent the sourceFace at the start.
      Vector3 startingSourceForward = Axes.FindForwardAxis(coplanarSourceFaceVertices);

      // Choose the axis.Right that is closest to the axis.Right of the snapFace to minimize the amount of the
      // rotation we apply to make the faces align. We will find the edge that is closest to the edge we used to
      // define snapAxes.right.
      Vector3 startingSourceRight =
        MeshMath.ClosestEdgeToEdge(coplanarSourceFaceVertices, mostRepresentativeEdge).normalized;
      Vector3 startingSourceUp = Axes.FindUpAxis(startingSourceRight, startingSourceForward);
      Axes startingSourceFaceAxes = new Axes(startingSourceRight, startingSourceUp, startingSourceForward);

      Quaternion sourceRotDelta = Axes.FromToRotation(startingSourceFaceAxes, finalSourceFaceAxes);
      sourceRotDelta *= sourceMeshRotation;

      return sourceRotDelta;
    }

    /// <summary>
    /// Checks if another SnapSpace is equivalent to this space. FaceSnapSpaces are equivalent if they have the same
    /// sourceFaceKey and TargetFaceKey.
    /// </summary>
    /// <param name="otherSpace">The other SnapSpace.</param>
    /// <returns>Whether they are equal.</returns>
    public override bool Equals(SnapSpace otherSpace) {
      if (otherSpace == null || otherSpace.SnapType != snapType) {
        return false;
      }

      FaceSnapSpace otherFaceSnapSpace = (FaceSnapSpace)otherSpace;
      if (sourceFaceKey == otherFaceSnapSpace.sourceFaceKey
        && targetFaceKey == otherFaceSnapSpace.targetFaceKey) {
        return true;
      }

      return false;
    }

    public override SnapType SnapType { get { return snapType; } }
  }
}
