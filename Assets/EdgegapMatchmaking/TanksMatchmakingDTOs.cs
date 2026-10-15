// Optional Edgegap matchmaking, see "Edgegap Matchmaker (optional)" in README.md.
// EDGEGAP_SDK is defined by Edgegap.Tanks.Matchmaking.asmdef when the
// com.edgegap.unity-sdk package is installed. Without it this file compiles to a stub.
#if EDGEGAP_SDK
using System.Collections.Generic;
using Edgegap.Matchmaking;
using Newtonsoft.Json;

/// <summary>
/// Ticket attributes sent to the matchmaker for the Tanks profile.
/// Add fields here for every custom rule you add to your matchmaker profile
/// (e.g. selected_map, elo_rating), using the same JSON names as the rule names.
/// </summary>
public class TanksTicketAttributes
{
    // Only serialized when latency beacons are measured. WebGL builds cannot ping
    // beacons (browsers can't send ICMP), so the key is omitted and the profile
    // must not define a `latencies` rule. The matchmaker then places the server
    // using the players' public IPs.
    [JsonProperty("beacons", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, float> Beacons;

    [JsonConstructor]
    public TanksTicketAttributes(Dictionary<string, float> beacons)
    {
        Beacons = beacons;
    }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}

/// <summary>
/// Group Up membership request. A solo player is a group of one that is ready immediately.
/// </summary>
public class TanksGroupUpRequestDTO : GroupUpRequestDTO<TanksTicketAttributes>
{
    public TanksGroupUpRequestDTO(
        string profile,
        Dictionary<string, float> beacons,
        bool isReady = true
    )
        : base(profile, isReady)
    {
        Attributes = new TanksTicketAttributes(beacons);
    }
}
#endif
