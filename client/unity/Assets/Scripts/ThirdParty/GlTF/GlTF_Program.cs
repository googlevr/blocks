using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GlTF_Program : GlTF_Writer {
  public List<string> attributes = new List<string>();
  public string vertexShader = "";
  public string fragmentShader = "";

  public static string GetNameFromObject(Object o) {
    return "program_" + GlTF_Writer.GetNameFromObject(o);
  }

  public override void Write() {
    Indent(); jsonWriter.Write("\"" + name + "\": {\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"attributes\": [\n");
    IndentIn();
    foreach (var a in attributes) {
      CommaNL();
      Indent(); jsonWriter.Write("\"" + a + "\"");
    }
    Indent(); jsonWriter.Write("\n");
    IndentOut();
    Indent(); jsonWriter.Write("],\n");
    Indent(); jsonWriter.Write("\"vertexShader\": \"" + vertexShader + "\",\n");
    Indent(); jsonWriter.Write("\"fragmentShader\": \"" + fragmentShader + "\"\n");
    IndentOut();
    Indent(); jsonWriter.Write("}");
  }
}
