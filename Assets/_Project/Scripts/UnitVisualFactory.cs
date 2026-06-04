using System;
using UnityEditor;
using UnityEngine;

public static class UnitVisualFactory
{
    public static GameObject GetUnitPrefab(UnitSizeClassificationEnum unitSizeClassification)
    {
        return unitSizeClassification switch
        {
            UnitSizeClassificationEnum.TINY => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/TinyUnit.prefab"),
            UnitSizeClassificationEnum.SMALL => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/SmallUnit.prefab"),
            UnitSizeClassificationEnum.MEDIUM => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/MediumUnit.prefab"),
            UnitSizeClassificationEnum.LARGE => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/LargeUnit.prefab"),
            UnitSizeClassificationEnum.EXTRALARGE => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/ExtraLargeUnit.prefab"),
            _ => throw new NotImplementedException(),
        };
    }
}
