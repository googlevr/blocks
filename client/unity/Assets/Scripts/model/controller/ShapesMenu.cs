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
using UnityEngine;

using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.tools;

namespace com.google.apps.peltzer.client.model.controller {
  /// <summary>
  ///   Delegate called when the selected shape changes.
  /// </summary>
  /// <param name="shape"></param>
  public delegate void ShapeMenuItemChangedHandler(int newShapeMenuItemId);

  /// <summary>
  ///   Shapes menu. This menu is the little tray of primitives that appears when the user is in the process of
  ///   selecting a shape to insert into the scene. It appears when the user is using the VolumeInserter tool.
  ///   This MonoBehaviour must be added to the object that represents the controller (so that the menu gets
  ///   anchored to the controller and moves around with it).
  /// </summary>
  public class ShapesMenu : MonoBehaviour {
    // The shape menu uses item IDs to identify its items. The IDs coincide with the values of Primitives.Shape
    // for the basic primitive types. In addition, we have these "special items" that are not primitives.
    // Their IDs are negative in order not to conflict with values in Primitives.Shape.
    public const int COPY_MODE_ID = -1;
    public const int CUSTOM_SHAPE_ID = -2;

    /// <summary>
    /// Defines the order in which shapes menu items appear in the menu.
    /// We do this instead of using the menu item IDs directly because we want some flexibility to reorder
    /// items if we want to, for UX purposes (say, move the "copy" item around, etc). Also, we can use this
    /// to add or remove items based on build flags. We generate this list at runtime.
    /// </summary>
    private static readonly int[] MENU_ITEMS;

    /// <summary>
    /// Reverse dictionary that maps a shapes menu item ID to the its index in SHAPES_MENU_ITEMS.
    /// </summary>
    private static readonly Dictionary<int, int> INDEX_FOR_ID = new Dictionary<int, int>();

    /// <summary>
    /// Size of the custom shape preview in the shapes menu, in world space.
    /// </summary>
    private const float SHAPES_MENU_CUSTOM_SHAPE_SIZE = 0.02f;

    private static readonly float SHAPE_MENU_ANIMATION_TIME = 0.1f;

    /// <summary>
    /// How long the shape menu remains on screen before automatically disappearing.
    /// </summary>
    private static readonly float SHAPE_MENU_SHOW_TIME = 1.5f;

    public bool showingShapeMenu { get; private set; }
    private float showShapeMenuTime = -10f; // So we hide on first pass.
    private int previousShapeMenuItemId;

    // Dictionary that stores the GameObjects that represent each menu item.
    // IMPORTANT: this is not indexed by the menu item ID! This is indexed in the order that the items
    // appear in the menu (same as ITEMS).
    public GameObject[] shapesMenu;

    public event ShapeMenuItemChangedHandler ShapeMenuItemChangedHandler;

    private int currentItemId = (int)Primitives.Shape.CUBE;
    public int CurrentItemId { get { return currentItemId; } }

    private WorldSpace worldSpace;

    /// <summary>
    /// GameObject that represents the tip of the controller (where the menu will appear).
    /// </summary>
    private GameObject wandTip;

    private MeshRepresentationCache meshRepresentationCache;


    static ShapesMenu() {
      int pos = 0;
      if (Features.stampingEnabled) {
        MENU_ITEMS = new int[Primitives.NUM_SHAPES + 2];
        // The menu starts with "copy" and "custom shape", then come the primitives.
        // If we ever want to change the order of these items in the menu, this is the place to do it.
        MENU_ITEMS[pos++] = COPY_MODE_ID;
        MENU_ITEMS[pos++] = CUSTOM_SHAPE_ID;
      } else {
        MENU_ITEMS = new int[Primitives.NUM_SHAPES];
      }
      // Add the primitives.
      foreach (Primitives.Shape shape in Enum.GetValues(typeof(Primitives.Shape))) {
        MENU_ITEMS[pos++] = (int)shape;
      }
      // Build the reverse dictionary.
      for (int i = 0; i < MENU_ITEMS.Length; i++) {
        INDEX_FOR_ID[MENU_ITEMS[i]] = i;
      }
    }

    /// <summary>
    /// Initializes this object. Must be called after this behavior is added to the GameObject.
    /// </summary>
    public void Setup(WorldSpace worldSpace, GameObject wandTip, int initialMaterial,
        MeshRepresentationCache meshRepresentationCache) {
      this.wandTip = wandTip;
      this.worldSpace = worldSpace;
      this.meshRepresentationCache = meshRepresentationCache;
      GenerateShapesMenu(initialMaterial);
    }

