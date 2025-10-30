using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[DefaultExecutionOrder(-1000)]
public class LevelManager : MonoBehaviour
{
    public static LevelManager ins;

    [Tooltip("Enable to use level data sequence for spawning blocks.")]
    public bool useLevelData = true;

    [Tooltip("LevelData asset that contains addressable prefab references and spawn sequence.")]
    public LevelData levelData;

    [Tooltip("Preload addressable prefabs when loading a level (recommended).")]
    public bool preloadPrefabs = true;

    [HideInInspector]
    public bool ready = false;

    private GameObject[] loadedPrefabs;
    private AsyncOperationHandle<GameObject>[] loadHandles;
    private int sequenceIndex = 0;
    private bool sequenceCompleted = false; // Track if sequence has finished once

    // Events callers can subscribe to
    public event Action OnLevelLoaded;
    public event Action OnLevelUnloaded;

    private void Awake()
    {
        if (!ins) ins = this;
        else if (ins != this)
        {
            Destroy(gameObject);
            return;
        }

        if (!useLevelData || levelData == null)
        {
            ready = true;
            return;
        }

        ready = false;
    }

    private IEnumerator PreloadAddressablePrefabs()
    {
        if (loadHandles != null)
        {
            for (int r = 0; r < loadHandles.Length; r++)
                if (loadHandles[r].IsValid())
                    Addressables.Release(loadHandles[r]);

            loadHandles = null;
            loadedPrefabs = null;
        }

        int n = levelData.prefabEntries.Length;
        loadedPrefabs = new GameObject[n];
        loadHandles = new AsyncOperationHandle<GameObject>[n];

        for (int i = 0; i < n; i++)
        {
            var entry = levelData.prefabEntries[i];
            if (entry != null && entry.prefab != null && entry.prefab.RuntimeKeyIsValid())
            {
                var handle = entry.prefab.LoadAssetAsync<GameObject>();
                loadHandles[i] = handle;
                yield return handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                    loadedPrefabs[i] = handle.Result;
                else
                    Debug.LogWarning($"LevelManager: failed to load addressable prefab at index {i} (key: {entry.prefab.RuntimeKey}).");
            }
            else
            {
                Debug.LogWarning($"LevelManager: prefab entry {i} is null or missing AssetReference.");
            }
        }

        ready = true;
    }

    /// <summary>
    /// Number of configured prefab entries in the current LevelData.
    /// </summary>
    public int PrefabCount => levelData != null && levelData.prefabEntries != null ? levelData.prefabEntries.Length : 0;

    /// <summary>
    /// Returns the play mode for the active LevelData (or Random if none).
    /// </summary>
    public PlayMode ActivePlayMode => levelData != null ? levelData.playMode : PlayMode.Random;

    /// <summary>
    /// Check if spawning is allowed (only relevant when sequence ends with Stop behavior)
    /// </summary>
    public bool CanSpawn()
    {
        if (!useLevelData || levelData == null)
            return true;

        // If not in sequence mode, always can spawn
        if (levelData.playMode != PlayMode.LevelSequence)
            return true;

        // If sequence hasn't completed, can spawn
        if (!sequenceCompleted)
            return true;

        // If completed and behavior is Stop, cannot spawn
        return levelData.sequenceEndBehavior != SequenceEndBehavior.Stop;
    }

    /// <summary>
    /// Choose the next prefab index according to the active play mode and sequence end behavior.
    /// Returns -1 if spawning should stop (when SequenceEndBehavior.Stop is active and sequence completed).
    /// </summary>
    public int GetPrefabIndexForNextSpawn()
    {
        if (!useLevelData || levelData == null)
            return UnityEngine.Random.Range(0, Math.Max(1, PrefabCount));

        if (levelData.playMode == PlayMode.LevelSequence && levelData.spawnSequence != null && levelData.spawnSequence.Length > 0)
        {
            // Check if we've reached the end of the sequence
            if (sequenceIndex >= levelData.spawnSequence.Length)
            {
                sequenceCompleted = true;

                switch (levelData.sequenceEndBehavior)
                {
                    case SequenceEndBehavior.Loop:
                        // Reset and continue from start
                        sequenceIndex = 0;
                        break;

                    case SequenceEndBehavior.Random:
                        // Switch to random spawning
                        int length = PrefabCount > 0 ? PrefabCount : 1;
                        return UnityEngine.Random.Range(0, length);

                    case SequenceEndBehavior.Stop:
                        // Stop spawning
                        return -1;
                }
            }

            int value = levelData.spawnSequence[sequenceIndex];
            sequenceIndex++;
            return value;
        }

        // Random mode
        int maxIndex = PrefabCount > 0 ? PrefabCount : 1;
        return UnityEngine.Random.Range(0, maxIndex);
    }

