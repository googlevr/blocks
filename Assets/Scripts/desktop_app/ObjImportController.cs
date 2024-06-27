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
using System.IO;
using System.Text;
// using System.Windows.Forms;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.export;

namespace com.google.apps.peltzer.client.desktop_app
{
    /// <summary>
    ///   Responsible for handling the button-click to import obj files from the desktop app, and 
    ///   loading them into the model.
    /// </summary>
    public class ObjImportController : MonoBehaviour
    {
        /// <summary>
        /// When an OBJ file is imported, this indicates the minimum distance at which it will appear in front of
        /// the user, to ensure the user is not too close to (or inside!) of the imported geometry.
        /// </summary>
        private const float MIN_IMPORTED_OBJ_DISTANCE_FROM_USER = 2.0f;

        /// <summary>
        ///   Handles the button-click to import an obj. Opens up a dialog and in the background, waits for the
        ///   user to hit 'ok' with two files selected.
        /// </summary>
        public void SelectObjToImport()
        {
            Model model = PeltzerMain.Instance.GetModel();
            BackgroundWork openDialog = new OpenFileDialogAndLoadObj(model);
            PeltzerMain.Instance.DoPolyMenuBackgroundWork(openDialog);
        }

        /// <summary>
        ///   Reads an entire file into a string.
        ///   Faster than File.ReadAllLines(): http://cc.davelozinski.com/c-sharp/fastest-way-to-read-text-files
        /// </summary>
        /// <param name="filename">The file to read.</param>
        /// <returns>The file as a string, with newlines preserved.</returns>
        private static string FileToString(string filename)
        {
            StringBuilder stringBuilder = new StringBuilder();
            using (FileStream fileStream = File.Open(filename, FileMode.Open))
            using (BufferedStream bufferedStream = new BufferedStream(fileStream))
            using (StreamReader streamReader = new StreamReader(bufferedStream))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    stringBuilder.AppendLine(line);
                }
                return stringBuilder.ToString();
            }
        }

        class OpenFileDialogAndLoadObj : BackgroundWork
        {
            // A reference to the model.
            private readonly Model model;
            // File contents to be passed from a background thread to a foreground thread.
            string mtlFileContents;
            string objFileContents;
            PeltzerFile peltzerFile;

            public OpenFileDialogAndLoadObj(Model model)
            {
                this.model = model;
            }

            // In the background we perform all the File I/O to get file contents. There are no graceful failures here,
            // and there is no feedback to the user in case of failure.
            public void BackgroundWork()
            {
                // OpenFileDialog dialog = new OpenFileDialog();
                // dialog.Multiselect = true;
                // Expect that the user selected two files, one .obj and one .mtl
                // if (dialog.ShowDialog() == DialogResult.OK) {
                //   if (dialog.FileNames.Length == 1) {
                //     if (dialog.FileNames[0].EndsWith(".peltzer") || dialog.FileNames[0].EndsWith(".poly")
                //        || dialog.FileNames[0].EndsWith(".blocks")) {
                //       byte[] peltzerFileBytes = File.ReadAllBytes(dialog.FileNames[0]);
                //       PeltzerFileHandler.PeltzerFileFromBytes(peltzerFileBytes, out peltzerFile);
                //     } else if (dialog.FileNames[0].EndsWith(".obj")) {
                //       objFileContents = FileToString(dialog.FileNames[0]);
                //     } else {
                //       Debug.Log("When selecting only one file for OBJ import, it must have a .obj extension");
                //     }
                //   } else if (dialog.FileNames.Length == 2) {
                //     string objFile = dialog.FileNames[0].EndsWith(".obj") ? dialog.FileNames[0] : dialog.FileNames[1];
                //     string mtlFile = dialog.FileNames[0].EndsWith(".mtl") ? dialog.FileNames[0] : dialog.FileNames[1];
                //     if (!objFile.EndsWith(".obj") || !mtlFile.EndsWith(".mtl")) {
                //       Debug.Log("When selecting two files for OBJ import, one must be .obj and the other .mtl");
                //     }

                //     objFileContents = FileToString(objFile);
                //     mtlFileContents = FileToString(mtlFile);
                //   } else {
                //     Debug.Log("Exactly one .obj file or a pair of .obj and .mtl files must be selected for OBJ import");
                //   }
                // }
            }

            // In the foreground we add the mesh to the model.
            public void PostWork()
            {
                if (peltzerFile != null)
                {
                    foreach (MMesh mesh in peltzerFile.meshes)
                    {
                        mesh.ChangeId(model.GenerateMeshId());
                        AssertOrThrow.True(model.AddMesh(mesh), "Attempted to load an invalid mesh");
                    }
                }
                else
                {
                    Vector3 headInModelSpace = PeltzerMain.Instance.worldSpace.WorldToModel(
                        PeltzerMain.Instance.hmd.transform.position);
                    Vector3 headForward = PeltzerMain.Instance.hmd.transform.forward;
                    // Request that the OBJ file's geometry be positioned reasonably, so that it appears in front
                    // of the user at the given minimum distance.
                    model.AddMeshFromObjAndMtl(objFileContents, mtlFileContents, headInModelSpace, headForward,
                        MIN_IMPORTED_OBJ_DISTANCE_FROM_USER);
                }
            }
        }
    }
}
