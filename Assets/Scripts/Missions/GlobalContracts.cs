using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One contract posted to the shared "global contract finder" by another agency somewhere in the world.</summary>
public class GlobalListing
{
    public string id, poster, country, title, objective, payloadType, target;
    public int payloadQuantity = 1;
    public float massLimit;
    public int deadlineDays;
    public long ceiling;                 // the most the poster will pay; bids go down from here
    public long floor;                   // the lowest bid the poster accepts
    public long lowestBid;               // best (lowest) bid so far, from anyone
    public string leader;                // who holds the lowest bid: another agency's name, or "You"
    public int bidCount;
    public long myBid;                   // 0 = the player has not bid
    public float closesAt;               // Time.unscaledTime when the auction ends

    public bool IAmLeading => myBid > 0 && leader == "You";
    public float SecondsLeft => Mathf.Max(0f, closesAt - Time.unscaledTime);
}

/// <summary>
/// Everything the Global tab needs from the server. The game only talks to this interface, so the offline
/// <see cref="MockGlobalContractService"/> can later be replaced by a real network client without touching the UI.
/// </summary>
public interface IGlobalContractService
{
    bool Connected { get; }
    int OnlineAgencies { get; }
    IReadOnlyList<GlobalListing> Listings { get; }
    /// <summary>Raised when listings are added or removed or someone bids (UI refreshes).</summary>
    event Action Changed;
    /// <summary>Raised when an auction ends: (listing, wonByPlayer).</summary>
    event Action<GlobalListing, bool> Closed;
    /// <summary>Places the player's bid. Returns an error text, or null when the bid was accepted.</summary>
    string PlaceBid(string listingId, long amount);
}

/// <summary>
/// Front-end stand-in for the server: it invents contracts from other agencies and makes them bid against the player.
/// There is no network code here. Auctions are reverse auctions: the lowest bid wins and is paid that amount.
/// </summary>
public class MockGlobalContractService : MonoBehaviour, IGlobalContractService
{
    public static MockGlobalContractService Instance { get; private set; }

    public bool Connected => true;
    public int OnlineAgencies { get; private set; } = 1284;
    public IReadOnlyList<GlobalListing> Listings => listings;
    public event Action Changed;
    public event Action<GlobalListing, bool> Closed;

    readonly List<GlobalListing> listings = new List<GlobalListing>();
    float nextBot, nextListing, nextOnline;
    int counter = 1000;
    readonly System.Random rng = new System.Random();

    static readonly (string name, string cc)[] Agencies =
    {
        ("Hoshizora Aerospace", "JP"), ("Nordlys Space", "NO"), ("Sahara Orbital", "EG"), ("Andes Launch Co.", "CL"),
        ("Kerala Space Works", "IN"), ("Maple Orbit", "CA"), ("Outback Rockets", "AU"), ("Bavaria Aerodynamics", "DE"),
        ("Fjord Dynamics", "IS"), ("Pampas Astro", "AR"), ("Lagos Launch", "NG"), ("Seoul Stellar", "KR"), ("Dhaka Orbital", "BD"),
    };

