using UnityEngine;
using System;
using System.Collections;

public class BoardManager : MonoBehaviour
{
    public static BoardManager ins;

    public const int BOARD_SIZE = 10;
    public const int BLOCKS_AMOUNT = 3;

    public GameObject boardTilePrefab;
    public GameObject blockTilePrefab;
    public Transform gameTransform;
    public Transform boardTransform;
    public Color boardColor;
    public Color highlightColor;

    [Tooltip("If true and LevelData is enabled, ignore saved block slots (PlayerPrefs) so spawnSequence controls initial slots.")]
    public bool ignoreSavedSlotsWhenUsingLevelData = true;

    private PlayMode cachedPlayMode = PlayMode.Random;
    private bool cachedUseLevelData = false;
    private bool cachedPreloadPrefabs = true;

    public Vector3 boardTileScale { get; set; }
    public Vector3 scaledBlockTileScale { get; set; }
    public SpriteRenderer[,] boardTiles { get; set; } = new SpriteRenderer[BOARD_SIZE, BOARD_SIZE];
    public BlockTile[,] boardBlocks { get; set; } = new BlockTile[BOARD_SIZE, BOARD_SIZE];
    public Block[] blocks { get; set; }

    private bool _boardBackgroundCreated = false;
    private bool _boardSlotsCreated = false;
    private bool _stopSpawning = false;

    private void UpdateCachedLevelSettings()
    {
        if (LevelManager.ins != null)
        {
            cachedUseLevelData = LevelManager.ins.useLevelData;
            cachedPreloadPrefabs = LevelManager.ins.preloadPrefabs;
            cachedPlayMode = LevelManager.ins.ActivePlayMode;
        }
        else
        {
            cachedUseLevelData = false;
            cachedPreloadPrefabs = true;
            cachedPlayMode = PlayMode.Random;
        }
    }

    private string GetNamespacedSlotKey(int slot)
    {
        if (LevelManager.ins != null && LevelManager.ins.levelData != null)
        {
            var ld = LevelManager.ins.levelData;
            string levelKey = !string.IsNullOrEmpty(ld.levelName) ? ld.levelName : ld.name;
            if (!string.IsNullOrEmpty(levelKey))
                return $"{levelKey}_block_{slot}";
        }
        return null;
    }

    private bool TryGetSavedBlockPrefabIndex(int slot, out int prefabIndex)
    {
        prefabIndex = -1;

        if (ignoreSavedSlotsWhenUsingLevelData && LevelManager.ins != null && LevelManager.ins.useLevelData)
            return false;

        string namespacedKey = GetNamespacedSlotKey(slot);
        if (!string.IsNullOrEmpty(namespacedKey) && PlayerPrefs.HasKey(namespacedKey))
        {
            prefabIndex = PlayerPrefs.GetInt(namespacedKey);
            return true;
        }

        string legacyKey = slot + "block";
        if (PlayerPrefs.HasKey(legacyKey))
        {
            prefabIndex = PlayerPrefs.GetInt(legacyKey);
            return true;
        }

        return false;
    }

    private void HandleLevelLoaded()
    {
        UpdateCachedLevelSettings();
        int size = Math.Max(BLOCKS_AMOUNT, LevelManager.ins != null ? LevelManager.ins.PrefabCount : BLOCKS_AMOUNT);
        blocks = new Block[size];
        CreateBlockSlots();

        for (int i = 0; i < BLOCKS_AMOUNT && i < blocks.Length; i++)
        {
            if (TryGetSavedBlockPrefabIndex(i, out int savedPrefabIndex))
            {
                if (LevelManager.ins == null)
                {
                    Debug.LogWarning($"HandleLevelLoaded: LevelManager missing - ignoring saved slot {i}.");
                    continue;
                }

                if (savedPrefabIndex < 0 || savedPrefabIndex >= LevelManager.ins.PrefabCount)
                {
                    Debug.LogWarning($"HandleLevelLoaded: saved prefabIndex {savedPrefabIndex} for slot {i} is invalid for current level. Ignoring saved value.");
                    continue;
                }

                if (cachedPreloadPrefabs && LevelManager.ins.ready)
                {
                    blocks[i] = SpawnBlock(i, savedPrefabIndex);
                }
                else
                {
                    int slot = i;
                    SpawnBlockAsync(slot, savedPrefabIndex, (b) => { blocks[slot] = b; });
                }
            }
        }
    }

