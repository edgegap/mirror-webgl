// Optional Edgegap matchmaking, see "Edgegap Matchmaker (optional)" in README.md.
// EDGEGAP_SDK is defined by Edgegap.Tanks.Matchmaking.asmdef when the
// com.edgegap.unity-sdk package is installed. Without it this file compiles to a stub.
#if EDGEGAP_SDK
using System;
using System.Collections;
using System.Collections.Generic;
using Edgegap;
using Edgegap.Matchmaking;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using L = Edgegap.Logger;

/// <summary>
/// Finds a match with the Edgegap Matchmaker (Group Up flow, solo group) and connects the
/// Mirror client to the assigned deployment over secure WebSockets.
/// Works in WebGL builds, the Editor and desktop clients. Disabled on the dedicated server.
/// </summary>
[DisallowMultipleComponent]
public class TanksMatchmakingClient : MonoBehaviour
{
    [Header("Matchmaker Instance")]
    [Tooltip("API URL from the matchmaker dashboard, e.g. https://abc123.edgegap.net")]
    public string BaseUrl;

    [Tooltip("Auth Token from the matchmaker dashboard. This token is shipped in the client build.")]
    public string AuthToken;

    [Tooltip("Profile name from your matchmaker configuration.")]
    public string Profile = "tanks-webgl";

    [Header("Game Server Connection")]
    [Tooltip("Port name defined in your Edgegap app version (WS protocol, TLS Upgrade enabled).")]
    public string PortName = "gameport";

    [Tooltip("Use wss:// (required for TLS Upgrade ports and for pages served over HTTPS).")]
    public bool UseSecureWebSocket = true;

    [Tooltip("The server may still be booting when the assignment arrives, so retry.")]
    public int ConnectionAttempts = 10;
    public float ConnectionRetryDelaySeconds = 2f;
    public float ConnectionAttemptTimeoutSeconds = 10f;

    [Header("Latency Beacons")]
    [Tooltip(
        "Measure ping to Edgegap beacons and send it in tickets. Requires a `latencies` rule in the profile. "
            + "Always off in WebGL builds: browsers cannot send ICMP pings."
    )]
    public bool UseLatencyBeacons = false;

    [Header("Exponential Retry")]
    public int RequestTimeoutSeconds = 3;
    public float PollingBackoffSeconds = 1f;
    public int MaxConsecutivePollingErrors = 10;

    [Header("Automatic Cleanup")]
    public float RemoveGroupSeconds = 30f;

    [Header("UI")]
    public bool ShowGUI = true;

    [Tooltip("Hide Mirror's Host/Client/Server HUD outside the Editor so players only see matchmaking.")]
    public bool HideNetworkManagerHUDInBuilds = false;

    [Header("Logging")]
    public bool LogGroupUpdates = true;
    public bool LogPollingUpdates = false;

    public GroupClient<TanksGroupUpRequestDTO, TanksTicketAttributes> MatchmakingClient
    {
        get;
        private set;
    }

    public string Status { get; private set; } = "Matchmaker not initialized.";
    public bool IsMatchmakerHealthy { get; private set; }
    public bool IsSearching { get; private set; }
    public bool IsConnecting { get; private set; }

    private Coroutine connectRoutine;

