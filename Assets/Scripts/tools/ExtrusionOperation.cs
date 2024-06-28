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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;

/// <summary>
/// A single face extrusion. Maintains state of the guides showing users how they're extruding.
///
/// THIS OPERATION CURRENTLY OPERATES ON VERTICES IN WORLD SPACE.  This should be changed to model space at some point
/// in the future - bug
/// </summary>
public class ExtrusionOperation
{

    // If the distance between two vertices is less than this distance, we merge.
    private static readonly float MERGE_DIST_THRESH = .01f;

    /// <summary>
    /// Specifies the parameters for an extrusion operation.
    /// </summary>
    public struct ExtrusionParams
    {
        // If true, extrusion is locked to the face's normal. Translation will be projected onto the normal vector.
        // Rotation will be ignored.
        public bool lockToNormal;
        // Indicates by how much we should translate the extruded face. This is in MODEL space.
        public Vector3 translationModel;
        // Indicates how we should rotate the extruded face. This is in MODEL space.
        public Quaternion rotationModel;
        // Indicates the point about which the extruded face should be rotated, AFTER the translation.
        // This is in MODEL space.
        public Vector3 rotationPivotModel;
    }

    /// <summary>
    /// Represents a side of our extrusion. A side is the face that connects the base face to the extrusion face.
    /// It may be a triangle or a quadrilateral. If it's a triangle, it has two base vertices and one extrusion
    /// vertex. If it's a quad, it has two base vertices and two extrusion vertices.
    ///
    /// QUADRILATERAL SIDE              TRIANGULAR SIDE
    ///
    ///       EL------------ER                 EL == ER
    ///        |            |                  /\
    ///        |            |                 /  \
    ///        |            |                /    \
    ///        |            |               /      \
    ///       BL------------BR          BL +--------+ BR
    ///
    ///
    /// BL = base left vertex
    /// BR = base right vertex
    /// EL = extrusion left vertex
    /// ER = extrusion right vertex
    ///
    /// Note that "left" and "right" are defined from the point of view of someone looking at the face
    /// and thinking of the base face as being "below" and the extrusion face as being "above".
    ///
    /// This class is public for testing.
    /// </summary>
    public class ExtrusionSideVertices
    {
        public readonly Vector3 baseLeft;
        public readonly int baseLeftIndex;
        public readonly Vector3 baseRight;
        public readonly int baseRightIndex;

        private ExtrusionOperation parent;

        /// <summary>
        /// Returns whether this face is a triangle (true) or a quadrilateral (false).
        /// </summary>
        public bool isTriangle { get; private set; }

        /// <summary>
        /// Returns the extrusion left vertex.
        /// Note that if this face is a triangle, the extrusion left and right vertices are the same.
        /// </summary>
        public Vector3 extrusionLeft { get; private set; }

        /// <summary>
        /// Returns the extrusion right vertex.
        /// Note that if this face is a triangle, the extrusion left and right vertices are the same.
        /// </summary>
        public Vector3 extrusionRight { get; private set; }

        /// <summary>
        /// Returns true if this face is a quadrilateral and the extrusion vertices are close enough together
        /// to require a merge.
        /// </summary>
        public bool requiresMerge { get; private set; }

        /// <summary>
        /// Creates a quadrilateral extrusion face with the given vertices.
        /// </summary>
        /// <param name="baseLeft">The base left vertex.</param>
        /// <param name="baseLeftIndex">The index of the base left vertex (in the base face).</param>
        /// <param name="baseRight">The base right vertex.</param>
        /// <param name="baseRightIndex">The index of the base right vertex (in the base face).</param>
        /// <param name="extrusionLeft">The extrusion left vertex.</param>
        /// <param name="extrusionRight">The extrusion right vertex.</param>
        public ExtrusionSideVertices(ExtrusionOperation parent,
          Vector3 baseLeft, int baseLeftIndex, Vector3 baseRight, int baseRightIndex,
          Vector3 extrusionLeft, Vector3 extrusionRight)
        {
            this.parent = parent;
            this.baseLeft = baseLeft;
            this.baseLeftIndex = baseLeftIndex;
            this.baseRight = baseRight;
            this.baseRightIndex = baseRightIndex;
            this.extrusionLeft = extrusionLeft;
            this.extrusionRight = extrusionRight;
            isTriangle = false;
            CheckIfUpdateRequiresMerge();
        }

