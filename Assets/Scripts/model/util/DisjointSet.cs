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

namespace com.google.apps.peltzer.client.model.util {
  /// <summary>
  /// A classic (if naive) implementation of a disjoint set with "Union-Find" algorithm, with path optimization.
  /// Implemented based on what I remember from CS classes in college, I can't really pinpoint the exact
  /// reference text.
  /// 
  /// A disjoint set is a data structure that contains many sets of elements. The fundamental mutations are
  /// adding a new element (which becomes a new set) and joining sets together (union). The caller can also
  /// ask if any two given elements are in the same set.
  ///
  /// But for more info on disjoint sets:
  /// https://en.wikipedia.org/wiki/Disjoint-set_data_structure
  /// </summary>
  /// <typeparam name="T">The type of the elements in the disjoint set.</typeparam>
  public class DisjointSet<T> {
    /// <summary>
    /// This dictionary is our the implementation of the disjoint set data structure. It's the representation
    /// of a graph: each element has a PARENT. Many elements can have the same parent. Elements who are parents
    /// of themselves are called "roots". If you take any element and follow its ancestry line up from parent to
    /// parent until you get to the root, you've found the "root of the element". Hence the fundamental property
    /// of the representation: ELEMENTS ARE IN THE SAME SET IF AND ONLY IF THEY HAVE THE SAME ROOT.
    /// So, using the notation 'X -> Y' to denote that X's parent is Y (lines go from child to parent),
    /// let's say we are in this state:
    ///
    ///     A -> B -> E     F -> G -> H     I -> I
    ///               ^
    ///               |
    ///          C -> D
    /// 
    /// Then our sets are { A, B, C, D, E }, { F, G, H } and { I }. The root of A is E.
    /// The root of B is also E. The root of C is also E. The root of G is H. The root of I
    /// is itself.
    ///
    /// Note that the property is observed:
    /// "An element has the same root as another element if and only if they are IN THE SAME SET."
    /// </summary>
    private Dictionary<T, T> parentOf = new Dictionary<T, T>();

    public DisjointSet() {}

    /// <summary>
    /// If the element isn't already in the structure, adds it as a new set that contains only the element.
    /// If the element is already in the structure, nothing is changed.
    /// </summary>
    /// <param name="element"></param>
    public void Add(T element) {
      if (!parentOf.ContainsKey(element)) {
        // An element that is its own parent represents a standalone set with only the element.
        parentOf[element] = element;
      }
    }

    /// <summary>
    /// Joins the sets to which the elements belong.
    /// Adds the elements to the disjoint set if they were not there before.
    /// </summary>
    /// <param name="element1">The first element.</param>
    /// <param name="element2">The second element.</param>
    public void Join(T element1, T element2) {
      Add(element1);
      Add(element2);
      T root1 = GetRoot(element1);
      T root2 = GetRoot(element2);
      // To unite the two sets, all we have to do is set the parent of one root to the other root.
      // This will guarantee that the root of all the elements in both sets are the same.
      if (!root1.Equals(root2)) {
        parentOf[root1] = root2;
      }
    }

    /// <summary>
    /// Returns whether or not an element is in the structure.
    /// </summary>
    /// <param name="element">The element to check.</param>
    /// <returns>True if and only if the element is in the structure.</returns>
    public bool Contains(T element) {
      return parentOf.ContainsKey(element);
    }

    /// <summary>
    /// Returns whether or not the two given elements are in the same set.
    /// </summary>
    /// <param name="element1">The first element.</param>
    /// <param name="element2">The second element.</param>
    /// <returns>True if and only if the elements are in the data structure and are in the same set.</returns>
    public bool AreInSameSet(T element1, T element2) {
      // If either element is not even in the structure, clearly they can't be in the same set.
      if (!Contains(element1) || !Contains(element2)) return false;
      // To tell whether two elements are in the same set, all we have to do is compare their roots.
      return GetRoot(element1).Equals(GetRoot(element2));
    }

    /// <summary>
    /// Looks up the root of the given element.
    /// </summary>
    /// <param name="element">The element whose root should be looked up.</param>
    /// <returns>The root element of the set to which the element belongs.</returns>
    private T GetRoot(T element) {
      // Move up until we find the root (an element whose parent is itself).
      T current = element;
      while (!parentOf[current].Equals(current)) {
        current = parentOf[current];
      }
      // 'current' is now the root.
      // This is our opportunity to optimize the paths in the data structure, now that we know the root of
      // that 'element' and all its ancestors.
      OptimizePath(element, current);
      return current;
    }

    /// <summary>
    /// Optimizes the data structure by shortening the paths to the root that pass through the
    /// given element. All elements that are ancestors of the given element will be made to point directly at the
    /// root node. Denoting the element as E and the root as R, here is what we would have before:
    /// 
    /// E -> p1 -> p2 -> p3 -> p4 -> R
    /// 
    /// Note how root lookups for E would take 5 hops, lookups for p1 would take 4 hops, etc. How inefficient!
    /// 
    /// After the operation, all nodes in the path will point directly to the root node, optimizing
    /// future lookups so that they can be done directly in 1 hop:
    /// 
    /// E -> R
    /// p1 -> R
    /// p2 -> R
    /// p3 -> R
    /// p4 -> R
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="root">The known root of the element.</param>
    private void OptimizePath(T element, T root) {
      T current = element;
      while (!parentOf[current].Equals(current)) {
        T parent = parentOf[current];
        parentOf[current] = root;
        current = parent;
      }
    }
  }
}