    #region Lifecycle
    private void Start()
    {
#if UNITY_SERVER
        bool isServerBuild = true;
#else
        bool isServerBuild = Application.isBatchMode;
#endif
        if (isServerBuild)
        {
            // dedicated server never matchmakes, see TanksMatchmakingServer instead
            enabled = false;
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (UseLatencyBeacons)
        {
            L.Warn("MM Tanks | Latency beacons are not available in WebGL, disabling.");
            UseLatencyBeacons = false;
        }
#endif

        if (HideNetworkManagerHUDInBuilds && !Application.isEditor)
        {
            NetworkManagerHUD hud = FindAnyObjectByType<NetworkManagerHUD>();
            if (hud != null)
                hud.enabled = false;
        }

        if (string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrWhiteSpace(AuthToken))
        {
            Status = "Set matchmaker Base URL and Auth Token.";
            L.Error($"MM Tanks | {Status}");
            return;
        }

        MatchmakingClient = new GroupClient<TanksGroupUpRequestDTO, TanksTicketAttributes>(
            this,
            BaseUrl.Trim().TrimEnd('/'),
            AuthToken.Trim(),
            RequestTimeoutSeconds,
            PollingBackoffSeconds,
            MaxConsecutivePollingErrors,
            RemoveGroupSeconds,
            LogGroupUpdates,
            LogPollingUpdates
        );

        Status = "Checking matchmaker...";
        MatchmakingClient.Initialize(OnMonitorUpdate, OnGroupUpdate);
    }

    private void OnApplicationQuit()
    {
        // Best effort only: browsers may close the tab without calling this.
        // Unmatched tickets also expire after the profile's ticket_expiration_period.
        if (IsSearching)
            MatchmakingClient?.StopMatchmaking();
    }
    #endregion

    #region Public API
    public void FindMatch()
    {
        if (MatchmakingClient == null || !IsMatchmakerHealthy || IsSearching || IsConnecting)
            return;

        if (NetworkClient.active)
        {
            L.Warn("MM Tanks | Already connected to a server, disconnect before matchmaking.");
            return;
        }

        IsSearching = true;

        if (!UseLatencyBeacons)
        {
            CreateTicket(null);
            return;
        }

        Status = "Measuring latency...";
        MatchmakingClient.Beacons(
            (BeaconsResponseDTO beacons) =>
            {
                MatchmakingClient.MeasureBeaconsRoundTripTime(beacons.Beacons, CreateTicket);
            },
            (string error, UnityWebRequest request) =>
            {
                IsSearching = false;
                Status = "Couldn't retrieve latency beacons.";
            }
        );
    }

    public void CancelMatchmaking()
    {
        if (MatchmakingClient == null || !IsSearching)
            return;

        Status = "Cancelling...";
        MatchmakingClient.StopMatchmaking(() =>
        {
            GroupUpResponseDTO current = MatchmakingClient.Group.Current;
            if (current?.GroupID != null)
            {
                // Delete was rejected (409) because a match was already found. The SDK stopped
                // polling, so resume to receive the assignment instead of stranding the player.
                Status = "Match already found, joining...";
                MatchmakingClient.ResumeMatchmaking(current, abandon: true);
                return;
            }

            IsSearching = false;
            Status = "Matchmaking cancelled.";
        });
    }

    public void RetryMatchmakerStatus()
    {
        if (MatchmakingClient == null)
            return;
        Status = "Checking matchmaker...";
        MatchmakingClient.Status();
    }
    #endregion

    #region Matchmaker Callbacks
    private void CreateTicket(Dictionary<string, float> beacons)
    {
        Status = "Searching for a match...";

        // abandon: true makes the SDK clean up any previous group first. Without it the first
        // call is rejected, because the SDK's Observable initializes Group.Current to an empty
        // object rather than null.
        MatchmakingClient.CreateGroup(
            new TanksGroupUpRequestDTO(Profile, beacons, isReady: true),
            abandon: true
        );
    }

    private void OnMonitorUpdate(
        Observable<MonitorResponseDTO> monitor,
        ObservableActionType action,
        string message
    )
    {
        if (action == ObservableActionType.Update && message == "healthy")
        {
            IsMatchmakerHealthy = true;
            Status = "Ready to matchmake.";
        }
        else if (action == ObservableActionType.Update || action == ObservableActionType.Error)
        {
            IsMatchmakerHealthy = false;
            Status = "Matchmaker unavailable.";
            if (IsSearching)
                MatchmakingClient.StopMatchmaking(() => IsSearching = false);
        }
    }

    private void OnGroupUpdate(
        Observable<GroupUpResponseDTO> group,
        ObservableActionType action,
        string message
    )
    {
        if (action == ObservableActionType.Error)
        {
            IsSearching = false;
            Status = "Matchmaking failed, try again.";
            return;
        }

        if (action != ObservableActionType.Update)
            return;

        GroupUpResponseDTO current = group.Current;

        // group abandoned or removed after assignment expiry
        if (current?.GroupID == null)
        {
            if (!IsConnecting && !NetworkClient.active)
                IsSearching = false;
            return;
        }

        switch (current.Status)
        {
            case "SEARCHING":
                Status = "Searching for players...";
                break;
            case "TEAM_FOUND":
                Status = "Team found...";
                break;
            case "MATCH_FOUND":
                Status = "Match found, starting server...";
                break;
            case "HOST_ASSIGNED":
                IsSearching = false;
                ConnectToAssignment(current.Assignment);
                break;
            case "CANCELLED":
                IsSearching = false;
                Status = "No match found in time, try again.";
                break;
        }
    }
    #endregion

    #region Mirror Connection
    private void ConnectToAssignment(DeploymentDTO assignment)
    {
        if (assignment?.Ports == null || !assignment.Ports.TryGetValue(PortName, out PortMappingDTO port))
        {
            Status = $"Assignment has no port named '{PortName}'.";
            L.Error($"MM Tanks | {Status}\n{assignment}");
            return;
        }

        if (!int.TryParse(port.External, out int externalPort))
        {
            Status = "Assignment has an invalid external port.";
            L.Error($"MM Tanks | {Status}\n{port}");
            return;
        }

        // Every deployment gets its own external port, so connect by URI instead of relying on
        // SimpleWebTransport's ClientPortOption (that setting only covers fixed ports).
        Uri uri = new UriBuilder
        {
            Scheme = UseSecureWebSocket ? "wss" : "ws",
            Host = assignment.Fqdn,
            Port = externalPort,
        }.Uri;

        if (connectRoutine != null)
            StopCoroutine(connectRoutine);
        connectRoutine = StartCoroutine(ConnectWithRetry(uri));
    }

    private IEnumerator ConnectWithRetry(Uri uri)
    {
        IsConnecting = true;
        NetworkManager manager = NetworkManager.singleton;

        for (int attempt = 1; attempt <= ConnectionAttempts; attempt++)
        {
            Status = $"Joining server (attempt {attempt}/{ConnectionAttempts})...";
            L.Log($"MM Tanks | Connecting to {uri} (attempt {attempt}).");

            manager.StartClient(uri);

            float started = Time.realtimeSinceStartup;
            yield return new WaitUntil(() =>
                !NetworkClient.isConnecting
                || Time.realtimeSinceStartup - started > ConnectionAttemptTimeoutSeconds
            );

            if (NetworkClient.isConnected)
            {
                IsConnecting = false;
                Status = "In match.";
                connectRoutine = null;
                yield break;
            }

            // reset client state before the next attempt
            if (NetworkClient.active)
                manager.StopClient();

            yield return new WaitForSeconds(ConnectionRetryDelaySeconds);
        }

        IsConnecting = false;
        connectRoutine = null;
        Status = "Couldn't connect to the game server.";
        L.Error($"MM Tanks | Failed to connect to {uri} after {ConnectionAttempts} attempts.");
    }
    #endregion

    #region GUI
    private void OnGUI()
    {
        if (!ShowGUI || NetworkClient.isConnected || NetworkServer.active)
            return;

        const float width = 260f;
        GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f, width, 200f), GUI.skin.box);
        GUILayout.Label("Edgegap Matchmaking");
        GUILayout.Label(Status);

        if (MatchmakingClient != null)
        {
            if (!IsMatchmakerHealthy)
            {
                if (GUILayout.Button("Retry"))
                    RetryMatchmakerStatus();
            }
            else if (IsSearching)
            {
                if (GUILayout.Button("Cancel"))
                    CancelMatchmaking();
            }
            else if (!IsConnecting)
            {
                if (GUILayout.Button("Find Match"))
                    FindMatch();
            }
        }

        GUILayout.EndArea();
    }
    #endregion
}
#else
using UnityEngine;

// Keeps the scene reference valid when the Edgegap SDK isn't installed.
[DisallowMultipleComponent]
public class TanksMatchmakingClient : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning(
            "TanksMatchmakingClient: the com.edgegap.unity-sdk package isn't installed, matchmaking is off. "
                + "See \"Edgegap Matchmaker (optional)\" in README.md."
        );
        enabled = false;
    }
}
#endif
