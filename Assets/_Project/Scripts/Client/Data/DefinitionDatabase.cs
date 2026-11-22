using System;
using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;

namespace RTS.Client
{
    /// <summary>
    /// ScriptableObject database of balance overrides. The simulation keeps its authoritative definitions in code
    /// (so headless tests and the editor-free build share one source of truth); this asset lets designers tune
    /// values in the Inspector without touching code. Create or refresh it with "RTS/Export Definitions Asset".
    /// The game loads Resources/RTSDefinitions.asset at startup if present.
    /// </summary>
    [CreateAssetMenu(fileName = "RTSDefinitions", menuName = "RTS/Definition Database")]
    public class DefinitionDatabase : ScriptableObject
    {
        public bool Enabled = true;
        public List<UnitTuning> Units = new List<UnitTuning>();
        public List<BuildingTuning> Buildings = new List<BuildingTuning>();
        public List<WeaponTuning> Weapons = new List<WeaponTuning>();
    }

}