        /// <summary>
        /// Convert this face from a quadrilateral to a triangle. This means that instead of having two extrusion vertices,
        /// this face will now only have one extrusion vertex, which will be computed as the average of the two.
        /// </summary>
        public void ConvertToTriangle()
        {
            AssertOrThrow.True(!isTriangle, "ExtrusionSideVertices is already a triangle.");
            isTriangle = true;
            Vector3 average = (extrusionRight + extrusionLeft) / 2.0f;
            extrusionRight = extrusionLeft = average;
            CheckIfUpdateRequiresMerge();
        }

        /// <summary>
        /// Sets the position of the extrusion left vertex.
        /// If this face is a triangle, the extrusion right vertex is also updated, as they coincide.
        /// </summary>
        /// <param name="newValue">The new position.</param>
        public void SetExtrusionLeft(Vector3 newValue)
        {
            extrusionLeft = newValue;
            if (isTriangle)
            {
                extrusionRight = newValue;
            }
            CheckIfUpdateRequiresMerge();
        }

        /// <summary>
        /// Sets the position of the extrusion right vertex.
        /// If this face is a triangle, the extrusion left vertex is also updated, as they coincide.
        /// </summary>
        /// <param name="newValue">The new position.</param>
        public void SetExtrusionRight(Vector3 newValue)
        {
            extrusionRight = newValue;
            if (isTriangle)
            {
                extrusionLeft = newValue;
            }
            CheckIfUpdateRequiresMerge();
        }

        private void CheckIfUpdateRequiresMerge()
        {
            // We only merge if a user has enlarged or shrunk the face -- otherwise we preserve the original face the
            // user has grabbed.
            if (parent.scaleOffset == 0)
            {
                return;
            }
            // A face requires a merge if it's a quadrilateral and its two extrusion vertices are too close together.
            float distanceBetweenVertsInModelSpace =
              Vector3.Distance(extrusionLeft, extrusionRight);
            requiresMerge = !isTriangle && distanceBetweenVertsInModelSpace < MERGE_DIST_THRESH;
        }
    }

    private WorldSpace worldSpace;
    private MMesh _mesh;
    private Face heldFace;
    private List<Mesh> sideMeshes;
    private Mesh extrudeFaceMesh;
    private float size = 1.0f;
    float lastSizeBeforeZero;
    float oldSize;
    private FaceProperties originalFaceProperties;
    // The cumulation of enlarge/shrink operations. At 0, this means the extrusion has not been enlarged or shrunk.
    private int scaleOffset;

    public ExtrusionOperation(WorldSpace worldSpace, MMesh mesh, Face heldFace)
    {
        this.worldSpace = worldSpace;
        this._mesh = mesh;
        this.heldFace = heldFace;

        originalFaceProperties = heldFace.properties;
        SetupExtrusionGuide();
    }

    /// <summary>
    /// Sets up a guide for the user which will show the current state of the extrusion.
    /// </summary>
    private void SetupExtrusionGuide()
    {
        extrudeFaceMesh = new Mesh();
        sideMeshes = new List<Mesh>(heldFace.vertexIds.Count);
        for (int i = 0; i < heldFace.vertexIds.Count; i++)
        {
            Mesh sideGuideMesh = new Mesh();
            sideMeshes.Add(sideGuideMesh);
        }
    }

    /// <summary>
    /// Updates the guide showing what the current extrusion will look like if the user
    /// completes the operation.
    /// </summary>
    /// <param name="extrusionParams">Extrusion parameters indicating how to extrude.</param>
    public void UpdateExtrudeGuide(ExtrusionParams extrusionParams)
    {
        extrudeFaceMesh.Clear();
        bool mergeOccurred;
        List<ExtrusionSideVertices> extrusionSides =
          BuildExtrusionSides(this, _mesh, heldFace, extrusionParams, size, out mergeOccurred);
        if (mergeOccurred && size != 0 && oldSize > size)
        {
            // If the vertices were merged because they were too close together on a resize down event,
            // the size is effectively 0 and should be updated. The lastSizeBeforeZero reflects the size the face was
            // before it had to be merged.
            lastSizeBeforeZero = oldSize;
            size = 0;
        }

        // Create our extrude face
        List<Vector3> extrusionFaceVertices = new List<Vector3>();
        for (int i = 0; i < extrusionSides.Count; i++)
        {
            ExtrusionSideVertices side = extrusionSides[i];
            extrusionFaceVertices.Add(side.extrusionLeft);
            if (!side.isTriangle)
            {
                extrusionFaceVertices.Add(side.extrusionRight);
            }
        }

        // Poly's material shaders require vertex colors - this sets up a color channel for the extrustion face vertices.
        int materialId = originalFaceProperties.materialId;
        if (materialId == MaterialRegistry.GEM_ID || materialId == MaterialRegistry.GLASS_ID)
        {
            materialId = MaterialRegistry.BLACK_ID;
        }
        Color vertColor = MaterialRegistry.GetMaterialColorById(materialId);
        Color[] colors = new Color[extrusionFaceVertices.Count];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = vertColor;
        }
        extrudeFaceMesh.SetVertices(extrusionFaceVertices);
        extrudeFaceMesh.SetTriangles(MeshHelper.GetTrianglesAsFan(extrusionFaceVertices.Count), 0);
        extrudeFaceMesh.colors = colors;
        extrudeFaceMesh.RecalculateNormals();
        extrudeFaceMesh.RecalculateBounds();

