// TODO(ineula): I've removed editor dependencies by stripping out some functionality:
// * animations
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;

public class GlTF_Writer {
  public static FileStream fs;
  public static StreamWriter jsonWriter;
  public static BinaryWriter binWriter;
  public static Stream binFile;
  public static int indent = 0;
  public static string binFileName;
  public static bool binary;
  public static bool b3dm;
  public static GlTF_BufferView ushortBufferView = new GlTF_BufferView("ushortBufferView", 34963);
  public static GlTF_BufferView floatBufferView = new GlTF_BufferView("floatBufferView");
  public static GlTF_BufferView vec2BufferView = new GlTF_BufferView("vec2BufferView");
  public static GlTF_BufferView vec3BufferView = new GlTF_BufferView("vec3BufferView");
  public static GlTF_BufferView vec4BufferView = new GlTF_BufferView("vec4BufferView");
  public static List<GlTF_BufferView> bufferViews = new List<GlTF_BufferView>();
  public static List<GlTF_Camera> cameras = new List<GlTF_Camera>();
  public static List<GlTF_Light> lights = new List<GlTF_Light>();
  public static List<GlTF_Mesh> meshes = new List<GlTF_Mesh>();
  public static List<GlTF_Accessor> accessors = new List<GlTF_Accessor>();
  public static Dictionary<string, GlTF_Node> nodes = new Dictionary<string, GlTF_Node>();
  public static Dictionary<string, GlTF_Material> materials = new Dictionary<string, GlTF_Material>();
  public static Dictionary<string, GlTF_Sampler> samplers = new Dictionary<string, GlTF_Sampler>();
  public static Dictionary<string, GlTF_Texture> textures = new Dictionary<string, GlTF_Texture>();
  public static List<GlTF_Image> images = new List<GlTF_Image>();
  public static Dictionary<string, GlTF_Technique> techniques = new Dictionary<string, GlTF_Technique>();
  public static List<GlTF_Program> programs = new List<GlTF_Program>();
  public static List<GlTF_Shader> shaders = new List<GlTF_Shader>();
  // Allows global-scope (key, value) string pairs to be passed along to the glTF consumer.
  public static Dictionary<string, string> extras = new Dictionary<string, string>();

  // glTF metadata to write out.
  public string Copyright {
    get { return OrUnknown(copyright); }
    set { copyright = value; }
  }
  public string Generator {
    get { return OrUnknown(generator); }
    set { generator = value; }
  }
  public string Version {
    get { return OrUnknown(version); }
    set { version = value; }
  }

  public double[] RTCCenter;

  public List<string> exportedFiles;

  static public string GetNameFromObject(Object o, bool useId = false) {
    var ret = o.name;
    ret = ret.Replace(" ", "_");
    ret = ret.Replace("/", "_");
    ret = ret.Replace("\\", "_");

    if (useId) {
      ret += "_" + o.GetInstanceID();
    }
    return ret;
  }

  public void Init() {
    firsts = new bool[100];
    ushortBufferView = new GlTF_BufferView("ushortBufferView", 34963);
    floatBufferView = new GlTF_BufferView("floatBufferView");
    vec2BufferView = new GlTF_BufferView("vec2BufferView");
    vec3BufferView = new GlTF_BufferView("vec3BufferView");
    vec4BufferView = new GlTF_BufferView("vec4BufferView");
    bufferViews = new List<GlTF_BufferView>();
    cameras = new List<GlTF_Camera>();
    lights = new List<GlTF_Light>();
    meshes = new List<GlTF_Mesh>();
    accessors = new List<GlTF_Accessor>();
    nodes = new Dictionary<string, GlTF_Node>();
    materials = new Dictionary<string, GlTF_Material>();
    samplers = new Dictionary<string, GlTF_Sampler>();
    textures = new Dictionary<string, GlTF_Texture>();
    images = new List<GlTF_Image>();
    techniques = new Dictionary<string, GlTF_Technique>();
    programs = new List<GlTF_Program>();
    shaders = new List<GlTF_Shader>();
    exportedFiles = new List<string>();
  }

  public void Indent() {
    for (int i = 0; i < indent; i++)
      jsonWriter.Write("\t");
  }

