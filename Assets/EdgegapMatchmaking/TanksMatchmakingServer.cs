// Optional Edgegap matchmaking, see "Edgegap Matchmaker (optional)" in README.md.
// EDGEGAP_SDK is defined by Edgegap.Tanks.Matchmaking.asmdef when the
// com.edgegap.unity-sdk package is installed. Without it this file compiles to a stub.
#if EDGEGAP_SDK
using System;
using System.Collections;
using Edgegap;
using Edgegap.Matchmaking;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using L = Edgegap.Logger;

/// <summary>
/// Runs on the dedicated server only. Reads deployment and matchmaker variables injected by
/// Edgegap, and stops its own deployment once the match is over so it doesn't keep running.
/// </summary>
[DisallowMultipleComponent]
public class TanksMatchmakingServer : MonoBehaviour
{
    [Tooltip("Stop the deployment if nobody connects within this time (matched players gave up).")]
    public float FirstPlayerTimeoutSeconds = 120f;

    [Tooltip("Stop the deployment once all players have been gone for this long.")]
    public float EmptyServerTimeoutSeconds = 30f;

    public DeploymentEnvironmentDTO DeploymentEnv { get; private set; }
    public MatchEnvironmentDTO<TanksTicketAttributes> MatchEnv { get; private set; }

    private SafeHttpRequest request;
    private float startedAt;
    private float emptySince = -1f;
    private bool anyPlayerJoined;
    private bool stopping;

    private void Start()
    {
#if UNITY_SERVER
        bool isServerBuild = true;
#else
        bool isServerBuild = Application.isBatchMode;
#endif
        if (!isServerBuild)
        {
            enabled = false;
            return;
        }

        IDictionary env = Environment.GetEnvironmentVariables();

        // Check before parsing: the SDK's DeploymentEnvironmentDTO throws when
        // ARBITRIUM_PORTS_MAPPING is missing, which is the case outside Edgegap.
        if (string.IsNullOrEmpty(env["ARBITRIUM_REQUEST_ID"] as string))
        {
            L.Warn("MM Tanks Server | Not running on Edgegap, self-stop disabled.");
            enabled = false;
            return;
        }

        DeploymentEnv = new DeploymentEnvironmentDTO(env);

        MatchEnv = new MatchEnvironmentDTO<TanksTicketAttributes>(env);
        request = new SafeHttpRequest(this);
        startedAt = Time.realtimeSinceStartup;

        int expectedPlayers = MatchEnv.TicketIds?.Count ?? 0;
        L.Log(
            $"MM Tanks Server | Deployment '{DeploymentEnv.RequestID}' started for match "
                + $"'{MatchEnv.MatchId ?? "n/a"}', profile '{MatchEnv.MatchProfile ?? "n/a"}', "
                + $"{expectedPlayers} matched player(s)."
        );
    }

    private void Update()
    {
        if (stopping || !NetworkServer.active)
            return;

        float now = Time.realtimeSinceStartup;

        if (NetworkServer.connections.Count > 0)
        {
            anyPlayerJoined = true;
            emptySince = -1f;
            return;
        }

        if (!anyPlayerJoined)
        {
            if (now - startedAt > FirstPlayerTimeoutSeconds)
                SelfStop("no player connected in time");
            return;
        }

        if (emptySince < 0f)
            emptySince = now;
        else if (now - emptySince > EmptyServerTimeoutSeconds)
            SelfStop("all players left");
    }

    public void SelfStop(string reason)
    {
        if (stopping)
            return;
        stopping = true;

        L.Log($"MM Tanks Server | Stopping deployment: {reason}.");

        if (
            string.IsNullOrEmpty(DeploymentEnv.SelfStopURL)
            || string.IsNullOrEmpty(DeploymentEnv.SelfStopToken)
        )
        {
            L.Error("MM Tanks Server | Self-stop URL or token missing, quitting process instead.");
            Application.Quit();
            return;
        }

        request.Delete(
            DeploymentEnv.SelfStopURL,
            DeploymentEnv.SelfStopToken,
            (string response, UnityWebRequest req) =>
            {
                L.Log("MM Tanks Server | Self-stop requested.");
            },
            (string error, UnityWebRequest req) =>
            {
                L.Error($"MM Tanks Server | Self-stop failed, quitting process.\n{error}");
                Application.Quit();
            },
            new RetryParameters { MaxAttempts = 10, RemainingAttempts = 10 }
        );
    }
}
#else
using UnityEngine;

// Keeps the scene reference valid when the Edgegap SDK isn't installed.
[DisallowMultipleComponent]
public class TanksMatchmakingServer : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning(
            "TanksMatchmakingServer: the com.edgegap.unity-sdk package isn't installed, matchmaking is off. "
                + "See \"Edgegap Matchmaker (optional)\" in README.md."
        );
        enabled = false;
    }
}
#endif