    private void HandleLevelUnloaded()
    {
        UpdateCachedLevelSettings();
        _boardSlotsCreated = false;
        _stopSpawning = false;
    }

    public bool IsInRange(Vector2 o, Vector2 e)
    {
        return o.x >= -0.5f && e.x <= BOARD_SIZE - 0.5f &&
               o.y >= -0.5f && e.y <= BOARD_SIZE - 0.5f;
    }

    public bool IsEmpty(Block b, Vector2 o)
    {
        for (int i = 0; i < b.structure.Length; i++)
        {
            if (b.transform.childCount > i)
            {
                if (b.transform.GetChild(i).name == "Block tile")
                {
                    Vector2Int coords = b.structure[i];
                    if (boardBlocks[(int)o.x + coords.x, (int)o.y + coords.y])
                        return false;
                }
            }
        }
        return true;
    }

    public static int Rand(int min, int max)
    {
        return (int)UnityEngine.Random.Range(min, max - 0.000001f);
    }

    // ... (keep all other code in BoardManager the same) ...

    /// <summary>
    /// Enhanced version that transfers special tile data from source block tile
    /// </summary>
    public BlockTile SpawnBlockTile(int x, int y, BlockTile sourceBlockTile = null)
    {
        // 1. Instantiate the prefab and get its GameObject
        GameObject newTileGO = Instantiate(blockTilePrefab, boardTransform);
        newTileGO.transform.position = new Vector3(x, y, -1);
        newTileGO.transform.localScale = boardTileScale;

        // 2. Get the base BlockTile component that's on the prefab
        BlockTile newTileComponent = newTileGO.GetComponent<BlockTile>();

        // 3. If a source tile is provided, check its type
        if (sourceBlockTile != null)
        {
            // Check if the source tile is a SpecialBlockTile using a cast
            SpecialBlockTile sourceSpecial = sourceBlockTile as SpecialBlockTile;

            Debug.Log($"SpawnBlockTile at ({x},{y}): source is special? {sourceSpecial != null}");

            if (sourceSpecial != null)
            {
                // IT IS SPECIAL.
                // 1. Destroy the base BlockTile component.
                Destroy(newTileComponent);

                // 2. Add the SpecialBlockTile component. Its Awake() will run.
                SpecialBlockTile newSpecial = newTileGO.AddComponent<SpecialBlockTile>();

                // 3. Copy the special properties.
                newSpecial.tileType = sourceSpecial.tileType;
                newSpecial.gemSprite = sourceSpecial.gemSprite;

                // 4. Set this as the component to be stored in the board array
                newTileComponent = newSpecial;

                Debug.Log($"Special tile {sourceSpecial.tileType} transferred to board at ({x}, {y})");
            }

            // 5. Copy visual properties
            SpriteRenderer sourceSR = sourceBlockTile.GetComponent<SpriteRenderer>();
            SpriteRenderer targetSR = newTileGO.GetComponent<SpriteRenderer>();

            if (sourceSR != null && targetSR != null)
            {
                // If it's a normal tile, copy the sprite.
                // If it's a special tile, its own Start() method will set the gemSprite.
                if (sourceSpecial == null)
                {
                    targetSR.sprite = sourceSR.sprite;
                }
                targetSR.color = sourceSR.color;
            }

            // 6. Ensure defaultColor is copied from the source
            newTileComponent.defaultColor = sourceBlockTile.defaultColor;
        }

        // 7. Store the correct component (BlockTile or SpecialBlockTile) in the array
        boardBlocks[x, y] = newTileComponent;
        return newTileComponent;
    }

