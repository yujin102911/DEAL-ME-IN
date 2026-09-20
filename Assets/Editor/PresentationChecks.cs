using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NumberTable;

public static class PresentationChecks
{
    public static void Run()
    {
        try {
            Debug.Log(DealerChecks.Run());
            Debug.Log(TableProjectSetup.RunCoreChecks());
            Debug.Log("PRESENTATION_COMPILE_AND_RULES_PASS");
            EditorApplication.Exit(0);
        } catch(System.Exception e) {Debug.LogException(e);EditorApplication.Exit(1);}
    }
    [MenuItem("Tools/Number Table/Preview Presentation")]
    public static void Preview()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/NumberTable.unity");
        MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost.StartAutoConnect();
        EditorApplication.isPlaying=true;
    }
}

