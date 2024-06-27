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

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using System.Collections.Generic;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   This class deals with the palette icon that shows for the Volume Inserter option.
    /// </summary>
    public class ShapeToolheadAnimation : MonoBehaviour
    {
        // The available shapes.
        private Dictionary<Primitives.Shape, GameObject> mockShapes = new Dictionary<Primitives.Shape, GameObject>();

        void Start()
        {
            mockShapes[Primitives.Shape.CONE] = ObjectFinder.ObjectById("ID_Cone");
            mockShapes[Primitives.Shape.CUBE] = ObjectFinder.ObjectById("ID_Cube");
            mockShapes[Primitives.Shape.CYLINDER] = ObjectFinder.ObjectById("ID_Cylinder");
            mockShapes[Primitives.Shape.TORUS] = ObjectFinder.ObjectById("ID_Torus");
            mockShapes[Primitives.Shape.SPHERE] = ObjectFinder.ObjectById("ID_Sphere");
            mockShapes[Primitives.Shape.ICOSAHEDRON] = ObjectFinder.ObjectById("ID_Icosahedron");
        }

        /// <summary>
        ///   Handler for shape changed event from the shape menu.
        /// </summary>
        /// <param name="shapeMenuItemId"></param>
        public void ShapeChangedHandler(int shapeMenuItemId)
        {
            foreach (KeyValuePair<Primitives.Shape, GameObject> mockShape in mockShapes)
            {
                if (shapeMenuItemId == (int)mockShape.Key)
                {
                    mockShape.Value.SetActive(true);
                }
                else
                {
                    mockShape.Value.SetActive(false);
                }
            }
        }
    }
}
