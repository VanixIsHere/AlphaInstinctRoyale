using System.Collections.Generic;
using UnityEngine;
using AIRBattleSimulation.Models;

#if UNITY_EDITOR
using UnityEditor;

[CustomPropertyDrawer(typeof(OriginFlagLibrary.OriginFlagEntry))]
public class OriginFlagEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty originProp = property.FindPropertyRelative("origin");
        SerializedProperty spriteProp = property.FindPropertyRelative("flagSprite");

        EditorGUI.BeginProperty(position, label, property);

        float labelWidth = EditorGUIUtility.labelWidth;
        float fieldWidth = position.width;

        Rect originRect = new Rect(position.x, position.y, fieldWidth * 0.4f, position.height);
        Rect spriteRect = new Rect(position.x + fieldWidth * 0.45f, position.y, fieldWidth * 0.55f, position.height);

        EditorGUI.PropertyField(originRect, originProp, GUIContent.none);
        EditorGUI.PropertyField(spriteRect, spriteProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
#endif

[CreateAssetMenu(menuName = "Alpha Instinct/Origin Flag Library")]
public class OriginFlagLibrary : ScriptableObject
{
    [System.Serializable]
    public class OriginFlagEntry
    {
        public UnitOrigin origin;
        public Texture2D flagSprite;
    }

    public List<OriginFlagEntry> flagEntries;

    private Dictionary<UnitOrigin, Texture2D> flagLookup;

    public Texture2D GetFlag(UnitOrigin origin)
    {
        if (flagLookup == null)
        {
            flagLookup = new Dictionary<UnitOrigin, Texture2D>();
            foreach (var entry in flagEntries)
            {
                flagLookup[entry.origin] = entry.flagSprite;
            }
        }
        return flagLookup.TryGetValue(origin, out var sprite) ? sprite : null;
    }
}