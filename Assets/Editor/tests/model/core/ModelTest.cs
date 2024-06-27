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
using NUnit.Framework;
using UnityEngine;

using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.model.core {

  [TestFixture]
  // Tests for Model.
  public class ModelTest {

    [Test]
    public void TestAddMesh() {
      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));
      int meshId = 1;

      // Add a mesh.
      MMesh mesh = Primitives.AxisAlignedBox(meshId, Vector3.zero, Vector3.one, /* materialId */ 2);

      model.AddMesh(mesh);

      NUnit.Framework.Assert.AreEqual(mesh, model.GetMesh(meshId));
      NUnit.Framework.Assert.True(model.HasMesh(meshId));
      NUnit.Framework.Assert.AreEqual(1, model.GetNumberOfMeshes());

      // Try to add a mesh with same id.  Should fail.
      try {
        MMesh dupMesh = Primitives.AxisAlignedBox(meshId, Vector3.zero, Vector3.one, /* materialId */ 2);
        model.AddMesh(dupMesh);
        NUnit.Framework.Assert.IsTrue(false, "Expected exception");
      } catch (Exception) {
        // Expected.
      }

      // Ensure mesh was not updated.
      NUnit.Framework.Assert.AreEqual(mesh, model.GetMesh(1));
    }

    [Test]
    public void TestDeleteMesh() {
      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));

      int meshId = 1;
      // Add a mesh.
      MMesh mesh = Primitives.AxisAlignedBox(meshId, Vector3.zero, Vector3.one, /* materialId */ 2);
      model.AddMesh(mesh);
      NUnit.Framework.Assert.AreEqual(mesh, model.GetMesh(meshId));
      NUnit.Framework.Assert.AreEqual(1, model.GetNumberOfMeshes());

      // Try to delete a non-existent mesh.  Should fail.
      try {
        model.DeleteMesh(999);
        NUnit.Framework.Assert.IsTrue(false, "Expected exception");
      } catch (Exception) {
        // Expected
      }

      // Delete the mesh.
      model.DeleteMesh(meshId);
      NUnit.Framework.Assert.False(model.HasMesh(meshId));
      NUnit.Framework.Assert.AreEqual(0, model.GetNumberOfMeshes());
      try {
        model.GetMesh(meshId);
        NUnit.Framework.Assert.IsTrue(false, "Expected exception");
      } catch (Exception) {
        // Expected.
      }
    }

    [Test]
    public void TestUndoRedo() {
      int meshOne = 1;
      int meshTwo = 2;

      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));
      NUnit.Framework.Assert.AreEqual(0, model.GetNumberOfMeshes(),
        "Initial model should be empty.");

      AddMeshCommand command = new AddMeshCommand(Primitives.AxisAlignedBox(
        meshOne, Vector3.zero, Vector3.one, /* materialId */ 3));

      model.ApplyCommand(command);
      NUnit.Framework.Assert.True(model.HasMesh(meshOne));

      command = new AddMeshCommand(Primitives.AxisAlignedBox(
        meshTwo, Vector3.one, Vector3.one, /* materialId */ 4));

      model.ApplyCommand(command);
      // Ensure both Meshes are in the model.
      NUnit.Framework.Assert.True(model.HasMesh(meshOne));
      NUnit.Framework.Assert.True(model.HasMesh(meshTwo));

      model.Undo();
      // Should have removed meshTwo.
      NUnit.Framework.Assert.True(model.HasMesh(meshOne));
      NUnit.Framework.Assert.False(model.HasMesh(meshTwo));

      model.Redo();
      // Both should be back.
      NUnit.Framework.Assert.True(model.HasMesh(meshOne));
      NUnit.Framework.Assert.True(model.HasMesh(meshTwo));

      model.Undo();
      model.Undo();
      // Both should be gone.
      NUnit.Framework.Assert.False(model.HasMesh(meshOne));
      NUnit.Framework.Assert.False(model.HasMesh(meshTwo));

      // Now add a new command.  It should force the redo stack
      // to clear.
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshOne, Vector3.zero, Vector3.one, /* materialId */ 3)));

      NUnit.Framework.Assert.IsFalse(model.Redo(),
        "Expected redo stack to be empty");
    }

    [Test]
    public void TestRemesher() {
      int meshOne = 1;
      int meshTwo = 2;

      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));

      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshOne, Vector3.zero, Vector3.one, /* materialId */ 3)));

      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshTwo, Vector3.one, Vector3.one, /* materialId */ 4)));

      // Make sure the ReMesher knows about these.
      ReMesher remesher = model.GetReMesher();
      NUnit.Framework.Assert.IsTrue(remesher.HasMesh(meshOne));
      NUnit.Framework.Assert.IsTrue(remesher.HasMesh(meshTwo));

      // Hide one and make sure ReMesher is updated.
      model.HideMeshForTestOrTutorial(meshOne);
      NUnit.Framework.Assert.IsFalse(remesher.HasMesh(meshOne));
      NUnit.Framework.Assert.IsTrue(remesher.HasMesh(meshTwo));

      // Unhide and check again.
      model.UnhideMeshForTestOrTutorial(meshOne);
      NUnit.Framework.Assert.IsTrue(remesher.HasMesh(meshOne));
      NUnit.Framework.Assert.IsTrue(remesher.HasMesh(meshTwo));
    }

    [Test]
    public void TestGrouping() {
      int meshOneId = 1;
      int meshTwoId = 2;
      int meshThreeId = 3;
      int meshFourId = 4;
      int meshFiveId = 5;
      int meshSixId = 6;

      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshOneId, Vector3.zero, Vector3.one, /* materialId */ 3)));
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshTwoId, Vector3.one, Vector3.one, /* materialId */ 4)));
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshThreeId, Vector3.zero, Vector3.one, /* materialId */ 3)));
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshFourId, Vector3.one, Vector3.one, /* materialId */ 4)));
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshFiveId, Vector3.one, Vector3.one, /* materialId */ 4)));
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshSixId, Vector3.one, Vector3.one, /* materialId */ 4)));

      MMesh meshOne = model.GetMesh(meshOneId);
      MMesh meshTwo = model.GetMesh(meshTwoId);
      MMesh meshThree = model.GetMesh(meshThreeId);
      MMesh meshFour = model.GetMesh(meshFourId);
      MMesh meshFive = model.GetMesh(meshFiveId);
      MMesh meshSix = model.GetMesh(meshSixId);

      // Group meshes one and two into one group.
      int groupA = model.GenerateGroupId();
      model.SetMeshGroup(meshOne.id, groupA);
      model.SetMeshGroup(meshTwo.id, groupA);

      // Group meshes three and four into another group.
      int groupB = model.GenerateGroupId();
      model.SetMeshGroup(meshThree.id, groupB);
      model.SetMeshGroup(meshFour.id, groupB);

      // Leave mesh5 and mesh6 alone, ungrouped.

      // Check that AreMeshesInSameGroup behaves as expected.
      NUnit.Framework.Assert.IsTrue(model.AreMeshesInSameGroup(new int[] {}));
      NUnit.Framework.Assert.IsTrue(model.AreMeshesInSameGroup(new int[] { meshOneId }));
      NUnit.Framework.Assert.IsTrue(
        model.AreMeshesInSameGroup(new int[] { meshOneId, meshTwoId }));
      NUnit.Framework.Assert.IsFalse(
        model.AreMeshesInSameGroup(new int[] { meshOneId, meshThreeId }));
      NUnit.Framework.Assert.IsTrue(
        model.AreMeshesInSameGroup(new int[] { meshThreeId, meshFourId }));
      NUnit.Framework.Assert.IsFalse(
        model.AreMeshesInSameGroup(new int[] { meshThreeId, meshFiveId }));
      // meshFive and meshSix are in GROUP_NONE, so it should return false even though
      // both are in the "same group".
      NUnit.Framework.Assert.IsFalse(
        model.AreMeshesInSameGroup(new int[] { meshFiveId, meshSixId }));

      // Sanity check with empty group.
      HashSet<int> meshIds = new HashSet<int>();
      model.ExpandMeshIdsToGroupMates(meshIds);
      NUnit.Framework.Assert.IsTrue(meshIds.Count == 0);

      // Group mates for mesh one should be { meshOne, meshTwo }.
      meshIds = new HashSet<int>(new int[] { meshOne.id });
      model.ExpandMeshIdsToGroupMates(meshIds);
      NUnit.Framework.Assert.AreEqual(2, meshIds.Count);
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshOne.id));
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshTwo.id));

      // Group mates for mesh four should be { meshThree, meshFour }.
      meshIds = new HashSet<int>(new int[] { meshFour.id });
      model.ExpandMeshIdsToGroupMates(meshIds);
      NUnit.Framework.Assert.AreEqual(2, meshIds.Count);
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshThree.id));
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshFour.id));

      // Group mates for mesh five (ungrouped) should be only itself.
      meshIds = new HashSet<int>(new int[] { meshFive.id });
      model.ExpandMeshIdsToGroupMates(meshIds);
      NUnit.Framework.Assert.AreEqual(1, meshIds.Count);
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshFive.id));

      // Now merge the two groups together.
      int groupC = model.GenerateGroupId();
      model.SetMeshGroup(meshOne.id, groupC);
      model.SetMeshGroup(meshTwo.id, groupC);
      model.SetMeshGroup(meshThree.id, groupC);
      model.SetMeshGroup(meshFour.id, groupC);

      // And check that it worked.
      meshIds = new HashSet<int>(new int[] { meshOne.id });
      model.ExpandMeshIdsToGroupMates(meshIds);
      NUnit.Framework.Assert.AreEqual(4, meshIds.Count);
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshOne.id));
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshTwo.id));
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshThree.id));
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshFour.id));

      // Try ungrouping.
      model.SetMeshGroup(meshOne.id, MMesh.GROUP_NONE);
      model.SetMeshGroup(meshTwo.id, MMesh.GROUP_NONE);
      model.SetMeshGroup(meshThree.id, MMesh.GROUP_NONE);
      model.SetMeshGroup(meshFour.id, MMesh.GROUP_NONE);

      // Now the meshes should not have any group mates.
      meshIds = new HashSet<int>(new int[] { meshTwo.id });
      model.ExpandMeshIdsToGroupMates(meshIds);
      NUnit.Framework.Assert.AreEqual(1, meshIds.Count);
      NUnit.Framework.Assert.IsTrue(meshIds.Contains(meshTwo.id));
    }
  }
}
