using UnityEditor;
using UnityEngine;

/// Runs the AI test bench (AiScenarios) from the command line, Unity closed: python3 Tools/setup.py --ai-scenarios
/// (Unity in batch mode without -quit: the play session must run). Exit code: number of failed scenarios (99 on time-out).
/// Pending imports are done first and script reloads are locked during the session: a reload in the middle would
/// empty the team and lose the request. The request survives a reload before the session (SessionState).
[InitializeOnLoad]
public static class AiScenarioBatch
{
    const string Pending = "AiScenarioBatch.Pending", StartedAt = "AiScenarioBatch.StartedAt";
    const double TimeOut = 300.0, Quiet = 2.0; // seconds; editor idle time required before playing

    static double idleSince = -1.0;

    static AiScenarioBatch()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Watch;
    }

    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        SessionState.SetFloat(StartedAt, (float)EditorApplication.timeSinceStartup);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
    }

    static void Watch()
    {
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAt, 0f) > TimeOut)
        {
            Debug.LogError("AISCENARIO TIMEOUT");
            Finish(99);
            return;
        }
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Waits until nothing compiles or imports for a moment, then starts the session.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                idleSince = -1.0;
                return;
            }
            if (idleSince < 0.0) idleSince = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - idleSince < Quiet) return;
            EditorApplication.LockReloadAssemblies();
            AiScenarios.Requested = true;
            EditorApplication.EnterPlaymode();
            return;
        }
        if (AiScenarios.Finished) Finish(AiScenarios.Failures);
    }

    static void Finish(int code)
    {
        EditorApplication.update -= Watch;
        SessionState.EraseBool(Pending);
        AiScenarios.Requested = false;
        EditorApplication.UnlockReloadAssemblies();
        Debug.Log($"AISCENARIO EXIT {code}");
        EditorApplication.Exit(code);
    }
}
