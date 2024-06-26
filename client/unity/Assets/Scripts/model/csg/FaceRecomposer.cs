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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.csg {
  public class FaceRecomposer {
    /// <summary>
    ///   Given a list of polygons (which are specified as a list of vertices) we try to merge together
    ///   as many polygons as possible.
    ///
    ///   We assume that two vertices with the same id are the same vertex (semantically speaking).  So our
    ///   strategy is to look for matching segments in two polygons.  If two polygons have the same pair
    ///   (in reverse order from each other) within their vertex list, we can join them at that segment.
    ///
    ///   If there are no overlapping segments, we also look for a shared vertex.  If we find one, we
    ///   look for a possible T-junction.  If we find one, we will join the polygons at that junction.
    /// </summary>
    public static List<List<SolidVertex>> RecomposeFace(List<List<SolidVertex>> startPieces) {
      List<List<SolidVertex>> joinedPieces = new List<List<SolidVertex>>();
      while (startPieces.Count > 0) {
        Dictionary<Edge, int> curSegments = new Dictionary<Edge, int>();
        Dictionary<int, int> curVerts = new Dictionary<int, int>();
        List<SolidVertex> curPiece = startPieces[0];
        startPieces.RemoveAt(0);
        LoadSegments(curPiece, curSegments, curVerts);

        // Walk through all other pieces to see if one shares a segment.
        bool foundJoin;
        do {
          foundJoin = false;
          for (int i = 0; i < startPieces.Count; i++) {
            List<SolidVertex> toJoin = startPieces[i];

            for (int j = 0; j < toJoin.Count; j++) {
              Edge seg = new Edge(toJoin[(j + 1) % toJoin.Count].vertexId, toJoin[j].vertexId);
              int joinAt;
              if (curSegments.TryGetValue(seg, out joinAt)) {
                JoinPiecesAt(curPiece, joinAt, toJoin, j);
                LoadSegments(curPiece, curSegments, curVerts);
                startPieces.RemoveAt(i);
                foundJoin = true;
                break;
              }
              if (foundJoin) {
                break;
              }
            }

            if (!foundJoin) {
              // Look for a matching point.  Maybe split the polygon at that point.
              for (int j = 0; j < toJoin.Count; j++) {
                int idx;
                if (curVerts.TryGetValue(toJoin[j].vertexId, out idx)) {
                  int jMinusOne = (j - 1 + toJoin.Count) % toJoin.Count;
                  int jPlusOne = (j + 1) % toJoin.Count;
                  int idxPlusOne = (idx + 1) % curPiece.Count;
                  int idxMinusOne = (idx - 1 + curPiece.Count) % curPiece.Count;

                  if (IsOnSegment(toJoin[jPlusOne], curPiece[idx], curPiece[idxMinusOne])
                    || IsOnSegment(curPiece[idxMinusOne], toJoin[j], toJoin[jPlusOne])) {
                    curPiece.RemoveAt(idx);
                    curPiece.InsertRange(idx, CycleAndRemoveN(toJoin, j + 1, 1));
                    LoadSegments(curPiece, curSegments, curVerts);
                    startPieces.RemoveAt(i);
                    foundJoin = true;
                    break;
                  } else if (IsOnSegment(toJoin[jMinusOne], curPiece[idx], curPiece[idxPlusOne])
                    || IsOnSegment(curPiece[idxPlusOne], toJoin[j], toJoin[jMinusOne])) {
                    curPiece.RemoveAt(idx);
                    curPiece.InsertRange(idx, CycleAndRemoveN(toJoin, j + 1, 1));
                    LoadSegments(curPiece, curSegments, curVerts);
                    startPieces.RemoveAt(i);
                    foundJoin = true;
                    break;
                  }
                }
              }
            }
            if (foundJoin) {
              break;
            }
          }
        } while (foundJoin);
        joinedPieces.Add(curPiece);
      }

      return joinedPieces;
    }

    /// <summary>
    ///   Look for a T-junction.
    /// </summary>
    private static bool IsOnSegment(SolidVertex point, SolidVertex start, SolidVertex end) {
      Vector3 startLoc = start.position;
      Vector3 lineVec = (end.position - start.position).normalized;
      float lineLength = Vector3.Distance(start.position, end.position);
      Vector3 startToPoint = point.position - startLoc;
      float t = Vector3.Dot(startToPoint, lineVec);
      Vector3 projected = startLoc + lineVec * t;
      return  t >= 0 && t <= lineLength && Vector3.Distance(projected, point.position) < 0.0005;
    }

    /// <summary>
    ///   Join two polygons that share a segment.
    /// </summary>
    private static void JoinPiecesAt(List<SolidVertex> mainPiece, int joinAt, List<SolidVertex> toJoin, int joinTo) {
      List<SolidVertex> toInsert = new List<SolidVertex>();
      for (int i = 0; i < (toJoin.Count - 2); i++) {
        toInsert.Add(toJoin[(i + joinTo + 2) % toJoin.Count]);
      }
      mainPiece.InsertRange((joinAt + 1) % mainPiece.Count, toInsert);
    }

    /// <summary>
    ///   Grab a sub-chain of a polygon.
    /// </summary>
    private static List<SolidVertex> CycleAndRemoveN(List<SolidVertex> orig, int offset, int n) {
      List<SolidVertex> copy = new List<SolidVertex>();
      for (int i = 0; i < (orig.Count - n); i++) {
        copy.Add(orig[(i + offset) % orig.Count]);
      }
      return copy;
    }

    /// <summary>
    ///   Fill the indexes with data from the given polygon.
    /// </summary>
    private static void LoadSegments(
        List<SolidVertex> curPiece, Dictionary<Edge, int> curSegments, Dictionary<int, int> curVerts) {
      curSegments.Clear();
      curVerts.Clear();
      for (int i = 0; i < curPiece.Count; i++) {
        curVerts[curPiece[i].vertexId] = i;
        curSegments[new Edge(curPiece[i].vertexId, curPiece[(i + 1) % curPiece.Count].vertexId)] = i;
      }
    }
  }
}
