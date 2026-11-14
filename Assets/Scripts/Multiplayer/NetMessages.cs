using PurrNet.Packing;

/// <summary>The four jobs in a shared agency. Each one owns a part of the game; nobody holding a role means anyone may do it.</summary>
public enum TeamRole { None = 0, Engineer = 1, Scientist = 2, Finance = 3, Safety = 4 }

public static class TeamRoles
{
    public static readonly TeamRole[] All = { TeamRole.Engineer, TeamRole.Scientist, TeamRole.Finance, TeamRole.Safety };

    public static string Name(TeamRole r) => r == TeamRole.None ? "No role" : r.ToString();

    public static string Duty(TeamRole r)
    {
        switch (r)
        {
            case TeamRole.Engineer: return "Designs, builds and deploys rockets";
            case TeamRole.Scientist: return "Runs training and tests";
            case TeamRole.Finance: return "Takes contracts and builds facilities";
            case TeamRole.Safety: return "Decides events and gives the launch go";
            default: return "Watches and chats";
        }
    }
}

// ---------------------------------------------------------------- client -> host
/// <summary>A client introduces itself right after it connects.</summary>
public struct HelloMsg : IPackedAuto
{
    public string name;
    public ulong steamId;
    public string version;
}

public struct RoleRequestMsg : IPackedAuto { public int role; }

public struct ReadyMsg : IPackedAuto { public bool ready; }

/// <summary>A clock change a client asks the host for (speed index, or -1 to toggle pause).</summary>
public struct SpeedRequestMsg : IPackedAuto { public int speedIndex; }

// ---------------------------------------------------------------- both ways
/// <summary>Sent to the host as a request, then relayed by the host to everyone with the sender filled in.</summary>
public struct ChatMsg : IPackedAuto
{
    public string from;
    public string text;
    public int role;
    public bool system;
}

// ---------------------------------------------------------------- host -> everyone
/// <summary>Everyone in the room, in join order. Parallel arrays keep the packing simple.</summary>
public struct RosterMsg : IPackedAuto
{
    public ulong[] playerIds;
    public string[] names;
    public ulong[] steamIds;
    public int[] roles;
    public bool[] ready;
    public string agencyName;
    public bool inGame;
}

/// <summary>The host's saved agency: every client loads this before entering the Game scene, so everyone starts from the same world.</summary>
public struct WorldMsg : IPackedAuto
{
    public string[] strKeys;
    public string[] strValues;
    public string[] intKeys;
    public int[] intValues;
}

/// <summary>The host's clock, several times a second.</summary>
public struct ClockMsg : IPackedAuto
{
    public long nowTicks;
    public double gameTime;
    public int speedIndex;
    public bool paused;
}

/// <summary>Sent by the host to a client it refuses or removes.</summary>
public struct KickMsg : IPackedAuto { public string reason; }

// ---------------------------------------------------------------- shared agency (in game)
public enum TeamAction
{
    Program = 1, Build = 2, Accept = 3, Decline = 4, RemovePending = 5, Abandon = 6,
    BuildRocket = 7, DeployRocket = 8, FinishRocket = 9, CancelDeploy = 10, Launch = 11, EventPick = 12,
}

/// <summary>A guest asks the host to do something in the shared agency. The fields mean what each <see cref="TeamAction"/> needs.</summary>
public struct ActionMsg : IPackedAuto
{
    public int kind;
    public string a, b, c;
    public int i;
    public float f;
    public long n;
}

/// <summary>The host's save keys that changed since the last update (same layout as <see cref="WorldMsg"/>, but it never changes scene).</summary>
public struct StateMsg : IPackedAuto
{
    public string[] strKeys;
    public string[] strValues;
    public string[] intKeys;
    public int[] intValues;
}

/// <summary>The contract lists, which only live in memory on the host.</summary>
public struct MissionsMsg : IPackedAuto
{
    public string[] available, pending, completed;
    public string[] running;
    public double[] runStart;
    public long[] runStartTicks;
    public double[] runShift;
    public float[] runProgress;
    public string tracked;
}

/// <summary>A random event or client call that the whole team sees; the Safety officer chooses.</summary>
public struct EventShowMsg : IPackedAuto
{
    public string key, kicker, title, body;
    public bool client;
    public string[] labels, benefits, costs, risks;
    public bool[] enabled;
}

public struct EventCloseMsg : IPackedAuto { public string key; public string by; public string choice; }

/// <summary>The host launched: everyone watches the same flight (the record is the host's, so the result is the same for all).</summary>
public struct LaunchMsg : IPackedAuto { public string record; }
