using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text;
using System.Reflection;

// Provides the ability to convert TB geometry into glTF format and save to disk. See the class
// GlTFEditorExporter for a simple usage example.
public class GlTFScriptableExporter {

  // Allows the caller to do a custom conversion on each object's transform as they are processed.
  // This is an appropriate place to remove unwanted parent transforms. Note that the DX-to-GL
  // (RH to LH) conversion will be done automatically and should not be performed by this delegate.
  public delegate Matrix4x4 TransformFilter(Transform tr);

  private TransformFilter CurrentTransformFilter { get; set; }


  // Total number of triangles exported.
  public int NumTris { get; private set; }

  // Conversion factor to convert point positions (etc) from local units into meters.
  public static float localUnitsToMeters = 1.0f;

  // List of all exported files (so far).
  public HashSet<string> exportedFiles;

  // Stores certain glTF elements to be included in the exported output.
  public Preset preset;

  // Corresponds to the current named object being exported. The name will be used to construct the
  // various dependent glTF components. If using ExportSimpleMesh(), this must be set first. If
  // using ExportMesh(), there's no need to set it as that routine will do so.
  public string BaseName {
    set { namedObject.name = value; }
    get { return namedObject.name; }
  }

  public Matrix4x4 IdentityFilter(Transform tr) {
    return tr.localToWorldMatrix;
  }

  // Call this first, specifying output path in outPath, the glTF preset, and directory with
  // existing assets to be included, sourceDir.
  public void BeginExport(string outPath, Preset preset, TransformFilter xfFilter) {
    CurrentTransformFilter = xfFilter;
    this.outPath = outPath;
    this.preset = preset;
    writer = new GlTF_Writer();
    writer.Init();
    writer.OpenFiles(outPath);
    NumTris = 0;
    exportedFiles = new HashSet<string>();
  }

  public void SetMetadata(string generator, string version, string copyright) {
    Debug.Assert(writer != null);
    writer.Generator = generator;
    writer.Version = version;
    writer.Copyright = copyright;
  }

  // Call this last.
  public void EndExport() {
    // sanity check because this code is bug-riddled
    foreach (var pair in GlTF_Writer.nodes) {
      if (pair.Key != pair.Value.name) {
        Debug.LogWarningFormat("Buggy key/value in nodes: {0} {1}", pair.Key, pair.Value.name);
      }
    }
    writer.Write();
    writer.CloseFiles();
    exportedFiles.UnionWith(writer.exportedFiles);
    Debug.LogFormat("Wrote files:\n  {0}", String.Join("\n  ", exportedFiles.ToArray()));
    Debug.LogFormat("Saved {0} triangle(s) to {1}.", NumTris, outPath);
    GameObject.Destroy(namedObject);
  }

  private GlTF_Material GetMaterial() {
    var mtlName = GlTF_Material.GetNameFromObject(namedObject);
    Debug.Assert(GlTF_Writer.materials.ContainsKey(mtlName));
    return GlTF_Writer.materials[mtlName];
  }

  // Export a single shader float uniform
  public void ExportShaderUniform(string name, float value) {
    GlTF_Material mtl = GetMaterial();
    var float_val = new GlTF_Material.FloatValue();
    float_val.name = name;
    float_val.value = value;
    mtl.values.Add(float_val);
    AddUniform(float_val.name, GlTF_Technique.Type.FLOAT, GlTF_Technique.Semantic.UNKNOWN);
  }

  // Export a single shader color uniform
  public void ExportShaderUniform(string name, Color value) {
    GlTF_Material mtl = GetMaterial();
    var color_val = new GlTF_Material.ColorValue();
    color_val.name = name;
    color_val.color = value;
    mtl.values.Add(color_val);
    AddUniform(color_val.name, GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN);
  }

  // Export a single shader vector uniform
  public void ExportShaderUniform(string name, Vector4 value) {
    GlTF_Material mtl = GetMaterial();
    var vec_val = new GlTF_Material.VectorValue();
    vec_val.name = name;
    vec_val.vector = value;
    mtl.values.Add(vec_val);
    AddUniform(vec_val.name, GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN);
  }

