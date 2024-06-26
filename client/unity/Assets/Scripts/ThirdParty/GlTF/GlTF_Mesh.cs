using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GlTF_Mesh : GlTF_Writer {
  public List<GlTF_Primitive> primitives;

  public GlTF_Mesh() {
    primitives = new List<GlTF_Primitive>();
  }

  public static string GetNameFromObject(Object o) {
    return "mesh_" + GlTF_Writer.GetNameFromObject(o, true);
  }

  public void Populate(Mesh m) {
    if (primitives.Count > 0) {
      // only populate first attributes because the data are shared between primitives
      primitives[0].attributes.Populate(m);
    }

    foreach (GlTF_Primitive p in primitives) {
      p.Populate(m);
    }
  }

  public override void Write() {
    Indent(); jsonWriter.Write("\"" + name + "\": {\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"name\": \"" + name + "\",\n");
    Indent(); jsonWriter.Write("\"primitives\": [\n");
    IndentIn();
    foreach (GlTF_Primitive p in primitives) {
      CommaNL();
      Indent(); jsonWriter.Write("{\n");
      p.Write();
      Indent(); jsonWriter.Write("}");
    }
    jsonWriter.WriteLine();
    IndentOut();
    Indent(); jsonWriter.Write("]\n");
    IndentOut();
    Indent(); jsonWriter.Write("}");
  }
}
