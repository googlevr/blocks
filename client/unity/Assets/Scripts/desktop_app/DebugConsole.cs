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
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using com.google.apps.peltzer.client.api_clients.objectstore_client;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.tools;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.app;

namespace com.google.apps.peltzer.client.desktop_app {
  /// <summary>
  ///   Controls the debug console that appears on the desktop app, where the user can give commands.
  /// </summary>
  public class DebugConsole : MonoBehaviour {
    private const string HELP_TEXT = "COMMANDS:\n" +
      "dump\n  dump debug logs/state to a file\n" +
      "env\n  change environment (background)\n" +
      "flag\n  lists/sets feature flags\n" +
      "fuse\n  fuses all selected meshes into a single mesh.\n" +
      "help\n  shows this help text\n" +
      "insert\n  insert primitives\n" +
      "insertduration <duration>\n  sets the mesh insert effect duration (e.g. 0.6).\n" +
      "loadfile <path>\n  loads a model from the given file (use full path).\n" +
      "loadres <path>\n  loads a model from the given resource file.\n" +
      "minfo\n  prints info about the selected meshes.\n" +
      "movev\n  moves vertices by a given delta.\n" +
      "osq <query>\n  queries objects from the object store.\n" +
      "osload <num>\n  loads the given search result# of the last osq command.\n" +
      "publish\n  saves & publishes the current scene.\n" +
      "rest\n  change restrictions.\n" +
      "savefile <path>\n  saves model to the given file (use full path).\n" +
      "setgid <gid>\n  sets the group ID of selected mesh group.\n" +
      "setmid <mid>\n  sets the mesh ID of selected mesh.\n" +
      "setmaxundo <size>\n  sets the maximum number of undos to store.\n" +
      "tut\n  tutorial-related commands.\n";

    // Set from the Unity editor:
    public GameObject consoleObject;
    public Text consoleOutput;
    public InputField consoleInput;

    private string lastCommand = "";

    private Material originalSkybox;

    // Results of the last search, null if none.
    ObjectStoreEntry[] objectStoreSearchResults;

    public void Start() {
      consoleOutput.text = "DEBUG CONSOLE\n" +
        "Blocks version: " + Config.Instance.version + "\n" +
        "For a list of available commands, type 'help'." +
        "Press ESC to close console.";
    }

    private void Update() {
      // Key combination: Ctrl + D
      bool keyComboPressed = Input.GetKeyDown(KeyCode.D) && Input.GetKey(KeyCode.LeftControl);
      bool escPressed = Input.GetKeyDown(KeyCode.Escape);

      // To open the console, the user has to press the key combo.
      // To close it, either ESC or the key combo are accepted.
      if (!consoleObject.activeSelf && keyComboPressed) {
        // Show console.
        consoleObject.SetActive(true);
        // Focus on the text field so the user can start typing right away.
        consoleInput.ActivateInputField();
        consoleInput.Select();
      } else if (consoleObject.activeSelf && (keyComboPressed || escPressed)) {
        // Hide console.
        consoleObject.SetActive(false);
      }

      if (!consoleObject.activeSelf) return;

      if (Input.GetKeyDown(KeyCode.Return)) {
        // Run command.
        RunCommand(consoleInput.text);
        consoleInput.text = "";
        consoleInput.ActivateInputField();
        consoleInput.Select();
      } else if (Input.GetKeyDown(KeyCode.UpArrow)) {
        // Recover last command and put it in the input text.
        consoleInput.text = lastCommand;
        consoleInput.ActivateInputField();
        consoleInput.Select();
      }
    }

