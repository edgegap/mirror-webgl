# Mirror WebGL Sample
This guide will help you use [Mirror](https://mirror-networking.com/)'s Websocket Transport and create a headless server on Edgegap for a Unity project.

This guide will use the open-source sample project `Tanks`, available in the Mirror sample under `Assets/Mirror/Examples/Tanks`.

## Preparation
This tutorial assumes you are already familiar with [using the Edgegap plugin](https://docs.edgegap.com/learn/unity-games/getting-started-with-servers).

## Tutorial
To test the sample,

- Build, Containerize & Upload the server to Edgegap using the plugin.
- When creating the app version, add the following port:
    - Port: `7778`;
    - Protocol: `WS`;
    - Name: `gameport`;
    - Verifications: enabled;
    - TLS Upgrade: enabled.
- Deploy a server.
- In the client `NetworkManager`:
    - Set the Network Address as the deployment's `HOST_URL`;
    - Ensure that the web transport's Client Port Option is set to `Match Webpage Protocol`;
    - Launch the game and click the `client` button.

You can see the full documentation [here](https://docs.edgegap.com/docs/sample-projects/mirror-on-edgegap/websocket-transport).