using UnityEngine;
using System.Collections;

public class GlTF_Target : GlTF_Writer
{
    public string id;
    public string path;
    public override void Write()
    {
        Indent(); jsonWriter.Write("\"" + "target" + "\": {\n");
        //		Indent();		jsonWriter.Write ("\"" + name + "\": {\n");
        IndentIn();
        Indent(); jsonWriter.Write("\"id\": \"" + id + "\",\n");
        Indent(); jsonWriter.Write("\"path\": \"" + path + "\"\n");
        IndentOut();
        Indent(); jsonWriter.Write("}");
    }
}
