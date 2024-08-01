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

#define STEAMVRBUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

using com.google.apps.peltzer.client.desktop_app;
using com.google.apps.peltzer.client.menu;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools;
using com.google.apps.peltzer.client.tools.utils;
using com.google.apps.peltzer.client.tutorial;
using com.google.apps.peltzer.client.zandria;
using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.api_clients.objectstore_client;
using com.google.apps.peltzer.client.serialization;
using com.google.apps.peltzer.client.entitlement;
using com.google.apps.peltzer.client.api_clients.assets_service_client;

namespace com.google.apps.peltzer.client.model.main
{
    public enum MenuAction
    {
        SAVE, LOAD, SHOWCASE, TAKE_PHOTO, SHARE, CLEAR, BLOCKMODE, NOTHING,
        SHOW_SAVE_CONFIRM, CANCEL_SAVE, NEW_WITH_SAVE, SIGN_IN, SIGN_OUT, ADD_REFERENCE,
        TOGGLE_SOUND, TOGGLE_PERMISSIONS, SAVE_COPY, PUBLISH, PUBLISHED_TAKE_OFF_HEADSET_DISMISS,
        TUTORIAL_START, TUTORIAL_DISMISS, TUTORIAL_PROMPT, TUTORIAL_CONFIRM_DISMISS,
        TUTORIAL_SAVE_AND_CONFIRM, TUTORIAL_DONT_SAVE_AND_CONFIRM, PUBLISH_AFTER_SAVE_DISMISS,
        PUBLISH_SIGN_IN_DISMISS, PUBLISH_AFTER_SAVE_CONFIRM, TUTORIAL_EXIT_YES, TUTORIAL_EXIT_NO,
        SAVE_LOCALLY, SAVE_LOCAL_SIGN_IN_INSTEAD, TOGGLE_LEFT_HANDED, TOGGLE_TOOLTIPS, PLAY_VIDEO,
        SAVE_SELECTED, TOGGLE_FEATURE, TOGGLE_EXPAND_WIREFRAME_FEATURE,
    }

    public enum Handedness { NONE, LEFT, RIGHT }

    /// <summary>
    ///   BackgroundWork for serializing a model into bytes (for saving).
    ///   This does not handle the actual saving, it just serializes in preparation for saving.
    /// </summary>
    public class SerializeWork : BackgroundWork
    {
        private readonly Model model;
        private ICollection<MMesh> meshes;
        private SaveData saveData;
        private byte[] thumbnailBytes;
        private Action<SaveData> callback;
        private PolySerializer serializer;
        private bool saveSelected;


        /// <summary>
        /// Serializes the model into bytes in the background.
        /// </summary>
        /// <param name="model">The model to serialize.</param>
        /// <param name="meshes">The meshes to serialize.</param>
        public SerializeWork(Model model, ICollection<MMesh> meshes,
            byte[] thumbnailBytes, Action<SaveData> callback, PolySerializer serializer, bool saveSelected)
        {
            this.model = model;
            this.meshes = meshes;
            this.thumbnailBytes = thumbnailBytes;
            this.callback = callback;
            this.serializer = serializer;
            this.saveSelected = saveSelected;
        }

        public void BackgroundWork()
        {
            // Need to make sure all meshes are back in remesher for coalesced gltf export.
            PeltzerMain.Instance.GetSelector().DeselectAll();
            saveData = ExportUtils.SerializeModel(model, meshes,
              /* saveGltf */ true, /* saveFbx */ true, /* saveTriangulatedObj */ true,
              /* includeDisplayRotation */ true, serializer, saveSelected);
            saveData.thumbnailBytes = thumbnailBytes;
        }

        public void PostWork()
        {
            // Callback to functions waiting for serialization to finish.
            callback(saveData);
        }
    }

    internal class SaveToDiskWork : BackgroundWork
    {
        private SaveData saveData;
        private string directory;
        private bool isOfflineModelsFolder;
        private bool isOverwrite;
        private bool success;

        public SaveToDiskWork(SaveData saveData, string directory, bool isOfflineModelsFolder, bool isOverwrite)
        {
            this.saveData = saveData;
            this.directory = directory;
            this.isOfflineModelsFolder = isOfflineModelsFolder;
            this.isOverwrite = isOverwrite;
        }

        public void BackgroundWork()
        {
            success = ExportUtils.SaveLocally(saveData, directory);
        }

