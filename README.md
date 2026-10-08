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

## Edgegap Matchmaker (optional)

Matchmaking is **completely optional and disabled by default**. The sample above works exactly as before. Out of the box, the Edgegap SDK package isn't installed, the matchmaking scripts compile to empty stubs, and the `EdgegapMatchmaking` object in the `MirrorTanks` scene is inactive.

When enabled, players click **Find Match** instead of typing a server address. The [Edgegap Matchmaker](https://docs.edgegap.com/learn/matchmaking) groups them, starts a deployment near them, and each client connects to it automatically.

### What's included

Everything lives in `Assets/EdgegapMatchmaking`:

- `TanksMatchmakingClient` creates a ticket (Group Up flow, solo group) and polls until a server is assigned. It then connects Mirror to `wss://{fqdn}:{external port}`, retrying while the server boots. It draws a small Find Match / Cancel panel in the top-right corner.
- `TanksMatchmakingServer` runs only on the dedicated server. It reads the variables Edgegap injects. It stops its own deployment when nobody joins within 2 minutes, or 30 seconds after the last player leaves.
- `TanksMatchmakingDTOs` defines the ticket attributes. Add fields here for any custom rules you add to your profile.
- `Edgegap.Tanks.Matchmaking.asmdef` defines `EDGEGAP_SDK` only when `com.edgegap.unity-sdk` 3.5.5 or newer is installed. 3.5.5 is the first version that compiles for WebGL.
- `link.xml` keeps the SDK's data classes from being stripped in IL2CPP builds.
- `matchmaker-config.json`, at the repository root, is an example matchmaker configuration.

### Enable it

1. **Install the Edgegap Unity SDK.** In Package Manager, choose **Add package from git URL** and enter `https://github.com/edgegap/edgegap-unity-sdk.git#3.5.5`. Or add `"com.edgegap.unity-sdk": "https://github.com/edgegap/edgegap-unity-sdk.git#3.5.5"` to `Packages/manifest.json`.
2. **Activate the object.** Open the `MirrorTanks` scene, select `EdgegapMatchmaking` and tick the checkbox next to its name.
3. **Upload the server.** Build and upload it as in the tutorial above, with the `gameport` port (`7778`, `WS`, TLS Upgrade enabled). Build it after step 2, so the server includes `TanksMatchmakingServer` and stops its deployment when the match is over.
4. **Create a matchmaker** in the [dashboard](https://app.edgegap.com/matchmaker-management-v2/matchmakers/list) from `matchmaker-config.json`:
    - Set `application.name` and `application.version` to your app version.
    - Set `allowed_cors_origins` to the origins that serve your WebGL build. The browser calls the matchmaker directly, so requests from any other origin are blocked.
5. **Connect the client.** Paste the matchmaker's **API URL** into `Base URL` on `TanksMatchmakingClient`, and its **Auth Token** into `Auth Token`.
6. **Test.** Build WebGL, host it on an allowed origin, open it in two browser windows, and click **Find Match** in each.

With the example profile, 2 to 4 players are matched together. After 30 seconds in the queue, a single player gets a server alone, which is convenient for testing.

To disable it again, deactivate `EdgegapMatchmaking`. To remove it completely, also remove the SDK package.

### WebGL notes

- **No latency beacons.** The SDK measures beacon latency with ICMP pings, which browsers can't send. The example profile has no `latencies` rule, and the matchmaker places the server based on the players' public IPs. Desktop builds can turn on `Use Latency Beacons` if you add a `latencies` rule to a separate profile.
- **The auth token ships in the client build**, as with any game client using the matchmaker. It gives no access to your Edgegap account.
- **Abandoned tickets expire on their own.** Browsers may close a tab without notifying the game, so an abandoned ticket is removed after `ticket_expiration_period` instead of immediately.
