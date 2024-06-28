using UnityEngine;
using System.Collections;

public class GlTF_ColorRGBA : GlTF_Writer
{
    private Color color;
    public GlTF_ColorRGBA(string n)
    {
        name = n;
    }
    public GlTF_ColorRGBA(Color c)
    {
        color = c;
    }
    public GlTF_ColorRGBA(string n, Color c)
    {
        name = n; color = c;
    }
    public override void Write()
    {
        Indent();
        if (name.Length > 0)
            jsonWriter.Write("\"" + name + "\": [");
        else
            jsonWriter.Write("\"color\": [");
        jsonWriter.Write(color.r.ToString() + ", " + color.g.ToString() + ", " + color.b.ToString() + ", " + color.a.ToString() + "]");
    }
}