    /// <summary>
    /// Place a block on the board, preserving special tile components from the addressable prefab
    /// </summary>
    public void PlaceBlockOnBoard(Block block, Vector2Int origin)
    {
        if (block == null)
        {
            Debug.LogError("PlaceBlockOnBoard: block is null");
            return;
        }

        for (int i = 0; i < block.structure.Length; i++)
        {
            if (block.transform.childCount > i)
            {
                Transform childTransform = block.transform.GetChild(i);
                if (childTransform.name == "Block tile")
                {
                    Vector2Int coords = block.structure[i];
                    int boardX = origin.x + coords.x;
                    int boardY = origin.y + coords.y;

                    // Check bounds
                    if (boardX < 0 || boardX >= BOARD_SIZE || boardY < 0 || boardY >= BOARD_SIZE)
                    {
                        Debug.LogWarning($"PlaceBlockOnBoard: coordinate ({boardX}, {boardY}) out of bounds");
                        continue;
                    }

                    // Get the source BlockTile. Because of inheritance, this will 
                    // get EITHER a BlockTile OR a SpecialBlockTile.
                    BlockTile sourceBlockTile = childTransform.GetComponent<BlockTile>();

                    // Spawn board tile and transfer special properties
                    SpawnBlockTile(boardX, boardY, sourceBlockTile);
                }
            }
        }
    }


    public Block SpawnBlock(int i, int prefabIndex)
    {
        if (!cachedUseLevelData || LevelManager.ins == null)
        {
            Debug.LogError("SpawnBlock: LevelManager missing or LevelData not enabled.");
            return null;
        }

        if (cachedPreloadPrefabs && LevelManager.ins.ready)
        {
            GameObject prefab = LevelManager.ins.GetPrefab(prefabIndex);
            if (prefab == null)
            {
                Debug.LogWarning($"SpawnBlock: requested prefabIndex {prefabIndex} is not preloaded.");
            }
            else
            {
                Block block = Instantiate(prefab, gameTransform).GetComponent<Block>();
                if (block != null)
                {
                    block.prefabIndex = prefabIndex;
                    block.SetBasePosition(i);
                }
                return block;
            }
        }

        LevelManager.ins.SpawnBlockAsync(i, prefabIndex, gameTransform, (spawned) =>
        {
            if (spawned != null)
            {
                spawned.prefabIndex = prefabIndex;
                spawned.SetBasePosition(i);
                blocks[i] = spawned;
            }
            else
            {
                Debug.LogError($"SpawnBlockAsync callback: failed to instantiate prefabIndex {prefabIndex} for slot {i}.");
            }
        });

        return null;
    }

    public void SpawnBlockAsync(int slotIndex, int prefabIndex, Action<Block> onComplete = null)
    {
        if (!cachedUseLevelData || LevelManager.ins == null)
        {
            Debug.LogError("SpawnBlockAsync: LevelManager not available or not using level data.");
            onComplete?.Invoke(null);
            return;
        }

        LevelManager.ins.SpawnBlockAsync(slotIndex, prefabIndex, gameTransform, (b) =>
        {
            if (b != null)
            {
                b.prefabIndex = prefabIndex;
                b.SetBasePosition(slotIndex);
            }
            onComplete?.Invoke(b);
        });
    }

    public void SpawnRandomBlockAsync(int slotIndex, Action<Block> onComplete = null)
    {
        if (!cachedUseLevelData || LevelManager.ins == null)
        {
            Debug.LogError("SpawnRandomBlockAsync: LevelManager not available.");
            onComplete?.Invoke(null);
            return;
        }

        LevelManager.ins.SpawnRandomBlockAsync(slotIndex, gameTransform, (b) =>
        {
            if (b != null)
                b.SetBasePosition(slotIndex);
            onComplete?.Invoke(b);
        });
    }

    public int GetEmptyFieldsAmount()
    {
        int x = 0;
        foreach (BlockTile b in boardBlocks)
            if (!b)
                x++;
        return x;
    }

