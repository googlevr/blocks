using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GlTF_Node : GlTF_Writer
{
    public string cameraName;
    public bool hasParent = false;
    public List<string> childrenNames = new List<string>();
    public bool uniqueItems = true;
    public string lightName;
    public List<string> bufferViewNames = new List<string>();
    public List<string> indexNames = new List<string>();
    public List<string> accessorNames = new List<string>();
    public List<string> meshNames = new List<string>();
    public GlTF_Matrix matrix;
    //	public GlTF_Mesh mesh;
    public GlTF_Rotation rotation;
    public GlTF_Scale scale;
    public GlTF_Translation translation;
    public bool additionalProperties = false;

    public static string GetNameFromObject(Object o)
    {
        return "node_" + GlTF_Writer.GetNameFromObject(o, false);
    }

    public override void Write()
    {
        Indent();
        jsonWriter.Write("\"" + name + "\": {\n");
        IndentIn();
        Indent();
        jsonWriter.Write("\"name\": \"" + name + "\",\n");
        if (cameraName != null)
        {
            CommaNL();
            Indent();
            jsonWriter.Write("\"camera\": \"" + cameraName + "\"");
        }
        else if (lightName != null)
        {
            CommaNL();
            Indent();
            jsonWriter.Write("\"light\": \"" + lightName + "\"");
        }
        else if (meshNames.Count > 0)
        {
            CommaNL();
            Indent();
            jsonWriter.Write("\"meshes\": [\n");
            IndentIn();
            foreach (string m in meshNames)
            {
                CommaNL();
                Indent(); jsonWriter.Write("\"" + m + "\"");
            }
            jsonWriter.WriteLine();
            IndentOut();
            Indent(); jsonWriter.Write("]");
        }

        if (childrenNames != null && childrenNames.Count > 0)
        {
            CommaNL();
            Indent(); jsonWriter.Write("\"children\": [\n");
            IndentIn();
            foreach (string ch in childrenNames)
            {
                CommaNL();
                Indent(); jsonWriter.Write("\"" + ch + "\"");
            }
            jsonWriter.WriteLine();
            IndentOut();
            Indent(); jsonWriter.Write("]");
        }
        if (matrix != null)
        {
            CommaNL();
            matrix.Write();
        }
        if (translation != null && (translation.items[0] != 0f || translation.items[1] != 0f || translation.items[2] != 0f))
        {
            CommaNL();
            translation.Write();
        }
        if (scale != null && (scale.items[0] != 1f || scale.items[1] != 1f || scale.items[2] != 1f))
        {
            CommaNL();
            scale.Write();
        }
        if (rotation != null && (rotation.items[0] != 0f || rotation.items[1] != 0f || rotation.items[2] != 0f || rotation.items[3] != 0f))
        {
            CommaNL();
            rotation.Write();
        }
        jsonWriter.WriteLine();
        IndentOut();
        Indent(); jsonWriter.Write("}");
    }
}