    private void RunCommand(string command) {
      lastCommand = command;
      consoleOutput.text = "";
      string[] parts = command.Split(' ');
      switch (parts[0]) {
        case "dump":
          CommandDump(parts);
          break;
        case "env":
          CommandEnv(parts);
          break;
        case "flag":
          CommandFlag(parts);
          break;
        case "fuse":
          CommandFuse(parts);
          break;
        case "help":
          PrintLn(HELP_TEXT);
          break;
        case "insert":
          CommandInsert(parts);
          break;
        case "insertduration":
          CommandInsertDuration(parts);
          break;
        case "loadfile":
          CommandLoadFile(parts);
          break;
        case "loadres":
          CommandLoadRes(parts);
          break;
        case "minfo":
          CommandMInfo(parts);
          break;
        case "movev":
          CommandMoveV(parts);
          break;
        case "osq":
          CommandOsQ(parts);
          break;
        case "osload":
          CommandOsLoad(parts);
          break;
        case "ospublish":
          CommandOsPublish(parts);
          break;
        case "publish":
          CommandPublish(parts);
          break;
        case "rest":
          CommandRest(parts);
          break;
        case "savefile":
          CommandSaveFile(parts);
          break;
        case "setgid":
          CommandSetGid(parts);
          break;
        case "setmid":
          CommandSetMid(parts);
          break;
        case "setmaxundo":
          CommandSetMaxUndo(parts);
          break;
        case "tut":
          CommandTut(parts);
          break;
        default:
          PrintLn("Unrecognized command: " + command);
          PrintLn("Type 'help' for a list of commands.");
          break;
      }
    }

    private void PrintLn(string message) {
      consoleOutput.text += message + "\n";
    }

    private void CommandOsQ(string[] parts) {
      if (parts.Length != 2) {
        PrintLn("Syntax: osq <query>");
        PrintLn("  Queries the object store with the given term or tag.");
        PrintLn("  Examples:");
        PrintLn("    osq featured");
        PrintLn("    osq tea");
        return;
      }
      string query = parts[1];
      ObjectStoreClient objectStoreClient = new ObjectStoreClient();
      StringBuilder builder = new StringBuilder(ObjectStoreClient.OBJECT_STORE_BASE_URL);
      builder.Append("/s?q=").Append(query);
      PrintLn("Querying for '" + query + "'...");
      StartCoroutine(objectStoreClient.GetObjectStoreListings(
        ObjectStoreClient.GetNewGetRequest(builder, "text/plain"), (ObjectStoreSearchResult result) => {
          if (result.results != null && result.results.Length > 0) {
            objectStoreSearchResults = result.results;
            PrintLn(objectStoreSearchResults.Length + " result(s).\n");
            PrintLn("To load any of these, use 'osload <index>'.\n");
            PrintLn("To publish any of these, use 'ospublish <index>'.\n\n");
            for (int i = 0; i < objectStoreSearchResults.Length; i++) {
              ObjectStoreEntry entry = objectStoreSearchResults[i];
              PrintLn(string.Format("{0}: '{1}' ({2})", i, entry.title, entry.id));
            }
          } else {
            objectStoreSearchResults = null;
            PrintLn("No query results.");
            return;
          }
        }));
    }

    private void CommandOsLoad(string[] parts) {
      int index;
      if (parts.Length != 2 || !int.TryParse(parts[1], out index)) {
        PrintLn("Syntax: osload <index>");
        PrintLn("  Loads the given search result (after calling osq)");
        return;
      }
      if (objectStoreSearchResults == null || index < 0 || index >= objectStoreSearchResults.Length) {
        PrintLn("Invalid search result index. Must be one of the results produced by the osq command.");
        return;
      }
      ObjectStoreClient objectStoreClient = new ObjectStoreClient();
      ObjectStoreEntry entry = objectStoreSearchResults[index];
      PrintLn(string.Format("Loading search result #{0}: {1} (id: {2})...", index, entry.title, entry.id));
      StartCoroutine(objectStoreClient.GetPeltzerFile(entry, (PeltzerFile peltzerFile) => {
        PrintLn("Loaded successfully!");
        PeltzerMain.Instance.CreateNewModel();
        PeltzerMain.Instance.LoadPeltzerFileIntoModel(peltzerFile);
      }));
    }

