// ListAllScripts.cs
using UnityEngine;
using UnityEditor;
using System.IO;

public static class ListAllScripts
{
    [MenuItem("Tools/List All Scripts In Project/Scripts Folder")]
    public static void ListScripts()
    {
        // Adjust this path if your scripts live in another folder
        string root = Application.dataPath + "/Script";

        if (!Directory.Exists(root))
        {
            Debug.LogError($"No folder found at: {root}");
            return;
        }

        string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

        Debug.Log($"--- Found {files.Length} scripts in {root} ---");

        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            Debug.Log(name);
        }
    }
}