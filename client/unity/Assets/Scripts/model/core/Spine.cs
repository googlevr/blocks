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

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.tools.utils;
using System.Collections.Generic;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  /// Struct for a vertebra. A vertebra is part of a spine and consists of: A position, which when combined with the
  /// spine's origin defines a spine; and the allowed normal of a face placed on this vertebra.
  /// See bug DIAGRAM 0 ANATOMY OF A SPINE.
  /// </summary>
  public class Vertebra {
    // The model space position of the vertebra.
    public Vector3 position;
    // The direction of this vertebra. This is equivalent to the vector that points from the origin of the spine to
    // the vertebra position. This is used to check if different vertebrae are in line with each other.
    public Vector3 direction;
    // The normal of a face if it is placed on top of this vertebra.
    public Vector3 normal;

    public Vertebra(Vector3 position, Vector3 direction, Vector3 normal) {
      this.position = position;
      this.direction = direction;
      this.normal = normal;
    }

    /// <summary>
    /// Checks if a given vertebra is equivalent to this vertebra.
    /// </summary>
    /// <param name="other">The vertebra being compared.</param>
    /// <returns>Whether they have the same position and normal.</returns>
    public bool IsEquivalent(Vertebra other) {
      if (other == null) {
        return false;
      }

      if (Math3d.CompareVectors(other.position, position, 0.001f)
        && Math3d.CompareVectors(other.normal, normal, 0.001f)) {
        return true;
      }

      return false;
    }
  }

  /// <summary>
  /// A spine is the geometric structure that a segment of a stroke is formed around. A group of spines connected
  /// together defines a stroke. A spine is just a line with a defined length and angle. The spine has an origin
  /// point which is the ending of the last spine segment and ends at one of a pre-determined set of "vertebrae".
  /// The positions of the valid vertebrae are geometrically designed so that a stroke face can be rotated around
  /// the vertebra and an edge on the stroke face will lay perfectly flush with the previous stroke face.
  /// 
  /// See bug DIAGRAM 0 ANATOMY OF A SPINE.
  /// </summary>
  public class Spine {
    /// <summary>
    /// The weight given to choosing the normalVertebra. A larger weight will create more controlled angular strokes
    /// by "sticking" to the normal while a smaller weight will produce more variation.
    /// </summary>
    private const float NORMAL_WEIGHT = 1.0f;
    /// <summary>
    /// An angle threshold in degrees to add weight for selecting the normal vertebra.
    /// </summary>
    private const float NORMAL_THRESHOLD = 65.0f;
    /// <summary>
    /// An angle threshold in degrees to add weight for re-selecting the current vertebra as the nearest vertebra.
    /// </summary>
    private const float CURRENT_VERTEBRA_THRESHOLD = 10f;
    /// <summary>
    /// An angle threshold in degrees to add weight for selecting a new vertebra that shares the same direction vector
    /// as the last spine.
    /// </summary>
    private const float LAST_SPINE_THRESHOLD = 5f;
    /// <summary>
    /// The angle we will "bend" the stroke so that we can safely create a cap on the stroke where it changes direction.
    /// This is almost 180 because we want to create a new face at an angle that points in the opposite direction but
    /// still has some volume to avoid exposing backfaces or creating points in the stroke with zero volume.
    /// </summary>
    private const float CAP_BEND = 170f;
    /// <summary>
    /// The allowed change in rotation from face to face.
    /// </summary>
    public const float CURVATURE = 45f;
    /// <summary>
    /// Handle floating point errors.
    /// </summary>
    public const float EPSILON = 0.01f;

    /// <summary>
    /// The length of the spine. The length of the spine is the distance between the centers of two faces such that if
    /// one of the faces is rotated at some angle theta (CURVATURE) to the other face the faces will meet but
    /// not intersect.
    /// </summary>
    public readonly float length;
    /// <summary>
    /// The normal of the face used to generate this spine.
    /// </summary>
    public readonly Vector3 normal;
    /// <summary>
    /// The origin of the spine. A spine segement is a line from the origin to one of the validVertebrae. The origin is
    /// equivalent to the center of the face used to define the spine.
    /// </summary>
    public readonly Vector3 origin;
    /// <summary>
    /// The angle in degrees between the normal and all valid vertebrae excluding the normalVertebra.
    /// </summary>
    private readonly float spineDegrees;
    /// <summary>
    /// The vertebra along the spine's normal.
    /// </summary>
    private readonly Vertebra normalVertebra;
    /// <summary>
    /// The other possible vertebrae for the spine excluding the normal. Each vertebra correlates to an edge of the
    /// defining face. See bug DIAGRAM 6.
    /// </summary>
    private readonly List<Vertebra> validVertebrae;
    /// <summary>
    /// The vertebra that is currently selected for this spine.
    /// </summary>
    private Vertebra currentVertebra;
    /// <summary>
    /// The radius of the face that defines this spine.
    /// </summary>
    private float radius;
    private float vertLength;

    /// <summary>
    /// Creates a spine from the coplanar clockwise vertices defining a face.
    /// </summary>
    /// <param name="vertices">The vertices of the defining face.</param>
    /// <param name="definingAxis">
    /// An axis that all vertebrae must be perpendicular to. The definingAxis lets us limit stroke creation so that the
    /// stroke is locked to a plane. The definingAxis represents the normal of the plane, which guarantees that any
    /// vector perpendicular to the definingAxis exists in the plane.
    /// </param>
    public Spine(List<Vector3> vertices, Vector3 definingAxis) {
      origin = MeshMath.CalculateGeometricCenter(vertices);
      normal = MeshMath.CalculateNormal(vertices);
      // spineDegrees is the angle between the normal and every non-normal valid vertebra. See bug DIAGRAM 2.
      spineDegrees = CURVATURE / 2f;

      // Find the length of the spine. See bug DIAGRAM 1.
      // This is the length if we bend the spine so that edges are flush. This is more restrictive than vertLength so we
      // use this as the defining length of the spine.
      length = FindSpineLength(MeshMath.FindHeightOfARegularPolygonalFace(vertices));
      // Find the radius of the defining face.
      radius = MeshMath.FindRadiusOfARegularPolygonalFace(vertices);
      vertLength = FindSpineLength(radius * 2.0f);
      Vector3 spine = normal * length;

      // We have everything we need to define the normalVertebra.
      normalVertebra = new Vertebra(origin + spine, spine, normal);

      // Now we want to find all the other vertebrae. To do this we define two vectors. One which will represent the
      // vertebra and one that will represent the normal for that vertebra. We rotate both vectors so that their
      // projections onto the defining face would be perpendicular with any edge. The one used to define vertebra is
      // rotated spineDegrees and the one used to define the normal is rotated by the allowed curvature. Now that we
      // have these vectors we just rotate them around the normal so we can use them to define a vertebra for each
      // edge. See bug DIAGRAM 3 and DIAGRAM 4.
      if (definingAxis == Vector3.zero) {
        // The faceAxis is used as an axis to rotate around so that the normal is perpendicular with an edge.
        Vector3 faceAxis = vertices[1] - vertices[0];

        Quaternion spineRotation = Quaternion.AngleAxis(spineDegrees, faceAxis);
        Vector3 rotatedNormal = (Quaternion.AngleAxis(CURVATURE, faceAxis) * spine).normalized;

        // The angle we will need to rotate the vectors so that we can define a vertebra for each edge.
        // Note: The number of vertices == number of edges.
        float angle = 360f / (vertices.Count * 2);
        validVertebrae = new List<Vertebra>();

        // Rotate around the normal and define a vertebra for each rotation until we complete a full rotation.
        // See bug DIAGRAM 5(top), DIAGRAM 5(side), DIAGRAM 6.
        for (int i = 0; i * angle < (360f - EPSILON); i++) {
          Vector3 referenceVector = i % 2 == 0 ? spineRotation * spine : spineRotation * (normal * vertLength);
          Vector3 vertebraPosition = origin + Quaternion.AngleAxis(i * angle, normal) * referenceVector;
          Vector3 vertebraNormal = Quaternion.AngleAxis(i * angle, normal) * rotatedNormal;
          validVertebrae.Add(new Vertebra(vertebraPosition, vertebraPosition - origin, vertebraNormal));
        }
      } else {
        // We have a definingAxis that is used to limit the number of possible vertebrae. All vertebrae must be
        // perpendicular to the definingAxis which forces the stroke to be created on Plane. We make this true by
        // rotating around the definingAxis to generate vertebrae. Note that there can only be the normalVertebra
        // and two other vertebrae in the plane so we can just directly calculate them.
        validVertebrae = new List<Vertebra>();
        Vector3 rotatedSpine = Quaternion.AngleAxis(spineDegrees, definingAxis) * spine;
        Vector3 rotatedNormal = (Quaternion.AngleAxis(CURVATURE, definingAxis) * spine).normalized;
        validVertebrae.Add(new Vertebra(origin + rotatedSpine, rotatedSpine, rotatedNormal));

        rotatedSpine = Quaternion.AngleAxis(-spineDegrees, definingAxis) * spine;
        rotatedNormal = (Quaternion.AngleAxis(-CURVATURE, definingAxis) * spine).normalized;
        validVertebrae.Add(new Vertebra(origin + rotatedSpine, rotatedSpine, rotatedNormal));
      }

      // Set the currently selected vertebra to be null on creation.
      currentVertebra = null;
    }

    public static float FindSpineLength (float faceHeight) {
      return faceHeight * Mathf.Sin((CURVATURE * Mathf.Deg2Rad) / 2);
    }

    /// <summary>
    /// Find the nearest vertebra to a given position.
    ///
    /// To find the nearest vertebra we compare the angle between vectors instead of position. We start by imagining
    /// a vector that goes from the origin to the position. This vector can have an angle from the normal of 0 to 180
    /// degrees. To decide which vertebra we should return we map all 180 possible degrees to a vertebra. For example
    /// if the position vector has a degree of separation from the normal of 0 to 15 degrees we'd return vertebra A.
    /// if the angle was 16 - 90 vertebra B and 90 - 180 vertebra C.
    /// 
    /// See bug DIAGRAM 7 (snapping and not snapping).
    /// </summary>
    /// <param name="position">The position we are comparing.</param>
    /// <param name="isSnapping">Whether we want to snap.</param>
    /// <param name="lastSpineVertebra">
    /// The selected vertebra of the spine before this spine. This is used to try and favor selecting a vertebra
    /// such that this spine will be in line with the last.
    /// </param>
    /// <param name="controllerUp">The upwards direction of the controller.</param>
    /// <param name="shouldForceCheckpoint">Whether or not we should enforce a checkpoint.</param>
    /// <returns>The nearest vertebra.</returns>
    public Vertebra NearestVertebra(Vector3 position, bool isSnapping, bool isManualCheckpointing,
      Vertebra lastSpineVertebra, Vector3 controllerUp, out bool shouldForceCheckpoint) {
      // We will only force a checkpoint in a few cases so set it to false by default.
      shouldForceCheckpoint = false;

      Vector3 requiredChange = position - origin;

      // Find the actual angle from the normal.
      float angleFromNormal = Vector3.Angle(normal, requiredChange);

      if (!isSnapping) {
        // If the user is about to move back on themselves bend the stroke backwards by creating a nearly flat "cap"
        // face where the stroke changes direction.
        if (angleFromNormal > 90f - EPSILON) {
          // We need to bend the stroke backwards in on itself without accidentally exposing back faces. To do this we
          // generate a spine that lays almost parallel to the previous face.

          // Find how long the spine will be.
          float length = Mathf.Sqrt(Mathf.Pow((radius), 2)
            + Mathf.Pow((0.001f), 2)
            - (2 * (radius) * (0.001f) * Mathf.Cos(CAP_BEND * Mathf.Deg2Rad)));

          Vector3 scaledNormal = normal.normalized * length;
          Vector3 rotationalAxis = Vector3.Cross(normal, requiredChange);
          float angle = 90f - ((180f - CAP_BEND) / 2f);
          Vector3 capDirection = Quaternion.AngleAxis(angle, rotationalAxis) * scaledNormal;

          Vector3 capPosition = origin + capDirection;
          Vector3 capNormal = Quaternion.AngleAxis(CAP_BEND, rotationalAxis) * normal;

          // We should consider any bend cap generation as automatic and force a checkpoint.
          shouldForceCheckpoint = true;
          return new Vertebra(capPosition, capDirection, capNormal);
        }

        // Determine the normal of the vertebra based on controllerUp. This allows the user more freedom.
        Vector3 currentNormal =
          Vector3.Angle(controllerUp, requiredChange) < Vector3.Angle(-controllerUp, requiredChange) ?
          controllerUp : -controllerUp;

        // Check for danger.

        // Check to see if the user has moved less than a spine's length. This is a sure fire way to create an invalid
        // mesh.
        if (Vector3.Distance(origin, position) < length) {
          // Check if the user also has the face bent at an angle greater than the allowed curvature. If this happens
          // while the user is too close to the previous face the verts could cross over the last face causing the mesh
          // to invert and be invalid.

          // Or if the user is greater than spineDegrees away from the normal. They are outside the magic threshold
          // where the length and curvature allowance forces valid meshes. If the user is in this zone we have know
          // guarantee they are creating a valid mesh.
          if (Vector3.Angle(normal, currentNormal) > CURVATURE || angleFromNormal > spineDegrees) {
            // Abort. The user is up to no good. Enforce our snapping logic so they can't create an invalid mesh.
            return NearestVertebra(position, /*isSnapping*/ true, isManualCheckpointing, lastSpineVertebra,
              controllerUp, out shouldForceCheckpoint);
          } else {
            // They aren't doing anything too outrageous. Lengthening the spine will enforce a valid mesh.
            Vector3 scaledChange = length * ((position - origin).normalized);
            position = origin + scaledChange;
            requiredChange = position - origin;
          }
        }

        return new Vertebra(position, position - origin, currentNormal);
      } else {
        // Reduce the normal threshold while manualCheckpointing. The further a user moves away from the last spine the
        // large the motion they need to make to cross the angular threshold so we want to reduce the threshold
        // reducing this affect.
        float normalThreshold = isManualCheckpointing ? NORMAL_THRESHOLD / 2.0f : NORMAL_THRESHOLD;

        // The position has an angle from the normal that falls within the range of the degrees mapped to the
        // normalVertebra.
        if (angleFromNormal < normalThreshold) {
          // If we are manually checkpointing we want to return a spine that fills the entire distance to the
          // controller.
          if (isManualCheckpointing) {
            // We know we want to choose the normalVertebra but a longer version of it that maps to the current
            // controller position. We can just project the controller position onto the normalVertebra to the nearest
            // increment of length.
            Vector3 projectedControllerPosition =
              GridUtils.ProjectPointOntoLine(position, normalVertebra.direction, origin, length);

            // Don't let the user create a segment that is smaller than a spine. Also stops them from going "back" on
            // themselves.
            if (Vector3.Distance(projectedControllerPosition, origin) < length || angleFromNormal > 89f) {
              return normalVertebra;
            }
            return new Vertebra(projectedControllerPosition, projectedControllerPosition - origin,
              normalVertebra.normal);
          } else {
            return normalVertebra;
          }
        }

        // We are outside the threshold of the normalVertebra. We need to figure out which of the validVertebrae
        // we are closest to.
        float nearestAngle = Mathf.Infinity;
        Vertebra nearestVertebra = null;

        foreach (Vertebra vertebra in validVertebrae) {
          // Find the actual angle from the vertebra.
          float angle = Vector3.Angle(requiredChange, vertebra.position - origin);

          if (angle < nearestAngle) {
            nearestAngle = angle;
            nearestVertebra = vertebra;
          }
        }

        if (isManualCheckpointing) {
          // We know we want to choose the nearestVertebra but a longer version of it that maps to the current
          // controller position. We can just project the controller position onto the nearestVertebra to the nearest
          // increment of length.
          Vector3 projectedControllerPosition =
            GridUtils.ProjectPointOntoLine(position, nearestVertebra.direction, origin, length);

          // Don't let the user create a segement that is smaller than a spine. Also stops them from going "back" on
          // themselves.
          if (Vector3.Distance(projectedControllerPosition, origin) < length || angleFromNormal > 89f) {
            return nearestVertebra;
          }
          return new Vertebra(projectedControllerPosition, projectedControllerPosition - origin,
            nearestVertebra.normal);
        } else {
          return nearestVertebra;
        }
      }
    }

    /// <summary>
    /// Sets a given vertebra as the currently selected vertebra for this spine.
    /// </summary>
    /// <param name="vertebra">The vertebra being selected.</param>
    public void SelectVertebra(Vertebra vertebra) {
      currentVertebra = vertebra;
    }

    /// <summary>
    /// Returns the currently selected vertebra for this spine.
    /// </summary>
    /// <returns>The currently selected vertebra.</returns>
    public Vertebra CurrentVertebra() {
      return currentVertebra;
    }
  }
}