  // Exports per-material specular parameters.
  public void ExportSpecularParams(float shininess, Color color) {
    var mtlName = GlTF_Material.GetNameFromObject(namedObject);
    Debug.Assert(GlTF_Writer.materials.ContainsKey(mtlName));
    GlTF_Material mtl = GlTF_Writer.materials[mtlName];

    var color_val = new GlTF_Material.ColorValue();
    color_val.name = "specular_color";
    color_val.color = color;
    mtl.values.Add(color_val);
    AddUniform(color_val.name,
               GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN);

    var float_val = new GlTF_Material.FloatValue();
    float_val.name = "specular_shininess";
    float_val.value = shininess;
    mtl.values.Add(float_val);
    AddUniform(float_val.name,
               GlTF_Technique.Type.FLOAT, GlTF_Technique.Semantic.UNKNOWN);
  }

  // Should be called per material.
  public void ExportAmbientLight(Color color) {
    var mtlName = GlTF_Material.GetNameFromObject(namedObject);
    Debug.Assert(GlTF_Writer.materials.ContainsKey(mtlName));
    GlTF_Material mtl = GlTF_Writer.materials[mtlName];
    var val = new GlTF_Material.ColorValue();
    val.name = "ambient_light_color";
    val.color = color;
    mtl.values.Add(val);
    AddUniform(val.name,
               GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN);
  }

  // Should be called per material.
  public void ExportLight(Transform tr) {
    Light unityLight = tr.GetComponent<Light>();
    Debug.Assert(unityLight != null);
    Color lightColor = unityLight.color * unityLight.intensity;
    lightColor.a = 1.0f;  // No use for alpha with light color.
    string lightName = GlTF_Writer.GetNameFromObject(tr);
    switch (tr.GetComponent<Light>().type) {
      case LightType.Point:
        GlTF_PointLight pl = new GlTF_PointLight();
        pl.color = new GlTF_ColorRGB(lightColor);
        pl.name = lightName;
        GlTF_Writer.lights.Add(pl);
        break;
      case LightType.Spot:
        GlTF_SpotLight sl = new GlTF_SpotLight();
        sl.color = new GlTF_ColorRGB(lightColor);
        sl.name = lightName;
        GlTF_Writer.lights.Add(sl);
        break;
      case LightType.Directional:
        GlTF_DirectionalLight dl = new GlTF_DirectionalLight();
        dl.color = new GlTF_ColorRGB(lightColor);
        dl.name = lightName;
        GlTF_Writer.lights.Add(dl);
        break;
      case LightType.Area:
        GlTF_AmbientLight al = new GlTF_AmbientLight();
        al.color = new GlTF_ColorRGB(lightColor);
        al.name = lightName;
        GlTF_Writer.lights.Add(al);
        break;
    }

    // Add light matrix.
    string nodeName = GlTF_Node.GetNameFromObject(tr);
    string namePrefix = GlTF_Writer.GetNameFromObject(tr);
    AddUniform(namePrefix + "_matrix",
               GlTF_Technique.Type.FLOAT_MAT4, GlTF_Technique.Semantic.MODELVIEW, nodeName);
    // This is yucky, but necessary unless we pull name-creation out of MakeNode
    GlTF_Node node = MakeNode(tr);
    if (!GlTF_Writer.nodes.ContainsKey(node.name)) {
      node.lightName = lightName;
      GlTF_Writer.nodes.Add(node.name, node);
    }

    // Add light color.
    var mtlName = GlTF_Material.GetNameFromObject(namedObject);
    Debug.Assert(GlTF_Writer.materials.ContainsKey(mtlName));
    GlTF_Material mtl = GlTF_Writer.materials[mtlName];
    var val = new GlTF_Material.ColorValue();
    val.name = namePrefix + "_color";
    val.color = lightColor;
    mtl.values.Add(val);
    AddUniform(val.name,
               GlTF_Technique.Type.FLOAT_VEC4, GlTF_Technique.Semantic.UNKNOWN, nodeName);
  }