    private void GenerateShapesMenu(int material) {
      // We don't want the brush previews to resize with worldspace changes, so we create
      // a dummy worldspace that is set for the identity transform.
      WorldSpace identityWorldSpace = new WorldSpace(worldSpace.bounds);

      shapesMenu = new GameObject[MENU_ITEMS.Length];

      // For each item on the menu, create a GameObject to represent it.
      for (int i = 0; i < shapesMenu.Length; i++) {
        int id = MENU_ITEMS[i];
        GameObject obj;
        if (id == COPY_MODE_ID) {
          obj = MeshHelper.GameObjectFromMMesh(identityWorldSpace,
            Primitives.AxisAlignedCone(0, Vector3.zero, Vector3.one * 0.0125f, MaterialRegistry.YELLOW_ID));
        } else if (id == CUSTOM_SHAPE_ID) {
          // We initially don't set a GameObject for CUSTOM_SHAPE_ID. We will only do that once the user configures
          // a custom shape via the copy tool.
          continue;
        } else if (IsBasicPrimitiveItem(id)) {
          obj = MeshHelper.GameObjectFromMMesh(worldSpace, Primitives.BuildPrimitive((Primitives.Shape)id,
            Vector3.one * 0.0125f, Vector3.zero, 0, material));
          obj.gameObject.transform.rotation = PeltzerMain.Instance.peltzerController.LastRotationWorld;
        } else {
          throw new Exception("Invalid menu item ID " + id);
        }

        obj.name = "ShapesMenuItem " + i;
        // Set up the GameObject to show up in the menu. We will make it a child of our gameObject (the controller)
        // so that it moves around with the controller.
        obj.transform.parent = gameObject.transform;
        obj.transform.localRotation = Quaternion.identity;
        // Default shapes smaller for the menu.
        obj.transform.localScale /= 1.6f;
        MeshWithMaterialRenderer meshRenderer = obj.GetComponent<MeshWithMaterialRenderer>();
        meshRenderer.ResetTransform();
        // The menu exists in world space, not model space, so indicate that MeshWithMaterialRenderer should
        // use the object's world position to render, not a model space position.
        meshRenderer.UseGameObjectPosition = true;
        // Should ignore worldSpace rotation, so as to always give the user the same view of the menu.
        meshRenderer.IgnoreWorldRotation = true;
        // And, likewise, should ignore worldSpace scale, so as to always give the user the same view of the menu.
        meshRenderer.IgnoreWorldScale = true;
        // Make the item active or inactive as appropriate.
        obj.SetActive(IsShapeMenuItemEnabled(id));
        // And finally add it to the menu array.
        shapesMenu[i] = obj;
      }

      // Put everything in its default position.
      UpdateShapesMenu();
      Hide();
    }

    /// <summary>
    /// Returns whether or not the given shape menu item corresponds to a basic primitive type.
    /// </summary>
    /// <param name="itemId">The item.</param>
    /// <returns>True if and only if the menu item corresponds to a basic primitive.</returns>
    public bool IsBasicPrimitiveItem(int itemId) {
      return itemId >= 0 && itemId < Primitives.NUM_SHAPES;
    }

    /// <summary>
    /// Updates the material of the shapes menu.
    /// </summary>
    /// <param name="newMaterialId">ID of the new material to use.</param>
    public void ChangeShapesMenuMaterial(int newMaterialId) {
      for (int i = 0; i < shapesMenu.Count(); i++) {
        // Only update the material on the items that represent basic primitives.
        if (IsBasicPrimitiveItem(MENU_ITEMS[i])) {
          shapesMenu[i].GetComponent<MeshWithMaterialRenderer>().OverrideWithNewMaterial(newMaterialId);
        }
      }
    }

    /// <summary>
    /// Sets the custom primitive to display in the shapes menu.
    /// </summary>
    /// <param name="meshes">The meshes that constitute the custom primitive.</param>
    public void SetShapesMenuCustomShape(IEnumerable<MMesh> meshes) {
      AssertOrThrow.True(meshes.Count() > 0, "Can't set a custom shape with an empty list of meshes.");
      // First destroy the previous object hierarchy, if any.
      if (null != shapesMenu[INDEX_FOR_ID[CUSTOM_SHAPE_ID]]) {
        GameObject.DestroyImmediate(shapesMenu[INDEX_FOR_ID[CUSTOM_SHAPE_ID]]);
      }
      // Now we generate the preview for the custom shapes.
      GameObject preview = GenerateCustomShapePreview(meshes);
      // The preview starts out inactive. We will activate it later when the menu gets shown.
      preview.SetActive(false);
      shapesMenu[INDEX_FOR_ID[CUSTOM_SHAPE_ID]] = preview;
      UpdateShapesMenu();
    }

