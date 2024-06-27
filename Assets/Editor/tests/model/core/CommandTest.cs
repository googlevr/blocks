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
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core {

  [TestFixture]
  // Tests for Command and implementations.
  public class CommandTest {

    [Test]
    public void TestCompositeMove() {
      int meshOne = 1;
      int meshTwo = 2;

      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));

      // Add two meshes.
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshOne, Vector3.zero, Vector3.one, /* materialId */ 3)));
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshTwo, Vector3.one, Vector3.one, /* materialId */ 4)));

      // Check the meshes are where we think they are.
      NUnit.Framework.Assert.Less(
        Vector3.Distance(model.GetMesh(meshOne).offset,
        new Vector3(0, 0, 0)), 0.001f);
      NUnit.Framework.Assert.Less(
        Vector3.Distance(model.GetMesh(meshTwo).offset,
        new Vector3(1, 1, 1)), 0.001f);

      // Make and apply a CompositeCommand that moves both of them.
      MoveMeshCommand move1 = new MoveMeshCommand(
        meshOne, new Vector3(1, 0, 0), Quaternion.identity);
      MoveMeshCommand move2 = new MoveMeshCommand(
        meshTwo, new Vector3(0, 1, 0), Quaternion.identity);
      List<Command> commands = new List<Command>();
      commands.Add(move1);
      commands.Add(move2);

      model.ApplyCommand(new CompositeCommand(commands));

      // Check the meshes' new locations.
      NUnit.Framework.Assert.Less(
        Vector3.Distance(model.GetMesh(meshOne).offset,
        new Vector3(1, 0, 0)), 0.001f);
      NUnit.Framework.Assert.Less(
        Vector3.Distance(model.GetMesh(meshTwo).offset,
        new Vector3(1, 2, 1)), 0.001f);

      // Undo and make sure they are back.
      model.Undo();
      NUnit.Framework.Assert.Less(
        Vector3.Distance(model.GetMesh(meshOne).offset,
        new Vector3(0, 0, 0)), 0.001f);
      NUnit.Framework.Assert.Less(
        Vector3.Distance(model.GetMesh(meshTwo).offset,
        new Vector3(1, 1, 1)), 0.001f);
    }

    [Test]
    public void TestCompositeUndo() {
      int meshOne = 1;

      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));

      // Add a Mesh.
      model.ApplyCommand(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshOne, Vector3.zero, Vector3.one, /* materialId */ 3)));

      // Create a CompositeCommand that deletes and adds the same mesh.
      List<Command> commands = new List<Command>();
      commands.Add(new DeleteMeshCommand(meshOne));
      commands.Add(new AddMeshCommand(Primitives.AxisAlignedBox(
        meshOne, Vector3.zero, Vector3.one, /* materialId */ 3)));

      CompositeCommand composite = new CompositeCommand(commands);

      CompositeCommand undoCommand =
        (CompositeCommand) composite.GetUndoCommand(model);

      List<Command> undoList = undoCommand.GetCommands();

      // The order of commands should be the same, we invert the commands
      // *and* invert the order.
      NUnit.Framework.Assert.NotNull(undoList[1] as AddMeshCommand);
      NUnit.Framework.Assert.NotNull(undoList[0] as DeleteMeshCommand);
    }

    [Test]
    public void TestRotation() {
      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));

      MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, /* materialId */ 2);
      model.AddMesh(mesh);

      CheckBounds(model.GetMesh(1).bounds, new Bounds(Vector3.zero, Vector3.one * 2.0f));

      MoveMeshCommand cmd = new MoveMeshCommand(1, Vector3.zero, Quaternion.AngleAxis(45, Vector3.up));
      model.ApplyCommand(cmd);
      // Bounds should have grown, since the box is no longer axis-aligned.
      float extends = 2.0f * Mathf.Sqrt(2);
      CheckBounds(model.GetMesh(1).bounds, new Bounds(Vector3.zero, new Vector3(extends, 2.0f, extends)));

      model.Undo();
      // Make sure we are back:
      CheckBounds(model.GetMesh(1).bounds, new Bounds(Vector3.zero, Vector3.one * 2.0f));
    }

    private void CheckBounds(Bounds left, Bounds right) {
      NUnit.Framework.Assert.Less(Vector3.Distance(left.center, right.center), 0.0001);
      NUnit.Framework.Assert.Less(Vector3.Distance(left.extents, right.extents), 0.0001);
    }

    [Test]
    public void TestChangeFacePropertiesCommand() {
      Model model = new Model(new Bounds(Vector3.zero, Vector3.one * 10));

      int baseMaterialId = 2;
      int newMaterial1 = 3;
      int newMaterial2 = 4;
      MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, baseMaterialId);
      model.AddMesh(mesh);

      Dictionary<int, FaceProperties> props = new Dictionary<int, FaceProperties>();
      props[1] = new FaceProperties(newMaterial1);
      props[2] = new FaceProperties(newMaterial2);

      ChangeFacePropertiesCommand command = new ChangeFacePropertiesCommand(1, props);

      // Apply the command, ensure the mesh was updated.
      model.ApplyCommand(command);
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(0).properties.materialId, baseMaterialId);
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(1).properties.materialId, newMaterial1);
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(2).properties.materialId, newMaterial2);

      // Undo, faces should be back to normal.
      model.Undo();
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(0).properties.materialId, baseMaterialId);
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(1).properties.materialId, baseMaterialId);
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(2).properties.materialId, baseMaterialId);

      // Redo, changes should be applied again.
      model.Redo();
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(0).properties.materialId, baseMaterialId);
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(1).properties.materialId, newMaterial1);
      NUnit.Framework.Assert.AreEqual(model.GetMesh(1).GetFace(2).properties.materialId, newMaterial2);
    }
  }
}
