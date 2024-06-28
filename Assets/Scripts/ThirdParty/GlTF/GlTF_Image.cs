using UnityEngine;
using System.Collections;

public class GlTF_Image : GlTF_Writer
{
    public string uri;

    public static string GetNameFromObject(Object o)
    {
        return "image_" + GlTF_Writer.GetNameFromObject(o, false);
    }

    public override void Write()
    {
        Indent(); jsonWriter.Write("\"" + name + "\": {\n");
        IndentIn();
        Indent(); jsonWriter.Write("\"uri\": \"" + uri + "\"\n");
        IndentOut();
        Indent(); jsonWriter.Write("}");
    }
}