    private void CommandOsPublish(string[] parts) {
      int index;
      if (parts.Length != 2 || !int.TryParse(parts[1], out index)) {
        PrintLn("Syntax: publish <index>");
        PrintLn("  Loads, saves, then opens the publish dialog for the given search result (after calling osq)");
        return;
      }
      if (objectStoreSearchResults == null || index < 0 || index >= objectStoreSearchResults.Length) {
        PrintLn("Invalid search result index. Must be one of the results produced by the osq command.");
        return;
      }
      ObjectStoreClient objectStoreClient = new ObjectStoreClient();
      ObjectStoreEntry entry = objectStoreSearchResults[index];
      PrintLn(string.Format("Publishing search result #{0}: {1} (id: {2})...", index, entry.title, entry.id));
      StartCoroutine(objectStoreClient.GetPeltzerFile(entry, (PeltzerFile peltzerFile) => {
        PrintLn("Loaded successfully, now trying to save & publish\n.");
        PrintLn("If no browser window opens after a minute or so, this might have failed.");
        PeltzerMain.Instance.CreateNewModel();
        PeltzerMain.Instance.LoadPeltzerFileIntoModel(peltzerFile);
        PeltzerMain.Instance.SaveCurrentModel(publish:true, saveSelected:false);
      }));
    }

    private void CommandPublish(string[] parts) {
      int index;
      if (parts.Length != 1) {
        PrintLn("Syntax: publish");
        PrintLn("Publishes the current scene");
        return;
      }
      PeltzerMain.Instance.SaveCurrentModel(publish:true, saveSelected:false);
    }

    private void CommandFlag(string[] parts) {
      string syntaxHelp = "Syntax:\n  flag list\n  flag set <flagname> {true|false}";
      if (parts.Length < 2) {
        PrintLn(syntaxHelp);
        return;
      }

      Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
      foreach (FieldInfo fieldInfo in typeof(Features).GetFields(BindingFlags.Static | BindingFlags.Public)) {
        // Only get fields that bool and not read-only.
        if (fieldInfo.FieldType == typeof(bool) && fieldInfo.MemberType == MemberTypes.Field &&
            !fieldInfo.IsInitOnly) {
          fields[fieldInfo.Name.ToLower()] = fieldInfo;
        }
      }

      if (parts[1] == "list") {
        List<string> keys = new List<string>(fields.Keys);
        keys.Sort();
        foreach (string fieldName in keys) {
          PrintLn(fields[fieldName].Name + ": " + fields[fieldName].GetValue(null).ToString().ToLower());
        }
      } else if (parts[1] == "set") {
        if (parts.Length != 4) {
          PrintLn(syntaxHelp);
          return;
        }
        string flagName = parts[2];

        if (!fields.ContainsKey(flagName.ToLower())) {
          PrintLn("Unknown flag: " + flagName);
          PrintLn("Use 'flag list' to list all flags.");
          return;
        }

        string flagValueString = parts[3].ToLower();
        bool flagValue;
        if (flagValueString == "true") {
          flagValue = true;
        } else if (flagValueString == "false") {
          flagValue = false;
        } else {
          PrintLn("Flag value must be 'true' or 'false'.");
          return;
        }

        // Set it.
        fields[flagName.ToLower()].SetValue(null, flagValue);
        PrintLn("Flag " + flagName + " set to " + flagValue.ToString().ToLower());
      } else {
        PrintLn(syntaxHelp);
        return;
      }
    }

    private void CommandRest(string[] parts) {
      string helpText = "Syntax:\n" +
        "   rest clear\n" +
        "     Clears all restrictions.\n" +
        "   rest cmode <controller_mode> <controller_mode> ...\n" +
        "     Sets the allowed controller modes (modes names are as in the ControllerMode enum)\n";
      if (parts.Length < 2) {
        PrintLn(helpText);
        return;
      }
      if (parts[1] == "clear") {
        PrintLn("Resetting restrictions.");
        PeltzerMain.Instance.restrictionManager.AllowAll();
      } else if (parts[1] == "cmode") {
        List<ControllerMode> allowedModes = new List<ControllerMode>();
        StringBuilder output = new StringBuilder();
        for (int i = 2; i < parts.Length; i++) {
          try {
            ControllerMode thisMode = (ControllerMode)Enum.Parse(typeof(ControllerMode), parts[i],
                /* ignoreCase */ true);
            allowedModes.Add(thisMode);
            output.Append(" ").Append(thisMode.ToString());
          } catch (Exception) {
            PrintLn("Failed to parse mode: " + parts[i]);
            return;
          }
        }
        PeltzerMain.Instance.restrictionManager.SetAllowedControllerModes(allowedModes);
        PrintLn("Allowed modes set:" + output);
      } else {
        PrintLn(helpText);
      }
    }