    // title, payload, quantity, target, mass kg, days, ceiling range (millions)
    static readonly (string title, string payload, int qty, string target, float mass, int days, int lo, int hi, string objective)[] Templates =
    {
        ("Weather Satellite Constellation", "Weather satellite", 3, "LEO 600 km", 450f, 90, 40, 80, "Place three weather satellites in a sun-synchronous orbit."),
        ("Lunar Imaging Orbiter", "Imaging satellite", 1, "Lunar orbit", 900f, 180, 120, 220, "Map the lunar south pole from a low polar orbit."),
        ("Mars Relay Demonstrator", "Comms relay", 1, "Mars transfer", 1400f, 480, 400, 700, "Fly a relay to Mars to test deep-space links."),
        ("Asteroid Sample Return", "Sample capsule", 1, "Near-Earth asteroid", 1100f, 720, 600, 950, "Bring back a sample from a near-Earth asteroid."),
        ("Telescope Servicing Mission", "Servicing probe", 1, "GEO", 2200f, 240, 250, 400, "Dock with an ageing telescope and extend its life."),
        ("Polar Ice Radar Mapper", "Radar satellite", 1, "Polar LEO", 700f, 120, 90, 150, "Measure polar ice thickness for two seasons."),
        ("CubeSat Rideshare Batch", "CubeSat", 12, "SSO 550 km", 150f, 45, 12, 25, "Deploy a batch of student CubeSats."),
        ("Deep Space Comms Beacon", "Beacon", 1, "Sun-Earth L2", 650f, 300, 180, 300, "Park a navigation beacon at the Sun-Earth L2 point."),
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        for (int i = 0; i < 4; i++) AddListing(initial: true);
        nextBot = Time.unscaledTime + 4f; nextListing = Time.unscaledTime + 25f; nextOnline = Time.unscaledTime + 3f;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void AddListing(bool initial = false)
    {
        var t = Templates[rng.Next(Templates.Length)];
        var a = Agencies[rng.Next(Agencies.Length)];
        long ceiling = (long)(t.lo + rng.NextDouble() * (t.hi - t.lo)) * 1_000_000L;
        long floor = (long)(ceiling * 0.45);
        var l = new GlobalListing
        {
            id = "G" + (++counter), poster = a.name, country = a.cc, title = t.title, objective = t.objective,
            payloadType = t.payload, payloadQuantity = t.qty, target = t.target, massLimit = t.mass, deadlineDays = t.days,
            ceiling = ceiling, floor = floor,
            leader = null, bidCount = 0,
            closesAt = Time.unscaledTime + (initial ? 45f + (float)rng.NextDouble() * 200f : 120f + (float)rng.NextDouble() * 180f),
        };
        // a few bots have already bid
        int pre = rng.Next(0, 4);
        l.lowestBid = ceiling;
        for (int i = 0; i < pre; i++) BotBid(l, notify: false);
        listings.Add(l);
    }

    void BotBid(GlobalListing l, bool notify = true)
    {
        long next = (long)(l.lowestBid * (1.0 - (0.012 + rng.NextDouble() * 0.035)));
        if (next < l.floor) return;
        bool wasMine = l.IAmLeading;
        l.lowestBid = next; l.leader = Agencies[rng.Next(Agencies.Length)].name; l.bidCount++;
        if (notify && wasMine) Hud.Toast("Outbid on \"" + l.title + "\" by " + l.leader);
    }

    void Update()
    {
        float now = Time.unscaledTime;
        bool changed = false;

        if (now >= nextOnline) { OnlineAgencies = Mathf.Clamp(OnlineAgencies + rng.Next(-6, 9), 1100, 1500); nextOnline = now + 5f; }

        if (now >= nextBot && listings.Count > 0)
        {
            nextBot = now + 3f + (float)rng.NextDouble() * 9f;
            var l = listings[rng.Next(listings.Count)];
            if (rng.NextDouble() < (l.myBid > 0 ? 0.7 : 0.5)) { BotBid(l); changed = true; }
        }
        if (now >= nextListing)
        {
            nextListing = now + 20f + (float)rng.NextDouble() * 30f;
            if (listings.Count < 6) { AddListing(); changed = true; }
        }
        // finished auctions
        for (int i = listings.Count - 1; i >= 0; i--)
        {
            var l = listings[i];
            if (l.SecondsLeft > 0f) continue;
            listings.RemoveAt(i);
            Closed?.Invoke(l, l.IAmLeading);
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    public string PlaceBid(string listingId, long amount)
    {
        var l = listings.Find(x => x.id == listingId);
        if (l == null) return "This auction has already closed.";
        if (amount > l.ceiling) return "Your bid is above the poster's ceiling.";
        if (amount < l.floor) return "Too low: the poster will not accept less than $" + UIKit.Money(l.floor) + ".";
        if (l.leader != null && amount >= l.lowestBid && !l.IAmLeading) return "Bid lower than $" + UIKit.Money(l.lowestBid) + " to take the lead.";
        if (l.IAmLeading && amount >= l.myBid) return "You already lead with a lower bid.";
        l.myBid = amount; l.lowestBid = amount; l.leader = "You"; l.bidCount++;
        Changed?.Invoke();
        return null;
    }

    /// <summary>The contract the player won, as a normal mission definition for the Active tab.</summary>
    public static MissionDefinition ToMission(GlobalListing l)
    {
        var m = ScriptableObject.CreateInstance<MissionDefinition>();
        m.id = "GLOBAL-" + l.id;
        m.displayName = l.title + " (" + l.poster + ")";
        m.missionType = "Global contract";
        m.target = l.target;
        m.budget = l.myBid;
        m.massLimit = l.massLimit;
        m.payload = new MissionPayload { type = l.payloadType, quantity = l.payloadQuantity };
        m.duration = new MissionDuration { value = l.deadlineDays, unit = DurationUnit.Days };
        m.objective = l.objective + " Posted by " + l.poster + " (" + l.country + ").";
        return m;
    }
}