    /// <summary>
    /// Dynamically load a new level (unloads previous if present). Call this when user presses Play.
    /// </summary>
    public void LoadLevel(LevelData newLevel)
    {
        StartCoroutine(LoadLevelCoroutine(newLevel));
    }

    private IEnumerator LoadLevelCoroutine(LevelData newLevel)
    {
        UnloadLevel(true);

        levelData = newLevel;
        sequenceIndex = 0;
        sequenceCompleted = false; // Reset sequence state
        ready = false;

        if (!useLevelData || levelData == null)
        {
            ready = true;
            OnLevelLoaded?.Invoke();
            yield break;
        }

        if (levelData.prefabEntries == null || levelData.prefabEntries.Length == 0)
        {
            Debug.LogWarning("LevelManager.LoadLevel: provided LevelData has no prefabEntries.");
            ready = true;
            OnLevelLoaded?.Invoke();
            yield break;
        }

        if (preloadPrefabs)
            yield return StartCoroutine(PreloadAddressablePrefabs());
        else
            ready = true;

        OnLevelLoaded?.Invoke();
    }

    /// <summary>
    /// Unload current level assets. If destroyInstances==true, ask BoardManager to destroy instantiated blocks/tiles.
    /// </summary>
    public void UnloadLevel(bool destroyInstances = true)
    {
        if (destroyInstances && BoardManager.ins != null)
            BoardManager.ins.ClearLevelBlocks();

        if (loadHandles != null)
        {
            for (int i = 0; i < loadHandles.Length; i++)
            {
                if (loadHandles[i].IsValid())
                    Addressables.Release(loadHandles[i]);
            }
            loadHandles = null;
        }

        loadedPrefabs = null;
        ready = false;
        sequenceIndex = 0;
        sequenceCompleted = false;

        OnLevelUnloaded?.Invoke();
    }

    /// <summary>
    /// Returns a loaded prefab if it was preloaded; otherwise null.
    /// </summary>
    public GameObject GetPrefab(int prefabIndex)
    {
        if (loadedPrefabs != null && prefabIndex >= 0 && prefabIndex < loadedPrefabs.Length)
            return loadedPrefabs[prefabIndex];

        return null;
    }

    /// <summary>
    /// Synchronous-or-async spawn helper. If prefab was preloaded it instantiates synchronously and invokes onComplete immediately.
    /// Otherwise it starts Addressables.InstantiateAsync and invokes onComplete when ready.
    /// </summary>
    public void SpawnBlockAsync(int spawnSlotIndex, int prefabIndex, Transform parent, Action<Block> onComplete)
    {
        // Check if spawning is stopped
        if (!CanSpawn() || prefabIndex < 0)
        {
            onComplete?.Invoke(null);
            return;
        }

        if (levelData == null || levelData.prefabEntries == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        if (prefabIndex < 0 || prefabIndex >= levelData.prefabEntries.Length)
        {
            onComplete?.Invoke(null);
            return;
        }

        var loaded = GetPrefab(prefabIndex);
        if (loaded != null)
        {
            var go = Instantiate(loaded, parent);
            var b = go.GetComponent<Block>();
            if (b != null)
            {
                b.prefabIndex = prefabIndex;
                b.SetBasePosition(spawnSlotIndex);
            }
            onComplete?.Invoke(b);
            return;
        }

        var assetRef = levelData.prefabEntries[prefabIndex].prefab;
        if (assetRef != null && assetRef.RuntimeKeyIsValid())
        {
            var handle = assetRef.InstantiateAsync(parent);
            handle.Completed += h =>
            {
                var b = h.Result.GetComponent<Block>();
                if (b != null)
                {
                    b.prefabIndex = prefabIndex;
                    b.SetBasePosition(spawnSlotIndex);
                }
                onComplete?.Invoke(b);
            };
            return;
        }

        onComplete?.Invoke(null);
    }

    /// <summary>
    /// Pick a random prefab index (within prefabEntries length) and spawn it async (or sync if preloaded).
    /// </summary>
    public void SpawnRandomBlockAsync(int spawnSlotIndex, Transform parent, Action<Block> onComplete)
    {
        if (levelData == null || levelData.prefabEntries == null || levelData.prefabEntries.Length == 0)
        {
            onComplete?.Invoke(null);
            return;
        }

        int randomPrefabIndex = UnityEngine.Random.Range(0, levelData.prefabEntries.Length);
        SpawnBlockAsync(spawnSlotIndex, randomPrefabIndex, parent, onComplete);
    }

    public int GetNextPrefabIndex()
    {
        return GetPrefabIndexForNextSpawn();
    }

    public void ResetSequence(int start = 0)
    {
        sequenceIndex = Math.Max(0, start);
        sequenceCompleted = false;
    }

    private void OnDestroy()
    {
        if (loadHandles != null)
        {
            for (int i = 0; i < loadHandles.Length; i++)
            {
                if (loadHandles[i].IsValid())
                    Addressables.Release(loadHandles[i]);
            }
        }
    }
}