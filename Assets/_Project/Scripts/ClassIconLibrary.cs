// ClassIconLibrary.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using AIRBattleSimulation.Models;

[CreateAssetMenu(fileName = "ClassIconLibrary", menuName = "Alpha Instinct/Class Icon Library")]
public class ClassIconLibrary : ScriptableObject
{
    [Serializable]
    public struct ClassIconEntry
    {
        public UnitClass unitClass;
        public Sprite icon;
    }

    public List<ClassIconEntry> icons;

    private Dictionary<UnitClass, Sprite> _lookup;

    public Sprite GetIcon(UnitClass unitClass)
    {
        if (_lookup == null)
        {
            _lookup = new Dictionary<UnitClass, Sprite>();
            foreach (var entry in icons)
                _lookup[entry.unitClass] = entry.icon;
        }

        return _lookup.TryGetValue(unitClass, out var sprite) ? sprite : null;
    }
}