  // Exports camera into glTF.
  public void ExportCamera(Transform tr) {
    GlTF_Node node = MakeNode(tr);
    Debug.Assert(tr.GetComponent<Camera>() != null);
    if (tr.GetComponent<Camera>().orthographic) {
      GlTF_Orthographic cam;
      cam = new GlTF_Orthographic();
      cam.type = "orthographic";
      cam.zfar = tr.GetComponent<Camera>().farClipPlane;
      cam.znear = tr.GetComponent<Camera>().nearClipPlane;
      cam.name = tr.name;
      GlTF_Writer.cameras.Add(cam);
    } else {
      GlTF_Perspective cam;
      cam = new GlTF_Perspective();
      cam.type = "perspective";
      cam.zfar = tr.GetComponent<Camera>().farClipPlane;
      cam.znear = tr.GetComponent<Camera>().nearClipPlane;
      cam.aspect_ratio = tr.GetComponent<Camera>().aspect;
      cam.yfov = tr.GetComponent<Camera>().fieldOfView;
      cam.name = tr.name;
      GlTF_Writer.cameras.Add(cam);
    }
    if (!GlTF_Writer.nodes.ContainsKey(tr.name)) {
      GlTF_Writer.nodes.Add(tr.name, node);
    }
  }

   // Exports a single-material unityMesh that has Transform tr. Unlike ExportMesh(), this API
   // has no dependency on a Renderer. However, the entire unityMesh must have a single
  // material and a single fragment/vertex shader pair.
  public void ExportSimpleMesh(Mesh unityMesh, Transform tr, GlTF_VertexLayout vertLayout) {

    // Zandria can't currently load custom attributes via the GLTFLoader (though glTF 1.0 does
    // support this), so we pack them into uv1.w, assuming uv1 starts out as a three element UV
    // channel.
    if (vertLayout.hasVertexIds && vertLayout.uv1 == GlTF_VertexLayout.UvElementCount.Three) {
      vertLayout.uv1 = GlTF_VertexLayout.UvElementCount.Four;
    } else if (vertLayout.hasVertexIds) {
      Debug.LogWarningFormat("{0} has vertex IDs, but does not follow the expected layout.",
        unityMesh.name);
    }

    Debug.Assert(unityMesh != null);
    int meshTris = unityMesh.triangles.Length / 3;
    if (meshTris < 1) {
      return;
    }
    NumTris += meshTris;
    GlTF_Mesh gltfMesh = new GlTF_Mesh();
    gltfMesh.name = GlTF_Mesh.GetNameFromObject(unityMesh);
    PopulateAccessors(unityMesh, vertLayout);
    AddMeshDependencies(unityMesh, 0, gltfMesh);
    gltfMesh.Populate(unityMesh);
    GlTF_Writer.meshes.Add(gltfMesh);

    // Yuck; would be better to pull name-creation out of MakeNode
    GlTF_Node node = MakeNode(tr);
    if (!GlTF_Writer.nodes.ContainsKey(node.name)) {
      GlTF_Writer.nodes.Add(node.name, node);
    } else {
      // Throw away the old one
      node = GlTF_Writer.nodes[node.name];
    }
    node.meshNames.Add(gltfMesh.name);
  }