  public void IndentIn() {
    indent++;
    firsts[indent] = true;
  }

  public void IndentOut() {
    indent--;
  }

  public void CommaStart() {
    firsts[indent] = false;
  }

  public void CommaNL() {
    if (!firsts[indent])
      jsonWriter.Write(",\n");
    //		else
    //			jsonWriter.Write ("\n");
    firsts[indent] = false;
  }

  public string name; // name of this object

  public void OpenFiles(string filepath) {
    fs = File.Open(filepath, FileMode.Create);

    exportedFiles.Add(filepath);
    if (binary) {
      binWriter = new BinaryWriter(fs);
      binFile = fs;
      long offset = 20 + (b3dm ? B3DM_HEADER_SIZE : 0);
      fs.Seek(offset, SeekOrigin.Begin); // header skip
    } else {
      // separate bin file
      binFileName = Path.GetFileNameWithoutExtension(filepath) + ".bin";
      var binPath = Path.Combine(Path.GetDirectoryName(filepath), binFileName);
      binFile = File.Open(binPath, FileMode.Create);
      exportedFiles.Add(binPath);
    }

    jsonWriter = new StreamWriter(fs);
    jsonWriter.NewLine = "\n";
  }

  public void CloseFiles() {
    if (binary) {
      binWriter.Close();
    } else {
      binFile.Close();
    }

    jsonWriter.Close();
    fs.Close();
  }

