using UnityEditor;
using UnityEngine;
using UC;
using UC.Editor;

[CustomEditor(typeof(OrbitCameraController))]
public class OrbitCameraControllerEditor : UnityCommonEditor
{
    SerializedProperty propTargetTag;
    SerializedProperty propTargetOffset;
    SerializedProperty propDistance;
    SerializedProperty propAngleX;
    SerializedProperty propAngleY;
    SerializedProperty propUpdateMode;
    SerializedProperty propFollowMode;
    SerializedProperty propFollowFactor;
    SerializedProperty propFollowSpeed;
    SerializedProperty propSnapOnStart;
    SerializedProperty propDisplayFocusPoint;
    SerializedProperty propDisplayFocusPointRadius;

    protected override void OnEnable()
    {
        base.OnEnable();

        propTargetTag = serializedObject.FindProperty("targetTag");
        propTargetOffset = serializedObject.FindProperty("targetOffset");
        propDistance = serializedObject.FindProperty("distance");
        propAngleX = serializedObject.FindProperty("angleX");
        propAngleY = serializedObject.FindProperty("angleY");
        propUpdateMode = serializedObject.FindProperty("updateMode");
        propFollowMode = serializedObject.FindProperty("followMode");
        propFollowFactor = serializedObject.FindProperty("followFactor");
        propFollowSpeed = serializedObject.FindProperty("followSpeed");
        propSnapOnStart = serializedObject.FindProperty("snapOnStart");
        propDisplayFocusPoint = serializedObject.FindProperty("displayFocusPoint");
        propDisplayFocusPointRadius = serializedObject.FindProperty("displayFocusPointRadius");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (WriteTitle())
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propTargetTag, new GUIContent("Target Tag", "Hypertag of the object the camera orbits around.\nThe first object found with this tag is used."));
            EditorGUILayout.PropertyField(propTargetOffset, new GUIContent("Target Offset", "Offset from the target position to the point the camera looks at, in world units.\nUseful to look at the chest instead of the feet."));
            EditorGUILayout.PropertyField(propDistance, new GUIContent("Distance", "Distance from the focus point to the camera, in world units"));
            EditorGUILayout.PropertyField(propAngleX, new GUIContent("Angle X", "Pitch of the camera, in degrees.\n0 is looking horizontally, 90 is looking straight down."));
            EditorGUILayout.PropertyField(propAngleY, new GUIContent("Angle Y", "Yaw of the camera, in degrees.\n0 is looking along the world Z axis."));

            EditorGUILayout.PropertyField(propUpdateMode, new GUIContent("Update Mode", "When is the camera updated?\nLate Update is usually the right choice, so the camera sees the final position of the target for this frame."));
            EditorGUILayout.PropertyField(propFollowMode, new GUIContent("Follow Mode", "How does the camera catch up with the target?\nExponential: Every update, the camera closes a fraction of the remaining distance, so it is fast when far and slow when close\nLinear: The camera moves towards the target at a constant maximum speed"));
            if (propFollowMode.intValue == (int)OrbitCameraController.FollowMode.Exponential)
            {
                EditorGUILayout.PropertyField(propFollowFactor, new GUIContent("Follow Factor", "Fraction of the remaining distance closed every update (0 to 1).\n1 means the camera is locked to the target, smaller values are smoother but lag more.\nThis depends on the update rate, so use Fixed Update if you want it to be consistent."));
            }
            else
            {
                EditorGUILayout.PropertyField(propFollowSpeed, new GUIContent("Follow Speed", "Maximum speed of the camera, in world units/second"));
            }
            EditorGUILayout.PropertyField(propSnapOnStart, new GUIContent("Snap On Start", "Should the camera jump to the target position on start, instead of travelling there from where it was placed?"));

            // Separator
            Rect separatorRect = GUILayoutUtility.GetLastRect();
            separatorRect.yMin = separatorRect.yMax + 5;
            separatorRect.height = 5.0f;
            EditorGUI.DrawRect(separatorRect, GUIUtils.ColorFromHex("#ff6060"));
            EditorGUILayout.Space(separatorRect.height + 5);

            EditorGUILayout.PropertyField(propDisplayFocusPoint, new GUIContent("Display Focus Point", "Draw the current focus point in the scene view when the camera is selected?"));
            if (propDisplayFocusPoint.boolValue)
            {
                EditorGUILayout.PropertyField(propDisplayFocusPointRadius, new GUIContent("Focus Point Radius", "Radius of the focus point gizmo, in world units"));
            }

            EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();
        }
    }

    protected override GUIStyle GetTitleSyle()
    {
        return GUIUtils.GetBehaviourTitleStyle();
    }

    protected override string GetTitle()
    {
        return "Orbit Camera Controller";
    }

    protected override (Texture2D, Rect) GetIcon()
    {
        var varTexture = GUIUtils.GetTexture("Movement");
        return (varTexture, new Rect(0.0f, 0.0f, 1.0f, 1.0f));
    }

    protected override (Color, Color, Color) GetColors()
    {
        return (GUIUtils.ColorFromHex("#FFE8D0"), GUIUtils.ColorFromHex("#58402f"), GUIUtils.ColorFromHex("#FFB786"));
    }
}