  // Adds material and sets up its dependent technique, program, shaders. This should be called
  // after adding meshes, but before populating lights, textures, etc.
  public void AddMaterialWithDependencies() {
    var mtlName = GlTF_Material.GetNameFromObject(namedObject);
    Debug.Assert(!GlTF_Writer.materials.ContainsKey(mtlName));
    GlTF_Material gltfMtl = new GlTF_Material();
    gltfMtl.name = mtlName;
    GlTF_Writer.materials.Add(gltfMtl.name, gltfMtl);

    // Set up technique.
    GlTF_Technique tech = GlTF_Writer.CreateTechnique(namedObject);
    gltfMtl.instanceTechniqueName = tech.name;
    GlTF_Technique.States states = null;
    if (preset.techniqueStates.ContainsKey(BaseName)) {
      states = preset.techniqueStates[BaseName];
    } else if (preset.techniqueStates.ContainsKey("*")) {
      states = preset.techniqueStates["*"];
    }

    if (states == null) {
      // Unless otherwise specified the preset, enable z-buffering.
      states = new GlTF_Technique.States();
      states.enable = new[] { GlTF_Technique.Enable.DEPTH_TEST }.ToList();
    }
    tech.states = states;
    AddAllAttributes(tech);
    if (preset.techniqueExtras.ContainsKey(BaseName)) {
      tech.materialExtra = preset.techniqueExtras[BaseName];
    }
    tech.AddDefaultUniforms(writer.RTCCenter != null);

    // Add program.
    GlTF_Program program = new GlTF_Program();
    program.name = GlTF_Program.GetNameFromObject(namedObject);
    tech.program = program.name;
    foreach (var attr in tech.attributes) {
      program.attributes.Add(attr.name);
    }
    GlTF_Writer.programs.Add(program);

    // Add vertex and fragment shaders.
    GlTF_Shader vertShader = new GlTF_Shader();
    vertShader.name = GlTF_Shader.GetNameFromObject(namedObject, GlTF_Shader.Type.Vertex);
    program.vertexShader = vertShader.name;
    vertShader.type = GlTF_Shader.Type.Vertex;
    vertShader.uri = preset.GetVertexShader(BaseName);
    GlTF_Writer.shaders.Add(vertShader);

    GlTF_Shader fragShader = new GlTF_Shader();
    fragShader.name = GlTF_Shader.GetNameFromObject(namedObject, GlTF_Shader.Type.Fragment);
    program.fragmentShader = fragShader.name;
    fragShader.type = GlTF_Shader.Type.Fragment;
    fragShader.uri = preset.GetFragmentShader(BaseName);
    GlTF_Writer.shaders.Add(fragShader);
  }

  // Adds a texture reference to the export.
  // The texture parameter will be referenced by the texParam string.
  // texUri is a URI to the image file
  public void ExportTexture(string texParam, string texUri) {
    var mtlName = GlTF_Material.GetNameFromObject(namedObject);
    Debug.Assert(GlTF_Writer.materials.ContainsKey(mtlName));
    var material = GlTF_Writer.materials[mtlName];
    var val = new GlTF_Material.StringValue();
    val.name = texParam;
    string texName = null;
    texName = GlTF_Texture.GetNameFromObject(namedObject) +"_" + texParam;
    val.value = texName;
    material.values.Add(val);
    if (GlTF_Writer.textures.ContainsKey(texName)) {
      return;
    }
    GlTF_Image img = new GlTF_Image();
    img.name = GlTF_Image.GetNameFromObject(namedObject) + "_" + texParam;
    img.uri = texUri;
    GlTF_Writer.images.Add(img);

    var sampler = new GlTF_Sampler();
    sampler.name = sampler.ComputeName();
    sampler.magFilter = GlTF_Sampler.MagFilter.LINEAR;
    sampler.minFilter = GlTF_Sampler.MinFilter.LINEAR_MIPMAP_LINEAR;

    if (!GlTF_Writer.samplers.ContainsKey(sampler.name)) {
      GlTF_Writer.samplers[sampler.name] = sampler;
    }

    GlTF_Texture texture = new GlTF_Texture();
    texture.name = texName;
    texture.source = img.name;
    texture.samplerName = sampler.name;

    GlTF_Writer.textures.Add(texName, texture);

    // Add texture-related parameter and uniform.
    AddUniform(texParam, GlTF_Technique.Type.SAMPLER_2D, GlTF_Technique.Semantic.UNKNOWN, null);
  }

  // Handles low-level write operations into glTF files.
  private GlTF_Writer writer;
  // Output path to .gltf file.
  private string outPath;

  // Accessors for the supported glTF attributes.
  private GlTF_Accessor positionAccessor;
  private GlTF_Accessor normalAccessor;
  private GlTF_Accessor colorAccessor;
  private GlTF_Accessor tangentAccessor;
  private GlTF_Accessor vertexIdAccessor;
  private GlTF_Accessor uv0Accessor;
  private GlTF_Accessor uv1Accessor;
  private GlTF_Accessor uv2Accessor;
  private GlTF_Accessor uv3Accessor;

  // Returns full path to assets.
  private static string GetAssetFullPath(string path) {
    if (path != null) {
      path = path.Remove(0, "Assets".Length);
      path = Application.dataPath + path;
    }
    return path;
  }

