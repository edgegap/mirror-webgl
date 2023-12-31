using Mirror;
using Mirror.SimpleWeb;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Network/Network Manager HUD")]
[RequireComponent(typeof(NetworkManager))]
[HelpURL("https://mirror-networking.gitbook.io/docs/components/network-manager-hud")]
public class MyNetworkManagerHUD : MonoBehaviour
{
    NetworkManager manager;
    SimpleWebTransport webTransport;

    public int offsetX;
    public int offsetY;

    void Awake()
    {
        manager = GetComponent<NetworkManager>();
        webTransport = (SimpleWebTransport)manager.transport;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10 + offsetX, 40 + offsetY, 250, 9999));
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            StartButtons();
        }
        else
        {
            StatusLabels();
        }

        // client ready
        if (NetworkClient.isConnected && !NetworkClient.ready)
        {
            if (GUILayout.Button("Client Ready"))
            {
                NetworkClient.Ready();
                if (NetworkClient.localPlayer == null)
                {
                    NetworkClient.AddPlayer();
                }
            }
        }

        StopButtons();

        GUILayout.EndArea();
    }

    void StartButtons()
    {
        if (!NetworkClient.active)
        {
            // Server + Client
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                if (GUILayout.Button("Host (Server + Client)"))
                {
                    manager.StartHost();
                }
            }

            GUILayout.Space(15);

            // Client + IP
            GUILayout.BeginHorizontal();
            GUILayout.Label("Address: ", GUILayout.ExpandWidth(false));
            // This updates networkAddress every frame from the TextField
            manager.networkAddress = GUILayout.TextField(manager.networkAddress);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port: ", GUILayout.ExpandWidth(false));
            ushort portValue;

            if (ushort.TryParse(GUILayout.TextField(webTransport.Port.ToString()), out portValue))
            {
                webTransport.Port = portValue;
            }
            else
            {
                webTransport.Port = 7778;
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Client"))
            {
                manager.StartClient();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // Server Only
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // cant be a server in webgl build
                GUILayout.Box("(  WebGL cannot be server  )");
            }
            else
            {
                if (GUILayout.Button("Server Only")) manager.StartServer();
            }
        }
        else
        {
            // Connecting
            GUILayout.Label($"Connecting to {manager.networkAddress}..");
            if (GUILayout.Button("Cancel Connection Attempt"))
            {
                manager.StopClient();
            }
        }
    }

    void StatusLabels()
    {
        // host mode
        // display separately because this always confused people:
        //   Server: ...
        //   Client: ...
        if (NetworkServer.active && NetworkClient.active)
        {
            GUILayout.Label($"<b>Host</b>: running via {Transport.active}");
        }
        // server only
        else if (NetworkServer.active)
        {
            GUILayout.Label($"<b>Server</b>: running via {Transport.active}");
        }
        // client only
        else if (NetworkClient.isConnected)
        {
            GUILayout.Label($"<b>Client</b>: connected to {manager.networkAddress} via {Transport.active}");
        }
    }

    void StopButtons()
    {
        // stop host if host mode
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop Host"))
            {
                manager.StopHost();
            }
            if (GUILayout.Button("Stop Client"))
            {
                manager.StopClient();
            }
            GUILayout.EndHorizontal();
        }
        // stop client if client-only
        else if (NetworkClient.isConnected)
        {
            if (GUILayout.Button("Stop Client"))
            {
                manager.StopClient();
            }
        }
        // stop server if server-only
        else if (NetworkServer.active)
        {
            if (GUILayout.Button("Stop Server"))
            {
                manager.StopServer();
            }
        }
    }
}
