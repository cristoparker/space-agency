using System.Collections.Generic;
using UnityEngine;

/// <summary>The path of one simulated flight: Earth-centred positions (km), time, altitude, speed and the key moments.</summary>
public class FlightProfile
{
    public List<Vector2> pos = new List<Vector2>();            // km from the centre of the Earth
    public List<float> t = new List<float>(), alt = new List<float>(), speed = new List<float>();   // seconds, km, m/s
    public List<(float t, string label)> events = new List<(float, string)>();
    public bool lifted, orbit, crashed, escape;
    public double energy;                                       // specific orbital energy after cut-off (J/kg), to compare tries
    public float apogeeKm, perigeeKm, burnoutT, duration, maxAltKm;
    public int Count => t.Count;
    public int IndexAt(float time) { int i = t.BinarySearch(time); return Mathf.Clamp(i < 0 ? ~i - 1 : i, 0, Mathf.Max(0, t.Count - 1)); }
}

/// <summary>
/// A small flight simulation of the real rocket design: thrust, fuel, mass, drag and gravity, a pitch programme that bends the path over
/// as the rocket climbs, and a check whether the engine cut-off leaves the rocket in orbit. If the thrust cannot lift the rocket, it never leaves the pad.
/// </summary>
public static class FlightSimulator
{
    const double G0 = 9.80665, Re = 6_371_000.0, Mu = 3.986004418e14;
    // The game says a rocket with 9000 m/s of delta-v reaches Earth orbit. A simple point-mass flight loses more to gravity and air than the
    // game's rule counts, so the engines are a little more efficient here (as if the steering were perfect), to keep the two in agreement.
    const double Idealised = 1.12;

    /// <summary>
    /// The autopilot tries a few climb programmes (how fast it bends over, and how high it flattens out) and flies the best one:
    /// the first that reaches orbit, or else the one with the most orbital energy.
    /// </summary>
    public static FlightProfile Simulate(RocketReport rep)
    {
        FlightProfile best = null; double bestScore = double.NegativeInfinity;
        foreach (double top in new[] { 150000.0, 110000.0, 200000.0 })
            foreach (double expo in new[] { 0.5, 0.4, 0.65, 0.8 })
            {
                var p = Run(rep, top, expo);
                if (!p.lifted) return p;                                    // it never leaves the pad: no need to try again
                double score = (p.orbit ? 1e9 : 0.0) + p.energy;
                if (score > bestScore) { bestScore = score; best = p; }
                if (p.orbit) return p;
            }
        return best;
    }

