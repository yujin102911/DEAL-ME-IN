using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NumberTable;

public static class PresentationChecks
{
    // Batch Play mode verification survives the domain reload on entering Play.
    private const string BatchKey="NumberTable.PresentationBatch";
    private static double deadline;
    private static bool started;
    public static void RunPlay()
    {
        Debug.Log(DealerChecks.Run());
        Debug.Log(TableProjectSetup.RunCoreChecks());
        SessionState.SetBool(BatchKey,true);
        EditorSceneManager.OpenScene("Assets/Scenes/NumberTable.unity");
        EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    private static void ResumeBatch()
    {
        if(!SessionState.GetBool(BatchKey,false))return;
        deadline=EditorApplication.timeSinceStartup+120;started=false;
        EditorApplication.update+=PollBatch;
    }
    private static void PollBatch()
    {
        if(EditorApplication.timeSinceStartup>deadline){EndBatch(1,"Presentation batch timed out");return;}
        if(!EditorApplication.isPlaying||TableView.Instance==null)return;
        if(!started){started=true;PresentationPlayChecks.Start();}
        if(PresentationPlayChecks.Status.StartsWith("PASS"))EndBatch(0,PresentationPlayChecks.Status);
        else if(PresentationPlayChecks.Status.StartsWith("FAIL"))EndBatch(1,PresentationPlayChecks.Status);
    }
    private static void EndBatch(int code,string message)
    {
        SessionState.SetBool(BatchKey,false);EditorApplication.update-=PollBatch;
        Debug.Log(message);EditorApplication.Exit(code);
    }
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
