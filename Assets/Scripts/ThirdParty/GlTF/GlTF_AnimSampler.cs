using UnityEngine;
using System.Collections;

public class GlTF_AnimSampler : GlTF_Writer
{
    public string input = "TIME";
    public string interpolation = "LINEAR"; // only things in glTF as of today
    public string output = "translation"; // or whatever

    public GlTF_AnimSampler(string n, string o)
    {
        name = n; output = o;
    }

    public override void Write()
    {
        Indent(); jsonWriter.Write("\"" + name + "\": {\n");
        IndentIn();
        Indent(); jsonWriter.Write("\"input\": \"" + input + "\",\n");
        Indent(); jsonWriter.Write("\"interpolation\": \"" + interpolation + "\",\n");
        Indent(); jsonWriter.Write("\"output\": \"" + output + "\"\n");
        IndentOut();
        Indent(); jsonWriter.Write("}");
    }
}