    private void CommandTut(string[] parts) {
      string help = "Syntax:\n" +
          "  tut <number>\n" +
          "    Plays tutorial lesson #number.\n" +
          "  tut exit\n" +
          "    Exits the current tutorial.";
      if (parts.Length != 2) {
        PrintLn(help);
        return;
      }
      if (parts[1] == "exit") {
        PrintLn("Exitting tutorial.");
        PeltzerMain.Instance.tutorialManager.ExitTutorial();
        return;
      }
      int tutorialNumber;
      if (parts.Length != 2 || !int.TryParse(parts[1], out tutorialNumber)) {
        PrintLn(help);
        return;
      }
      PrintLn("Starting tutorial #" + tutorialNumber);
      PeltzerMain.Instance.tutorialManager.StartTutorial(tutorialNumber);
    }

    private void CommandLoadRes(string[] parts) {
      if (parts.Length != 2) {
        PrintLn("Syntax: loadres <path>");
        return;
      }
      PrintLn("Loading model from resource path: " + parts[1] + "...");
      try {
        PeltzerMain.Instance.LoadPeltzerFileFromResources(parts[1]);
        PrintLn("Loaded successfully.");
      } catch (Exception e) {
        PrintLn("Load failed (see logs).");
        throw e;
      }
    }

    private void CommandSetMid(string[] parts) {
      int newId;
      PeltzerMain main = PeltzerMain.Instance;
      if (parts.Length != 2 || !int.TryParse(parts[1], out newId) || newId <= 0) {
        PrintLn("Syntax: setmid <id>");
        PrintLn("   Sets the mesh ID of the selected mesh to the given ID.");
        PrintLn("   Exactly one mesh must be selected for this to work.");
        PrintLn("   The ID must be a positive integer.");
        return;
      }
      Selector sel = main.GetSelector();
      List<int> meshIds = new List<int>(sel.SelectedOrHoveredMeshes());
      if (meshIds.Count != 1) {
        PrintLn("Error: exactly one mesh must be selected.");
        return;
      }
      int oldId = meshIds[0];

      sel.DeselectAll();

      // To ensure there are no collisions with the new ID, move the mesh that already had
      // the ID newId to something else (if it happens to exist, which would be rare).
      ChangeMeshId(newId, main.model.GenerateMeshId());
      // Now move oldId -> newId.
      ChangeMeshId(oldId, newId);

      PrintLn("Successfully changed mesh ID " + oldId + " --> " + newId);
    }

    private void CommandSetMaxUndo(string[] parts) {
      int newMaxUndo;
      if (parts.Length != 2 || !int.TryParse(parts[1], out newMaxUndo) || newMaxUndo < 5) {
        PrintLn("Syntax: setmaxundo <max>");
        PrintLn("   Sets the maximum size of the undo stack - minimum 5");
        return;
      }
      Model.SetMaxUndoStackSize(newMaxUndo);
    }

    private void ChangeMeshId(int oldId, int newId) {
      Model model = PeltzerMain.Instance.model;
      if (model.HasMesh(oldId)) {
        model.AddMesh(model.GetMesh(oldId).CloneWithNewId(newId));
        model.DeleteMesh(oldId);
        PeltzerMain.Instance.ModelChangedSinceLastSave = true;
      }
    }