    public void MoveBlocks(int i)
    {
        if (blocks == null || blocks.Length < BLOCKS_AMOUNT)
            return;

        if (!cachedUseLevelData || LevelManager.ins == null)
        {
            Debug.LogError("MoveBlocks: LevelManager missing.");
            return;
        }

        if (!LevelManager.ins.CanSpawn())
        {
            Debug.Log("MoveBlocks: Spawning stopped - sequence has ended with Stop behavior.");
            return;
        }

        while (i < BLOCKS_AMOUNT - 1)
        {
            blocks[i] = blocks[i + 1];
            blocks[i].SetBasePosition(i, false);
            blocks[i].Move(0.2f, blocks[i].basePosition);
            blocks[++i] = null;
        }

        int spawnSlot = i + 2;

        if (!cachedPreloadPrefabs)
        {
            if (cachedPlayMode == PlayMode.LevelSequence)
            {
                int prefabIndex = LevelManager.ins.GetPrefabIndexForNextSpawn();

                if (prefabIndex < 0)
                {
                    _stopSpawning = true;
                    return;
                }

                SpawnBlockAsync(spawnSlot, prefabIndex, (b) =>
                {
                    blocks[i] = b;
                    if (blocks[i] != null)
                    {
                        blocks[i].SetBasePosition(i, false);
                        blocks[i].Move(0.2f, blocks[i].basePosition);
                    }
                });
            }
            else
            {
                SpawnRandomBlockAsync(spawnSlot, (b) =>
                {
                    blocks[i] = b;
                    if (blocks[i] != null)
                    {
                        blocks[i].SetBasePosition(i, false);
                        blocks[i].Move(0.2f, blocks[i].basePosition);
                    }
                });
            }
            return;
        }

        int prefabIndexSync = (cachedPlayMode == PlayMode.LevelSequence)
            ? LevelManager.ins.GetPrefabIndexForNextSpawn()
            : UnityEngine.Random.Range(0, Math.Max(1, LevelManager.ins.PrefabCount));

        if (prefabIndexSync < 0)
        {
            Debug.Log("MoveBlocks: Spawning stopped - sequence ended.");
            return;
        }

        blocks[i] = SpawnBlock(spawnSlot, prefabIndexSync);
        if (blocks[i] != null)
        {
            blocks[i].SetBasePosition(i, false);
            blocks[i].Move(0.2f, blocks[i].basePosition);
        }
    }

    public void CheckSpace(bool oa)
    {
        if (blocks == null || blocks.Length < BLOCKS_AMOUNT)
            return;

        int count = 0;
        for (int i = 0; i < BLOCKS_AMOUNT; i++)
        {
            if (blocks[i] == null)
            {
                if (cachedUseLevelData && LevelManager.ins != null && LevelManager.ins.CanSpawn())
                {
                    if (cachedPreloadPrefabs && LevelManager.ins.ready)
                    {
                        int prefabIndex = (cachedPlayMode == PlayMode.LevelSequence)
                            ? LevelManager.ins.GetPrefabIndexForNextSpawn()
                            : UnityEngine.Random.Range(0, Math.Max(1, LevelManager.ins.PrefabCount));

                        if (prefabIndex >= 0)
                        {
                            blocks[i] = SpawnBlock(i, prefabIndex);
                            if (blocks[i] != null)
                                blocks[i].SetBasePosition(i, false);
                        }
                    }
                }
            }

            if (blocks[i] != null && CheckBlock(i))
            {
                blocks[i].movable = true;
                Color c = blocks[i].GetColor();
                c.a = 1;
                blocks[i].ChangeColor(c);
            }
            else
            {
                if (blocks[i] != null)
                {
                    blocks[i].movable = false;
                    Color c = blocks[i].GetColor();
                    c.a = 0.5f;
                    blocks[i].ChangeColor(c);
                }
                count++;
            }

            if (oa && count == BLOCKS_AMOUNT)
                GameManager.ins.RestartGame();
            else if (count == BLOCKS_AMOUNT)
                StartCoroutine(GameManager.ins.WaitForFade());
        }
    }

