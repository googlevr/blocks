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
using System.Linq;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.serialization;

namespace com.google.apps.peltzer.client.model.export {
  public class AutoSave {
    // The file pattern of autosave directories and filename-bases. Unique filenames have second-level granularity,
    // but in case of a collision (which would be rare, as it would require two commands in the same second *and*
    // the first auto-save to complete within that second), we just overwrite.
    private static readonly string AUTO_SAVE_PATTERN = "Autosave_{0:yyyy-MM-dd_HH-mm-ss}";
    // The start of the auto-save pattern, such that we can cheaply check if a file looks like an auto-save file.
    private static readonly string AUTO_SAVE_START = AUTO_SAVE_PATTERN.Substring(0, AUTO_SAVE_PATTERN.IndexOf("{"));
    // The number of auto-saves we will keep at any given time. The more we have, the better our chances at
    // least one is not corrupt.
    private static readonly int AUTO_SAVE_FILE_COUNT = 5;
    // A path to the user's Poly models auto-save data folder.
    private string autoSavePath;

    // The directory of the current (next) autosave.
    public string CurrentAutoSaveDirectory { get; private set; }
    // The base of the filename of the current (next) autosave.
    public string CurrentAutoSaveFilenameBase { get; private set; }

    // The last data that was auto-saved.
    public SaveData MostRecentAutoSaveData { get; private set; }

    // Whether a save is currently in progress. We won't try and perform two saves at the same time.
    public bool IsCurrentlySaving;

    // The model.
    private Model model;

    /// <summary>
    ///   Creates a new AutoSave, with the given model. Creates the Auto Save directory, and any parents, if needed.
    /// </summary>
    public AutoSave(Model model, string modelsPath) {
      this.model = model;

      autoSavePath = Path.Combine(modelsPath, "Autosave");
      if (!Directory.Exists(autoSavePath)) {
        Directory.CreateDirectory(autoSavePath);
      }
    }

    /// <summary>
    ///   Gets a list of all auto-save directories.
    /// </summary>
    private IEnumerable<DirectoryInfo> GetAutoSaveDirectories() {
      return new DirectoryInfo(autoSavePath).GetDirectories()
        .Where(x => x.Name.StartsWith(AUTO_SAVE_START));
    }

    /// <summary>
    ///   Deletes the oldest auto-saves such that a maximum of AUTO_SAVE_FILE_COUNT remain.
    /// </summary>
    private void DeletePreviousSaves() {
      try {
        // Least recent first.
        DirectoryInfo[] autoSaves = GetAutoSaveDirectories().OrderBy(x => x.LastWriteTimeUtc).ToArray();
        if (autoSaves.Length > AUTO_SAVE_FILE_COUNT) {
          for (int i = autoSaves.Length - AUTO_SAVE_FILE_COUNT; i >= 0; i--) {
            Directory.Delete(autoSaves[i].FullName, /* recursive */ true);
          }
        }
      } catch (Exception exception) {
        Debug.LogWarningFormat("Error deleting previous saves: {0}\n{1}", exception.Message, exception.StackTrace);
      }
    }

    /// <summary>
    ///   Writes an auto-save to the user's local disk. Then, cleans up if we have too many auto-saves.
    /// </summary>
    /// <param name="saveData">A struct containing all the binary data for the auto-save</param>
    public void WriteAutoSave(SaveData saveData) {
      if (ExportUtils.SaveLocally(saveData, CurrentAutoSaveDirectory)) {
        MostRecentAutoSaveData = saveData;
        DeletePreviousSaves();
      }
    }

    /// <summary>
    ///   Returns whether at least one auto-save directory exists.
    /// </summary>
    public bool AutoSaveDirectoryExists() {
      return GetAutoSaveDirectories().Count() > 0;
    }

    /// <summary>
    ///   Updates the current directory and filename base for the current (next) auto-save.
    /// </summary>
    public void UpdateCurrentAutoSavePath() {
      CurrentAutoSaveFilenameBase = string.Format(AUTO_SAVE_PATTERN, DateTime.Now);
      CurrentAutoSaveDirectory = Path.Combine(autoSavePath, CurrentAutoSaveFilenameBase);
    }

    /// <summary>
    ///   Loads the PeltzerFile for the most-recent auto-save (or returns 'false' and sets the peltzerFile to null).
    /// </summary>
    /// <param name="peltzerFile">The PeltzerFile of the most-recent auto-save, or null on failure</param>
    /// <returns>If the file could be loaded</returns>
    public bool LoadMostRecentAutoSave(out PeltzerFile peltzerFile) {
      // Most recent first.
      IEnumerable<DirectoryInfo> autoSaveDirectories = GetAutoSaveDirectories().
        OrderByDescending(x => x.LastWriteTimeUtc);
      if (autoSaveDirectories.Count() == 0) {
        Debug.Log("No autosave directories found");
        peltzerFile = null;
        return false;
      }
      FileInfo[] autoSaveFile = autoSaveDirectories.First().GetFiles("*.poly");
      if (autoSaveFile.Count() == 0) {
        Debug.Log("No .poly file found in autosave directory");
        peltzerFile = null;
        return false;
      }
      if (PeltzerFileHandler.PeltzerFileFromBytes(File.ReadAllBytes(autoSaveFile[0].FullName), out peltzerFile)) {
        return true;
      } else {
        // TODO(bug): Deal with corrupt files? Perhaps by iterating over directories until a
        // non -failing case is found?
        Debug.Log("Latest .poly file was corrupt");
        peltzerFile = null;
        return false;
      }
    }
  }

  /// <summary>
  ///   BackgroundWork for serializing a model into bytes (for saving).
  /// </summary>
  public class AutoSaveWork : BackgroundWork {
    private readonly AutoSave autoSave;
    private readonly Model model;
    private SaveData saveData;
    private MeshRepresentationCache meshRepresentationCache;
    private PolySerializer serializer;

    public AutoSaveWork(Model model, MeshRepresentationCache meshRepresentationCache, AutoSave autoSave,
        PolySerializer serializer) {
      this.model = model;
      this.meshRepresentationCache = meshRepresentationCache;
      this.autoSave = autoSave;
      this.serializer = serializer;
    }

    public void BackgroundWork() {
      // We expect autosave may fail (due to the mesh being modified as autosave reads it), which is
      // acceptable -- it's not terrible to miss one autosave. See bug for details.
      try {
        autoSave.UpdateCurrentAutoSavePath();
        saveData = ExportUtils.SerializeModel(model, model.GetAllMeshes(),
          /* saveGltf */ false, /* saveFbx */ false, /* saveTriangulatedObj */ false,
          /* includeDisplayRotation */ false, serializer, saveSelected:false);
        autoSave.WriteAutoSave(saveData);
      } catch (Exception e) {
        Debug.LogWarning(e);
        PeltzerMain.Instance.LastAutoSaveDenied = true;
      }
      PeltzerMain.Instance.autoSave.IsCurrentlySaving = false;
    }

    public void PostWork() {
    }
  }
}