    private void CommandSetGid(string[] parts) {
      int newGroupId;
      PeltzerMain main = PeltzerMain.Instance;
      if (parts.Length != 2 || !int.TryParse(parts[1], out newGroupId) || newGroupId <= 0) {
        PrintLn("Syntax: setgid <id>");
        PrintLn("   Sets the group ID of the selected group to the given ID.");
        PrintLn("   Exactly one group must be selected for this to work (group the meshes first).");
        PrintLn("   The ID must be a positive integer.");
        return;
      }
      Selector sel = main.GetSelector();
      List<int> meshIds = new List<int>(sel.SelectedOrHoveredMeshes());
      if (meshIds.Count < 1) {
        PrintLn("Error: nothing is selected. You must select a group.");
        return;
      }

      // Check that all selected meshes are part of the same group.
      int oldGroupId = main.model.GetMesh(meshIds[0]).groupId;
      if (oldGroupId == MMesh.GROUP_NONE) {
        PrintLn("Error: the selected meshes must be grouped.");
        return;
      }
      foreach (int id in meshIds) {
        if (main.model.GetMesh(id).groupId != oldGroupId) {
          PrintLn("Error: all selected meshes must belong to the same group.");
          return;
        }
      }

      sel.DeselectAll();

      // If there is already a group with ID newGroupId, first change its ID to something else.
      ChangeGroupId(newGroupId, main.model.GenerateGroupId());
      // Now move oldGroupId -> newGroupId.
      ChangeGroupId(oldGroupId, newGroupId);

      PrintLn("Successfully changed group ID " + oldGroupId + " --> " + newGroupId);
    }

    private void ChangeGroupId(int oldGroupId, int newGroupId) {
      Model model = PeltzerMain.Instance.model;
      foreach (MMesh mesh in model.GetAllMeshes()) {
        if (mesh.groupId == oldGroupId) {
          model.SetMeshGroup(mesh.id, newGroupId);
        }
      }
      PeltzerMain.Instance.ModelChangedSinceLastSave = true;
    }

    private void CommandMInfo(string[] parts) {
      Model model = PeltzerMain.Instance.model;
      Selector selector = PeltzerMain.Instance.GetSelector();
      List<int> meshIds = new List<int>(selector.SelectedOrHoveredMeshes());
      List<FaceKey> faceKeys = new List<FaceKey>(selector.SelectedOrHoveredFaces());
      List<EdgeKey> edgeKeys = new List<EdgeKey>(selector.SelectedOrHoveredEdges());
      List<VertexKey> vertexKeys = new List<VertexKey>(selector.SelectedOrHoveredVertices());

      if (meshIds.Count > 0) {
        foreach (int meshId in meshIds) {
          PrintLn(GetMeshInfo(PeltzerMain.Instance.model.GetMesh(meshId)));
        }
      } else if (faceKeys.Count > 0) {
        foreach (FaceKey faceKey in faceKeys) {
          MMesh mesh = model.GetMesh(faceKey.meshId);
          PrintLn(GetFaceInfo(mesh, mesh.GetFace(faceKey.faceId)));
        }
      } else if (edgeKeys.Count > 0) {
        foreach (EdgeKey edgeKey in edgeKeys) {
          MMesh mesh = model.GetMesh(edgeKey.meshId);
          PrintLn(GetEdgeInfo(mesh, edgeKey));
        }
      } else if (vertexKeys.Count > 0) {
        foreach (VertexKey vertexKey in vertexKeys) {
          MMesh mesh = model.GetMesh(vertexKey.meshId);
          PrintLn(GetVertexInfo(mesh, vertexKey.vertexId));
        }
      } else {
        PrintLn("Nothing selected. Model info:\n" + GetModelInfo());
      }
    }

    private string GetMeshInfo(MMesh mesh) {
      StringBuilder sb = new StringBuilder()
        .AppendFormat("MESH id: {0}", mesh.id).Append("\n")
        .AppendFormat("   groupId: {0}", mesh.groupId).Append("\n")
        .AppendFormat("   offset: {0}", DebugUtils.Vector3ToString(mesh.offset)).Append("\n")
        .AppendFormat("   rotation: {0}", mesh.rotation).Append("\n")
        .AppendFormat("   rotation (euler): {0}", mesh.rotation.eulerAngles).Append("\n")
        .AppendFormat("   bounds: {0}", DebugUtils.BoundsToString(mesh.bounds)).Append("\n")
        .AppendFormat("   #faces: {0}", mesh.faceCount).Append("\n")
        .AppendFormat("   #vertices: {0}", mesh.vertexCount).Append("\n")
        .AppendFormat("   remix IDs: {0}",
          mesh.remixIds != null ? string.Join(",", new List<string>(mesh.remixIds).ToArray()) : "NONE")
          .AppendLine();

      foreach (Face face in mesh.GetFaces()) {
        sb.AppendLine(GetFaceInfo(mesh, face));
      }

      foreach (int vertexId in mesh.GetVertexIds()) {
        sb.AppendLine(GetVertexInfo(mesh, vertexId));
      }

      return sb.ToString();
    }

