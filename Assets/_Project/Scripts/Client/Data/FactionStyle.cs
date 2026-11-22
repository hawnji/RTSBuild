using System;
using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;

namespace RTS.Client
{
    /// <summary>Faction presentation data (colours, music) editable as a ScriptableObject.</summary>
    [CreateAssetMenu(fileName = "FactionStyle", menuName = "RTS/Faction Style")]
    public class FactionStyle : ScriptableObject
    {
        public FactionId Faction;
        public Color Primary = Color.white;
        public Color Secondary = Color.grey;
        public string MusicTheme = "battle";
    }

}
