using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CharacterAnimationsSO.SpritesInfo))]
public class SpritesInfoDrawer : PropertyDrawer
{
    private const float CellSizeHeight = 180;
    private const float CellSizeWidth = 180;
    private const float Padding = 4f;
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 580f;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float startY = position.y + EditorGUIUtility.singleLineHeight + 8f;

        DrawGrid(
            new Rect(position.x, startY, position.width, position.height),
            property
        );

        EditorGUI.EndProperty();
    }
    private static readonly string[] cellNames = {
        "upLeft", "up", "upRight", "left", "midle", "right", "downLeft", "down", "downRight"
    };

    private void DrawGrid(Rect rect, SerializedProperty property)
    {
        SerializedProperty[] cells =
        {
            property.FindPropertyRelative("upLeft"),
            property.FindPropertyRelative("up"),
            property.FindPropertyRelative("upRight"),
            property.FindPropertyRelative("left"),
            property.FindPropertyRelative("midle"),
            property.FindPropertyRelative("right"),
            property.FindPropertyRelative("downLeft"),
            property.FindPropertyRelative("down"),
            property.FindPropertyRelative("downRight")
        };

        for (int x = 0; x < 3; x++)
        {
            var cellRect = new Rect(
                rect.x + x * (CellSizeWidth + Padding),
                rect.y,
                CellSizeWidth,
                CellSizeHeight
            );
            DrawCell(cellRect, cells[x], cellNames[x]);
        }

        for (int x = 0; x < 3; x++)
        {
            var cellRect = new Rect(
                rect.x + x * (CellSizeWidth + Padding),
                rect.y + (CellSizeHeight + Padding),
                CellSizeWidth,
                CellSizeHeight
            );
            DrawCell(cellRect, cells[3 + x], cellNames[3 + x]);
        }

        for (int x = 0; x < 3; x++)
        {
            var cellRect = new Rect(
                rect.x + x * (CellSizeWidth + Padding),
                rect.y + (CellSizeHeight * 2 + Padding),
                CellSizeWidth,
                CellSizeHeight
            );
            DrawCell(cellRect, cells[6 + x], cellNames[6 + x]);
        }

    }
    private void DrawCell(Rect rect, SerializedProperty spriteDataProp, string cellName)
    {
        GUI.Box(rect, GUIContent.none);

        float columnPadding = 6f;
        float leftWidth = 175f;
        float rightWidth = 108f;

        Rect leftRect = new Rect(
            rect.x + columnPadding,
            rect.y + columnPadding + 80,
            leftWidth - columnPadding,
            rect.height
        );

        Rect rightRect = new Rect(
            rect.x + 54,
            rect.y + columnPadding,
            rightWidth,
            rightWidth
        );

        float y = leftRect.y;

        y += 18;

        DrawVector3Compact(
            leftRect,
            "L P",
            spriteDataProp.FindPropertyRelative("leftHandPos"),
            ref y
        );

        DrawRotationXYZ(
            leftRect,
            "L R",
            spriteDataProp.FindPropertyRelative("leftHandRotation"),
            ref y
        );

        DrawVector3Compact(
            leftRect,
            "R P",
            spriteDataProp.FindPropertyRelative("rightHandPos"),
            ref y
        );

        DrawRotationXYZ(
            leftRect,
            "R R",
            spriteDataProp.FindPropertyRelative("rightHandRotation"),
            ref y
        );

        var spriteProp = spriteDataProp.FindPropertyRelative("characterSprite");

        EditorGUI.LabelField(
            new Rect(rightRect.x, rightRect.y, rightRect.width, 16),
            cellName,
            EditorStyles.miniBoldLabel
        );

        Rect previewRect = new Rect(
            rightRect.x,
            rightRect.y + 18,
            rightRect.width - 36,
            rightRect.height - 36
        );

        EditorGUI.DrawRect(previewRect, new Color(0.18f, 0.18f, 0.18f, 1f));

        if (spriteProp.objectReferenceValue != null)
        {
            DrawSpritePreview(previewRect, spriteProp.objectReferenceValue as Sprite);
        }

        EditorGUI.ObjectField(
            previewRect,
            spriteProp,
            typeof(Sprite),
            GUIContent.none
        );
    }

    private void DrawRotationXYZ(Rect rect, string label, SerializedProperty prop, ref float y)
    {
        EditorGUI.LabelField(
            new Rect(rect.x, y, 18, EditorGUIUtility.singleLineHeight),
            label,
            EditorStyles.miniLabel
        );

        Rect fieldRect = new Rect(
            rect.x + 20,
            y,
            rect.width - 20,
            EditorGUIUtility.singleLineHeight
        );

        Vector3 euler = prop.quaternionValue.eulerAngles;
        euler = EditorGUI.Vector3Field(fieldRect, GUIContent.none, euler);
        prop.quaternionValue = Quaternion.Euler(euler);

        y += 18;
    }

    private void DrawVector3Compact(Rect rect, string label, SerializedProperty prop, ref float y)
    {
        EditorGUI.LabelField(
            new Rect(rect.x, y, 18, EditorGUIUtility.singleLineHeight),
            label,
            EditorStyles.miniLabel
        );

        Rect fieldRect = new Rect(
            rect.x + 20,
            y,
            rect.width - 20,
            EditorGUIUtility.singleLineHeight
        );

        prop.vector3Value = EditorGUI.Vector3Field(
            fieldRect,
            GUIContent.none,
            prop.vector3Value
        );

        y += 18;
    }

    private void DrawSpritePreview(Rect rect, Sprite sprite)
    {
        if (sprite == null) return;

        var tex = sprite.texture;
        var uv = sprite.rect;
        uv.x /= tex.width;
        uv.y /= tex.height;
        uv.width /= tex.width;
        uv.height /= tex.height;

        GUI.DrawTextureWithTexCoords(rect, tex, uv);
    }
}