    private string GetModelInfo() {
      Model model = PeltzerMain.Instance.model;
      return new StringBuilder()
        .AppendFormat("MODEL").Append("\n")
        .AppendFormat("  #meshes: {0}", model.GetAllMeshes().Count).AppendLine()
        .AppendFormat("  undo stack size: {0}", model.GetUndoStack().Count).AppendLine()
        .AppendFormat("  redo stack size: {0}", model.GetRedoStack().Count).AppendLine()
        .AppendFormat("  remix IDs: {0}",
          string.Join(",", new List<string>(model.GetAllRemixIds()).ToArray())).AppendLine()
        .ToString();
    }

    private static string GetFaceInfo(MMesh mesh, Face face) {
      StringBuilder sb = new StringBuilder();
      sb.AppendFormat("FACE {0}, {1} vertices:", face.id, face.vertexIds.Count).AppendLine();
      foreach (int vertexId in face.vertexIds) {
        sb.Append("  ").AppendLine(GetVertexInfo(mesh, vertexId));
      }
      return sb.ToString();
    }

    private static string GetEdgeInfo(MMesh mesh, EdgeKey edgeKey) {
      return new StringBuilder()
        .AppendFormat("EDGE {0} - {1}", edgeKey.vertexId1, edgeKey.vertexId2)
        .AppendLine()
        .Append("  From: ").AppendLine(GetVertexInfo(mesh, edgeKey.vertexId1))
        .Append("  To: ").AppendLine(GetVertexInfo(mesh, edgeKey.vertexId2))
        .ToString();
    }

    private static string GetVertexInfo(MMesh mesh, int id) {
      return string.Format("VERTEX {0}: {1} (model space: {2})", id,
        DebugUtils.Vector3ToString(mesh.VertexPositionInMeshCoords(id)), 
        DebugUtils.Vector3ToString(mesh.VertexPositionInModelCoords(id)));
    }

    private void CommandDump(string[] unused) {
      Debug.Log("=== DEBUG DUMP START ===");

      string reportName = string.Format("BlocksDebugReport{0:yyyyMMdd-HHmmss}", DateTime.Now);
      string path = Path.Combine(Path.Combine(PeltzerMain.Instance.userPath, "Reports"), reportName);
      Directory.CreateDirectory(path);

      // Copy current log file to output file path.
      string logFilePath = GetLogFilePath();
      File.Copy(logFilePath, Path.Combine(path, "output_log.txt"));

      // Save a snapshot of the model to output file path.
      File.WriteAllBytes(Path.Combine(path, "model.blocks"),
        PeltzerFileHandler.PeltzerFileFromMeshes(PeltzerMain.Instance.model.GetAllMeshes()));

      string modelDumpOutput = PeltzerMain.Instance.model.DebugConsoleDump();
      File.WriteAllBytes(Path.Combine(path, "model.dump"), Encoding.ASCII.GetBytes(modelDumpOutput));
      PrintLn("Debug dump generated: " + path);
    }

    private static string GetLogFilePath() {
#if UNITY_EDITOR
      string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
      AssertOrThrow.NotNull(localAppData, "LOCALAPPDATA environment variable is not defined.");
      return localAppData + "\\Unity\\Editor\\Editor.log";
#else
      return Application.dataPath + "\\output_log.txt";
#endif
    }

    private void PrintInsertCommandHelp() {
      PrintLn("Syntax: insert {cone|cube|cylinder|sphere|torus} [<offset>] [<scale>]");
      PrintLn("Where <offset> and <scale> are expressed as x,y,z (without any spaces).");
      PrintLn("For example:");
      PrintLn("   insert cube 2.5,3.12,6.5 1.1,2.0,3.0");
      PrintLn("<offset> defaults to 0,0,0 and <scale> defaults to 1,1,1.");
    }