        public void PostWork()
        {
            if (isOfflineModelsFolder)
            {
                if (success)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(directory);
                    PeltzerMain.Instance.HandleSaveComplete(true, "Saved locally");
                    if (isOverwrite)
                    {
                        PeltzerMain.Instance.UpdateLocalModelOntoPolyMenu(directoryInfo);
                    }
                    else
                    {
                        PeltzerMain.Instance.LoadLocallySavedModelOntoPolyMenu(directoryInfo);
                    }
                }
                else
                {
                    PeltzerMain.Instance.HandleSaveComplete(false, "Save failed");
                }
            }
        }
    }


    /// <summary>
    ///   Add a mesh to the spatial index in the background.
    /// </summary>
    internal class AddToIndex : BackgroundWork
    {
        private SpatialIndex spatialIndex;
        private MMesh mmesh;

        internal AddToIndex(SpatialIndex spatialIndex, MMesh mmesh)
        {
            this.spatialIndex = spatialIndex;
            this.mmesh = mmesh;
        }

        public void BackgroundWork()
        {
            spatialIndex.AddMesh(mmesh);
        }

        public void PostWork()
        {
        }
    }

    /// <summary>
    ///   Update a mesh in the spatial index in the background.
    /// </summary>
    internal class UpdateInIndex : BackgroundWork
    {
        private SpatialIndex spatialIndex;
        private MMesh mmesh;

        internal UpdateInIndex(SpatialIndex spatialIndex, MMesh mmesh)
        {
            this.spatialIndex = spatialIndex;
            this.mmesh = mmesh;
        }

        public void BackgroundWork()
        {
            spatialIndex.RemoveMesh(mmesh);
            spatialIndex.AddMesh(mmesh);
        }

        public void PostWork()
        {
        }
    }

    /// <summary>
    ///   Remove a mesh from the spatial index in the background.
    /// </summary>
    internal class DeleteFromIndex : BackgroundWork
    {
        private SpatialIndex spatialIndex;
        private MMesh mmesh;

        internal DeleteFromIndex(SpatialIndex spatialIndex, MMesh mmesh)
        {
            this.spatialIndex = spatialIndex;
            this.mmesh = mmesh;
        }

        public void BackgroundWork()
        {
            spatialIndex.RemoveMesh(mmesh);
        }

        public void PostWork()
        {
        }
    }

    /// <summary>
    ///   Main controller for the Peltzer app. This is a singleton.
    ///    * Holds and renders the Model.
    ///    * Controls all background work and background thread.
    ///
    ///   This is a singleton and guaranteed to be available at any time (the GameObject with this behavior
    ///   is statically part of MainScene). To obtain an instance, use the static property PeltzerMain.Instance.
    /// </summary>
    public class PeltzerMain : MonoBehaviour
    {
        /// <summary>
        /// Key for the player preferences to determine if a user has started Blocks before.
        /// </summary>
        private static string FIRST_TIME_KEY = "blocks_first_time";
        /// <summary>
        /// Key for the player preferences to determine if a user is left-handed.
        /// </summary>
        public static string LEFT_HANDED_KEY = "blocks_left_handed";
        /// <summary>
        /// Key for the player preferences to determine if a user has disabled tooltips.
        /// </summary>
        public static string DISABLE_TOOLTIPS_KEY = "blocks_disable_tooltips";
        /// <summary>
        /// Key for the player preferences to determine if a user has disabled sounds.
        /// </summary>
        public static string DISABLE_SOUNDS_KEY = "blocks_disable_sounds";
        /// <summary>
        /// Key for the player preferences to determine if a user has revoked analytics permissions.
        /// </summary>
        public static string DISABLE_ANALYTICS_KEY = "blocks_disable_analytics";
        /// <summary>
        /// Key for the player preferences to determine which environment theme to present.
        /// </summary>
        public static string ENVIRONMENT_THEME_KEY = "blocks_environment_theme";

        /// <summary>
        /// Message displayed to the user when saving the model.
        /// </summary>
        private const string SAVE_MESSAGE = "Saving...";

        // The default workspace, a room of 10x10x10 metres.
        public static readonly Bounds DEFAULT_BOUNDS = new Bounds(Vector3.zero, new Vector3(10f, 10f, 10f));

        // Menu actions that are selectable while a tutorial is occurring.
        public static readonly List<MenuAction> TUTORIAL_MENU_ACTIONS =
          new List<MenuAction> {MenuAction.TUTORIAL_EXIT_NO, MenuAction.TUTORIAL_EXIT_YES, MenuAction.TUTORIAL_DISMISS,
        MenuAction.TUTORIAL_PROMPT};

        /// <summary>
        /// How far the controller trigger must be pressed to be considered to be "down".
        /// This is a number from 0 to 1. If it's closer to 0 it means the user has to press the trigger
        /// only slightly to activate it; if it's closer to 1, it means the user must push it all the way
        /// in order to activate it.
        ///
        /// Note: we're setting a high value because we want RELEASING the trigger to be fast, because when
        /// the user releases the trigger, their hand is exactly where they want, and will often slip from the
        /// correct position during the course of releasing the trigger.
        /// </summary>
        public static float TRIGGER_THRESHOLD = 0.95f;

        /// <summary>
        /// Initial size of serializer buffers.
        /// This must be reasonably big in order to avoid reallocation when saving models.
        /// </summary>
        private const int SERIALIZER_BUFFER_INITIAL_SIZE = 128 * 1024 * 1024;  // 128 MB

        /// <summary>
        /// The (singleton) instance. Lazily cached when the Instance property is read for the first time.
        /// </summary>
        private static PeltzerMain instance;

        /// <summary>
        /// Returns the singleton instance of PeltzerMain.
        /// WARNING: This object performs late initialization of certain subsystems through the TrySetup() method, which
        /// runs some time *after* the object is initialized. So even though PeltzerMain.Instance will always return
        /// a valid reference to the singleton PeltzerMain object, it is not guaranteed to be initialized. Be aware
        /// of that when using PeltzerMain.Instance.
        /// </summary>
        public static PeltzerMain Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = GameObject.FindObjectOfType<PeltzerMain>();
                    AssertOrThrow.NotNull(instance, "No PeltzerMain object found in scene!");
                }
                return instance;
            }
        }

        /// <summary>
        ///   Distance threshold in metric units for helping determine a handedness switch / resolution.
        ///   In Unity units where 1.0 = 1 metre.
        /// </summary>
        public static readonly float HANDEDNESS_DISTANCE_THRESHOLD = 0.44f;

        // Set from within Unity.
        public PeltzerController peltzerController;
        public PaletteController paletteController;
        public GameObject hmd;
        [SerializeField]
        private GameObject controllerGeometryLeftRiftPrefab;
        [SerializeField]
        private GameObject controllerGeometryRightRiftPrefab;
        [SerializeField]
        private GameObject controllerGeometryVivePrefab;

        private bool running = true;
        private SpatialIndex spatialIndex;
        public AudioLibrary audioLibrary { get; private set; }
        private MaterialLibrary materialLibrary;
        private Thread generalBackgroundThread;
        private Thread polyMenuBackgroundThread;
        private Thread filePickerBackgroundThread;
        private ConcurrentQueue<BackgroundWork> generalBackgroundQueue = new ConcurrentQueue<BackgroundWork>();
        private ConcurrentQueue<BackgroundWork> polyMenuBackgroundQueue = new ConcurrentQueue<BackgroundWork>();
        private ConcurrentQueue<BackgroundWork> filePickerBackgroundQueue = new ConcurrentQueue<BackgroundWork>();
        private ConcurrentQueue<BackgroundWork> forMainThread = new ConcurrentQueue<BackgroundWork>();
        public AutoThumbnailCamera autoThumbnailCamera;
        public Camera eyeCamera;
        public Vector3 eyeCameraPosition;

        /// <summary>
        ///   Whether the peltzer controller is in the right hand, which is the default state.
        /// </summary>
        public bool peltzerControllerInRightHand = true;

        // Controller.
        public ControllerMain controllerMain { get; private set; }

        // Tools.
        private Reshaper reshaper;
        private Freeform freeform;
        private VolumeInserter volumeInserter;
        private Extruder extruder;
        private Selector selector;
        private Subdivider subdivider;
        private Deleter deleter;
        private Mover mover;
        private Painter painter;
        private GifRecorder gifRecorder;
        private Zoomer zoomer;

        // Creations Handler.
        private ZandriaCreationsManager zandriaCreationsManager;

        // Environment Handler.
        public EnvironmentThemeManager environmentThemeManager;

        // Model.
        public Model model { private set; get; }
        private Exporter exporter;
        public WorldSpace worldSpace { get; private set; }
        private GridHighlightComponent gridHighlighter;

        // The ID of the current model for local saves.
        public string LocalId;
        // The ID of the current model for cloud saves.
        public string AssetId;
        // The Asset ID of the model that was most-recently saved to Zandria.
        public string LastSavedAssetId;

        // Saving
        public AutoSave autoSave { get; private set; }
        public bool ModelChangedSinceLastSave;
        // Whether the last auto-save request was denied.
        public bool LastAutoSaveDenied;
        // A path to the user's Poly data folder.
        public string userPath { get; private set; }
        // A path to the user's Poly models data folder.
        public string modelsPath { get; private set; }
        // A path to a special offline cache of models the user saved whilst not authenticated.
        public string offlineModelsPath { get; private set; }

        // Track this user's app-level settings.
        public bool HasEverStartedPoly { get; private set; }
        public bool HasEverChangedColor { get; set; }
        public bool HasEverShownFeaturedTooltip { get; set; }
        public bool HasDisabledTooltips { get; set; }

        // Track this user's session-level settings.
        // Has this user opened the save url before?
        public bool HasOpenedSaveUrlThisSession { get; set; }
        // Whether we've shown "click the menu button to view your models" before.
        public bool HasShownMenuTooltipThisSession;

        /// <summary>
        /// Indicates whether the one-time setup is complete.
        /// We try it from Start() and retry it from Update() until we succeed.
        /// </summary>
        private bool setupDone;
        private DesktopMain desktopMain;

        public PolyMenuMain polyMenuMain;

        private FloatingMessage floatingMessage;

        private PreviewController previewController;

        /// <summary>
        /// Restriction manager, which indicates which modes/features are allowed or disallowed.
        /// Used to implement special restricted experiences such as tutorials, mini-games, etc.
        /// </summary>
        public RestrictionManager restrictionManager { get; private set; }

        /// <summary>
        /// Tutorial manager, which directs the execution of tutorials.
        /// </summary>
        public TutorialManager tutorialManager { get; private set; }

        /// <summary>
        /// Attention caller, responsible for calling the user's attention to parts of the UI.
        /// </summary>
        public AttentionCaller attentionCaller { get; private set; }

        public TooltipManager applicationButtonToolTips { get; private set; }

        /// <summary>
        /// Progress indicator. Responsible for showing the indicator that appears on the left controller
        /// to inform the user that some long operation is in progress.
        /// </summary>
        public ProgressIndicator progressIndicator { get; private set; }

        /// <summary>
        /// Action to execute after a successful save.  Currently only used to ensure that the model actually saves when
        /// NEW_WITH_SAVE is executed from the menu.
        /// </summary>
        public Action saveCompleteAction;

        /// <summary>
        /// Controller swap gesture detector.
        /// </summary>
        private ControllerSwapDetector controllerSwapDetector;

        /// <summary>
        /// Creates and animates a preview of the model that a user just saved.
        /// </summary>
        public SavePreview savePreview;

        /// <summary>
        /// Creates and animates a hint for the menu button.
        /// </summary>
        public MenuHint menuHint;

        /// <summary>
        /// Manages reference images.
        /// </summary>
        public ReferenceImageManager referenceImageManager { get; private set; }

        /// <summary>
        /// Takes care of choreographing the startup sequence.
        /// </summary>
        public IntroChoreographer introChoreographer { get; private set; }

        /// <summary>
        /// Restriction manager, which indicates which modes/features are allowed or disallowed.
        /// Used to implement special restricted experiences such as tutorials, mini-games, etc.
        /// </summary>
        public HighlightUtils highlightUtils { get; private set; }

        /// <summary>
        /// Poly Worldspace bounding box reference.
        /// </summary>
        public PolyWorldBounds polyWorldBounds;

        // Variables to determine when to trigger snap tooltips.
        // TODO: Move all tooltip logic to a separate manager to avoid polluting PeltzerMain.
        public int volumesInserted;
        public bool snappedInVolumeInserter;
        public int movesCompleted;
        public bool snappedInMover;
        public int faceReshapesCompleted;
        public bool snappedWhenReshapingFaces;
        public int extrudesCompleted;
        public bool snappedInExtruder;
        public int subdividesCompleted;
        public bool snappedInSubdivider;

        // An ugly hack for bug -- we let the assets service client directly update the modelId in PeltzerMain
        // once save has completed (such that we can overwrite), but this can be problematic if a user has chosen to
        // start a new model since first hitting save.
        public bool newModelSinceLastSaved;

        // Serializer we use when saving models. This is for "manual" save (invoked by the user), as opposed to auto save.
        private PolySerializer serializerForManualSave = new PolySerializer();

        // Serializer we use for auto-save (must be separate from serializerForManualSave because autosave happens on
        // the background thread).
        private PolySerializer serializerForAutoSave = new PolySerializer();

        /// <summary>
        /// Web request manager, which centralizes the logic of issuing and waiting for web requests.
        /// </summary>
        public WebRequestManager webRequestManager { get; private set; }

        public PeltzerMain()
        {
            // Working space is a 6m cube centered at the origin.
            worldSpace = new WorldSpace(DEFAULT_BOUNDS);
        }

        void Start()
        {
            // Check the user's app-level settings in the registry, if any.
            if (PlayerPrefs.HasKey(FIRST_TIME_KEY))
            {
                HasEverStartedPoly = true;
            }
            else
            {
                HasEverStartedPoly = false;
                PlayerPrefs.SetString(FIRST_TIME_KEY, "true");
                PlayerPrefs.Save();
            }

            // Initializes static buffers we're using for optimizing setting of list values.
            ReMesher.InitBufferCaches();

            // Set up the authentication.
            gameObject.AddComponent<OAuth2Identity>();

            // Create and set up the web request manager.
            webRequestManager = gameObject.AddComponent<WebRequestManager>();
            webRequestManager.Setup(new WebRequestManager.WebRequestManagerConfig(AssetsServiceClient.POLY_KEY));

            // Add Oculus SDK stuff.
            if (Config.Instance.sdkMode == SdkMode.Oculus)
            {
                OVRManager manager = gameObject.AddComponent<OVRManager>();
                manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                OculusAuth oculusAuth = gameObject.AddComponent<OculusAuth>();
            }

            // Add Vive hardware stuff.
            if (Config.Instance.VrHardware == VrHardware.Rift)
            {
                // Create the left controller geometry for the palette controller.
                GameObject controllerGeometryLeft;
                if (Config.Instance.sdkMode == SdkMode.Oculus)
                {
                    controllerGeometryLeft = Instantiate<GameObject>(controllerGeometryLeftRiftPrefab, paletteController.oculusRiftHolder.transform, false);
                }
                else
                {
                    controllerGeometryLeft = Instantiate<GameObject>(controllerGeometryLeftRiftPrefab, paletteController.steamRiftHolder.transform, false);
                }
                paletteController.controllerGeometry = controllerGeometryLeft.GetComponent<ControllerGeometry>();

                // Create the right controller geometry for the peltzer controller.
                GameObject controllerGeometryRight;
                if (Config.Instance.sdkMode == SdkMode.Oculus)
                {
                    controllerGeometryRight = Instantiate<GameObject>(controllerGeometryRightRiftPrefab, peltzerController.oculusRiftHolder.transform, false);
                }
                else
                {
                    controllerGeometryRight = Instantiate<GameObject>(controllerGeometryRightRiftPrefab, peltzerController.steamRiftHolder.transform, false);
                }
                peltzerController.controllerGeometry = controllerGeometryRight.GetComponent<ControllerGeometry>();

                // Only allow hand toggling from the menu if the user is using a Rift.
                ObjectFinder.ObjectById("ID_toggle_left_handed").SetActive(true);
                ObjectFinder.ObjectById("ID_small_menu_div").SetActive(true);
                ObjectFinder.ObjectById("ID_large_menu_div").SetActive(false);
            }
            else
            {
                // Create the left controller geometry for the palette controller.
                var controllerGeometryLeft = Instantiate<GameObject>(controllerGeometryVivePrefab, paletteController.transform, false);
                paletteController.controllerGeometry = controllerGeometryLeft.GetComponent<ControllerGeometry>();

                // Create the right controller geometry for the peltzer controller.
                var controllerGeometryRight = Instantiate<GameObject>(controllerGeometryVivePrefab, peltzerController.transform, false);
                peltzerController.controllerGeometry = controllerGeometryRight.GetComponent<ControllerGeometry>();

                // Don't allow hand toggling if the user is using a Vive.
                ObjectFinder.ObjectById("ID_toggle_left_handed").SetActive(false);
                ObjectFinder.ObjectById("ID_small_menu_div").SetActive(false);
                ObjectFinder.ObjectById("ID_large_menu_div").SetActive(true);
            }

            HashSet<ControllerTooltip> tips = new HashSet<ControllerTooltip>();
            tips.Add(new ControllerTooltip("ViewSaved", "View saved models", .044f));
            tips.Add(new ControllerTooltip("ViewFeatured", "View featured models", .052f));

            applicationButtonToolTips = new TooltipManager(tips,
              paletteController.controllerGeometry.applicationButtonTooltipRoot,
              paletteController.controllerGeometry.applicationButtonTooltipLeft,
              paletteController.controllerGeometry.applicationButtonTooltipRight);

            // Get the MaterialLibrary
            materialLibrary = FindObjectOfType<MaterialLibrary>();

            // Init Materials
            MaterialRegistry.init(materialLibrary);

            // Pass the highlight material to the MeshHelper.
            MeshHelper.highlightSilhouetteMaterial = MaterialRegistry.getHighlightSilhouetteMaterial();

            // Pre-allocate the serializer buffers to avoid having to do that when saving the model.
            serializerForAutoSave = new PolySerializer();
            serializerForAutoSave.SetupForWriting(/* minInitialCapacity */ SERIALIZER_BUFFER_INITIAL_SIZE);
            serializerForManualSave = new PolySerializer();
            serializerForManualSave.SetupForWriting(/* minInitialCapacity */ SERIALIZER_BUFFER_INITIAL_SIZE);

            // Find the eye camera.
            eyeCamera = ObjectFinder.ComponentById<Camera>("ID_Camera (eye)") as Camera;

            // Create initial empty model.
            model = new Model(worldSpace.bounds);
            spatialIndex = new SpatialIndex(model, worldSpace.bounds);
            SetupSpatialIndex();
            generalBackgroundThread = new Thread(ProcessGeneralBackgroundWork);
            generalBackgroundThread.IsBackground = true;
            generalBackgroundThread.Priority = System.Threading.ThreadPriority.Lowest;
            generalBackgroundThread.Start();
            polyMenuBackgroundThread = new Thread(ProcessPolyMenuBackgroundWork);
            polyMenuBackgroundThread.IsBackground = true;
            polyMenuBackgroundThread.Priority = System.Threading.ThreadPriority.Lowest;
            polyMenuBackgroundThread.Start();
            filePickerBackgroundThread = new Thread(ProcessFilePickerBackgroundWork);
            filePickerBackgroundThread.IsBackground = true;
            filePickerBackgroundThread.Priority = System.Threading.ThreadPriority.Lowest;
            filePickerBackgroundThread.Start();

            // Set up auto-saving.
            userPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);

            // GetFolderPath() can fail, returning an empty string.
            if (userPath == "")
            {
                // If that happens, try a bunch of other folders.
                userPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.MyDocuments);
                if (userPath == "")
                {
                    userPath = System.Environment.GetFolderPath(
                        System.Environment.SpecialFolder.DesktopDirectory);
                }
            }

            userPath = Path.Combine(userPath, "Blocks");
            if (!Path.IsPathRooted(userPath))
            {
                Debug.Log("Failed to find Documents folder.");
            }

            modelsPath = Path.Combine(userPath, "Models");
            offlineModelsPath = Path.Combine(userPath, "OfflineModels");
            // TODO(bug): Actually do something incremental with these commands instead of persisting the
            // whole state after every command.
            autoSave = new AutoSave(model, modelsPath);
            model.OnCommandApplied += ((Command command) =>
            {
                TryAutoSave();
                ModelChangedSinceLastSave = true;
                SetSaveButtonActiveIfModelNotEmpty();
            });
            model.OnUndo += ((Command command) =>
            {
                TryAutoSave();
                ModelChangedSinceLastSave = true;
                SetSaveButtonActiveIfModelNotEmpty();
            });
            model.OnRedo += ((Command command) =>
            {
                TryAutoSave();
                ModelChangedSinceLastSave = true;
                SetSaveButtonActiveIfModelNotEmpty();
            });

            // Get the AudioLibrary and play the startup sound.
            audioLibrary = FindObjectOfType<AudioLibrary>();
            audioLibrary.Setup();

            // Get the menu main.
            polyMenuMain = FindObjectOfType<PolyMenuMain>();

            // The previewController handles opening the image dialog and loading a reference.
            previewController = FindObjectOfType<PreviewController>();

            // Get the desktop UI Main
            desktopMain = FindObjectOfType<DesktopMain>();

            // Get the ZandriaCreationsManager.
            zandriaCreationsManager = FindObjectOfType<ZandriaCreationsManager>();

            // Get the EnvironmentThemeManager.
            environmentThemeManager = ObjectFinder.ObjectById("ID_Environment").GetComponent<EnvironmentThemeManager>();

            // Get the worldspace bounding box.
            polyWorldBounds = ObjectFinder.ObjectById("ID_PolyWorldBounds").GetComponent<PolyWorldBounds>();

            // Try to perform the setup. If we fail, that's ok, we'll try again in Update() until we succeed.
            TrySetup();
        }

        /// <summary>
        /// Tries to perform the one-time setup, if we're ready.
        /// </summary>
        /// <returns>True if setup was done, false if not done.</returns>
        private bool TrySetup()
        {
            if (!PeltzerController.AcquireIfNecessary(ref peltzerController))
            {
                Debug.LogWarning("couldn't find peltzer controller!");
                return false;
            }
            if (!PaletteController.AcquireIfNecessary(ref paletteController))
            {
                Debug.LogWarning("couldn't find palette controller!");
                return false;
            }

            Debug.Log("Starting up, v" + Config.Instance.version);

            restrictionManager = new RestrictionManager();

            // PeltzerController needs to grab references to some tools, so we need to add them first.
            // We call Setup() on them later, though (since that requires PeltzerController to be fully set up).
            volumeInserter = gameObject.AddComponent<VolumeInserter>();
            freeform = gameObject.AddComponent<Freeform>();
            peltzerController.Setup(volumeInserter, freeform);
            paletteController.Setup();
            controllerMain = new ControllerMain(peltzerController, paletteController);
            tutorialManager = gameObject.AddComponent<TutorialManager>();
            attentionCaller = gameObject.AddComponent<AttentionCaller>();
            attentionCaller.Setup(peltzerController, paletteController);
            progressIndicator = gameObject.AddComponent<ProgressIndicator>();

            savePreview = gameObject.AddComponent<SavePreview>();
            savePreview.Setup();

            menuHint = gameObject.AddComponent<MenuHint>();
            menuHint.Setup();

            // Tools.
            zoomer = gameObject.AddComponent<Zoomer>();
            zoomer.Setup(controllerMain, peltzerController, paletteController, worldSpace, audioLibrary);
            highlightUtils = gameObject.AddComponent<HighlightUtils>();
            highlightUtils.Setup(worldSpace, model, materialLibrary);
            selector = gameObject.AddComponent<Selector>();
            selector.Setup(model, controllerMain, peltzerController, paletteController, worldSpace, spatialIndex,
              highlightUtils, materialLibrary);
            freeform.Setup(model, controllerMain, peltzerController, audioLibrary, worldSpace);
            volumeInserter.Setup(model, controllerMain, peltzerController, audioLibrary, worldSpace,
              spatialIndex, model.meshRepresentationCache, selector);
            reshaper = gameObject.AddComponent<Reshaper>();
            reshaper.Setup(model, controllerMain, peltzerController, paletteController, selector,
              audioLibrary, worldSpace, spatialIndex, model.meshRepresentationCache);
            mover = gameObject.AddComponent<Mover>();
            mover.Setup(model, controllerMain, peltzerController, paletteController, selector, volumeInserter,
              audioLibrary, worldSpace, spatialIndex, model.meshRepresentationCache);
            extruder = gameObject.AddComponent<Extruder>();
            extruder.Setup(model, controllerMain, peltzerController, paletteController, selector,
              audioLibrary, worldSpace);
            subdivider = gameObject.AddComponent<Subdivider>();
            subdivider.Setup(model, controllerMain, peltzerController, paletteController, selector,
              audioLibrary, worldSpace);
            deleter = gameObject.AddComponent<Deleter>();
            deleter.Setup(model, controllerMain, peltzerController, selector, audioLibrary);
            painter = gameObject.AddComponent<Painter>();
            painter.Setup(model, controllerMain, peltzerController, selector, audioLibrary);
            gifRecorder = gameObject.AddComponent<GifRecorder>();
            UXEffectManager.Setup(model.meshRepresentationCache, materialLibrary, worldSpace);
            gridHighlighter = gameObject.AddComponent<GridHighlightComponent>();
            gridHighlighter.Setup(materialLibrary, worldSpace, peltzerController);

            // Register cross controller handlers.
            paletteController.RegisterCrossControllerHandlers(peltzerController);

            desktopMain.Setup();

            // Model.
            exporter = gameObject.AddComponent<Exporter>();
            // Setup FBX exporter.
            FbxExporter.Setup();

            // Starts the call to authenticate.
            zandriaCreationsManager.Setup();

            // Menu.
            polyMenuMain.Setup(zandriaCreationsManager, paletteController);

            introChoreographer = gameObject.AddComponent<IntroChoreographer>();
            introChoreographer.Setup(audioLibrary, peltzerController, paletteController);

            floatingMessage = gameObject.AddComponent<FloatingMessage>();
            floatingMessage.Setup();

            // Controller switch gesture detector.
            if (Features.enableControllerSwapping)
            {
                controllerSwapDetector = gameObject.AddComponent<ControllerSwapDetector>();
                controllerSwapDetector.Setup();
            }

            referenceImageManager = gameObject.AddComponent<ReferenceImageManager>();

            // If the user logged in previously, then load their logged-in state, but don't prompt them to login otherwise.
            SignIn(/* promptUserIfNoToken */ false);

            // If the user has disabled tooltips according to their player preferences, toggle tooltips to 'off'.
            if (PlayerPrefs.HasKey(DISABLE_TOOLTIPS_KEY) && PlayerPrefs.GetString(DISABLE_TOOLTIPS_KEY) == "true")
            {
                ToggleTooltipDisplay();
            }

            // If the user has disabled sound according to their player preferences, toggle sounds to 'off'.
            if (PlayerPrefs.HasKey(DISABLE_SOUNDS_KEY) && PlayerPrefs.GetString(DISABLE_SOUNDS_KEY) == "true")
            {
                audioLibrary.ToggleSounds();
            }

            // Set the environment theme based on last session.
            environmentThemeManager.Setup();
            if (PlayerPrefs.HasKey(ENVIRONMENT_THEME_KEY))
            {
                switch (PlayerPrefs.GetInt(ENVIRONMENT_THEME_KEY))
                {
                    case (int)EnvironmentThemeManager.EnvironmentTheme.DAY:
                        environmentThemeManager.SetEnvironment(EnvironmentThemeManager.EnvironmentTheme.DAY);
                        break;
                    case (int)EnvironmentThemeManager.EnvironmentTheme.PURPLE:
                        environmentThemeManager.SetEnvironment(EnvironmentThemeManager.EnvironmentTheme.PURPLE);
                        break;
                    case (int)EnvironmentThemeManager.EnvironmentTheme.BLACK:
                        environmentThemeManager.SetEnvironment(EnvironmentThemeManager.EnvironmentTheme.BLACK);
                        break;
                    case (int)EnvironmentThemeManager.EnvironmentTheme.WHITE:
                        environmentThemeManager.SetEnvironment(EnvironmentThemeManager.EnvironmentTheme.WHITE);
                        break;
                    default:
                        environmentThemeManager.SetEnvironment(EnvironmentThemeManager.EnvironmentTheme.DAY);
                        break;
                }
            }
            setupDone = true;
            return true;
        }

        void Update()
        {
            if (!setupDone && !TrySetup())
            {
                // Couldn't do set up yet, so wait.
                return;
            }

            if (LastAutoSaveDenied)
            {
                TryAutoSave();
            }

            // Detect if the user is left- or right- handed. We do this every frame to deal with them putting down
            // and picking up controllers.
            ResolveControllerHandedness();

            // While we have done less than 5ms of work from the work queue, start doing new work. Note that the entirety of
            // the de-queued new work will be performed this frame.
            float startTime = Time.realtimeSinceStartup;
            BackgroundWork work;
            while ((Time.realtimeSinceStartup - startTime) < 0.005f && forMainThread.Dequeue(out work))
            {
                work.PostWork();
            }

            UXEffectManager.GetEffectManager().Update();

            // Record the position of the camera because it is needed for saves, and background threads
            // are unable to access gameObject transforms.
            eyeCameraPosition = eyeCamera.transform.position;
        }


        // All rendering needs to be done at the very end of the frame after all the state changes to
        // the models have been made.
        void LateUpdate()
        {
            // Render the model.
            model.Render();
            // Render UX Effects
            UXEffectManager.GetEffectManager().Render();
        }

        /// <summary>
        ///   Sets the save button active if the model is not empty.
        /// </summary>
        private void SetSaveButtonActiveIfModelNotEmpty()
        {
            if (model.GetNumberOfMeshes() == 0)
            {
                attentionCaller.GreyOut(AttentionCaller.Element.SAVE_BUTTON_ICON, 0f);
            }
            else if (restrictionManager.menuActionsAllowed)
            {
                attentionCaller.Recolor(AttentionCaller.Element.SAVE_BUTTON_ICON);
            }
        }

        /// <summary>
        ///   Sets the save button active if the selection is not empty.
        /// </summary>
        public void SetSaveSelectedButtonActiveIfSelectionNotEmpty()
        {
            if (!(selector.selectedMeshes.Count > 0))
            {
                attentionCaller.GreyOut(AttentionCaller.Element.SAVE_SELECTED_BUTTON, 0f);
            }
            else if (restrictionManager.menuActionsAllowed)
            {
                attentionCaller.Recolor(AttentionCaller.Element.SAVE_SELECTED_BUTTON);
            }
        }

        /// <summary>
        ///   Try and perform an auto-save. If the auto-saver is busy, mark that this request was denied, such that we can
        ///   try again on Update. This is preferable to implementing a Queue, as we only want to auto-save the most recent
        ///   state at any given point, rather than try and persist the whole command stack as individual auto-saves which
        ///   will overwrite eachother anyway.
        /// </summary>
        private void TryAutoSave()
        {
            if (!autoSave.IsCurrentlySaving)
            {
                LastAutoSaveDenied = false;
                autoSave.IsCurrentlySaving = true;
                DoPolyMenuBackgroundWork(new AutoSaveWork(model, model.meshRepresentationCache, autoSave, serializerForAutoSave));
            }
            else
            {
                LastAutoSaveDenied = true;
            }
        }

        /// <summary>
        ///   Called from the palette when an item in the file menu is "clicked".
        /// </summary>
        /// <param name="action"></param>
        public void InvokeMenuAction(MenuAction action, String featureString = null)
        {
            switch (action)
            {
                case MenuAction.CLEAR:
                    SetAllPromptsInactive();
                    CreateNewModel();
                    break;
                case MenuAction.LOAD:
                    break;
                case MenuAction.SHOW_SAVE_CONFIRM:
                    // Only show the save confirmation dialog if modified since last save.
                    // Do not offer the option to save if being called within the tutorial.
                    if (ModelChangedSinceLastSave && !tutorialManager.TutorialOccurring() && model.GetNumberOfMeshes() > 0)
                    {
                        // The save confirmation dialog will call InvokeMenuAction according to the user's
                        // decision (CLEAR, CANCEL_SAVE or NEW_WITH_SAVE).
                        SetAllPromptsInactive();
                        paletteController.newModelPrompt.SetActive(true);
                    }
                    else
                    {
                        // Not modified since last save, so we can clear without confirmation.
                        InvokeMenuAction(MenuAction.CLEAR);
                    }
                    break;
                case MenuAction.CANCEL_SAVE:
                    SetAllPromptsInactive();
                    break;
                case MenuAction.SAVE:
                    SetAllPromptsInactive();
                    if (OAuth2Identity.Instance.LoggedIn)
                    {
                        SaveCurrentModel(publish: false, saveSelected: false);
                    }
                    else
                    {
                        paletteController.saveLocallyPrompt.SetActive(true);
                    }
                    break;
                case MenuAction.SAVE_COPY:
                    SetAllPromptsInactive();
                    SaveCurrentModelAsCopy();
                    break;
                case MenuAction.SAVE_SELECTED:
                    SaveCurrentSelectedModel();
                    break;
                case MenuAction.PUBLISH:
                    SetAllPromptsInactive();
                    if (!OAuth2Identity.Instance.LoggedIn)
                    {
                        SignIn(/* promptUserIfNoToken */ true);
                        paletteController.publishSignInPrompt.SetActive(true);
                    }
                    else
                    {
                        SaveCurrentModel(publish: true, saveSelected: false);
                        paletteController.SetPublishDialogActive();
                    }
                    break;
                case MenuAction.PUBLISHED_TAKE_OFF_HEADSET_DISMISS:
                    SetAllPromptsInactive();
                    break;
                case MenuAction.NEW_WITH_SAVE:
                    SetAllPromptsInactive();
                    saveCompleteAction = () =>
                    {
                        InvokeMenuAction(MenuAction.CLEAR);
                    };
                    // After the model is serialized and we're free to clear it:
                    SaveCurrentModel(publish: false, saveSelected: false);
                    break;
                case MenuAction.SHARE:
                    break;
                case MenuAction.SHOWCASE:
                    break;
                case MenuAction.TAKE_PHOTO:
                    break;
                case MenuAction.BLOCKMODE:
                    peltzerController.ToggleBlockMode(/* initiatedByUser */ true);
                    break;
                case MenuAction.SIGN_IN:
                    // Prompt the user to take off their headset and sign in.
                    polyMenuMain.PromptUserToSignIn();
                    SignIn(/* promptUserIfNoToken */ true);
                    break;
                case MenuAction.SIGN_OUT:
                    SignOut();
                    break;
                case MenuAction.ADD_REFERENCE:
                    // Open a dialog to select an image.
                    previewController.SelectPreviewImage();
                    break;
                case MenuAction.TOGGLE_SOUND:
                    audioLibrary.ToggleSounds();
                    break;
                case MenuAction.TOGGLE_FEATURE:
                    polyWorldBounds.HandleFeatureToggle();
                    break;
                case MenuAction.TUTORIAL_PROMPT:
                    if (paletteController.tutorialBeginPrompt.activeInHierarchy ||
                        paletteController.tutorialSavePrompt.activeInHierarchy ||
                        paletteController.tutorialExitPrompt.activeInHierarchy)
                    {
                        SetAllPromptsInactive();
                    }
                    else
                    {
                        SetAllPromptsInactive();
                        paletteController.tutorialButton.GetComponent<Renderer>().material.color = PeltzerController.MENU_BUTTON_GREEN;
                        if (tutorialManager.TutorialOccurring())
                        {
                            paletteController.tutorialExitPrompt.SetActive(true);
                        }
                        else
                        {
                            paletteController.tutorialBeginPrompt.SetActive(true);
                        }
                    }
                    break;
                case MenuAction.TUTORIAL_DISMISS:
                    SetAllPromptsInactive();
                    if (!HasEverShownFeaturedTooltip)
                    {
                        applicationButtonToolTips.TurnOn("ViewFeatured");
                        polyMenuMain.SwitchToFeaturedSection();
                        HasEverShownFeaturedTooltip = true;
                    }
                    break;
                case MenuAction.TUTORIAL_START:
                    SetAllPromptsInactive();
                    attentionCaller.StopGlowing(AttentionCaller.Element.TAKE_A_TUTORIAL_BUTTON);

                    if (ModelChangedSinceLastSave && model.GetNumberOfMeshes() > 0)
                    {
                        paletteController.tutorialButton.GetComponent<Renderer>().material.color = PeltzerController.MENU_BUTTON_GREEN;
                        paletteController.tutorialSavePrompt.SetActive(true);
                    }
                    else
                    {
                        StartTutorial();
                    }
                    break;
                case MenuAction.TUTORIAL_CONFIRM_DISMISS:
                    SetAllPromptsInactive();
                    break;
                case MenuAction.TUTORIAL_SAVE_AND_CONFIRM:
                    SetAllPromptsInactive();
                    saveCompleteAction = () =>
                    {
                        StartTutorial();
                    };
                    // After the model is serialized and we're free to clear it:
                    SaveCurrentModel(publish: false, saveSelected: false);
                    break;
                case MenuAction.TUTORIAL_DONT_SAVE_AND_CONFIRM:
                    SetAllPromptsInactive();
                    StartTutorial();
                    break;
                case MenuAction.TUTORIAL_EXIT_YES:
                    SetAllPromptsInactive();
                    tutorialManager.ExitTutorial(/* isForceExit */ true);
                    break;
                case MenuAction.TUTORIAL_EXIT_NO:
                    SetAllPromptsInactive();
                    break;
                case MenuAction.PUBLISH_AFTER_SAVE_DISMISS:
                    SetAllPromptsInactive();
                    break;
                case MenuAction.PUBLISH_SIGN_IN_DISMISS:
                    SetAllPromptsInactive();
                    break;
                case MenuAction.PUBLISH_AFTER_SAVE_CONFIRM:
                    AssetsServiceClient.OpenPublishUrl(LastSavedAssetId);
                    SetAllPromptsInactive();
                    paletteController.SetPublishDialogActive();
                    break;
                case MenuAction.SAVE_LOCALLY:
                    SetAllPromptsInactive();
                    SaveCurrentModel(publish: false, saveSelected: false);
                    break;
                case MenuAction.SAVE_LOCAL_SIGN_IN_INSTEAD:
                    SetAllPromptsInactive();
                    SignInThenSaveModel(/* promptIfNoUserToken */ true);
                    paletteController.SetPublishDialogActive();
                    break;
                case MenuAction.TOGGLE_LEFT_HANDED:
                    controllerSwapDetector.TrySwappingControllers();
                    break;
                case MenuAction.TOGGLE_TOOLTIPS:
                    ToggleTooltipDisplay();
                    break;
                case MenuAction.TOGGLE_EXPAND_WIREFRAME_FEATURE:
                    selector.ResetInactive();
                    break;
            }
        }

        /// <summary>
        ///   Gets the video viewer.
        /// </summary>
        public GameObject GetVideoViewer()
        {
            return ObjectFinder.ObjectById("VideoViewer");
        }

        /// <summary>
        ///   Toggles whether all tooltips should be disabled in the app.
        /// </summary>
        private void ToggleTooltipDisplay()
        {
            // Make the switch.
            HasDisabledTooltips = !HasDisabledTooltips;
            peltzerController.HideTooltips();
            paletteController.HideTooltips();

            // Update player preferences.
            PlayerPrefs.SetString(DISABLE_TOOLTIPS_KEY, HasDisabledTooltips ? "true" : "false");

            // Update menu text.
            ObjectFinder.ObjectById("ID_tooltips_are_enabled").SetActive(!HasDisabledTooltips);
            ObjectFinder.ObjectById("ID_tooltips_are_disabled").SetActive(HasDisabledTooltips);
        }

        /// <summary>
        ///   Checks if the user has the 'is left handed' preference set and switches accordingly. This can't happen in
        ///   PeltzerMain Setup, and so is called once the intro sequence is complete.
        /// </summary>
        public void CheckLeftHandedPlayerPreference()
        {
            // If the user is left-handed according to their player preferences, switch their hands at setup.
            if (Config.Instance.VrHardware == VrHardware.Rift && PlayerPrefs.HasKey(LEFT_HANDED_KEY)
              && PlayerPrefs.GetString(LEFT_HANDED_KEY) == "true")
            {
                controllerSwapDetector.TrySwappingControllers();
            }
        }

        public void SetPublishAfterSavePromptActive()
        {
            paletteController.publishAfterSavePrompt.SetActive(true);
        }

        private void SetAllPromptsInactive()
        {
            paletteController.newModelPrompt.SetActive(false);
            paletteController.publishedTakeOffHeadsetPrompt.SetActive(false);
            paletteController.tutorialBeginPrompt.SetActive(false);
            paletteController.tutorialSavePrompt.SetActive(false);
            paletteController.tutorialExitPrompt.SetActive(false);
            paletteController.publishSignInPrompt.SetActive(false);
            paletteController.publishAfterSavePrompt.SetActive(false);
            paletteController.saveLocallyPrompt.SetActive(false);
            paletteController.tutorialButton.GetComponent<Renderer>().material.color = PeltzerController.MENU_BUTTON_DARK;
            applicationButtonToolTips.TurnOff();
        }

        /// <summary>
        ///   Called when an EnvironmentMenuItem is clicked indicated a theme change.
        /// </summary>
        /// <param name="theme">The environment theme.</param>
        public void SetEnvironmentTheme(EnvironmentThemeManager.EnvironmentTheme theme)
        {
            environmentThemeManager.SetEnvironment(theme);
            PlayerPrefs.SetInt(ENVIRONMENT_THEME_KEY, (int)theme);
        }

        private void StartTutorial()
        {
            GetMover().currentMoveType = tools.Mover.MoveType.MOVE;
            paletteController.ChangeTouchpadOverlay(TouchpadOverlay.UNDO_REDO);
            tutorialManager.StartTutorial(0);
        }

        /// <summary>
        /// Load Zandria creations related to the logged-in user.
        /// </summary>
        public void LoadCreations()
        {
            // Start loading user creations and creations liked by the user.
            zandriaCreationsManager.StartLoad(PolyMenuMain.CreationType.YOUR);
            zandriaCreationsManager.StartLoad(PolyMenuMain.CreationType.LIKED);
        }

        public void SignIn(bool promptUserIfNoToken)
        {
            // Sign the user in.
            OAuth2Identity.Instance.Login(SignInSuccess, SignInFailure, promptUserIfNoToken);
        }

        public void SignInThenSaveModel(bool promptUserIfNoToken)
        {
            // Sign the user in.
            OAuth2Identity.Instance.Login(SignInSuccessWithModelSave, SignInFailure, promptUserIfNoToken);
        }

        private void SignInSuccess()
        {
            // After authentication if the user actually authenticated.
            // Load Zandria creations if the startup animation is not currently occurring.
            if (introChoreographer.state == IntroChoreographer.State.DONE)
            {
                LoadCreations();
            }
            else
            {
                // Otherwise, tell the introChoreographer to load the creations when startup is finished.
                introChoreographer.loadCreationsWhenDone = true;
            }

            // Change the PolyMenu buttons.
            polyMenuMain.SignIn(OAuth2Identity.Instance.Profile.icon, OAuth2Identity.Instance.Profile.name);
            // They logged in, change the "Sign In" button to sign out.
            GetDesktopMain().SignIn(OAuth2Identity.Instance.Profile.icon, OAuth2Identity.Instance.Profile.name);

            paletteController.publishSignInPrompt.SetActive(false);
        }

        private void SignInSuccessWithModelSave()
        {
            SignInSuccess();
            SaveCurrentModel(publish: false, saveSelected: false);
        }

        private void SignInFailure()
        {
            // Change the PolyMenu buttons.
            polyMenuMain.SignOut();
            // Update the desktop menu.
            desktopMain.SignOut();
        }

        public void SignOut()
        {
            zandriaCreationsManager.ClearLoad(PolyMenuMain.CreationType.YOUR);
            zandriaCreationsManager.ClearLoad(PolyMenuMain.CreationType.LIKED);
            AssetsServiceClient.mostRecentLikedAssetId = null;

            // Try to load any local models.
            zandriaCreationsManager.LoadOfflineModels();

            // Sign the user out.
            OAuth2Identity.Instance.Logout();
            // Change the PolyMenu buttons.
            polyMenuMain.SignOut();
            // Update the desktop menu.
            desktopMain.SignOut();
        }

        /// <summary>
        ///   Initiates a save operation for the current model.  This sets the model to read-only mode,
        ///   serializes the bytes on the background thread, makes the model writable then saves the
        ///   bytes in a Coroutine.
        /// </summary>
        /// <param name="publish">If true, also opens the url to publish the content.</param>
        /// <param name="saveSelected">If true, only saves the current selected content rather than
        /// the whole model.</param>
        public void SaveCurrentModel(bool publish, bool saveSelected)
        {
            // Don't save empty scenes (the button will already be disabled).
            if (model.GetNumberOfMeshes() == 0)
            {
                return;
            }

            // Keep track of model changes so we know whether or not to pop up the 'are you sure' dialog on 'new'.
            ModelChangedSinceLastSave = false;
            newModelSinceLastSaved = false;

            if (!model.writeable)
            {
                // Make sure we don't try to lock down the model twice, since unlocking would be an issue.
                Debug.Log("Already saving.");
                return;
            }

            // Mark the model as read-only while we serialize everything on the background thread.  This is
            // a pretty low-rent way to handle things.  But it also minimizes complexity.
            restrictionManager.controllerEventsAllowed = false;
            model.writeable = false;

            progressIndicator.StartOperation(SAVE_MESSAGE);
            ICollection<MMesh> meshes = saveSelected ? model.GetMatchingMeshes(selector.selectedMeshes) : model.GetAllMeshes();
            // Take a screenshot at the end of the next frame.
            StartCoroutine(PeltzerMain.Instance.autoThumbnailCamera.TakeScreenShot((byte[] pngBytes) =>
            {
                // TODO bug - Temporarily doing this in the foreground, as GLTF export can't run in the background.
                // This should be moved back to the background thread as soon as GLTF export is fixed to not use Unity objects.
                SerializeWork serWork = new SerializeWork(model, meshes, pngBytes, (SaveData saveData) =>
                {
                    // NOTE: this callback only means data is now serialized. It hasn't been saved yet!

                    // The model can now be written, since it's already serialized. From this point on in the save process,
                    // we will no longer look at the model, we will only look at the serialized data. So we don't care what
                    // happens to the model from now on.
                    restrictionManager.controllerEventsAllowed = true;
                    model.writeable = true;
                    saveData.remixIds = model.GetAllRemixIds(meshes);
                    // Now let's save the serialized data. This will be done asynchronously.
                    SaveSerializedData(saveData, publish, saveSelected);
                }, serializerForManualSave, saveSelected);

                serWork.BackgroundWork();
                serWork.PostWork();
            }));
        }

        /// <summary>
        ///   Saves the current model, and lets the user keep working on it in a new branch. That is to say, any edits
        ///   after this point will apply to a new modelId and hitting 'save' after this operation will create a second,
        ///   distinct save file.
        ///   This does not remove 'remix' info.
        /// </summary>
        public void SaveCurrentModelAsCopy()
        {
            SaveCurrentModel(publish: false, saveSelected: false);
            LocalId = null;
            AssetId = null;
            ModelChangedSinceLastSave = true;
        }

        /// <summary>
        ///   Saves the current selected models only as a asset. Any edits after this point will apply to a previous
        ///   modelId and hitting 'save' after this operation will overwrite the previous model, not the selected
        ///   content.
        ///   This does not remove 'remix' info.
        /// </summary>
        public void SaveCurrentSelectedModel()
        {
            if (selector.selectedMeshes.Count > 0)
            {
                SaveCurrentModel(publish: false, saveSelected: true);
            }
        }

        /// <summary>
        /// Called (on UI thread) when the model data has been serialized and is ready to save.
        /// </summary>
        public void SaveSerializedData(SaveData saveData, bool publish, bool saveSelected)
        {
            // Generate an ID if needed. A new id will be needed if the LocalId is null or we are currently
            // only saving the selected content, otherwise we are just overwriting existing save data and
            // can use the existing LocalId.
            bool isOverwrite = (LocalId != null && !saveSelected);
            string modelIdForSaving = isOverwrite ? LocalId : ObjFileExporter.RandomOpaqueId();

            // Save locally to a regular directory.
            string directory = Path.Combine(modelsPath, modelIdForSaving);
            DoPolyMenuBackgroundWork(
              new SaveToDiskWork(saveData, directory, /* isOfflineModelsFolder */ false, isOverwrite));
            if (OAuth2Identity.Instance.LoggedIn)
            {
                // If the user is authenticated, save to the assets service.
                // This is asynchronous, and will ultimately call HandleSaveComplete to report the result of the
                // save operation when it ends.
                exporter.UploadToVrAssetsService(saveData, publish, saveSelected);
            }
            else
            {
                // Take a screenshot at the end of the next frame, then save to a special 'offline' directory locally
                // so the user doesn't lose their work just because they weren't authenticated/online.
                // TODO(bug): Ensure thumbnail only contains selected content when saveSelected is true.
                StartCoroutine(autoThumbnailCamera.TakeScreenShot((byte[] pngBytes) =>
                {
                    saveData.thumbnailBytes = pngBytes;
                    directory = Path.Combine(offlineModelsPath, modelIdForSaving);

                    DoPolyMenuBackgroundWork(new SaveToDiskWork(saveData, directory, /* isOfflineModelsFolder */ true,
                      isOverwrite));
                    // If we are only saving the selected content, we don't want to overwrite the LocalId
                    // as the current id for the model we saved is only for the temporary selected content.
                    if (!saveSelected)
                    {
                        LocalId = modelIdForSaving;
                    }
                }));
            }
        }

        /// <summary>
        /// Signals that a saving operation has completed.
        /// </summary>
        /// <param name="success">True if the save operation finished successfully, or false if it
        /// caught fire, crashed and burned.</param>
        public void HandleSaveComplete(bool success, string message)
        {
            if (saveCompleteAction != null)
            {
                if (success)
                {
                    saveCompleteAction();
                    saveCompleteAction = null;
                }
                else
                {
                    //Right now we don't need a saveFailedHandler, but one can be inserted here at such a point as we do.
                    saveCompleteAction = null;
                }
            }
            progressIndicator.FinishOperation(success, message);
            peltzerController.TriggerHapticFeedback();
        }

        /// <summary>
        ///   Creates a new model, cache and spatial index, resets the state of the app,
        ///   and optionally removes any reference images present.
        /// </summary>
        public void CreateNewModel(bool clearReferenceImages = true, bool resetAttentionCaller = true,
          bool resetRestrictions = true)
        {
            LocalId = null;
            AssetId = null;
            newModelSinceLastSaved = true;
            ResetState();
            if (clearReferenceImages)
            {
                foreach (MoveableReferenceImage refImg in GameObject.FindObjectsOfType<MoveableReferenceImage>())
                {
                    DestroyImmediate(refImg.gameObject);
                }
            }

            worldSpace.SetToDefault();
            zoomer.ClearState();
            model.Clear(worldSpace);
            volumeInserter.ClearState();
            spatialIndex.Reset(DEFAULT_BOUNDS);
            if (resetAttentionCaller)
            {
                attentionCaller.ResetAll();
            }
            // By default, all operations are allowed.
            if (resetRestrictions)
            {
                restrictionManager.AllowAll();
            }

            // When creating a new model, there will be no meshes, so the save button should be inactive.
            SetSaveButtonActiveIfModelNotEmpty();
            SetSaveSelectedButtonActiveIfSelectionNotEmpty();

            // Open the save url once per unique model when saved.
            HasOpenedSaveUrlThisSession = false;
        }

        /// <summary>
        ///   Resets the state of every tool to the state it was in at startup, and switches to the default tool.
        ///   This gives a 'clean' experience when loading, hitting 'new', or starting a tutorial.
        /// </summary>
        public void ResetState()
        {
            mover.currentMoveType = Mover.MoveType.MOVE;
            worldSpace.SetToDefault();
            selector.DeselectAll();
            peltzerController.shapesMenu.SetShapeMenuItem((int)Primitives.Shape.CUBE, /* showMenu */ false);
            peltzerController.currentMaterial = PeltzerController.DEFAULT_MATERIAL;
            peltzerController.ChangeToolColor();
            peltzerController.SetDefaultMode();
            if (peltzerController.isBlockMode)
            {
                peltzerController.ToggleBlockMode(/* initiatedByUser */ false);
            }
        }

        private void SetupSpatialIndex()
        {

            // Do spatial indexing on the background thread.  Some important notes:
            // 1) We copy the mesh on the main thread, since we don't want the background thread to be reading from
            //    it while we are changing it.  Deep copies are fairly fast, since most parts of an MMesh are immutable.
            // 2) When removing from the index, we do it in two steps: on the main thread, we simply mark the mesh as
            //    condemned (pending removal), so that it will immediately stop being returned in queries. For the actual
            //    deletion (which is more expensive), we do it in the background thread.
            //    We also have to worry about the case where something is deleted and quickly added back (which can happen
            //    for Moves, for example). That's why we have to be careful that all MUTATION to the index happens
            //    in the same thread (background), to guarantee ordering. We can't have the main thread adding to the
            //    index and the background thread removing stuff from the index, for example.
            model.OnMeshAdded += (MMesh mesh) => DoBackgroundWork(new AddToIndex(spatialIndex, mesh.Clone()));
            model.OnMeshChanged += (MMesh mesh, bool materialsChanged, bool geometryChanged, bool facesOrVertsChanged) =>
            {
                if (geometryChanged)
                {
                    // Mark the mesh as condemned on the main thread so that it is no longer reported by the spatial
                    // index. The actual deletion will happen in the background thread, because it's an expensive
                    // operation.
                    spatialIndex.CondemnMesh(mesh.id);
                    DoBackgroundWork(new UpdateInIndex(spatialIndex, mesh.Clone()));
                }
            };
            model.OnMeshDeleted += (MMesh mesh) =>
            {
                // Mark the mesh as condemned on the main thread so that it is no longer reported by the spatial
                // index. The actual deletion will happen in the background thread, because it's an expensive
                // operation.
                spatialIndex.CondemnMesh(mesh.id);
                DoBackgroundWork(new DeleteFromIndex(spatialIndex, mesh.Clone()));
            };
        }

        /// <summary>
        ///   Takes in an identifier for a cloud-saved creation and then calls the creationsManager to load the creation.
        /// </summary>
        public void LoadSavedModelOntoPolyMenu(string assetId, bool wasPublished)
        {
            zandriaCreationsManager.GetAssetFromAssetsService(assetId, delegate (ObjectStoreEntry objectStoreResult)
            {
                zandriaCreationsManager.StartSingleCreationLoad(PolyMenuMain.CreationType.YOUR, objectStoreResult,
                  /* isLocal */ false, /* isSave */ true);
            });

            if (!HasShownMenuTooltipThisSession)
            {
                applicationButtonToolTips.TurnOn("ViewSaved");
                polyMenuMain.SwitchToYourModelsSection();
                HasShownMenuTooltipThisSession = true;
            }
        }

        /// <summary>
        ///   Takes in the directory for a locally-saved creation and then calls the creationsManager to load the creation.
        /// </summary>
        public void LoadLocallySavedModelOntoPolyMenu(DirectoryInfo directory)
        {
            ObjectStoreEntry objectStoreEntry;
            if (zandriaCreationsManager.GetObjectStoreEntryFromLocalDirectory(directory, out objectStoreEntry))
            {
                zandriaCreationsManager.StartSingleCreationLoad(PolyMenuMain.CreationType.YOUR, objectStoreEntry,
                  /* isLocal */ true, /* isSave */ true);
            }

            if (!HasShownMenuTooltipThisSession)
            {
                applicationButtonToolTips.TurnOn("ViewSaved");
                polyMenuMain.SwitchToYourModelsSection();
                HasShownMenuTooltipThisSession = true;
            }
        }

        /// <summary>
        ///   Takes in an updated model for a cloud-saved creation and then calls the creationsManager to update it.
        /// </summary>
        public void UpdateCloudModelOntoPolyMenu(string asset)
        {
            zandriaCreationsManager.UpdateSingleCloudCreationOnYourModels(asset);
        }

        /// <summary>
        ///   Takes in an updated model for a locally-saved creation and then calls the creationsManager to update it.
        /// </summary>
        public void UpdateLocalModelOntoPolyMenu(DirectoryInfo directoryInfo)
        {
            zandriaCreationsManager.UpdateSingleLocalCreationOnYourModels(directoryInfo);
        }

        /// <summary>
        ///   Loads a given peltzer file into the model, optionally with replaying.
        /// </summary>
        /// <param name="loadOptions">Options controlling how to load the file. If null,
        /// uses defaults.</param>
        public void LoadPeltzerFileIntoModel(PeltzerFile file, LoadOptions loadOptions = null)
        {
            loadOptions = loadOptions ?? LoadOptions.DEFAULTS;

            foreach (MMesh originalMesh in file.meshes)
            {
                MMesh mesh = loadOptions.cloneBeforeLoad ? originalMesh.Clone() : originalMesh;

                // Override the remix ID, if requested.
                if (loadOptions.overrideRemixId != null)
                {
                    mesh.remixIds = new HashSet<string>();
                    mesh.remixIds.Add(loadOptions.overrideRemixId);
                }

                // Give the mesh a new ID if necessary (if it conflicts with a mesh that's already in the
                // model).
                if (model.HasMesh(mesh.id))
                {
                    mesh.ChangeId(model.GenerateMeshId());
                }
                AssertOrThrow.True(model.AddMesh(mesh), "Attempted to load an invalid mesh");
            }
        }

        /// <summary>
        /// Loads a PeltzerFile from the project's resources.
        /// </summary>
        /// <param name="resourcePath">The resource path. Note that due to a weird Unity thing, the file
        /// should be saved with the .bytes extension in the Assets/Resources/ folder, but resourcePath should
        /// NOT contain the .bytes extension. So if your file is in Assets/Resources/Foo/bar.bytes,
        /// then resourcePath should be "Foo/bar".</param>
        public void LoadPeltzerFileFromResources(string resourcePath, bool resetAttentionCaller = true,
          bool resetRestrictions = true, bool clearReferenceImages = true)
        {
            TextAsset file = Resources.Load<TextAsset>(resourcePath);
            AssertOrThrow.NotNull(file, "Failed to load PeltzerFile from resource: " + resourcePath);
            PeltzerFile peltzerFile;
            if (!PeltzerFileHandler.PeltzerFileFromBytes(file.bytes, out peltzerFile))
            {
                throw new Exception("Failed to parse PeltzerFile from resource: " + resourcePath);
            }
            CreateNewModel(clearReferenceImages, resetAttentionCaller, resetRestrictions);
            LoadPeltzerFileIntoModel(peltzerFile);
        }

        public void RecordGif()
        {
            gifRecorder.RecordGif();
        }

        /// <summary>
        ///   Call before exit.  Shuts down any background threads.  Finalizes any saved data.
        /// </summary>
        public void Shutdown()
        {
            running = false;
        }

        /// <summary>
        ///   Whether an operation is in progress. If so, we'll deny Undo/Redo operations.
        /// </summary>
        /// <returns></returns>
        public bool OperationInProgress()
        {
            switch (peltzerController.mode)
            {
                case ControllerMode.delete:
                    return deleter.isDeleting;
                case ControllerMode.extrude:
                    return extruder.IsExtrudingFace();
                case ControllerMode.insertStroke:
                    return freeform.IsStroking();
                case ControllerMode.insertVolume:
                    return volumeInserter.IsFilling();
                case ControllerMode.move:
                    return mover.IsMoving();
                case ControllerMode.paintFace:
                case ControllerMode.paintMesh:
                    return painter.IsPainting();
                case ControllerMode.reshape:
                    return reshaper.IsReshaping();
                case ControllerMode.subdivideFace:
                    return false;
                case ControllerMode.subtract:
                    return volumeInserter.IsFilling();
            }

            return false;
        }

        /// <summary>
        ///   Get the Peltzer model.
        /// </summary>
        /// <returns>The model.</returns>
        public Model GetModel()
        {
            return model;
        }

        public Extruder GetExtruder()
        {
            return extruder;
        }

        public Exporter GetExporter()
        {
            return exporter;
        }

        public Mover GetMover()
        {
            return mover;
        }

        public Deleter GetDeleter()
        {
            return deleter;
        }

        public Reshaper GetReshaper()
        {
            return reshaper;
        }

        public Selector GetSelector()
        {
            return selector;
        }

        public Freeform GetFreeform()
        {
            return freeform;
        }

        public VolumeInserter GetVolumeInserter()
        {
            return volumeInserter;
        }

        public Subdivider GetSubdivider()
        {
            return subdivider;
        }

        public DesktopMain GetDesktopMain()
        {
            return desktopMain;
        }

        public PolyMenuMain GetPolyMenuMain()
        {
            return polyMenuMain;
        }

        public PreviewController GetPreviewController()
        {
            return previewController;
        }

        public FloatingMessage GetFloatingMessage()
        {
            return floatingMessage;
        }

        public Painter GetPainter()
        {
            return painter;
        }

        public SpatialIndex GetSpatialIndex()
        {
            return spatialIndex;
        }

        public Zoomer Zoomer { get { return zoomer; } }

        /// <summary>
        ///   Enqueue work that should be done on the general background thread.
        ///   This thread is expected to be used only for operations affecting the model.
        /// </summary>
        /// <param name="work">The work</param>
        public void DoBackgroundWork(BackgroundWork work)
        {
            generalBackgroundQueue.Enqueue(work);
        }

        /// <summary>
        ///   Enqueue work that should be done on the Poly Menu background thread.
        ///   This thread is expected to be used for anything around saving or loading objects.
        /// </summary>
        /// <param name="work">The work</param>
        public void DoPolyMenuBackgroundWork(BackgroundWork work)
        {
            polyMenuBackgroundQueue.Enqueue(work);
        }

        /// <summary>
        ///   Enqueue work that should be done on the File Picker background thread.
        ///   This thread is expected to be used for the operations where a user picks a file.
        /// </summary>
        /// <param name="work">The work</param>
        public void DoFilePickerBackgroundWork(BackgroundWork work)
        {
            filePickerBackgroundQueue.Enqueue(work);
        }

        /// <summary>
        ///   Main function for general background thread.
        /// </summary>
        private void ProcessGeneralBackgroundWork()
        {
            while (running)
            {
                BackgroundWork work;
                if (generalBackgroundQueue.WaitAndDequeue(/* wait time ms */ 1000, out work))
                {
                    try
                    {
                        work.BackgroundWork();
                        forMainThread.Enqueue(work);
                    }
                    catch (Exception e)
                    {
                        // Should probably be a fatal error.  For now, just log something.
                        Debug.LogError("Exception handling background work: " + e);
                    }
                }
            }
        }

        /// <summary>
        ///   Main function for Poly Menu background thread.
        /// </summary>
        private void ProcessPolyMenuBackgroundWork()
        {
            while (running)
            {
                BackgroundWork work;
                if (polyMenuBackgroundQueue.WaitAndDequeue(/* wait time ms */ 1000, out work))
                {
                    try
                    {
                        work.BackgroundWork();
                        forMainThread.Enqueue(work);
                    }
                    catch (Exception e)
                    {
                        // Should probably be a fatal error.  For now, just log something.
                        Debug.LogError("Exception handling background work: " + e);
                    }
                }
            }
        }

        /// <summary>
        ///   Main function for File Picker background thread.
        /// </summary>
        private void ProcessFilePickerBackgroundWork()
        {
            while (running)
            {
                BackgroundWork work;
                if (filePickerBackgroundQueue.WaitAndDequeue(/* wait time ms */ 1000, out work))
                {
                    try
                    {
                        work.BackgroundWork();
                        forMainThread.Enqueue(work);
                    }
                    catch (Exception e)
                    {
                        // Should probably be a fatal error.  For now, just log something.
                        Debug.LogError("Exception handling background work: " + e);
                    }
                }
            }
        }

        /// <summary>
        ///   Detects when the handedness changes of the controllers and accomodates necessary changes.
        ///   For the PaletteController, this means placing the menu on the opposite side.
        /// </summary>
        public void ResolveControllerHandedness()
        {
            // We only need to check for the Vive, the Rift's handedness is known, and is modified only by controller bumps.
            if (Config.Instance.VrHardware == VrHardware.Vive)
            {
                Vector3 vectorBetweenControllers = peltzerController.transform.position - paletteController.transform.position;
                if (vectorBetweenControllers.magnitude < HANDEDNESS_DISTANCE_THRESHOLD) return;
                // If the sign is positive, the user is right-handed (holding the peltzer controller in their right hand).
                float dotProduct = Vector3.Dot(vectorBetweenControllers, hmd.transform.right);
                peltzerControllerInRightHand = dotProduct > 0;
            }

            if (peltzerController.handedness == Handedness.LEFT && peltzerControllerInRightHand)
            {
                peltzerController.handedness = Handedness.RIGHT;
                peltzerController.ControllerHandednessChanged();
                paletteController.handedness = Handedness.LEFT;
                paletteController.ControllerHandednessChanged();
            }
            else if (peltzerController.handedness == Handedness.RIGHT && !peltzerControllerInRightHand)
            {
                peltzerController.handedness = Handedness.LEFT;
                peltzerController.ControllerHandednessChanged();
                paletteController.handedness = Handedness.RIGHT;
                paletteController.ControllerHandednessChanged();
            }

            // Now the switch is complete it is okay to re-enable tooltips.
            peltzerController.ShowTooltips();
        }

        /// <summary>
        /// Options controlling how to load files.
        /// </summary>
        public class LoadOptions
        {
            public static readonly LoadOptions DEFAULTS = new LoadOptions();

            /// <summary>
            /// If true, clone the meshes from the PeltzerFile instead of using them directly.
            /// Use this if you want to keep the PeltzerFile for other purposes.
            /// </summary>
            public bool cloneBeforeLoad = false;

            /// <summary>
            /// If not null, all remix IDs of all loaded meshes will be overridden with this value.
            /// Use this if you want all meshes to have the same remix ID (for example, after loading
            /// a model from Zandria that belongs to someone else, to give appropriate credit).
            /// </summary>
            public string overrideRemixId = null;
        }
    }
}
