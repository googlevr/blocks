using UnityEngine;
using System.Collections;

public class GlTF_Texture : GlTF_Writer {
  /*
        "texture_O21_jpg": {
            "format": 6408,
            "internalFormat": 6408,
            "sampler": "sampler_0",
            "source": "O21_jpg",
            "target": 3553,
            "type": 5121
        },
*/
  public int format = 6408;
  public int internalFormat = 6408;
  public string samplerName;
  public string source;
  public int target = 3553;
  public int tType = 5121;

  public static string GetNameFromObject(Object o) {
    return "texture_" + GlTF_Writer.GetNameFromObject(o, false);
  }

  public override void Write() {
    Indent(); jsonWriter.Write("\"" + name + "\": {\n");
    IndentIn();
    Indent(); jsonWriter.Write("\"format\": " + format + ",\n");
    Indent(); jsonWriter.Write("\"internalFormat\": " + internalFormat + ",\n");
    Indent(); jsonWriter.Write("\"sampler\": \"" + samplerName + "\",\n");
    Indent(); jsonWriter.Write("\"source\": \"" + source + "\",\n");
    Indent(); jsonWriter.Write("\"target\": " + target + ",\n");
    Indent(); jsonWriter.Write("\"type\": " + tType + "\n");
    IndentOut();
    Indent(); jsonWriter.Write("}");
  }
}