  // Returns Unity Renderer, if any, given Transform.
  private static Renderer GetRenderer(Transform tr) {
    Debug.Assert(tr != null);
    Renderer mr = tr.GetComponent<MeshRenderer>();
    if (mr == null) {
      mr = tr.GetComponent<SkinnedMeshRenderer>();
    }
    return mr;
  }

  // Returns a (Unity) Mesh, if any, given Transform tr. Note that tr must also have a
  // Renderer. Otherwise, returns null.
  private static Mesh GetMesh(Transform tr) {
    Debug.Assert(tr != null);
    var mr = GetRenderer(tr);
    Mesh mesh = null;
    if (mr != null) {
      var t = mr.GetType();
      if (t == typeof(MeshRenderer)) {
        MeshFilter mf = tr.GetComponent<MeshFilter>();
        if (mf == null) {
          return null;
        }
        mesh = mf.sharedMesh;
      } else if (t == typeof(SkinnedMeshRenderer)) {
        SkinnedMeshRenderer smr = mr as SkinnedMeshRenderer;
        mesh = smr.sharedMesh;
      }
    }
    return mesh;
  }

  private GlTF_BufferView GetBufferView(GlTF_Accessor accessor) {
    switch (accessor.type) {
    case GlTF_Accessor.Type.SCALAR:
      return GlTF_BufferView.floatBufferView;
    case GlTF_Accessor.Type.VEC2:
      return GlTF_BufferView.vec2BufferView;
    case GlTF_Accessor.Type.VEC3:
      return GlTF_BufferView.vec3BufferView;
    case GlTF_Accessor.Type.VEC4:
      return GlTF_BufferView.vec4BufferView;
    default:
      throw new ArgumentException(string.Format("Unsupported accessor type {0}", accessor.type));
    }
  }

