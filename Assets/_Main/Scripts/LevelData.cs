using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "LevelData", menuName = "BlockPuzzle/Level Data")]
public class LevelData : ScriptableObject
{
    [Serializable]
    public class PrefabEntry
    {
        [Tooltip("Optional friendly name shown in the inspector.")]
        public string displayName;
        [Tooltip("Addressable reference to the block prefab.")]
        public AssetReferenceGameObject prefab;
    }

    [Header("Metadata")]
    public string levelName;

    [Header("Addressable prefabs (required)")]
    [Tooltip("Assign AssetReferenceGameObject entries for every block prefab used by this level.")]
    public PrefabEntry[] prefabEntries = new PrefabEntry[0];

    [Header("Play mode")]
    [Tooltip("Random = pick randomly each spawn. LevelSequence = follow spawnSequence.")]
    public PlayMode playMode = PlayMode.Random;

    [Header("Spawn sequence")]
    [Tooltip("Sequence of prefab indices to spawn; indices refer to the prefab entries array.")]
    public int[] spawnSequence = new int[0];

    [Tooltip("What happens when the spawn sequence reaches its end")]
    public SequenceEndBehavior sequenceEndBehavior = SequenceEndBehavior.Loop;

    // Runtime helpers
    public int GetPrefabCount()
    {
        return prefabEntries != null ? prefabEntries.Length : 0;
    }

    public string[] GetPrefabNames()
    {
        int n = GetPrefabCount();
        string[] names = new string[n];
        for (int i = 0; i < n; i++)
        {
            var entry = prefabEntries[i];
            if (!string.IsNullOrEmpty(entry.displayName))
            {
                names[i] = entry.displayName;
            }
            else if (entry.prefab != null && entry.prefab.RuntimeKeyIsValid())
            {
                var k = entry.prefab.RuntimeKey;
                names[i] = k != null ? k.ToString() : $"Prefab [{i}]";
            }
            else
            {
                names[i] = $"Prefab [{i}] (missing)";
            }
        }
        return names;
    }
}