  public virtual void Write() {
    bufferViews.Add(ushortBufferView);
    bufferViews.Add(floatBufferView);
    bufferViews.Add(vec2BufferView);
    bufferViews.Add(vec3BufferView);
    bufferViews.Add(vec4BufferView);

    ushortBufferView.bin = binary;
    floatBufferView.bin = binary;
    vec2BufferView.bin = binary;
    vec3BufferView.bin = binary;
    vec4BufferView.bin = binary;

    // write memory streams to binary file
    ushortBufferView.byteOffset = 0;
    floatBufferView.byteOffset = ushortBufferView.byteLength;
    vec2BufferView.byteOffset = floatBufferView.byteOffset + floatBufferView.byteLength;
    vec3BufferView.byteOffset = vec2BufferView.byteOffset + vec2BufferView.byteLength;
    vec4BufferView.byteOffset = vec3BufferView.byteOffset + vec3BufferView.byteLength;

    jsonWriter.Write("{\n");
    IndentIn();

    // asset
    CommaNL();
    Indent(); jsonWriter.Write("\"asset\": {\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"generator\": \"" + Generator + "\",\n");
    Indent(); jsonWriter.Write("\"version\": \"" + Version + "\",\n");
    Indent(); jsonWriter.Write("\"copyright\": \"" + Copyright + "\"\n");
    IndentOut();
    Indent(); jsonWriter.Write("}");

    if (!binary) {
      // FIX: Should support multiple buffers
      CommaNL();
      Indent(); jsonWriter.Write("\"buffers\": {\n");
      IndentIn();
      Indent(); jsonWriter.Write("\"" + Path.GetFileNameWithoutExtension(GlTF_Writer.binFileName) + "\": {\n");
      IndentIn();
      Indent(); jsonWriter.Write("\"byteLength\": " + (vec4BufferView.byteOffset + vec4BufferView.byteLength) + ",\n");
      Indent(); jsonWriter.Write("\"type\": \"arraybuffer\",\n");
      Indent(); jsonWriter.Write("\"uri\": \"" + GlTF_Writer.binFileName + "\"\n");

      IndentOut();
      Indent(); jsonWriter.Write("}\n");

      IndentOut();
      Indent(); jsonWriter.Write("}");
    } else {
      CommaNL();
      Indent(); jsonWriter.Write("\"buffers\": {\n");
      IndentIn();
      Indent(); jsonWriter.Write("\"binary_glTF\": {\n");
      IndentIn();
      Indent(); jsonWriter.Write("\"byteLength\": " + (vec4BufferView.byteOffset + vec4BufferView.byteLength) + ",\n");
      Indent(); jsonWriter.Write("\"type\": \"arraybuffer\"\n");

      IndentOut();
      Indent(); jsonWriter.Write("}\n");

      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    if (cameras != null && cameras.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"cameras\": {\n");
      IndentIn();
      foreach (GlTF_Camera c in cameras) {
        CommaNL();
        c.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    if (accessors != null && accessors.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"accessors\": {\n");
      IndentIn();
      foreach (GlTF_Accessor a in accessors) {
        CommaNL();
        a.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    if (bufferViews != null && bufferViews.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"bufferViews\": {\n");
      IndentIn();
      foreach (GlTF_BufferView bv in bufferViews) {
        if (bv.byteLength > 0) {
          CommaNL();
          bv.Write();
        }
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    if (meshes != null && meshes.Count > 0) {
      CommaNL();
      Indent();
      jsonWriter.Write("\"meshes\": {\n");
      IndentIn();
      foreach (GlTF_Mesh m in meshes) {
        CommaNL();
        m.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent();
      jsonWriter.Write("}");
    }

    if (shaders != null && shaders.Count > 0) {
      CommaNL();
      Indent();
      jsonWriter.Write("\"shaders\": {\n");
      IndentIn();
      foreach (var s in shaders) {
        CommaNL();
        s.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent();
      jsonWriter.Write("}");
    }

    if (programs != null && programs.Count > 0) {
      CommaNL();
      Indent();
      jsonWriter.Write("\"programs\": {\n");
      IndentIn();
      foreach (var p in programs) {
        CommaNL();
        p.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent();
      jsonWriter.Write("}");
    }

    if (techniques != null && techniques.Count > 0) {
      CommaNL();
      Indent();
      jsonWriter.Write("\"techniques\": {\n");
      IndentIn();
      foreach (KeyValuePair<string, GlTF_Technique> k in techniques) {
        CommaNL();
        k.Value.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent();
      jsonWriter.Write("}");
    }

    if (samplers.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"samplers\": {\n");
      IndentIn();
      foreach (KeyValuePair<string, GlTF_Sampler> s in samplers) {
        CommaNL();
        s.Value.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    if (textures.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"textures\": {\n");
      IndentIn();
      foreach (KeyValuePair<string, GlTF_Texture> t in textures) {
        CommaNL();
        t.Value.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    if (images.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"images\": {\n");
      IndentIn();
      foreach (var i in images) {
        CommaNL();
        i.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    if (materials.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"materials\": {\n");
      IndentIn();
      foreach (KeyValuePair<string, GlTF_Material> m in materials) {
        CommaNL();
        m.Value.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    if (nodes != null && nodes.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"nodes\": {\n");
      IndentIn();
      foreach (KeyValuePair<string, GlTF_Node> n in nodes) {
        CommaNL();
        n.Value.Write();
      }
      jsonWriter.WriteLine();
      IndentOut();
      Indent(); jsonWriter.Write("}");

    }
    CommaNL();

    Indent(); jsonWriter.Write("\"scene\": \"defaultScene\",\n");
    Indent(); jsonWriter.Write("\"scenes\": {\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"defaultScene\": {\n");
    IndentIn();
    CommaNL();
    Indent(); jsonWriter.Write("\"nodes\": [\n");
    IndentIn();
    foreach (KeyValuePair<string, GlTF_Node> n in nodes) {
      CommaNL();
      Indent(); jsonWriter.Write("\"" + n.Value.name + "\"");
    }
    jsonWriter.WriteLine();
    IndentOut();
    Indent(); jsonWriter.Write("],\n");

    Indent(); jsonWriter.Write("\"extras\": {\n");
    IndentIn();
    foreach (KeyValuePair<string, string> e in extras) {
      CommaNL();
      Indent(); jsonWriter.Write("\"" + e.Key + "\": \"" + e.Value + "\"");
    }
    jsonWriter.WriteLine();
    IndentOut();
    Indent(); jsonWriter.Write("}\n");  // end extras
    IndentOut();
    Indent(); jsonWriter.Write("}\n");  // end defaultScene
    IndentOut();
    Indent(); jsonWriter.Write("}");  // end scenes

    List<string> extUsed = new List<string>();
    if (binary) {
      extUsed.Add("KHR_binary_glTF");
    }
    var rtc = RTCCenter != null && RTCCenter.Length == 3;
    if (rtc) {
      extUsed.Add("CESIUM_RTC");
    }

    if (extUsed.Count > 0) {
      CommaNL();
      Indent(); jsonWriter.Write("\"extensionsUsed\": [\n");
      IndentIn();

      for (var i = 0; i < extUsed.Count; ++i) {
        CommaNL();
        Indent(); jsonWriter.Write("\"" + extUsed[i] + "\"");
      }

      jsonWriter.Write("\n");
      IndentOut();
      Indent(); jsonWriter.Write("]");

    }

    if (rtc) {
      CommaNL();
      Indent(); jsonWriter.Write("\"extensions\": {\n");
      IndentIn();
      Indent(); jsonWriter.Write("\"CESIUM_RTC\": {\n");
      IndentIn();
      Indent(); jsonWriter.Write("\"center\": [\n");
      IndentIn();
      for (var i = 0; i < 3; ++i) {
        CommaNL();
        Indent(); jsonWriter.Write(RTCCenter[i]);
      }
      jsonWriter.Write("\n");
      IndentOut();
      Indent(); jsonWriter.Write("]\n");
      IndentOut();
      Indent(); jsonWriter.Write("}\n");
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    jsonWriter.Write("\n");
    IndentOut();
    Indent(); jsonWriter.Write("}");

    jsonWriter.Flush();

    uint contentLength = 0;
    if (binary) {
      long curLen = fs.Position;
      var rem = curLen % 4;
      if (rem != 0) {
        // add padding if not aligned to 4 bytes
        var next = (curLen / 4 + 1) * 4;
        rem = next - curLen;
        for (int i = 0; i < rem; ++i) {
          jsonWriter.Write(" ");
        }
      }
      jsonWriter.Flush();

      // current pos - header size
      int offset = 20 + (b3dm ? B3DM_HEADER_SIZE : 0);
      contentLength = (uint) (fs.Position - offset);
    }


    ushortBufferView.memoryStream.WriteTo(binFile);
    floatBufferView.memoryStream.WriteTo(binFile);
    vec2BufferView.memoryStream.WriteTo(binFile);
    vec3BufferView.memoryStream.WriteTo(binFile);
    vec4BufferView.memoryStream.WriteTo(binFile);

    binFile.Flush();
    if (binary) {
      uint fileLength = (uint) fs.Length;

      // write header
      fs.Seek(0, SeekOrigin.Begin);

      if (b3dm) {
        jsonWriter.Write("b3dm"); // magic
        jsonWriter.Flush();
        binWriter.Write(1); // version
        binWriter.Write(fileLength);
        binWriter.Write(0); // batchTableJSONByteLength
        binWriter.Write(0); // batchTableBinaryByteLength
        binWriter.Write(0); // batchLength
        binWriter.Flush();
      }

      jsonWriter.Write("glTF"); // magic
      jsonWriter.Flush();
      binWriter.Write(1); // version
      uint l = (uint) (fileLength - (b3dm ? B3DM_HEADER_SIZE : 0)); // min b3dm header
      binWriter.Write(l);
      binWriter.Write(contentLength);
      binWriter.Write(0); // format
      binWriter.Flush();
    }
  }

  // Returns existing technique based on name of object.
  public static GlTF_Technique GetTechnique(GameObject namedObject) {
    var name = GlTF_Technique.GetNameFromObject(namedObject);
    Debug.Assert(techniques.ContainsKey(name));
    return techniques[name];
  }

  // Creates new technique based on name of object.
  public static GlTF_Technique CreateTechnique(GameObject namedObject) {
    var name = GlTF_Technique.GetNameFromObject(namedObject);
    Debug.Assert(!techniques.ContainsKey(name));
    var ret = new GlTF_Technique();
    ret.name = name;
    techniques.Add(name, ret);
    return ret;
  }


  private static string OrUnknown(string str) { return str != null ? str : "Unknown."; }

  private const int B3DM_HEADER_SIZE = 24;
  private static bool[] firsts = new bool[100];

  private string copyright;
  private string generator;
  private string version;


}
