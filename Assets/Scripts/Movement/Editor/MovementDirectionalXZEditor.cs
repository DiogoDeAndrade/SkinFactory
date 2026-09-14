using UnityEditor;
using UnityEngine;
using UC.Editor;
using UC;

[CustomEditor(typeof(MovementDirectionalXZ))]
public class MovementDirectionalXZEditor : UnityCommonEditor
{
    SerializedProperty propSpeed;
    SerializedProperty propMovementControl;
    SerializedProperty propAcceleration;
    SerializedProperty propDeceleration;
    SerializedProperty propPlayerInput;
    SerializedProperty propMoveInput;
    SerializedProperty propCameraTag;
    SerializedProperty propTurnBehaviour;
    SerializedProperty propTurnSpeed;
    SerializedProperty propUseAnimator;
    SerializedProperty propAnimator;
    SerializedProperty propSpeedParameter;
    SerializedProperty propNormalizedSpeedParameter;
    SerializedProperty propIsMovingParameter;

    protected override void OnEnable()
    {
        base.OnEnable();

        propSpeed = serializedObject.FindProperty("speed");
        propMovementControl = serializedObject.FindProperty("movementControl");
        propAcceleration = serializedObject.FindProperty("acceleration");
        propDeceleration = serializedObject.FindProperty("deceleration");
        propPlayerInput = serializedObject.FindProperty("playerInput");
        propMoveInput = serializedObject.FindProperty("moveInput");
        propCameraTag = serializedObject.FindProperty("cameraTag");
        propTurnBehaviour = serializedObject.FindProperty("turnBehaviour");
        propTurnSpeed = serializedObject.FindProperty("turnSpeed");
        propUseAnimator = serializedObject.FindProperty("useAnimator");
        propAnimator = serializedObject.FindProperty("animator");
        propSpeedParameter = serializedObject.FindProperty("speedParameter");
        propNormalizedSpeedParameter = serializedObject.FindProperty("normalizedSpeedParameter");
        propIsMovingParameter = serializedObject.FindProperty("isMovingParameter");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (WriteTitle())
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propSpeed, new GUIContent("Speed", "Maximum movement speed on the XZ plane, in world units/second"));
            EditorGUILayout.PropertyField(propMovementControl, new GUIContent("Movement Control", "How movement input drives the character.\nVelocity: Input sets the planar velocity directly\nAcceleration: Input accelerates towards the target velocity, so external pushes (explosions, etc) have to be fought"));
            if (propMovementControl.intValue == (int)MovementDirectionalXZ.MovementControl.Acceleration)
            {
                EditorGUILayout.PropertyField(propAcceleration, new GUIContent("Acceleration", "Acceleration while there is input, in world units/second^2"));
                EditorGUILayout.PropertyField(propDeceleration, new GUIContent("Deceleration", "Deceleration while there is no input, in world units/second^2"));
            }

            MovementDirectionalXZ movement = target as MovementDirectionalXZ;

            if (movement.needNewInputSystem)
            {
                EditorGUILayout.PropertyField(propPlayerInput, new GUIContent("Player Input", "Player input component.\nNeeded if we're using the new input system."));
            }

            EditorGUILayout.PropertyField(propMoveInput, new GUIContent("Move Input", "2D axis for movement.\nX is right/left, Y is forward/back, relative to the camera.\nThis must be a Vector2 action from the new input system."));

            EditorGUILayout.PropertyField(propCameraTag, new GUIContent("Camera Tag", "Hypertag of the camera that defines the movement directions.\nForward is the camera forward projected on the XZ plane.\nIf empty or not found, the main camera is used."));

            EditorGUILayout.PropertyField(propTurnBehaviour, new GUIContent("Turn Behaviour", "What to do with the object's facing direction?\nNone: Object doesn't rotate\nInput Rotates Object: Object faces the input direction\nVelocity Rotates Object: Object faces the direction it is actually moving in"));
            if (propTurnBehaviour.intValue != (int)MovementDirectionalXZ.TurnBehaviour.None)
            {
                EditorGUILayout.PropertyField(propTurnSpeed, new GUIContent("Turn Speed", "How fast does the object turn, in degrees/second?\nZero means the turn is instant."));
            }

            // Separator
            Rect separatorRect = GUILayoutUtility.GetLastRect();
            separatorRect.yMin = separatorRect.yMax + 5;
            separatorRect.height = 5.0f;
            EditorGUI.DrawRect(separatorRect, GUIUtils.ColorFromHex("#ff6060"));
            EditorGUILayout.Space(separatorRect.height + 5);

            EditorGUILayout.PropertyField(propUseAnimator, new GUIContent("Use Animator", "Should we drive an animator with this movement controller?"));
            if (propUseAnimator.boolValue)
            {
                EditorGUILayout.PropertyField(propAnimator, new GUIContent("Animator", "What animator to use?"));
                EditorGUILayout.PropertyField(propSpeedParameter, new GUIContent("Speed Parameter", "What is the parameter to set to the planar speed, in world units/second?"));
                EditorGUILayout.PropertyField(propNormalizedSpeedParameter, new GUIContent("Normalized Speed Parameter", "What is the parameter to set to the planar speed divided by the maximum speed (0 to 1)?"));
                EditorGUILayout.PropertyField(propIsMovingParameter, new GUIContent("Is Moving Parameter", "What is the parameter to set to true/false when the object is moving?"));
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
        return "Directional XZ Controller";
    }

    protected override (Texture2D, Rect) GetIcon()
    {
        var varTexture = GUIUtils.GetTexture("Movement");
        return (varTexture, new Rect(0.0f, 0.0f, 1.0f, 1.0f));
    }

    protected override (Color, Color, Color) GetColors()
    {
        return (GUIUtils.ColorFromHex("#D0FFFF"), GUIUtils.ColorFromHex("#2f4858"), GUIUtils.ColorFromHex("#86CBFF"));
    }
}