    private void PrintInsertDurationCommandHelp() {
      PrintLn("Syntax: insertduration {time in seconds}");
      PrintLn("For example:");
      PrintLn("   insertduration 0.6");
    }

    private void CommandInsert(string[] parts) {
      if (parts.Length < 2) {
        PrintInsertCommandHelp();
        return;
      }

      // Parse the desired primitive type.
      Primitives.Shape shape;
      try {
        shape = (Primitives.Shape)Enum.Parse(typeof(Primitives.Shape), parts[1], /* ignoreCase */ true);
      } catch (Exception) {
        PrintLn("Error: invalid primitive: " + parts[1]);
        PrintInsertCommandHelp();
        return;
      }

      Vector3 offset = Vector3.zero;
      Vector3 scale = Vector3.one;

      // Parse the offset, if it was provided.
      if (parts.Length >= 3 && !TryParseVector3(parts[2], out offset)) {
        PrintInsertCommandHelp();
        return;
      }

      // Parse the scale, if it was provided.
      if (parts.Length >= 4 && !TryParseVector3(parts[3], out scale)) {
        PrintInsertCommandHelp();
        return;
      }

      int meshId = PeltzerMain.Instance.model.GenerateMeshId();
      MMesh mesh = Primitives.BuildPrimitive(shape, scale, offset, meshId, /* material */ 0);
      PeltzerMain.Instance.model.AddMesh(mesh);

      PrintLn(string.Format("Inserted {0} at {1}, scale {2}, mesh ID {3}", shape, offset, scale, meshId));
    }

    private void CommandInsertDuration(string[] parts) {
      if (parts.Length != 2) {
        PrintInsertDurationCommandHelp();
        return;
      }

      float newDuration;
      if (!float.TryParse(parts[1], out newDuration)) {
        PrintInsertDurationCommandHelp();
        return;
      }

      MeshInsertEffect.DURATION_BASE = newDuration;

      PrintLn(string.Format("Updated insert duration to {0}", newDuration));
    }

    private void CommandMoveV(string[] parts) {
      Vector3 delta;
      if (parts.Length != 2 || !TryParseVector3(parts[1], out delta)) {
        PrintLn("Syntax: move <delta_x>,<delta_y>,<delta_z>");
        PrintLn("  Moves the selected vertices by the given delta in model space.");
        PrintLn("");
        PrintLn("  IMPORTANT: do not use spaces between the coordinates.");
        PrintLn("  Example: move 1.5,2.0,-3.1");
        return;
      }
      List<Vertex> updatedVerts = new List<Vertex>();
      int meshId = -1;
      MMesh original = null;
      foreach (VertexKey vkey in PeltzerMain.Instance.GetSelector().SelectedOrHoveredVertices()) {
        if (meshId < 0) {
          meshId = vkey.meshId;
          original = PeltzerMain.Instance.model.GetMesh(meshId);
        } else if (meshId != vkey.meshId) {
          PrintLn("Selected vertices must belong to same mesh.");
          return;
        }
        updatedVerts.Add(new Vertex(vkey.vertexId, original.VertexPositionInMeshCoords(vkey.vertexId) + delta));
      }
      if (meshId < 0) {
        PrintLn("No vertices selected.");
        return;
      }

      MMesh clone = original.Clone();
      if (!MeshFixer.MoveVerticesAndMutateMeshAndFix(original, clone, updatedVerts, /* forPreview */ false)) {
        PrintLn("Failed to move vertices. Resulting mesh was invalid.");
        return;
      }

      PeltzerMain.Instance.model.ApplyCommand(new ReplaceMeshCommand(meshId, clone));
      PrintLn(string.Format("Mesh {0} successfully modified ({1} vertices displaced by {2})",
        meshId, updatedVerts.Count, delta));
    }

    // Parses a string like "1.1,2.2,3.3" into a Vector3.
    private static bool TryParseVector3(string s, out Vector3 result) {
      result = Vector3.zero;
      string[] coords = s.Split(',');
      return (coords.Length == 3) &&
        float.TryParse(coords[0], out result.x) &&
        float.TryParse(coords[1], out result.y) &&
        float.TryParse(coords[2], out result.z);
    }

