using UnityEngine;

public enum SelKind { None, Stack, Booster, Fin, Payload, Equip }

/// <summary>Which part of the design is selected (a stack slot, the side boosters, the fins, the payload or one piece of equipment).</summary>
public struct Sel
{
    public SelKind kind;
    public int index;
    public static Sel None => new Sel { kind = SelKind.None, index = -1 };
    public static Sel Of(SelKind k, int i = 0) => new Sel { kind = k, index = i };
    public bool IsNone => kind == SelKind.None;
}

/// <summary>Put on every spawned part of the 3D rocket so clicks and highlights can find the design entry behind it.</summary>
public class PartRef : MonoBehaviour
{
    public SelKind kind;
    public int index;

    public bool Matches(Sel s)
    {
        if (s.kind != kind) return false;
        return kind == SelKind.Stack || kind == SelKind.Equip ? s.index == index : true;   // boosters / fins highlight together
    }
}