  // Populates the accessor memberes based on the given mesh.
  private void PopulateAccessors(Mesh unityMesh, GlTF_VertexLayout vertLayout) {
     positionAccessor =
        new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "position"),
      GlTF_Accessor.Type.VEC3, GlTF_Accessor.ComponentType.FLOAT);
    positionAccessor.bufferView = GetBufferView(positionAccessor);
    GlTF_Writer.accessors.Add(positionAccessor);

    normalAccessor = null;
    if (vertLayout.hasNormals) {
      normalAccessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "normal"),
        GlTF_Accessor.Type.VEC3, GlTF_Accessor.ComponentType.FLOAT);
      normalAccessor.bufferView = GetBufferView(normalAccessor);
      GlTF_Writer.accessors.Add(normalAccessor);
    }

    colorAccessor = null;
    if (vertLayout.hasColors) {
      colorAccessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "color"),
        GlTF_Accessor.Type.VEC4, GlTF_Accessor.ComponentType.FLOAT);
      colorAccessor.bufferView = GetBufferView(colorAccessor);
      GlTF_Writer.accessors.Add(colorAccessor);
    }

    tangentAccessor = null;
    if (vertLayout.hasTangents) {
      tangentAccessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "tangent"),
        GlTF_Accessor.Type.VEC4, GlTF_Accessor.ComponentType.FLOAT);
      tangentAccessor.bufferView = GetBufferView(tangentAccessor);
      GlTF_Writer.accessors.Add(tangentAccessor);
    }

    vertexIdAccessor = null;
    if (vertLayout.hasVertexIds) {
      vertexIdAccessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "vertexId"),
        GlTF_Accessor.Type.SCALAR, GlTF_Accessor.ComponentType.FLOAT);
      vertexIdAccessor.bufferView = GetBufferView(vertexIdAccessor);
      GlTF_Writer.accessors.Add(vertexIdAccessor);
    }

    uv0Accessor = null;
    if (vertLayout.uv0 != GlTF_VertexLayout.UvElementCount.None) {
      uv0Accessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "uv0"),
        GlTF_VertexLayout.GetUvType(vertLayout.uv0), GlTF_Accessor.ComponentType.FLOAT);
      uv0Accessor.bufferView = GetBufferView(uv0Accessor);
      GlTF_Writer.accessors.Add(uv0Accessor);
    }

    uv1Accessor = null;
    if (vertLayout.uv1 != GlTF_VertexLayout.UvElementCount.None) {
      uv1Accessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "uv1"),
        GlTF_VertexLayout.GetUvType(vertLayout.uv1), GlTF_Accessor.ComponentType.FLOAT);
      uv1Accessor.bufferView = GetBufferView(uv1Accessor);
      GlTF_Writer.accessors.Add(uv1Accessor);
    }

    uv2Accessor = null;
    if (vertLayout.uv2 != GlTF_VertexLayout.UvElementCount.None) {
      uv2Accessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "uv2"),
        GlTF_VertexLayout.GetUvType(vertLayout.uv2), GlTF_Accessor.ComponentType.FLOAT);
      uv2Accessor.bufferView = GetBufferView(uv2Accessor);
      GlTF_Writer.accessors.Add(uv2Accessor);
    }

    uv3Accessor = null;
    if (vertLayout.uv3 != GlTF_VertexLayout.UvElementCount.None) {
      uv3Accessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh, "uv3"),
        GlTF_VertexLayout.GetUvType(vertLayout.uv3), GlTF_Accessor.ComponentType.FLOAT);
      uv3Accessor.bufferView = GetBufferView(uv3Accessor);
      GlTF_Writer.accessors.Add(uv3Accessor);
    }
  }

  // Adds a glTF attribute, as described by name, type, and semantic, to the given technique tech.
  private static void AddAttribute(string name, GlTF_Technique.Type type,
                                   GlTF_Technique.Semantic semantic, GlTF_Technique tech) {
    GlTF_Technique.Parameter tParam = new GlTF_Technique.Parameter();
    tParam.name = name;
    tParam.type = type;
    tParam.semantic = semantic;
    tech.parameters.Add(tParam);
    GlTF_Technique.Attribute tAttr = new GlTF_Technique.Attribute();
    tAttr.name = "a_" + name;
    tAttr.param = tParam.name;
    tech.attributes.Add(tAttr);
  }

  // Adds a glTF uniform, as described by name, type, and semantic, to the given technique tech. If
  // node is non-null, that is also included (e.g. for lights).
  private void AddUniform(string name, GlTF_Technique.Type type,
                          GlTF_Technique.Semantic semantic, string node = null) {
    //var techName = GlTF_Technique.GetNameFromObject(namedObject);
    var tech = GlTF_Writer.GetTechnique(namedObject);
    GlTF_Technique.Parameter tParam = new GlTF_Technique.Parameter();
    tParam.name = name;
    tParam.type = type;
    tParam.semantic = semantic;
    if (node != null) {
      tParam.node = node;
    }
    tech.parameters.Add(tParam);
    GlTF_Technique.Uniform tUniform = new GlTF_Technique.Uniform();
    tUniform.name = "u_" + name;
    tUniform.param = tParam.name;
    tech.uniforms.Add(tUniform);
  }

  GlTF_Technique.Type GetTechniqueType(GlTF_Accessor accessor) {
    switch (accessor.type) {
    case GlTF_Accessor.Type.SCALAR:
      return GlTF_Technique.Type.FLOAT;
    case GlTF_Accessor.Type.VEC2:
      return GlTF_Technique.Type.FLOAT_VEC2;
    case GlTF_Accessor.Type.VEC3:
      return GlTF_Technique.Type.FLOAT_VEC3;
    case GlTF_Accessor.Type.VEC4:
      return GlTF_Technique.Type.FLOAT_VEC4;
    default:
      throw new ArgumentException(string.Format("Unsupported accessor type {0}", accessor.type));
    }
  }

  // Updates glTF technique tech by adding all relevant attributes.
  private void AddAllAttributes(GlTF_Technique tech) {
    AddAttribute("position", GetTechniqueType(positionAccessor),
                 GlTF_Technique.Semantic.POSITION, tech);
    if (normalAccessor != null) {
      AddAttribute("normal", GetTechniqueType(normalAccessor),
                   GlTF_Technique.Semantic.NORMAL, tech);
    }
    if (colorAccessor != null) {
      AddAttribute("color", GetTechniqueType(colorAccessor),
                   GlTF_Technique.Semantic.COLOR, tech);
    }
    if (tangentAccessor != null) {
      AddAttribute("tangent", GetTechniqueType(tangentAccessor),
                   GlTF_Technique.Semantic.TANGENT, tech);
    }
    if (vertexIdAccessor != null) {
      AddAttribute("vertexId", GetTechniqueType(vertexIdAccessor),
                   GlTF_Technique.Semantic.UNKNOWN, tech);
    }
    if (uv0Accessor != null) {
      AddAttribute("texcoord0", GetTechniqueType(uv0Accessor),
                   GlTF_Technique.Semantic.TEXCOORD_0, tech);
    }
    if (uv1Accessor != null) {
      AddAttribute("texcoord1", GetTechniqueType(uv1Accessor),
                   GlTF_Technique.Semantic.TEXCOORD_1, tech);
    }
    if (uv2Accessor != null) {
      AddAttribute("texcoord2", GetTechniqueType(uv2Accessor),
                   GlTF_Technique.Semantic.TEXCOORD_2, tech);
    }
    if (uv3Accessor != null) {
      AddAttribute("texcoord3", GetTechniqueType(uv3Accessor),
                   GlTF_Technique.Semantic.TEXCOORD_3, tech);
    }
  }

  // Attaches glTF attributes to the given glTF primitive.
  private void AttachAttributes(GlTF_Primitive primitive) {
    GlTF_Attributes attributes = new GlTF_Attributes();
    attributes.positionAccessor = positionAccessor;
    attributes.normalAccessor = normalAccessor;
    attributes.colorAccessor = colorAccessor;
    attributes.tangentAccessor = tangentAccessor;
    attributes.vertexIdAccessor = vertexIdAccessor;
    attributes.texCoord0Accessor = uv0Accessor;
    attributes.texCoord1Accessor = uv1Accessor;
    attributes.texCoord2Accessor = uv2Accessor;
    attributes.texCoord3Accessor = uv3Accessor;
    primitive.attributes = attributes;
  }

  // Adds to gltfMesh the glTF dependencies (primitive, material, technique, program, shaders)
  // required by unityMesh, using the index value and BaseName for naming the various glTF
  // components. This does not add any geometry from the mesh (that's done separately using
  // GlTF_Mesh.Populate()).
  private void AddMeshDependencies(Mesh unityMesh, int index, GlTF_Mesh gltfMesh) {
    GlTF_Primitive primitive = new GlTF_Primitive();
    primitive.name = GlTF_Primitive.GetNameFromObject(unityMesh, index);
    primitive.index = index;
    AttachAttributes(primitive);

    GlTF_Accessor indexAccessor = new GlTF_Accessor(GlTF_Accessor.GetNameFromObject(unityMesh,
        "indices_" + index), GlTF_Accessor.Type.SCALAR, GlTF_Accessor.ComponentType.USHORT);
    indexAccessor.bufferView = GlTF_Writer.ushortBufferView;
    GlTF_Writer.accessors.Add(indexAccessor);
    primitive.indices = indexAccessor;
    gltfMesh.primitives.Add(primitive);

    var mtlName = GlTF_Material.GetNameFromObject(namedObject);
    primitive.materialName = mtlName;
  }

  // Creates new node based on given transform.
  private GlTF_Node MakeNode(Transform tr) {
    Debug.Assert(tr != null);
    GlTF_Node node = new GlTF_Node();

    var mat = CurrentTransformFilter.Invoke(tr);

    // Convert from DirectX/LeftHanded/(fwd=z,rt=x,up=y) -> OpenGL/RightHanded/(fwd=-z,rt=x,up=y)
    // Matrix4x4 basis = Matrix4x4.zero;
    // basis[0, 0] = 1;
    // basis[1, 1] = 1;
    // basis[2, 2] = -1;
    // basis[3, 3] = 1;
    // mat = basis * mat * basis.inverse;
    //
    // The change of basis above is equivalent to the following:
    mat[2, 0] *= -1;
    mat[2, 1] *= -1;
    mat[0, 2] *= -1;
    mat[1, 2] *= -1;

    node.matrix = new GlTF_Matrix(mat);
    node.name = GlTF_Node.GetNameFromObject(tr);
    return node;
  }

  private GameObject namedObject = new GameObject();
}