        // okay, now we connect the original face to the new extrude face by creating the sides.
        for (int i = 0; i < sideMeshes.Count; i++)
        {
            Mesh sideGuide = sideMeshes[i];
            sideGuide.Clear();
            ExtrusionSideVertices extrusionSideVertices = extrusionSides[i];
            if (extrusionSideVertices.isTriangle)
            {
                sideGuide.SetVertices(new List<Vector3>() {
          extrusionSideVertices.baseLeft,
          extrusionSideVertices.baseRight,
          extrusionSideVertices.extrusionLeft,
        });
                // Vertex colors for side faces
                sideGuide.colors = new[] { vertColor, vertColor, vertColor };
                // The size of the extrude face is zero, so its sides are just triangles.
                sideGuide.SetTriangles(new int[] { 0, 1, 2 }, /** submesh */ 0);
            }
            else
            {
                sideGuide.SetVertices(new List<Vector3>() {
          extrusionSideVertices.baseLeft,
          extrusionSideVertices.baseRight,
          extrusionSideVertices.extrusionRight,
          extrusionSideVertices.extrusionLeft
        });
                // Vertex colors for side faces
                sideGuide.colors = new[] { vertColor, vertColor, vertColor, vertColor };
                // Side faces always have four vertices, so there are two triangles.
                sideGuide.SetTriangles(new int[] { 0, 1, 2, 0, 2, 3 }, /** submesh */ 0);
            }
            sideGuide.RecalculateNormals();
            sideGuide.RecalculateBounds();
        }
    }

    /// <summary>
    /// Render extrustion guide.
    /// </summary>
    public void Render()
    {
        foreach (Mesh sideMesh in sideMeshes)
        {
            Graphics.DrawMesh(sideMesh, worldSpace.modelToWorld,
              MaterialRegistry.GetMaterialAndColorById(originalFaceProperties.materialId).material, 0);
        }
        if (extrudeFaceMesh != null)
        {
            Graphics.DrawMesh(extrudeFaceMesh, worldSpace.modelToWorld,
              MaterialRegistry.GetMaterialAndColorById(originalFaceProperties.materialId).material, 0);
        }
    }

    /// <summary>
    /// Extrusion is over (or cancelled), so get rid of the guides.
    /// </summary>
    public void ClearExtrusionGuide()
    {
        extrudeFaceMesh = null;
        sideMeshes.Clear();

        heldFace.SetProperties(originalFaceProperties);
    }

    /// <summary>
    /// Shrinks the face the user is extruding.
    /// </summary>
    public void ShrinkExtrusionFace()
    {
        oldSize = size;
        if (size <= .5f)
        {
            size -= .08f;
        }
        else
        {
            size *= .9f;
        }
        if (size < 0)
        {
            size = 0;
        }
        if (size != oldSize)
        {
            scaleOffset--;
        }

        // Keep track of the size before a user scales to zero, so the next scale-up is the opposite of the scale-down.
        if (oldSize != 0 && size == 0)
        {
            lastSizeBeforeZero = oldSize;
        }
    }

    /// <summary>
    /// Enlarges the face the user is extruding
    /// </summary>
    public void EnlargeExtrusionFace()
    {
        oldSize = size;
        if (size == 0)
        {
            size = lastSizeBeforeZero;
        }
        else
        {
            size *= 1.1f;
        }
        scaleOffset++;
    }

    /// <summary>
    /// Generates a new mesh with the extrusion performed upon it.
    /// </summary>
    /// <param name="extrudeMesh">Mesh to extrude.</param>
    /// <param name="extrusionParams">The parameters indicating how to perform the extrusion.</param>
    /// <param name="addedVertices">All added vertices.</param>
    /// <returns>Extruded version of the mesh.</returns>
    public MMesh DoExtrusion(MMesh mesh, ExtrusionParams extrusionParams, ref HashSet<Vertex> addedVertices)
    {
        heldFace.SetProperties(originalFaceProperties);
        MMesh.GeometryOperation operation = mesh.StartOperation();
        operation.DeleteFace(heldFace.id);

        bool mergeOccurred;
        List<ExtrusionSideVertices> extrusionSides =
          BuildExtrusionSides(this, _mesh, heldFace, extrusionParams, size, out mergeOccurred);
        if (mergeOccurred && size != 0 && oldSize > size)
        {
            // If the vertices were merged because they were too close together on a resize down event,
            // the size is effectively 0 and should be updated. The lastSizeBeforeZero reflects the size the face was
            // before it had to be merged.
            lastSizeBeforeZero = oldSize;
            size = 0;
        }

        List<Vertex> extrusionFaceVertices = new List<Vertex>();
        foreach (ExtrusionSideVertices side in extrusionSides)
        {
            Vector3 extrusion1Local = mesh.ModelCoordsToMeshCoords(side.extrusionLeft);
            Vertex vertex1 = extrusionFaceVertices.FirstOrDefault(v => v.loc == extrusion1Local);
            if (vertex1 == null)
            {
                vertex1 = operation.AddVertexMeshSpace(extrusion1Local);

                addedVertices.Add(vertex1);
                extrusionFaceVertices.Add(vertex1);
            }
            if (side.isTriangle)
            {
                operation.AddFace(new List<int>() { side.baseLeftIndex, side.baseRightIndex, vertex1.id },
                 originalFaceProperties);
            }
            else
            {
                Vector3 extrusion2Local = mesh.ModelCoordsToMeshCoords(side.extrusionRight);
                Vertex vertex2 = extrusionFaceVertices.FirstOrDefault(v => v.loc == extrusion2Local);
                if (vertex2 == null)
                {
                    vertex2 = operation.AddVertexMeshSpace(extrusion2Local);
                    addedVertices.Add(vertex2);
                    extrusionFaceVertices.Add(vertex2);
                }
                List<int> indicesForFace = new List<int>() { side.baseLeftIndex, side.baseRightIndex, vertex2.id, vertex1.id };
                operation.AddFace(indicesForFace, originalFaceProperties);
            }
        }
        if (extrusionFaceVertices.Count > 2)
        {
            operation.AddFace(extrusionFaceVertices, originalFaceProperties);
        }
        operation.Commit();
        return mesh;
    }

    public MMesh mesh { get { return _mesh; } }
    public Face face { get { return heldFace; } }

    /// <summary>
    /// Builds the sides of the extrusion. If the size is small enough, some vertices of the extrusion face may merge.
    /// A size of zero means the vertices will merge into a point and you'll be extruding a conical shape.
    ///
    /// This method is public for testing.
    /// </summary>
    /// <param name="mesh">Mesh being extruded.</param>
    /// <param name="face">Face being extruded.</param>
    /// <param name="extrusionParams">Parameters indicating how to perform the extrusion.</param>
    /// <param name="size">Current size of extrude face.</param>
    /// <param name="mergeOccurred">Whether or not this extrusion side creation needed to merge vertices of the face.</param>
    /// <returns>A list of extrusion sides. The sides contain info whether or not they are triangular.</returns>
    public static List<ExtrusionSideVertices> BuildExtrusionSides(
        ExtrusionOperation extrusionOperation, MMesh mesh, Face face,
        ExtrusionParams extrusionParams, float size, out bool mergeOccurred)
    {
        mergeOccurred = false;

        Vector3 projectedDelta;
        if (extrusionParams.lockToNormal)
        {
            List<Vector3> coplanar = new List<Vector3>() {
        mesh.VertexPositionInModelCoords(face.vertexIds[0]),
        mesh.VertexPositionInModelCoords(face.vertexIds[1]),
        mesh.VertexPositionInModelCoords(face.vertexIds[2])
      };
            Vector3 normal = MeshMath.CalculateNormal(coplanar);
            projectedDelta =
              Vector3.Project(GridUtils.SnapToGrid(extrusionParams.translationModel), normal);
        }
        else
        {
            projectedDelta = extrusionParams.translationModel;
        }

        List<Vector3> originalFace = new List<Vector3>(face.vertexIds.Count);
        for (int i = 0; i < face.vertexIds.Count; i++)
        {
            originalFace.Add(mesh.VertexPositionInModelCoords(face.vertexIds[i]));
        }
        Vector3 extrudeFaceCenter = MeshMath.CalculateGeometricCenter(originalFace) + projectedDelta;
        List<Vector3> extrudeFace = originalFace.Select(delegate (Vector3 v)
        {
            Vector3 moved = v + projectedDelta;

            if (!extrusionParams.lockToNormal)
            {
                // Rotate the point about the requested pivot.
                Vector3 fromPivotToPosition = moved - extrusionParams.rotationPivotModel;
                moved = extrusionParams.rotationPivotModel + extrusionParams.rotationModel * fromPivotToPosition;
            }

            return extrudeFaceCenter + (moved - extrudeFaceCenter) * size;
        }).ToList();

        // Build the sides, using quads for each side. Later we'll figure out if we need to convert any of the quads
        // into triangles.
        bool requiresMerge = false;
        List<ExtrusionSideVertices> sides = new List<ExtrusionSideVertices>();
        for (int i = 0; i < originalFace.Count; i++)
        {
            int nextIndex = (i + 1) % originalFace.Count;
            // Make a quad connecting corresponding sides of the original and extruded face.
            ExtrusionSideVertices side = new ExtrusionSideVertices(extrusionOperation,
              // Base left, first vertex of base (on original face):
              originalFace[i], face.vertexIds[i],
              // Base right, second vertex of base (on original face):
              originalFace[nextIndex], face.vertexIds[nextIndex],
              // Extrusion vertex left (on extruded face):
              extrudeFace[i],
              // Extrusion vertex right (on extruded face):
              extrudeFace[nextIndex]);
            sides.Add(side);
            requiresMerge |= side.requiresMerge;
        }

        // Now we will start merging extrusion vertices, which means converting quadrilateral sides into triangular
        // sides whenever we find a quadrilateral side where the extrusion vertices are too close together.
        while (requiresMerge)
        {
            requiresMerge = false;
            // Look for a side that requires a merge (quads with extrusion vertices too close together).
            int i;
            for (i = 0; i < sides.Count; i++)
            {
                if (sides[i].requiresMerge)
                {
                    // Perform the merge. Note that the merge may affect other sides, as the mutation propagates to
                    // adjacent faces.
                    Merge(sides, i);
                    // After the merge, sides may have changed, so we have to start over.
                    requiresMerge = true;
                    mergeOccurred = true;
                    break;
                }
            }
        }

        return sides;
    }

    /// <summary>
    /// Merges the two extrusion vertices on the given side, updating the other sides as necessary to propagate the
    /// mutation. All other sides may potentially be mutated as a result of the merge.
    /// </summary>
    /// <param name="sides">The array of sides.</param>
    /// <param name="sideToMerge">The index of the side on which to perform the merge. Must be a quadrilateral side, not
    /// triangular, since merging only makes sense on quadrilaterals.</param>
    private static void Merge(List<ExtrusionSideVertices> sides, int sideToMerge)
    {
        // Merging converts a quadrilateral face into a triangular face.
        AssertOrThrow.True(!sides[sideToMerge].isTriangle, "Can't merge extrusion verts of a triangular face.");

        // Make it into a triangle.
        sides[sideToMerge].ConvertToTriangle();
        Vector3 newPos = sides[sideToMerge].extrusionLeft;

        // One doesn't simply convert a side into a triangle. We must now propagate this mutation to the other sides.
        // First, let's go to the left. We only have to propagate until we hit a quadrilateral.
        for (int offsetLeft = 0; offsetLeft < sides.Count; offsetLeft++)
        {
            // Index of the side we're looking at right now (corrected for circular indexing).
            int thisSide = (sideToMerge - offsetLeft + sides.Count) % sides.Count;
            // Move the side's right extrusion vertex to match with the updated extrusion vertex.
            sides[thisSide].SetExtrusionRight(newPos);
            // If this side is a quad, then it has a left and a right extrusion vertices, which means the propagation
            // stops, because further faces to the left are not affected.
            if (!sides[thisSide].isTriangle) break;
        }

        // Now let's propagate to the right until we hit a quad.
        for (int offsetRight = 0; offsetRight < sides.Count; offsetRight++)
        {
            // Index of the side we're looking at right now (corrected for circular indexing).
            int thisSide = (sideToMerge + offsetRight + sides.Count) % sides.Count;
            // Move the side's left extrusion vertex to match with the updated extrusion vertex.
            sides[thisSide].SetExtrusionLeft(newPos);
            // By the same logic as above, if we hit a quad, then we stop propagating because further faces to
            // the right are not affected.
            if (!sides[thisSide].isTriangle) break;
        }
    }
}
