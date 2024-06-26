using UnityEngine;
using System.Collections;

public class GlTF_Primitive : GlTF_Writer {
  public GlTF_Attributes attributes = new GlTF_Attributes();
  public GlTF_Accessor indices;
  public string materialName;
  public int primitive = 4;
  public int semantics = 4;
  public int index = 0;

  public static string GetNameFromObject(Object o, int index) {
    return "primitive_" + index + "_" + GlTF_Writer.GetNameFromObject(o, false);
  }

  public void Populate(Mesh m) {
    if (m.GetTopology(index) == MeshTopology.Triangles) {
      indices.PopulateUshort(m.GetTriangles(index), true);
    }
  }

  public override void Write() {
    IndentIn();
    CommaNL();
    if (attributes != null)
      attributes.Write();
    CommaNL();
    Indent(); jsonWriter.Write("\"indices\": \"" + indices.name + "\",\n");
    Indent(); jsonWriter.Write("\"material\": \"" + materialName + "\",\n");
    Indent(); jsonWriter.Write("\"mode\": " + primitive + "\n");
    // semantics
    IndentOut();
  }
}
