using System.Collections.Generic;
using UnityEngine;

/// <summary>The list of all missions the game knows about. Add more definitions here to make new contracts appear.</summary>
[CreateAssetMenu(menuName = "Space Agency/Mission Database", fileName = "MissionDatabase")]
public class MissionDatabase : ScriptableObject
{
    public List<MissionDefinition> missions = new List<MissionDefinition>();
}
