using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private SerializedProperty prefabEntriesProp;
    private SerializedProperty spawnSequenceProp;
    private SerializedProperty playModeProp;
    private ReorderableList prefabList;
    private ReorderableList sequenceList;
    private LevelData targetData;

    private void OnEnable()
    {
        prefabEntriesProp = serializedObject.FindProperty("prefabEntries");
        spawnSequenceProp = serializedObject.FindProperty("spawnSequence");
        playModeProp = serializedObject.FindProperty("playMode");
        targetData = (LevelData)target;

        prefabList = new ReorderableList(serializedObject, prefabEntriesProp, true, true, true, true);
        prefabList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Prefab Entries");
        prefabList.elementHeight = EditorGUIUtility.singleLineHeight * 2 + 8;
        prefabList.drawElementCallback = (rect, index, active, focused) =>
        {
            var el = prefabEntriesProp.GetArrayElementAtIndex(index);
            var nameProp = el.FindPropertyRelative("displayName");
            var prefabProp = el.FindPropertyRelative("prefab");

            Rect nameRect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
            Rect prefabRect = new Rect(rect.x, rect.y + 2 + EditorGUIUtility.singleLineHeight + 2, rect.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(prefabRect, prefabProp, new GUIContent("Prefab"));
            // Auto-fill displayName from editor asset if empty
            if (string.IsNullOrEmpty(nameProp.stringValue))
            {
                // Access the actual target object instead of trying to use objectReferenceValue on SerializedProperty
                if (targetData != null && targetData.prefabEntries != null && index >= 0 && index < targetData.prefabEntries.Length)
                {
                    var entry = targetData.prefabEntries[index];
                    if (entry != null && entry.prefab != null)
                    {
#if UNITY_EDITOR
                        var editorAsset = entry.prefab.editorAsset;
                        if (editorAsset != null)
                            nameProp.stringValue = editorAsset.name;
                        else if (!string.IsNullOrEmpty(entry.displayName))
                            nameProp.stringValue = entry.displayName;
#endif
                    }
                }
            }

            EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("Display Name"));
        };

        sequenceList = new ReorderableList(serializedObject, spawnSequenceProp, true, true, true, true);
        sequenceList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Spawn Sequence (select by prefab name)");
        sequenceList.elementHeight = EditorGUIUtility.singleLineHeight + 4;
        sequenceList.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.y += 2;
            EditorGUI.BeginChangeCheck();

            // Build popup options from current prefab list
            string[] names = GetPrefabNamesForPopup();
            int current = 0;
            if (index < spawnSequenceProp.arraySize)
            {
                current = spawnSequenceProp.GetArrayElementAtIndex(index).intValue;
                if (current < 0) current = 0;
            }

            int newIndex = EditorGUI.Popup(rect, current, names);
            if (EditorGUI.EndChangeCheck())
            {
                spawnSequenceProp.GetArrayElementAtIndex(index).intValue = newIndex;
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelName"));

        prefabList.DoLayoutList();

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(playModeProp);

        // Buttons for sequence generation
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sequence tools", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto-fill sequence (one of each)"))
        {
            AutoFillSequenceOneOfEach();
        }
        if (GUILayout.Button("Clear sequence"))
        {
            spawnSequenceProp.arraySize = 0;
        }
        EditorGUILayout.EndHorizontal();

        // Randomize helper
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Randomize sequence (length 10)"))
        {
            RandomizeSequence(10);
        }
        if (GUILayout.Button("Randomize sequence (length 20)"))
        {
            RandomizeSequence(20);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        sequenceList.DoLayoutList();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("sequenceEndBehavior"));

        serializedObject.ApplyModifiedProperties();
    }

    private string[] GetPrefabNamesForPopup()
    {
        if (targetData == null || targetData.prefabEntries == null || targetData.prefabEntries.Length == 0)
            return new string[] { "No prefabs" };

        string[] names = new string[targetData.prefabEntries.Length];
        for (int i = 0; i < targetData.prefabEntries.Length; i++)
        {
            var e = targetData.prefabEntries[i];
            if (e == null)
            {
                names[i] = $"Prefab [{i}] (null)";
                continue;
            }

            if (!string.IsNullOrEmpty(e.displayName))
                names[i] = e.displayName;
            else
            {
#if UNITY_EDITOR
                if (e.prefab != null && e.prefab.editorAsset != null)
                    names[i] = e.prefab.editorAsset.name;
                else
                    names[i] = $"Prefab [{i}]";
#else
                names[i] = $"Prefab [{i}]";
#endif
            }
        }
        return names;
    }

    private void AutoFillSequenceOneOfEach()
    {
        if (targetData == null || targetData.prefabEntries == null) return;
        int n = targetData.prefabEntries.Length;
        spawnSequenceProp.arraySize = n;
        for (int i = 0; i < n; i++)
            spawnSequenceProp.GetArrayElementAtIndex(i).intValue = i;
    }

    private void RandomizeSequence(int length)
    {
        if (targetData == null || targetData.prefabEntries == null) return;
        int n = targetData.prefabEntries.Length;
        spawnSequenceProp.arraySize = length;
        for (int i = 0; i < length; i++)
            spawnSequenceProp.GetArrayElementAtIndex(i).intValue = Random.Range(0, n);
    }
}