    /// <summary>
    /// Generates previews for the given meshes, configured to be displayed in the shapes menu.
    /// </summary>
    /// <param name="meshes">The meshes to use for the preview</param>
    /// <returns>A GameObject hierarchy that contains all the previews that represent the given list of meshes.
    /// These will be set up such that their local positions represent the positions of the mesh, scaled such
    /// that the whole set of previews doesn't exceed the maximum size that would fit in the menu.
    /// The returned GameObject will be parented to this behavior's GameObject.</returns>
    private GameObject GenerateCustomShapePreview(IEnumerable<MMesh> meshes) {
      // We will use a "specially constructed" (a.k.a. "hacky") worldspace to ensure that the custom shape preview
      // has the right size for the menu.
      IEnumerator<MMesh> enumerator = meshes.GetEnumerator();
      AssertOrThrow.True(enumerator.MoveNext(), "Can't generate custom shape preview with no meshes.");
      WorldSpace customWorldSpace = new WorldSpace(worldSpace.bounds);
      GameObject preview = new GameObject();
      Bounds bounds = enumerator.Current.bounds;
      do {
        bounds.Encapsulate(enumerator.Current.bounds);
      } while (enumerator.MoveNext());

      // Now we need to scale this preview such that it's the right size for the menu,
      // instead of using its natural size.
      // Let's figure out how big the bounding box is in world space.
      //
      // Note: in the future, we should figure out if/how rotation matters here. We're ignoring it for now:
      float maxSideInWorldSpace =
        Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * worldSpace.scale;

      // We want the bounding box's size to be about SHAPE_MENU_CUSTOM_SHAPE_SIZE, so let's calculate the scale
      // factor that makes this true.
      // If the size is too small (< 0.001f) we don't attempt to rescale, to avoid the risk of insanity when
      // dividing by something that close to zero.
      float scaleFactor = (maxSideInWorldSpace > 0.001f) ? SHAPES_MENU_CUSTOM_SHAPE_SIZE / maxSideInWorldSpace : 1.0f;
      customWorldSpace.scale = scaleFactor;

      // Now that we know the scale factor and how much to translate each preview, let's go through them and set
      // them up.
      foreach (MMesh mesh in meshes) {
        GameObject thisPreview = meshRepresentationCache.GeneratePreview(mesh);
        MeshWithMaterialRenderer renderer = thisPreview.GetComponent<MeshWithMaterialRenderer>();
        renderer.worldSpace = customWorldSpace;
        thisPreview.transform.SetParent(preview.transform, /* worldPositionStays */ true);
        thisPreview.transform.localRotation = Quaternion.identity;
        // Position this preview such that its local position is the offset to the bounding box's center
        // and scaled such that it fits in the menu.
        thisPreview.transform.localPosition = (mesh.offset - bounds.center) * scaleFactor;
      }

      // The preview should be parented to our gameObject (the controller) so that the preview follows
      // the controller (as it's part of the shapes menu).
      // We pass worldPositionStays=false because we want the object to be repositioned such that it
      // lies its correct position in the new parent.
      preview.transform.SetParent(gameObject.transform, /* worldPositionStays */ false);

      return preview;
    }

    private void Update() {
      if (showingShapeMenu) {
        if (Time.time > (showShapeMenuTime + SHAPE_MENU_SHOW_TIME)) {
          // Done showing menu, hide everything.
          Hide();
        } else if (Time.time < (showShapeMenuTime + SHAPE_MENU_ANIMATION_TIME)) {
          UpdateShapesMenu();
          for (int i = 0; i < shapesMenu.Count(); i++) {
            int id = MENU_ITEMS[i];
            if (shapesMenu[i] != null) {
              shapesMenu[i].SetActive(IsShapeMenuItemEnabled(id) && id != currentItemId);
            }
          }
        }
      }

    }

    public void SetShapeMenuItem(int newItemId, bool showMenu) {
      if (newItemId == currentItemId) return;

      // Lerp the previously-selected shape's scale down from what was showing in Volume Inserter.
      if (!showingShapeMenu) {
        VolumeInserter volumeInserter = PeltzerMain.Instance.GetVolumeInserter();
        float oldScale = volumeInserter.GetScaleForScaleDelta(volumeInserter.scaleDelta);
        float newScale = volumeInserter.GetScaleForScaleDelta(0) / worldSpace.scale;
        float scaleDiff = oldScale / newScale;
        shapesMenu[INDEX_FOR_ID[currentItemId]].GetComponent<MeshWithMaterialRenderer>().AnimateScaleFrom(scaleDiff);
      }

      currentItemId = newItemId;

      if (showMenu) {
        showingShapeMenu = true;
        showShapeMenuTime = Time.time;
      } else {
        showingShapeMenu = false;
      }

      UpdateShapesMenu();

      // Notify listeners.
      if (ShapeMenuItemChangedHandler != null) {
        ShapeMenuItemChangedHandler(newItemId);
      }
    }