    public void CheckBoard(bool onAwake = false)
    {
        if (blocks == null || blocks.Length < BLOCKS_AMOUNT)
            return;

        DestroyManager.ins.SetDestroy();

        for (int x = 0; x < BOARD_SIZE; x++)
            CheckVLine(x);
        for (int y = 0; y < BOARD_SIZE; y++)
            CheckHLine(y);

        if (DestroyManager.ins.destroyedLines > 0)
            StartCoroutine(DestroyManager.ins.DestroyAllBlocks());
        else
            CheckSpace(onAwake);
    }

    public void HighlightBlocks()
    {
        if (blocks == null || blocks.Length < BLOCKS_AMOUNT)
            return;

        Block db = InputManager.ins.draggedBlock;
        Vector2Int c = db.GetFirstCoords();

        for (int x = c.x; x < c.x + db.size.x; x++)
            CheckVLine(x, true);
        for (int y = c.y; y < c.y + db.size.y; y++)
            CheckHLine(y, true);
    }

    private void Awake()
    {
        if (ins == null)
            ins = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        boardTileScale = GameScaler.GetBoardTileScale();
        scaledBlockTileScale = GameScaler.GetScaledBlockTileScale();

        CreateBoardBackground();

        UpdateCachedLevelSettings();
        if (LevelManager.ins != null)
        {
            LevelManager.ins.OnLevelLoaded += HandleLevelLoaded;
            LevelManager.ins.OnLevelUnloaded += HandleLevelUnloaded;

            if (LevelManager.ins.ready)
                HandleLevelLoaded();
        }
        else
        {
            Debug.LogWarning("BoardManager Awake: LevelManager not found.");
        }
    }

    private void CreateBoardBackground()
    {
        if (_boardBackgroundCreated) return;
        _boardBackgroundCreated = true;

        Vector3 scale = GameScaler.GetBoardTileScale();

        for (int y = 0; y < BOARD_SIZE; y++)
        {
            for (int x = 0; x < BOARD_SIZE; x++)
            {
                Transform t = Instantiate(boardTilePrefab, boardTransform).transform;
                t.position = new Vector3(x, y, 0);
                t.localScale = scale;
                boardTiles[x, y] = t.GetComponent<SpriteRenderer>();
            }
        }
    }

    private void CreateBlockSlots()
    {
        if (_boardSlotsCreated) return;
        _boardSlotsCreated = true;

        for (int i = 0; i < BLOCKS_AMOUNT; i++)
        {
            if (!TryGetSavedBlockPrefabIndex(i, out _))
            {
                if (!cachedUseLevelData || LevelManager.ins == null)
                {
                    Debug.LogError("CreateBlockSlots: LevelManager missing or LevelData disabled.");
                    continue;
                }

                if (!LevelManager.ins.CanSpawn())
                {
                    Debug.Log("CreateBlockSlots: Spawning stopped - sequence ended with Stop behavior.");
                    continue;
                }

                if (!cachedPreloadPrefabs)
                {
                    if (cachedPlayMode == PlayMode.LevelSequence)
                    {
                        int prefabIndex = LevelManager.ins.GetPrefabIndexForNextSpawn();
                        if (prefabIndex >= 0)
                        {
                            int slot = i;
                            SpawnBlockAsync(slot, prefabIndex, (b) => { blocks[slot] = b; });
                        }
                    }
                    else
                    {
                        int slot = i;
                        SpawnRandomBlockAsync(slot, (b) => { blocks[slot] = b; });
                    }
                }
                else
                {
                    int prefabIndex = (cachedPlayMode == PlayMode.LevelSequence)
                        ? LevelManager.ins.GetPrefabIndexForNextSpawn()
                        : UnityEngine.Random.Range(0, Math.Max(1, LevelManager.ins.PrefabCount));

                    if (prefabIndex >= 0)
                        blocks[i] = SpawnBlock(i, prefabIndex);
                }
            }
        }
    }

