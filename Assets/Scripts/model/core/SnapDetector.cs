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

using com.google.apps.peltzer.client.alignment;
using com.google.apps.peltzer.client.guides;
using com.google.apps.peltzer.client.model.main;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.render;
using UnityEngine;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    ///   Detects what type of snap should occur and what should be snapped given the current state. We can either
    ///   mesh --> mesh snap, face --> face snap or mesh --> universe snap and we decide this by:
    ///   1) Checking if there is a target mesh to snap to.
    ///     - A target mesh can be snapped to if the source meshes offset is within the bounding box of the target
    ///     mesh and the delta between their offsets is within a threshold. Note bug is still in progress and
    ///     we are still improving mesh snapping.
    ///   2) If not check if there are faces to snap together.
    ///     - A source face can be snapped onto a target face if they point towards each other and the physical
    ///     distance between the faces is within a threshold.
    ///   3) If not default to snapping the source mesh to the universe.
    ///   
    ///   While detecting the snap we will maintain as much relevant information as possible to avoid extraneous
    ///   calculations when the snap is performed.
    /// </summary>
    public class SnapDetector
    {
        // Defining thresholds in world space accounts for a user's available precision and their perspective. When a user
        // can see less they are more likely to snap or stick but when a user is zoomed in and working in detail they need
        // to be more precise to snap or stick because they can visualize what they are working on better.

        // The threshold for searching for any nearby meshes in the spatialIndex that could be snapped to.
        private static readonly float MESH_DETECTION_THRESHOLD_WORLDSPACE = 0.02f;
        // The threshold for searching for any nearby faces in the spatialIndex that could be snapped to.
        private static readonly float FACE_DETECTION_THRESHOLD_WORLDSPACE = 0.06f;
        // The threshold that a source mesh and target mesh must be within to be snapped together.
        private static readonly float MESH_SNAP_THRESHOLD_WORLDSPACE = 0.04f;
        // The percentage of the inside of a mesh's bounds that should be considered the mesh's center for detecting
        // interesections with other meshes.
        private static readonly float CENTER_INTERSECTION_PERCENTAGE = 0.65f;

        private Rope rope;
        private ContinuousFaceSnapEffect continuousFaceSnapEffect;
        private ContinuousPointStickEffect continuousSourcePointSnapEffect;
        private ContinuousPointStickEffect continuousTargetPointSnapEffect;
        private ContinuousMeshSnapEffect continuousMeshSnapEffect;
        private SnapSpace snapSpace;

        public SnapDetector()
        {
            // Instantiate the snap detection rope. This is only updated and shown to the user if Features.showSnappingGuides
            // is enabled but we'll instantiate regardless so that the flag can be flipped mid session.
            rope = new Rope();
            rope.Hide();
            continuousFaceSnapEffect = new ContinuousFaceSnapEffect();
            continuousSourcePointSnapEffect = new ContinuousPointStickEffect();
            continuousTargetPointSnapEffect = new ContinuousPointStickEffect();
            continuousMeshSnapEffect = new ContinuousMeshSnapEffect();
            snapSpace = null;
        }

        /// <summary>
        ///   Detects what type of snap should be performed when required and populates snap with all the correct info
        ///   for maximum performance and reusability.
        /// </summary>
        /// <param name="sourceMesh">The mesh being snapped. This is the preview or held mesh.</param>
        /// <param name="sourceMeshOffset">The actual position in model space for the sourceMesh.</param>
        /// <param name="sourceMeshRotation">The actual model space rotation for the sourceMesh.</param>
        public void DetectSnap(MMesh sourceMesh, Vector3 sourceMeshOffset, Quaternion sourceMeshRotation)
        {
            if (!PeltzerMain.Instance.restrictionManager.snappingAllowed)
            {
                return;
            }
            // Calculate the radius of the sphere needed to encapsulate the sourceMesh. This is used for searching
            // the spatial index and calculated once here to avoid calculating it multiple times.
            float sourceRadius =
              Mathf.Max(sourceMesh.bounds.extents.x, sourceMesh.bounds.extents.y, sourceMesh.bounds.extents.z);

            // Detect what type of snap should be performed.
            // This is either mesh --> mesh, face --> face or mesh --> universe by:
            // 1) Check if there is a mesh to snap to.
            // 2) If not check if there are faces to snap together.
            // 3) If not default to snapping to the universe.
            if (!DetectRelationalSnap(sourceMesh, sourceMeshOffset, sourceMeshRotation, sourceRadius))
            {
                ChangeSnapSpace(new UniversalSnapSpace(sourceMesh.bounds));
            }
        }

        /// <summary>
        /// Executes the last detected snap and returns the SnapSpace to the tool that is snapping.
        /// </summary>
        /// <param name="sourceMesh">The mesh being snapped. This is the preview or held mesh.</param>
        /// <param name="sourceMeshOffset">The actual position in model space for the sourceMesh.</param>
        /// <param name="sourceMeshRotation">The actual model space rotation for the sourceMesh.</param>
        /// <returns>The fully setup SnapSpace.</returns>
        public SnapSpace ExecuteSnap(MMesh sourceMesh, Vector3 sourceMeshOffset, Quaternion sourceMeshRotation)
        {
            if (snapSpace == null || !snapSpace.IsValid())
            {
                // No snapSpace has been detected. We'll have to detect before executing. This happens if the user skips over
                // the detection step by just pulling the full alt-trigger.
                DetectSnap(sourceMesh, sourceMeshOffset, sourceMeshRotation);
            }

            snapSpace.Execute();

            // Relinquish ownership of this snapSpace. Its belongs to the tool now.
            SnapSpace passedOffSnapSpace = snapSpace;

            // Clear out any detection UI.
            Reset();
            return passedOffSnapSpace;
        }

        /// <summary>
        /// Resets SnapDetector by clearing out the previous detected SnapSpace and turning off any existing highlights.
        /// </summary>
        public void Reset()
        {
            HideGuides();
            snapSpace = null;
        }

        public void UpdateHints(SnapSpace snapSpace, MMesh sourceMesh, Vector3 sourceMeshOffset, Quaternion sourceMeshRotation)
        {
            if (snapSpace.SnapType == SnapType.FACE)
            {
                continuousFaceSnapEffect.UpdateFromSnapSpace(snapSpace as FaceSnapSpace);
                FaceSnapSpace tempSpace = (FaceSnapSpace)snapSpace;
                continuousSourcePointSnapEffect.UpdateFromPoint(tempSpace.sourceFaceCenter);
                continuousTargetPointSnapEffect.UpdateFromPoint(tempSpace.snapPoint);
            }
        }

        /// <summary>
        /// We will try to detect a relational snap (Mesh or Face snap) following this algorithm. We combine Face and Mesh
        /// snap detection to avoid doing two passes of calculations on the target and source faces.

        /// 1) Find all the the meshes that interesect a search radius larger than the sourceMesh. This will ensure
        /// that we'll find any meshes that the faces of the sourceMesh intersect as well.

        /// 2) MESH SNAP if any source face is inside any possible target mesh. This indicates that the source mesh is
        /// overlapping or intersecting another mesh in the scene. We will use a heuristic to check if the source mesh is
        /// intersecting any target mesh by checking if the center of a source face is inside a target mesh. This doesn't
        /// detect full geometric overlap but seems good enough.

        /// 3) Account for two edge cases missed by the intersection check in step two:
        ///   1. If the source mesh is inside a hole of the target mesh it won't intersect (A cylinder inside a torus)
        ///   2. If the source mesh is larger than a target mesh and overlaps it entirely no face center will intersect
        /// the smaller target mesh.
        /// We'll use a pretty simple heuristic to catch these two cases. Given all the meshes we found nearby in step one
        /// just check to see if the offset of a nearby mesh is inside the source mesh bounds. Bounds and offset are just
        /// a proxy for mesh geometry and can easily be fooled by anything more complex than a cube. To try to avoid false
        /// positives for mesh snapping here we will check if the target mesh offset is some threshold from the offset of
        /// the source mesh. But this is just a heuristic. It's possible we'll mesh snap when we should face snap and that
        /// there may be a dead zone in the source mesh where we don't mesh snap to a target mesh thats inside the
        /// geometry but outside the threshold. We want to favor face snapping though so we'll keep the threshold small.

        /// 4) FACE SNAP based on information gathered in step two. Step two requires us to compare every source face to
        /// every target face to determine if we should mesh snap so we simultaneously calculate the separation between
        /// the faces and can then face snap the closest pair if we don't mesh snap.
        /// 
        /// 
        /// To be as efficient as possible we will actually:
        /// 1) Find all nearby meshes ordered by nearness of offsets.
        /// 2) Check if the offset of the nearest mesh is within x% of the sourceMesh radius from the sourceMesh offset and
        ///    break before comparing any faces. We only need to compare the nearest mesh, if the nearest mesh is not in
        ///    the threshold, no mesh is.
        /// 3) Compare the set of sourceMesh faces against every face of target meshes one mesh at time. If we determine
        ///    that a source face is inside a target mesh we will break early and mesh snap. We know the first mesh we
        ///    detect a source face is inside is the closest mesh because the nearby meshes were sorted by nearness.
        /// 4) We compare each pair of faces by:
        ///    Doing Mesh Snap checks:
        ///    1. Checking if the center of the source face is behind the plane of the target face. This helps us determine
        ///       if the source face is intersecting the target mesh.
        ///       
        ///    Doing Face Snap checks:
        ///    1. Check if the center of the source face when projected onto the target face is actually within the target
        ///       face boundaries. If it's not we won't snap the faces together since we can't tell for sure they overlap.
        ///       Using the center is only a heuristic and makes its hard to snap large source faces to small target faces
        ///       since.
        ///    2. Check if the angle between the source face and target face point towards each other. We can do this by
        ///       checking that the angle between the normals is greater than 90 degrees.
        ///    3. Calculate separation. This is a combination of the physical distance between the faces and how flush they
        ///       are. See bug for a diagram on how we calculate separation.
        ///    4. Check that the separation is within a threshold. The threshold used to find nearby meshes isn't strict
        ///       enough because we just check that a nearby mesh intersects that threshold. Its possible a face on that
        ///       mesh is far out of reach.
        ///    4. If this is the closest separation detected so far we store it.
        ///  5) If we compare all faces and don't break early to mesh snap we will try to snap the pair of faces we
        ///     determined have the smallest separation.
        ///  6) If we never found an eligible pair of faces to snap we return false.
        /// </summary>
        private bool DetectRelationalSnap(MMesh sourceMesh, Vector3 sourceMeshOffset, Quaternion sourceMeshRotation,
          float sourceRadius)
        {
            // We use a worldspace thresholds so that the user has to move the same amount despite zoom level to get within
            // the mesh snap threshold. This means when they zoom out and meshes are small they are more likely to mesh snap
            // which makes sense given faces are so small they shouldn't be trying to snap them together.

            // Find the threshold used as a search radius around the sourceMesh to find intersecting target meshes.
            float meshClosenessThresholdModelSpace =
              MESH_DETECTION_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale
              + sourceRadius;

            // We use another threshold to determine if two faces are close enough to snap together.
            float faceClosenessThresholdModelSpace =
              FACE_DETECTION_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale
              + sourceRadius;

            // Find all the meshes that the meshClosenessThreshold intersects ordered by nearness.
            List<DistancePair<int>> nearestMeshes;
            bool hasNearbyMeshes = PeltzerMain.Instance.GetSpatialIndex().FindNearestMeshesToNotIncludingPoint(
              sourceMeshOffset,
              meshClosenessThresholdModelSpace,
              out nearestMeshes,
              /*ignoreHiddenMeshes*/ true);

            // If there aren't any nearby meshes we can't mesh or face snap.
            if (hasNearbyMeshes)
            {

                // Run a heuristic check to see if the nearest mesh is "inside" the source mesh by seeing if their offsets are
                // within a threshold apart. The threshold is some fraction of the sourceMesh bounds so we use a proxy for
                // "inside".
                MMesh nearestMesh = PeltzerMain.Instance.model.GetMesh(nearestMeshes[0].value);
                if (Vector3.Distance(nearestMesh.bounds.center, sourceMeshOffset)
                  < sourceRadius * CENTER_INTERSECTION_PERCENTAGE)
                {
                    MeshSnapSpace newMeshSnapSpace = new MeshSnapSpace(nearestMesh.id);
                    newMeshSnapSpace.unsnappedPosition = sourceMeshOffset;
                    newMeshSnapSpace.snappedPosition = nearestMesh.bounds.center;
                    ChangeSnapSpace(newMeshSnapSpace);
                    return true;
                }

                // We determine mesh and face snapping by comparing every face pair in one pass so we set up variables to keep
                // track of what should snap.

                // Values to track the pair of faces that should snap.
                FaceSnapSpace tempFaceSnapSpace = null;
                float closestSeparation = Mathf.Infinity;

                // Compare every face from the source mesh to every possible target mesh and the mesh's faces.
                foreach (Face sourceFace in sourceMesh.GetFaces())
                {
                    // Find the position of the vertices in model space for the sourceMeshFace so we can calculate the face's
                    // center and normal.
                    List<Vector3> sourceMeshFaceVerticesInModelSpace = MeshMath.CalculateVertexPositions(
                      sourceFace.vertexIds,
                      sourceMesh,
                      sourceMeshOffset,
                      sourceMeshRotation);

                    Vector3 sourceFaceCenter = MeshMath.CalculateGeometricCenter(sourceMeshFaceVerticesInModelSpace);
                    Vector3 sourceFaceNormal = MeshMath.CalculateNormal(sourceMeshFaceVerticesInModelSpace);

                    // Compare every source face to every possible target mesh and then the faces of the target mesh. We can
                    // break early on a target mesh if we should mesh snap but use the first pass comparing the faces to keep
                    // track of the closest pair of faces that might be snapped if we don't mesh snap.
                    foreach (DistancePair<int> targetMeshId in nearestMeshes)
                    {
                        // Grab the targetMesh.
                        MMesh targetMesh = PeltzerMain.Instance.model.GetMesh(targetMeshId.value);

                        // Start tracking if the source face is inside this targetMesh. As we iterate over every target face in the
                        // target mesh we'll check if the source face is behind the target face. If at any point this is false we
                        // stop checking because we know that when the source face is not behind one target face then it is not
                        // within the mesh. If sourceFaceBehindEachTargetFace is still true when we have compared the source face
                        // to every face on the target mesh we know the source face intersects the target mesh and we should mesh
                        // snap.
                        bool sourceFaceBehindEachTargetFace = true;
                        foreach (Face targetFace in targetMesh.GetFaces())
                        {
                            FaceInfo targetFaceInfo;
                            FaceKey targetFaceKey = new FaceKey(targetMesh.id, targetFace.id);
                            if (PeltzerMain.Instance.GetSpatialIndex().TryGetFaceInfo(targetFaceKey, out targetFaceInfo))
                            {
                                // Up to this point we were just getting all the information we needed as efficiently as possible. Now
                                // we start the detection algorithm.

                                // 1) Check if the source face is "behind" the target face. If the source face is behind every target
                                // face on the target mesh then we can say that the source mesh intersects the target mesh and we
                                // should mesh snap. As soon as the source face is not "behind" one target face then it does not
                                // intersect and we can stop checking.
                                if (sourceFaceBehindEachTargetFace)
                                {
                                    // GetSide() returns false if the sourceFaceCenter is behind the targetFace.
                                    sourceFaceBehindEachTargetFace = !targetFaceInfo.plane.GetSide(sourceFaceCenter);
                                }

                                // Compare the source face to target face and find their separation. We might mesh snap instead but
                                // if we don't want to have to compare all the faces again so we just do that calculation now. This is
                                // a combination of the physical distance between the faces and how flush they are. See bug for
                                // a diagram on how we calculate separation.

                                // We only want to snap faces that are pointing towards each other so we can breakout by checking the
                                // angle of the normals. If its 90 degrees or less the faces point away from each other.
                                if (Vector3.Angle(sourceFaceNormal, targetFaceInfo.plane.normal) <= 90f)
                                {
                                    continue;
                                }

                                // A ray out of the source face center along the inverse of the target face's normal. This is the
                                // "straight down" projection of the source center onto the target face.
                                Ray projectionRay = new Ray(sourceFaceCenter, -targetFaceInfo.plane.normal);
                                // The projectionLength is a measure of closeness. Small distances along the projection mean the faces
                                // are physically close to each other.
                                float projectionLength;
                                targetFaceInfo.plane.Raycast(projectionRay, out projectionLength);

                                // Check to see if the sourceFaceCenter projected onto the plane of the targetFace is actually within
                                // the boundary of the face. This projected point is called the snap point. If the snap point is not
                                // within the target face we won't try to snap the faces together and don't have to find the normalRay.
                                Vector3 snapPoint = projectionRay.GetPoint(projectionLength);
                                if (!Math3d.IsInside(targetFaceInfo.border, snapPoint))
                                {
                                    continue;
                                }

                                // A ray out of the source face center along the normal of the source face.
                                Ray normalRay = new Ray(sourceFaceCenter, sourceFaceNormal);
                                // Find the distance from the source face center to the target face plane along the normal and
                                // projection. The normalLength is a measure of flushness. Small distances along the normal mean the
                                // faces point towards each other and are flush.
                                float normalLength;
                                targetFaceInfo.plane.Raycast(normalRay, out normalLength);

                                // Calculate the separation which is the sum of these lengths. Not taking the average favours faces
                                // that are close and flush.
                                float separation = Mathf.Abs(normalLength) + Mathf.Abs(projectionLength);

                                if (separation < faceClosenessThresholdModelSpace && separation < closestSeparation)
                                {
                                    closestSeparation = separation;
                                    // Calculate the snapPoint on the target face while we have already done all the heavy calculations.
                                    //Vector3 snapPoint = projectionRay.GetPoint(projectionLength);
                                    FaceKey sourceFaceKey = new FaceKey(sourceMesh.id, sourceFace.id);
                                    tempFaceSnapSpace = new FaceSnapSpace(sourceMesh, sourceFaceKey, targetFaceKey, sourceFaceCenter,
                                      targetFaceInfo.baryCenter, snapPoint);
                                    tempFaceSnapSpace.sourceMeshOffset = sourceMeshOffset;
                                    tempFaceSnapSpace.sourceMeshRotation = sourceMeshRotation;
                                }
                            }
                            else
                            {
                                // Failed to get the faceInfo from the spatialIndex.
                                continue;
                            }
                        }

                        // We've checked every target face for this target mesh against the sourceFace. Check to see if we've
                        // determined if the source face is inside the target mesh and we can break out early and mesh snap.
                        if (sourceFaceBehindEachTargetFace)
                        {
                            MeshSnapSpace newMeshSnapSpace = new MeshSnapSpace(targetMesh.id);
                            newMeshSnapSpace.unsnappedPosition = sourceMeshOffset;
                            newMeshSnapSpace.snappedPosition = targetMesh.bounds.center;
                            ChangeSnapSpace(newMeshSnapSpace);
                            return true;
                        }
                    }
                }

                // We've compared all the source faces to all the nearby target mesh faces. If we've reached this point we
                // already know we should mesh snap so if there is a pair of faces to snap together we'll do that.
                if (tempFaceSnapSpace != null)
                {
                    // But first see if we should be sticking the initial snapPoint of the FaceSnapSpace. We didn't do this
                    // while comparing faces to avoid extraneous calculations on faces we didn't end up using to snap.

                    // Check to see if the initialSnapPoint is close enough to the center of the target face. If it is we should
                    // stick to the center by overriding snapPosition with the target face's center.
                    float distanceFromCenter =
                      Vector3.Distance(tempFaceSnapSpace.initialSnapPoint, tempFaceSnapSpace.targetFaceCenter);
                    tempFaceSnapSpace.initialSnapPoint = distanceFromCenter
                      < CoordinateSystem.STICK_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale
                      ? tempFaceSnapSpace.targetFaceCenter : tempFaceSnapSpace.initialSnapPoint;

                    ChangeSnapSpace(tempFaceSnapSpace);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///   Determines if there is a mesh nearby that should be snapped to and maintains all the snapInfo from detecting
        ///   the mesh snap to be reused when the snap is actually performed.
        /// </summary>
        /// <param name="sourceRadius">The model space radius required to encapsulate the entire source mesh.</param>
        /// <returns>Whether there is a mesh to snap to.</returns>
        private bool DetectMeshSnap(MMesh sourceMesh, Vector3 sourceMeshOffset, Quaternion sourceMeshRotation,
          float sourceRadius)
        {
            // We use a worldspace threshold so that the user has to move the same amount despite zoom level to get within
            // the mesh snap threshold. This means when they zoom out and meshes are small they are more likely to mesh snap
            // which makes sense given faces are so small they shouldn't be trying to snap them together.
            float meshClosenessThresholdModelSpace =
              MESH_DETECTION_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale
              + sourceRadius;

            // Use the spatial index to find the nearest mesh.
            int? nearestMeshId;
            bool hasNearbyMesh = PeltzerMain.Instance.GetSpatialIndex().FindNearestMeshToNotIncludingPoint(
              sourceMeshOffset,
              meshClosenessThresholdModelSpace,
              out nearestMeshId,
              /*ignoreHiddenMeshes*/ true);

            if (hasNearbyMesh)
            {
                MMesh nearestMesh = PeltzerMain.Instance.model.GetMesh(nearestMeshId.Value);
                if (Vector3.Distance(nearestMesh.bounds.center, sourceMeshOffset)
                  < MESH_SNAP_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale)
                {

                    MeshSnapSpace newMeshSnapSpace = new MeshSnapSpace(nearestMeshId.Value);
                    newMeshSnapSpace.unsnappedPosition = sourceMeshOffset;
                    newMeshSnapSpace.snappedPosition = nearestMesh.bounds.center;
                    ChangeSnapSpace(newMeshSnapSpace);
                    return true;
                }
            }

            return false;
        }

        public void HideGuides()
        {
            rope.Hide();
            UXEffectManager.GetEffectManager().EndEffect(continuousFaceSnapEffect);
            UXEffectManager.GetEffectManager().EndEffect(continuousSourcePointSnapEffect);
            UXEffectManager.GetEffectManager().EndEffect(continuousTargetPointSnapEffect);
            continuousMeshSnapEffect.Finish();
        }

        /// <summary>
        ///   Determines if there is a pair of appropriate faces that should be snapped together and maintains all the
        ///   snapInfo from detecting the snap to be reused when the snap is actually performed.
        /// </summary>
        /// <param name="sourceRadius">The model space radius required to encapsulate the source mesh.</param>
        /// <returns>Whether there are faces to snap together.</returns>
        private bool DetectFaceSnap(MMesh sourceMesh, Vector3 sourceMeshOffset, Quaternion sourceMeshRotation,
          float sourceRadius)
        {
            // Calculate the search radius for finding nearby faces. This is the radius of the sphere needed to encapsulate
            // the sourceMesh plus a world space threshold. Defining the threshold in world space accounts for a user's
            // available precision and their perspective.
            float faceClosenessThresholdModelSpace =
              FACE_DETECTION_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale
              + sourceRadius;

            // Grab a set of faces that are within the search radius. The spatialIndex will return faces in a given radius
            // from a point. This isn't useful for face to face snapping because the face closest to the search point isn't
            // necessarily close to any other face. Instead we grab a dump of all the faces nearby and use
            // FindClosestFaces() to check each source face against every nearby face to determine which pair of faces are
            // closest.
            List<DistancePair<FaceKey>> targetFaces;
            // Increasing the number of nearby faces to 300 allows us to snap from a greater distance.
            PeltzerMain.Instance.GetSpatialIndex().FindFacesClosestTo(sourceMeshOffset, faceClosenessThresholdModelSpace,
              /*ignoreInFace*/ false, out targetFaces, /*limit*/ 300);

            // If there are any nearby faces to the mesh parse through the faces and compare them to find the closest pair.
            // Just because there are target faces doesn't mean that they are valid canditates for snapping. Faces can only
            // be snapped together if they point towards each other or the physical separation from face to face is below
            // a threshold.
            if (targetFaces.Count > 0)
            {
                FaceSnapSpace tempSnapSpace = null;
                float closestSeparation = Mathf.Infinity;

                // Iterate through every pair and compare the faces.
                foreach (Face sourceFace in sourceMesh.GetFaces())
                {
                    // Find the position of the vertices in model space for the sourceMeshFace.
                    List<Vector3> sourceMeshFaceVerticesInModelSpace = MeshMath.CalculateVertexPositions(
                      sourceFace.vertexIds,
                      sourceMesh,
                      sourceMeshOffset,
                      sourceMeshRotation);

                    Vector3 sourceFaceNormal = MeshMath.CalculateNormal(sourceMeshFaceVerticesInModelSpace);
                    Vector3 sourceFaceCenter = MeshMath.CalculateGeometricCenter(sourceMeshFaceVerticesInModelSpace);

                    foreach (DistancePair<FaceKey> distancePair in targetFaces)
                    {
                        FaceKey targetFaceKey = distancePair.value;

                        // Don't try to snap to a hidden mesh.
                        if (PeltzerMain.Instance.GetModel().IsMeshHidden(targetFaceKey.meshId))
                        {
                            continue;
                        }

                        FaceInfo targetFaceInfo = PeltzerMain.Instance.GetSpatialIndex().GetFaceInfo(targetFaceKey);

                        // Now we can calculate the separation between the faces. This is a combination of the physical distance
                        // between the faces and how flush they are. See bug for a diagram on how we calculate separation.

                        // We only want to snap faces that are pointing towards each other so we can breakout by checking the
                        // angle of the normals. If its 90 degrees or less the faces point away from each other.
                        if (Vector3.Angle(sourceFaceNormal, targetFaceInfo.plane.normal) <= 90f)
                        {
                            continue;
                        }

                        // A ray out of the source face center along the normal of the source face.
                        Ray normalRay = new Ray(sourceFaceCenter, sourceFaceNormal);

                        // A ray out of the source face center along the inverse of the target face's normal. This is the
                        // "straight down" projection of the source center onto the target face.
                        Ray projectionRay = new Ray(sourceFaceCenter, -targetFaceInfo.plane.normal);
                        // Find the distance from the source face center to the target face plane along the normal and projection.
                        // The normalLength is a measure of flushness. Small distances along the normal mean the faces point
                        // towards each other and are flush.
                        float normalLength;
                        // The projectionLength is a measure of closeness. Small distances along the projection mean the faces are
                        // physically close to each other.
                        float projectionLength;
                        targetFaceInfo.plane.Raycast(normalRay, out normalLength);
                        targetFaceInfo.plane.Raycast(projectionRay, out projectionLength);

                        // Calculate the separation which is the sum of these lengths. Not taking the average favours faces that
                        // are close and flush.
                        float separation = Mathf.Abs(normalLength) + Mathf.Abs(projectionLength);

                        if (separation < closestSeparation)
                        {
                            closestSeparation = separation;
                            // Calculate the snapPoint on the target face while we have already done all the heavy calculations.
                            Vector3 snapPoint = projectionRay.GetPoint(projectionLength);
                            FaceKey sourceFaceKey = new FaceKey(sourceMesh.id, sourceFace.id);
                            tempSnapSpace = new FaceSnapSpace(sourceMesh, sourceFaceKey, targetFaceKey, sourceFaceCenter,
                              targetFaceInfo.baryCenter, snapPoint);
                            tempSnapSpace.sourceMeshOffset = sourceMeshOffset;
                            tempSnapSpace.sourceMeshRotation = sourceMeshRotation;
                        }
                    }
                }

                // We found a face to snap to!
                if (tempSnapSpace != null)
                {
                    // But first see if we should be sticking the initial snapPoint of the FaceSnapSpace. We didn't do this
                    // while comparing faces to avoid extraneous calculations on faces we didn't end up using to snap.

                    // Check to see if the initialSnapPoint is close enough to the center of the target face. If it is we should
                    // stick to the center by overriding snapPosition with the target face's center.
                    float distanceFromCenter = Vector3.Distance(tempSnapSpace.initialSnapPoint, tempSnapSpace.targetFaceCenter);
                    tempSnapSpace.initialSnapPoint = distanceFromCenter
                      < CoordinateSystem.STICK_THRESHOLD_WORLDSPACE / PeltzerMain.Instance.worldSpace.scale
                      ? tempSnapSpace.targetFaceCenter : tempSnapSpace.initialSnapPoint;

                    ChangeSnapSpace(tempSnapSpace);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Changes the currently detected snapSpace to a new one if there are any changes. Will toggle off old UI hints
        /// and turn on any new ones.
        /// </summary>
        /// <param name="newSnapSpace">The newly detected SnapSpace.</param>
        private void ChangeSnapSpace(SnapSpace newSnapSpace)
        {
            SnapSpace previousSnapSpace = snapSpace;

            // Check to see if the snapSpace hasn't actually changed since the last detection. If it hasn't we can avoid
            // updating UI elements.
            if (newSnapSpace.Equals(previousSnapSpace))
            {
                switch (newSnapSpace.SnapType)
                {
                    case SnapType.FACE:
                        // TODO (bug): Update the snap line for the new initialSnapPoint. FaceSnapSpace.Equals ignores initialSnapPoint.
                        FaceSnapSpace tempFaceSpace = (FaceSnapSpace)newSnapSpace;
                        if (Features.showSnappingGuides)
                        {
                            continuousFaceSnapEffect.UpdateFromSnapSpace(newSnapSpace as FaceSnapSpace);
                            continuousSourcePointSnapEffect.UpdateFromPoint(tempFaceSpace.sourceFaceCenter);
                            continuousTargetPointSnapEffect.UpdateFromPoint(tempFaceSpace.snapPoint);
                            //rope.UpdatePosition(PeltzerMain.Instance.worldSpace.ModelToWorld(tempFaceSpace.sourceFaceCenter),
                            // PeltzerMain.Instance.worldSpace.ModelToWorld(tempFaceSpace.initialSnapPoint));
                        }
                        // Overwrite snapSpace so that it has the new initialSnapPoint.
                        snapSpace = newSnapSpace;
                        break;
                    case SnapType.MESH:
                        MeshSnapSpace tempMeshSpace = (MeshSnapSpace)newSnapSpace;
                        continuousMeshSnapEffect.UpdateFromSnapSpace(tempMeshSpace);
                        continuousSourcePointSnapEffect.UpdateFromPoint(tempMeshSpace.unsnappedPosition);
                        continuousTargetPointSnapEffect.UpdateFromPoint(tempMeshSpace.snappedPosition);
                        break;
                    default:
                        break;
                }
                return;
            }

            if (previousSnapSpace != null)
            {
                HideGuides();
                // Turn off any previous highlights.
                switch (previousSnapSpace.SnapType)
                {
                    case SnapType.UNIVERSAL:
                        // There are currently no Universal snap hints.
                        break;
                    case SnapType.MESH:
                        continuousMeshSnapEffect.Finish();
                        UXEffectManager.GetEffectManager().EndEffect(continuousSourcePointSnapEffect);
                        UXEffectManager.GetEffectManager().EndEffect(continuousTargetPointSnapEffect);
                        break;
                    case SnapType.FACE:
                        UXEffectManager.GetEffectManager().EndEffect(continuousFaceSnapEffect);
                        UXEffectManager.GetEffectManager().EndEffect(continuousSourcePointSnapEffect);
                        UXEffectManager.GetEffectManager().EndEffect(continuousTargetPointSnapEffect);
                        break;
                }
            }

            // Turn on any new highlights.
            switch (newSnapSpace.SnapType)
            {
                case SnapType.UNIVERSAL:
                    // There are currently no Universal snap hints.
                    HideGuides();
                    break;
                case SnapType.MESH:
                    MeshSnapSpace tempMeshSpace = (MeshSnapSpace)newSnapSpace;
                    continuousMeshSnapEffect.UpdateFromSnapSpace(tempMeshSpace);
                    UXEffectManager.GetEffectManager().StartEffect(continuousSourcePointSnapEffect);
                    continuousSourcePointSnapEffect.UpdateFromPoint(tempMeshSpace.unsnappedPosition);
                    UXEffectManager.GetEffectManager().StartEffect(continuousTargetPointSnapEffect);
                    continuousTargetPointSnapEffect.UpdateFromPoint(tempMeshSpace.snappedPosition);
                    // TODO (bug): Turn on the mesh snap hint for newSnapSpace.targetMeshId.
                    break;
                case SnapType.FACE:
                    FaceSnapSpace tempFaceSpace = (FaceSnapSpace)newSnapSpace;
                    // TODO (bug): Turn on the hints for the new faces.
                    if (Features.showSnappingGuides)
                    {
                        UXEffectManager.GetEffectManager().StartEffect(continuousFaceSnapEffect);
                        continuousFaceSnapEffect.UpdateFromSnapSpace(tempFaceSpace);
                        UXEffectManager.GetEffectManager().StartEffect(continuousSourcePointSnapEffect);
                        continuousSourcePointSnapEffect.UpdateFromPoint(tempFaceSpace.sourceFaceCenter);
                        UXEffectManager.GetEffectManager().StartEffect(continuousTargetPointSnapEffect);
                        continuousTargetPointSnapEffect.UpdateFromPoint(tempFaceSpace.snapPoint);
                    }
                    break;
            }

            snapSpace = newSnapSpace;
        }
    }
}
