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

namespace com.google.apps.peltzer.client.model.core {
  /// <summary>
  ///   Assigns given meshes to given groups. This command is designed to be generic so that it
  ///   can do and undo grouping and ungrouping operations. It expresses the operation as a
  ///   set of "assignments", each of which given by a mesh ID, a "from" group and a "to" group.
  /// </summary>
  public class SetMeshGroupsCommand : Command {
    public const string COMMAND_NAME = "setMeshGroups";

    // The list of assignments that comprise this command.
    public readonly List<GroupAssignment> assignments;

    // Used internally. Users of this class should use one of the static creation methods below
    // to create a SetMeshGroupsCommand that's appropriate for each use case.
    private SetMeshGroupsCommand(IEnumerable<GroupAssignment> assignments) {
      this.assignments = new List<GroupAssignment>(assignments);
    }

    /// <summary>
    /// Creates a SetMeshGroupsCommand that groups the given meshes into a new group.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="meshIds">The meshes to group together</param>
    /// <returns></returns>
    public static SetMeshGroupsCommand CreateGroupMeshesCommand(Model model, IEnumerable<int> meshIds) {
      int newGroupId = model.GenerateGroupId();
      // Assign each mesh to the new group.
      List<GroupAssignment> groups = new List<GroupAssignment>(meshIds.Count());
      foreach (int meshId in meshIds) {
        groups.Add(new GroupAssignment(meshId, model.GetMesh(meshId).groupId, newGroupId));
      }
      return new SetMeshGroupsCommand(groups);
    }

    /// <summary>
    /// Creates a SetMeshGroupsCommand that ungroups the given meshes.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="meshIds">The meshes to ungroup.</param>
    /// <returns></returns>
    public static SetMeshGroupsCommand CreateUngroupMeshesCommand(Model model, IEnumerable<int> meshIds) {
      // Assign each mesh to the MMesh.GROUP_NONE group.
      List<GroupAssignment> groups = new List<GroupAssignment>(meshIds.Count());
      foreach (int meshId in meshIds) {
        groups.Add(new GroupAssignment(meshId, model.GetMesh(meshId).groupId, MMesh.GROUP_NONE));
      }
      return new SetMeshGroupsCommand(groups);
    }

    public void ApplyToModel(Model model) {
      // Assign each mesh to the prescribed group.
      foreach (GroupAssignment assignment in assignments) {
        model.SetMeshGroup(assignment.meshId, assignment.toGroupId);
      }
    }

    public Command GetUndoCommand(Model model) {
      // To undo the command, we just invert the "to" and "from" groups.
      return new SetMeshGroupsCommand(assignments.Select(assignment => assignment.Reversed()));
    }

    /// <summary>
    /// Represents each assignment in the command. An assignment represents the fact that we
    /// have to assign one particular mesh from one group to another.
    /// </summary>
    public class GroupAssignment {
      // The mesh to reassign.
      public int meshId;
      // The mesh's original group.
      public int fromGroupId;
      // The mesh's new group.
      public int toGroupId;

      public GroupAssignment(int meshId, int fromGroup, int toGroup) {
        this.meshId = meshId;
        this.fromGroupId = fromGroup;
        this.toGroupId = toGroup;
      }

      // Returns the reverse assignment (with from and to flipped).
      public GroupAssignment Reversed() {
        return new GroupAssignment(meshId, toGroupId, fromGroupId);
      }
    }
  }
}