    static FlightProfile Run(RocketReport rep, double topM, double expo)
    {
        var prof = new FlightProfile();
        var stages = new List<StageInfo>();
        foreach (var s in rep.stages) if (s.parts.Count > 0 && s.thrustKn > 0f && s.isp > 0f && s.fuel > 0f) stages.Add(s);
        double mass = rep.stages.Count > 0 ? rep.stages[0].m0 : rep.launchKg;
        if (stages.Count == 0 || mass <= 0 || rep.liftoffTwr < 1f)
        {
            Add(prof, 0f, 0.0, 0.0, 0.0, 0.0);
            prof.events.Add((0f, rep.liftoffTwr < 1f ? "Thrust too low: no liftoff" : "No engine"));
            prof.duration = 12f; prof.maxAltKm = 0f;
            Add(prof, 12f, 0.0, 0.0, 0.0, 0.0);
            return prof;
        }

        double area = Mathf.PI * Mathf.Pow(Mathf.Max(0.6f, rep.widthM) * 0.5f, 2f);
        double x = 0, y = Re, vx = 0, vy = 0;
        int si = 0; double fuel = stages[0].fuel;
        double thrust = stages[0].thrustKn * 1000.0, mdot = thrust / (stages[0].isp * Idealised * G0);
        double t = 0, dt = 0.5; bool burning = true;
        prof.lifted = true; prof.events.Add((0f, "Liftoff"));
        double nextRecord = 0;
        while (t < 1800)
        {
            double r = System.Math.Sqrt(x * x + y * y), h = r - Re;
            double ux = x / r, uy = y / r;                              // up
            double fx = uy, fy = -ux;                                   // forward (the direction of travel round the Earth)
            double v = System.Math.Sqrt(vx * vx + vy * vy);
            // pitch programme: straight up first, then bending over to the horizon by about 150 km
            double pitch = 90.0 * (1.0 - System.Math.Pow(System.Math.Clamp(h / topM, 0.0, 1.0), expo));
            if (h < 300) pitch = 90.0;
            double pr = pitch * System.Math.PI / 180.0;
            double dx = ux * System.Math.Sin(pr) + fx * System.Math.Cos(pr), dy = uy * System.Math.Sin(pr) + fy * System.Math.Cos(pr);

            double ax = -Mu / (r * r) * ux, ay = -Mu / (r * r) * uy;
            if (burning)
            {
                ax += thrust / mass * dx; ay += thrust / mass * dy;
                double used = mdot * dt; fuel -= used; mass -= used;
                if (fuel <= 0)
                {
                    // this stage is empty: drop it and light the next one, or stop the engines
                    if (si + 1 < stages.Count)
                    {
                        mass -= stages[si].dry;                              // the spent stage falls away (the last one stays with the payload)
                        si++; fuel = stages[si].fuel; thrust = stages[si].thrustKn * 1000.0; mdot = thrust / (stages[si].isp * Idealised * G0);
                        prof.events.Add(((float)t, "Stage " + si + " separation"));
                    }
                    else { burning = false; prof.burnoutT = (float)t; prof.events.Add(((float)t, "Engine cut-off")); }
                }
            }
            double rho = 1.225 * System.Math.Exp(-System.Math.Max(0.0, h) / 7200.0);
            vx += ax * dt; vy += ay * dt;
            if (v > 1e-3) { double k = 0.5 * rho * v * 0.35 * area / mass; double f = 1.0 / (1.0 + k * dt); vx *= f; vy *= f; }      // drag (stable even when it is strong)
            x += vx * dt; y += vy * dt; t += dt;

            if (t >= nextRecord) { Add(prof, (float)t, x, y, vx, vy); nextRecord += 1.0; }
            double rn = System.Math.Sqrt(x * x + y * y);
            if (rn - Re < -1.0 && t > 5) { prof.crashed = true; prof.events.Add(((float)t, "Fell back to Earth")); break; }
            if (!burning)
            {
                // after cut-off: is the rocket in orbit? (perigee above the thick air)
                double vv2 = vx * vx + vy * vy, E = vv2 / 2 - Mu / rn, a = -Mu / (2 * E);
                double hAng = x * vy - y * vx, ecc = System.Math.Sqrt(System.Math.Max(0, 1 + 2 * E * hAng * hAng / (Mu * Mu)));
                prof.perigeeKm = (float)((a * (1 - ecc) - Re) / 1000.0); prof.apogeeKm = (float)((a * (1 + ecc) - Re) / 1000.0);
                prof.energy = E;
                if (E >= 0) { prof.orbit = true; prof.escape = true; prof.events.Add(((float)t, "Escape trajectory")); }
                else if (prof.perigeeKm > 90f) { prof.orbit = true; prof.events.Add(((float)t, "Orbit reached")); }
                // keep flying a little longer so the path is shown, then stop
                double stopAt = prof.burnoutT + (prof.orbit ? 150 : 600);
                if (t >= stopAt) break;
                if (prof.orbit && t >= prof.burnoutT + 150) break;
            }
        }
        prof.duration = prof.t.Count > 0 ? prof.t[prof.t.Count - 1] : 0f;
        foreach (var a in prof.alt) prof.maxAltKm = Mathf.Max(prof.maxAltKm, a);
        if (!prof.orbit && !prof.crashed) prof.events.Add((prof.duration, "Suborbital: apogee " + prof.maxAltKm.ToString("0") + " km"));
        return prof;
    }

    static void Add(FlightProfile p, float t, double x, double y, double vx, double vy)
    {
        p.pos.Add(new Vector2((float)(x / 1000.0), (float)(y / 1000.0)));
        p.t.Add(t);
        p.alt.Add((float)((System.Math.Sqrt(x * x + y * y) - Re) / 1000.0));
        p.speed.Add((float)System.Math.Sqrt(vx * vx + vy * vy));
    }

    public static bool ReachesOrbit(RocketReport rep) => Simulate(rep).orbit;
}