    /// <summary>
    /// Returns whether the given shape menu item is enabled. Enabled items show in the menu and are
    /// selectable.
    /// </summary>
    /// <param name="itemId">The item to check.</param>
    /// <returns>True if the item is enabled, false if not.</returns>
    public bool IsShapeMenuItemEnabled(int itemId) {
      // If stamping is not enabled, then the only items enabled are the basic primitives.
      if (IsBasicPrimitiveItem(itemId)) {
        // Basic primitives are always enabled.
        return true;
      } else if (Features.stampingEnabled && itemId == COPY_MODE_ID) {
        // Copy mode is always enabled (if stamping is enabled in the build).
        return true;
      } else if (Features.stampingEnabled && itemId == CUSTOM_SHAPE_ID) {
        // The "custom shape" mode is enabled if there is a custom shape.
        return HasCustomShape();
      }
      return false;
    }

    private bool HasCustomShape() {
      return null != shapesMenu[INDEX_FOR_ID[CUSTOM_SHAPE_ID]];
    }

    /// <summary>
    /// Selects the next enabled item in the shapes menu.
    /// </summary>
    /// <returns>True if successful, false on failure (there are no more enabled items in that direction).</returns>
    public bool SelectNextShapesMenuItem() {
      int index = INDEX_FOR_ID[currentItemId];
      do {
        index++;
        if (index >= shapesMenu.Length) {
          showingShapeMenu = true;
          showShapeMenuTime = Time.time;
          return false;
        }
      } while (!IsShapeMenuItemEnabled(MENU_ITEMS[index]));
      SetShapeMenuItem(MENU_ITEMS[index], /* showMenu */ true);
      return true;
    }

    /// <summary>
    /// Selects the previous enabled item in the shapes menu.
    /// </summary>
    /// <returns>True if successful, false on failure (there are no more enabled items in that direction).</returns>
    public bool SelectPreviousShapesMenuItem() {
      int index = INDEX_FOR_ID[currentItemId];
      do {
        index--;
        if (index < 0) {
          showingShapeMenu = true;
          showShapeMenuTime = Time.time;
          return false;
        }
      } while (!IsShapeMenuItemEnabled(MENU_ITEMS[index]));
      SetShapeMenuItem(MENU_ITEMS[index], /* showMenu */ true);
      return true;
    }

    /// <summary>
    /// Returns the order of the given menu item index amongst ENABLED menu items.
    /// For example, if items 0 and 1 are disabled, then item 2 would have and order of 0,
    /// item 3 would have an order of 1, etc.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>The order of the item amongst the enabled menu items.</returns>
    private int GetOrderAmongEnabledItems(int index) {
      int order = 0;
      // The index amongst ENABLED items is simply the count of how many enabled items
      // exist before it.
      for (int i = 0; i < index; i++) {
        if (IsShapeMenuItemEnabled(MENU_ITEMS[i])) order++;
      }
      return order;
    }

    /// <summary>
    /// Updates the shapes menu (to be called once per frame).
    /// </summary>
    private void UpdateShapesMenu() {
      // Show the menu around the brush primitive.
      int curOrder = GetOrderAmongEnabledItems(INDEX_FOR_ID[currentItemId]);
      for (int i = 0; i < shapesMenu.Length; i++) {
        if (shapesMenu[i] == null) continue;
        int thisOrder = GetOrderAmongEnabledItems(i);
        float xOff = (thisOrder - curOrder) * 0.06f;
        if (Config.Instance.VrHardware == VrHardware.Vive) {
          shapesMenu[i].transform.localPosition = wandTip.transform.localPosition + new Vector3(xOff, 0, 0.1f);
        } else {
          if (Config.Instance.sdkMode == SdkMode.SteamVR) {
            shapesMenu[i].transform.localPosition = wandTip.transform.localPosition + new Vector3(xOff, -.075f, 0);
          } else {
            shapesMenu[i].transform.localPosition = wandTip.transform.localPosition + new Vector3(xOff, 0, 0.1f);
          }
        }
        shapesMenu[i].transform.rotation = PeltzerMain.Instance.peltzerController.LastRotationWorld;
      }
    }

    /// <summary>
    ///   Hides the shapes menu, if it is currently showing.
    /// </summary>
    public void Hide() {
      showingShapeMenu = false;
      for (int i = 0; i < shapesMenu.Length; i++) {
        if (shapesMenu[i] != null) {
          shapesMenu[i].SetActive(false);
        }
      }
    }
  }
}