    private void CommandEnv(string[] parts) {
      string helpText = "env {reset|white|black|r,g,b}\n" +
        "  Sets/resets the environment (background).\n" +
        "  r,g,b must be in floating point with no spaces, example: 1.0,0.5,0.5\n";
      if (parts.Length < 2) {
        PrintLn(helpText);
        return;
      }

      GameObject envObj = ObjectFinder.ObjectById("ID_Environment");
      GameObject terrain = ObjectFinder.ObjectById("ID_TerrainLift");
      GameObject terrainNoMountains = ObjectFinder.ObjectById("ID_TerrainNoMountains");
      Color bgColor;
      Vector3 colorV;

      if (parts[1] == "reset") {
        if (originalSkybox != null) {
          RenderSettings.skybox = originalSkybox;
        }
        envObj.SetActive(true);
        terrain.SetActive(true);
        terrainNoMountains.SetActive(true);
        PrintLn("Environment reset.");
        return;
      } else if (parts[1] == "white") {
        bgColor = Color.white;
      } else if (parts[1] == "black") {
        bgColor = Color.black;
      } else if (TryParseVector3(parts[1], out colorV)) {
        bgColor = new Color(colorV.x, colorV.y, colorV.z);
      } else {
        PrintLn(helpText);
        return;
      }

      if (originalSkybox == null) {
        originalSkybox = RenderSettings.skybox;
      }
      RenderSettings.skybox = new Material(Resources.Load<Material>("Materials/UnlitWhite"));
      envObj.SetActive(false);
      terrain.SetActive(false);
      terrainNoMountains.SetActive(false);
      RenderSettings.skybox.color = bgColor;
      PrintLn("Environment color set to " + bgColor);
    }

    private void CommandLoadFile(string[] parts) {
      if (parts.Length != 2) {
        PrintLn("Syntax: loadfile <path>");
        return;
      }
      string filePath = parts[1];
      if (!File.Exists(filePath)) {
        PrintLn("Error: file does not exist: " + filePath);
        return;
      }
      PrintLn("Loading model from file path: " + filePath + "...");
      try {
        PeltzerFile peltzerFile;
        byte[] fileBytes = File.ReadAllBytes(filePath);
        if (!PeltzerFileHandler.PeltzerFileFromBytes(fileBytes, out peltzerFile)) {
          PrintLn("Failed to load. Bad format?");
          return;
        }
        PeltzerMain.Instance.LoadPeltzerFileIntoModel(peltzerFile);
        PrintLn("Loaded successfully: " + filePath);
      } catch (Exception e) {
        PrintLn("Load failed (see logs).");
        throw e;
      }
    }

    private void CommandSaveFile(string[] parts) {
      if (parts.Length != 2) {
        PrintLn("Syntax: savefile <path>");
        return;
      }
      string filePath = parts[1];
      PrintLn("Saving model to file path: " + filePath + "...");
      try {
        File.WriteAllBytes(filePath, PeltzerFileHandler.PeltzerFileFromMeshes(PeltzerMain.Instance.model.GetAllMeshes()));
        PrintLn("Saved successfully: " + filePath);
      } catch (Exception e) {
        PrintLn("Save failed (see logs).");
        throw e;
      }
    }

    private void CommandFuse(string[] parts) {
      HashSet<int> meshIds = new HashSet<int>(PeltzerMain.Instance.GetSelector().selectedMeshes);
      if (meshIds.Count < 2) {
        PrintLn("Select at least 2 meshes to fuse.");
        return;
      }
      List<MMesh> meshes = new List<MMesh>();
      foreach (int meshId in meshIds) {
        meshes.Add(PeltzerMain.Instance.model.GetMesh(meshId));
      }
      int newId = PeltzerMain.Instance.model.GenerateMeshId();
      PeltzerMain.Instance.model.AddMesh(Fuser.FuseMeshes(meshes, newId));

      PeltzerMain.Instance.GetSelector().DeselectAll();
      foreach (int meshId in meshIds) {
        PeltzerMain.Instance.model.DeleteMesh(meshId);
      }

      PrintLn(string.Format("Created fused mesh from {0} meshes.", meshIds.Count));
    }
  }
}