    public void ClearLevelBlocks(bool destroyBoardTiles = false)
    {
        for (int y = 0; y < BOARD_SIZE; y++)
        {
            for (int x = 0; x < BOARD_SIZE; x++)
            {
                if (boardBlocks[x, y])
                {
                    Destroy(boardBlocks[x, y].gameObject);
                    boardBlocks[x, y] = null;
                }
            }
        }

        if (blocks != null)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i])
                {
                    Destroy(blocks[i].gameObject);
                    blocks[i] = null;
                }
            }
        }

        if (destroyBoardTiles)
        {
            for (int y = 0; y < BOARD_SIZE; y++)
                for (int x = 0; x < BOARD_SIZE; x++)
                    if (boardTiles[x, y])
                    {
                        Destroy(boardTiles[x, y].gameObject);
                        boardTiles[x, y] = null;
                    }
        }
    }

    private void CheckHLine(int y, bool h = false)
    {
        if (h)
        {
            BlockTile[,] b = new BlockTile[BOARD_SIZE, BOARD_SIZE];
            Array.Copy(boardBlocks, b, boardBlocks.Length);

            Block db = InputManager.ins.draggedBlock;
            Vector2Int c = db.GetFirstCoords();
            for (int i = 0; i < db.structure.Length; i++)
            {
                if (db.transform.GetChild(i).name == "Block tile")
                {
                    BlockTile bt = db.transform.GetChild(i).GetComponent<BlockTile>();
                    b[c.x + db.structure[i].x, c.y + db.structure[i].y] = bt;
                }
            }

            for (int x = 0; x < BOARD_SIZE; x++)
                if (!b[x, y])
                    return;

            for (int x = 0; x < BOARD_SIZE; x++)
                if (boardBlocks[x, y])
                    boardBlocks[x, y].Fade(0.2f, db.defaultColor);
        }
        else
        {
            for (int x = 0; x < BOARD_SIZE; x++)
                if (!boardBlocks[x, y])
                    return;

            DestroyManager.ins.PrepareToDestroy(y, false);
        }
    }

    private void CheckVLine(int x, bool h = false)
    {
        if (h)
        {
            BlockTile[,] b = new BlockTile[BOARD_SIZE, BOARD_SIZE];
            Array.Copy(boardBlocks, b, boardBlocks.Length);

            Block db = InputManager.ins.draggedBlock;
            Vector2Int c = db.GetFirstCoords();
            for (int i = 0; i < db.structure.Length; i++)
            {
                if (db.transform.GetChild(i).name == "Block tile")
                {
                    BlockTile bt = db.transform.GetChild(i).GetComponent<BlockTile>();
                    b[c.x + db.structure[i].x, c.y + db.structure[i].y] = bt;
                }
            }

            for (int y = 0; y < BOARD_SIZE; y++)
                if (!b[x, y])
                    return;

            for (int y = 0; y < BOARD_SIZE; y++)
                if (boardBlocks[x, y])
                    boardBlocks[x, y].Fade(0.2f, db.defaultColor);
        }
        else
        {
            for (int y = 0; y < BOARD_SIZE; y++)
                if (!boardBlocks[x, y])
                    return;

            DestroyManager.ins.PrepareToDestroy(x, true);
        }
    }

    private bool CheckBlock(int i)
    {
        for (int y = 0; y < BOARD_SIZE; y++)
        {
            for (int x = 0; x < BOARD_SIZE; x++)
            {
                Vector2 size = new Vector2(blocks[i].size.x - 1, blocks[i].size.y - 1);
                Vector2 origin = new Vector2(x, y);
                Vector2 end = origin + size;

                if (IsInRange(origin, end) && IsEmpty(blocks[i], origin))
                    return true;
            }
        }

        return false;
    }

    private void OnDestroy()
    {
        if (LevelManager.ins != null)
        {
            LevelManager.ins.OnLevelLoaded -= HandleLevelLoaded;
            LevelManager.ins.OnLevelUnloaded -= HandleLevelUnloaded;
        }
    }
}