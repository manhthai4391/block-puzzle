using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager ins;

    [HideInInspector]
    public Vector3 lastPosition;
    [HideInInspector]
    public Block draggedBlock;

    private ScreenOrientation screenOrientation;
    private bool orientationInitialized = false;

    private Vector3 startPos;
    private Vector2Int lastPos = new Vector2Int(-1, -1);
    private SpriteRenderer[] highlightedTiles = new SpriteRenderer[9];

    public void ResetBlock()
    {
        if (draggedBlock)
        {
            MoveDraggedBlock();
            ResetDraggedBlock();
        }
    }

    private void Awake()
    {
        if (ins == null)
        {
            ins = this;
        }
        else
        {
            Destroy(gameObject);
        }

        screenOrientation = Screen.orientation;
        orientationInitialized = true;
    }

    private void Update()
    {
        if (orientationInitialized && screenOrientation != Screen.orientation)
        {
            ResetBlock();
            screenOrientation = Screen.orientation;
        }

        if (GameManager.ins.paused)
        {
            ResetBlock();
            return;
        }

        // TOUCH input (mobile)
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);

            // BEGAN
            if (t.phase == TouchPhase.Began)
            {
                Ray ray = Camera.main.ScreenPointToRay(t.position);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 15))
                {
                    Collider c = hit.collider;
                    Block blockComponent = c.GetComponent<Block>();

                    if (blockComponent != null && blockComponent.movable && !blockComponent.IsMoving())
                    {
                        draggedBlock = blockComponent;

                        draggedBlock.Scale(true, 0.2f);

                        Color cl = draggedBlock.defaultColor; cl.a = 0.66f;
                        draggedBlock.ChangeColor(cl);

                        Vector3 p = draggedBlock.transform.position;
                        startPos = Camera.main.ScreenToWorldPoint(t.position);
                        startPos = new Vector3(startPos.x - p.x, startPos.y - p.y, 0);
                    }
                }
            }

            // MOVED
            else if (t.phase == TouchPhase.Moved && draggedBlock)
            {
                Vector3 pos = Camera.main.ScreenToWorldPoint(t.position);
                pos = new Vector3(pos.x, pos.y, -2);

                draggedBlock.transform.position = pos - startPos;

                Vector3 size = draggedBlock.GetComponent<Block>().size;
                size = new Vector3(size.x - 1, size.y - 1, 0);

                Vector2 origin = draggedBlock.transform.GetChild(0).position;
                Vector2 end = draggedBlock.transform.GetChild(0).position + size;

                if (IsInRange(origin, end) && IsEmpty(draggedBlock, RoundVector2(origin)))
                {
                    Vector2Int start = RoundVector2(origin);

                    if (lastPos != start)
                    {
                        RemoveAllHighlights();
                        BoardManager.ins.HighlightBlocks();

                        for (int i = 0; i < draggedBlock.structure.Length; i++)
                        {
                            if (draggedBlock.transform.GetChild(i).name == "Block tile")
                            {
                                Vector2Int coords = draggedBlock.structure[i];
                                highlightedTiles[i] = BoardManager.ins.boardTiles[start.x + coords.x, start.y + coords.y];
                                highlightedTiles[i].color = BoardManager.ins.highlightColor;
                            }
                        }
                    }

                    lastPos = start;
                }
                else
                {
                    RemoveAllHighlights();
                    lastPos = new Vector2Int(-1, -1);
                }
            }

            // ENDED
            else if (t.phase == TouchPhase.Ended && draggedBlock)
            {
                Vector3 size = draggedBlock.size;
                size = new Vector3(size.x - 1, size.y - 1, 0);

                Vector2 origin = draggedBlock.transform.GetChild(0).position;
                Vector2 end = draggedBlock.transform.GetChild(0).position + size;

                // CAN PUT ON BOARD
                if (IsInRange(origin, end) && IsEmpty(draggedBlock, RoundVector2(origin)))
                {
                    Vector2Int start = RoundVector2(origin);

                    // Spawn NEW board tiles with special component transfer
                    PlaceBlockOnBoard(draggedBlock, start);

                    AudioManager.ins.PlayBlockSound();

                    // Store position index before destroying
                    int blockPosIndex = draggedBlock.posIndex;

                    // Destroy the dragged block immediately (prevents duplicate tiles)
                    Destroy(draggedBlock.gameObject);

                    BoardManager.ins.MoveBlocks(blockPosIndex);
                    BoardManager.ins.CheckBoard();
                }
                // CANNOT PUT ON BOARD
                else
                {
                    MoveDraggedBlock();
                }

                ResetDraggedBlock();
            }
        }
        // MOUSE input (Editor / Desktop)
        else
        {
            // BEGAN
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 15))
                {
                    Collider c = hit.collider;
                    Block blockComponent = c.GetComponent<Block>();

                    if (blockComponent != null && blockComponent.movable && !blockComponent.IsMoving())
                    {
                        draggedBlock = blockComponent;

                        draggedBlock.Scale(true, 0.2f);

                        Color cl = draggedBlock.defaultColor; cl.a = 0.66f;
                        draggedBlock.ChangeColor(cl);

                        Vector3 p = draggedBlock.transform.position;
                        startPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                        startPos = new Vector3(startPos.x - p.x, startPos.y - p.y, 0);
                    }
                }
            }

            // MOVED (mouse held)
            else if (Input.GetMouseButton(0) && draggedBlock)
            {
                Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                pos = new Vector3(pos.x, pos.y, -2);

                draggedBlock.transform.position = pos - startPos;

                Vector3 size = draggedBlock.GetComponent<Block>().size;
                size = new Vector3(size.x - 1, size.y - 1, 0);

                Vector2 origin = draggedBlock.transform.GetChild(0).position;
                Vector2 end = draggedBlock.transform.GetChild(0).position + size;

                if (IsInRange(origin, end) && IsEmpty(draggedBlock, RoundVector2(origin)))
                {
                    Vector2Int start = RoundVector2(origin);

                    if (lastPos != start)
                    {
                        RemoveAllHighlights();
                        BoardManager.ins.HighlightBlocks();

                        for (int i = 0; i < draggedBlock.structure.Length; i++)
                        {
                            if (draggedBlock.transform.GetChild(i).name == "Block tile")
                            {
                                Vector2Int coords = draggedBlock.structure[i];
                                highlightedTiles[i] = BoardManager.ins.boardTiles[start.x + coords.x, start.y + coords.y];
                                highlightedTiles[i].color = BoardManager.ins.highlightColor;
                            }
                        }
                    }

                    lastPos = start;
                }
                else
                {
                    RemoveAllHighlights();
                    lastPos = new Vector2Int(-1, -1);
                }
            }

            // ENDED
            else if (Input.GetMouseButtonUp(0) && draggedBlock)
            {
                Vector3 size = draggedBlock.size;
                size = new Vector3(size.x - 1, size.y - 1, 0);

                Vector2 origin = draggedBlock.transform.GetChild(0).position;
                Vector2 end = draggedBlock.transform.GetChild(0).position + size;

                // CAN PUT ON BOARD
                if (IsInRange(origin, end) && IsEmpty(draggedBlock, RoundVector2(origin)))
                {
                    Vector2Int start = RoundVector2(origin);

                    // Spawn NEW board tiles with special component transfer
                    PlaceBlockOnBoard(draggedBlock, start);

                    AudioManager.ins.PlayBlockSound();

                    // Store position index before destroying
                    int blockPosIndex = draggedBlock.posIndex;

                    // Destroy the dragged block immediately (prevents duplicate tiles)
                    Destroy(draggedBlock.gameObject);

                    BoardManager.ins.MoveBlocks(blockPosIndex);
                    BoardManager.ins.CheckBoard();
                }
                // CANNOT PUT ON BOARD
                else
                {
                    MoveDraggedBlock();
                }

                ResetDraggedBlock();
            }
        }
    }

    /// <summary>
    /// Place block tiles on board while preserving special tile components.
    /// Creates NEW tiles as children of the board (not the block).
    /// </summary>
    private void PlaceBlockOnBoard(Block block, Vector2Int start)
    {
        Debug.Log($"=== PlaceBlockOnBoard: start position ({start.x}, {start.y}), structure length: {block.structure.Length} ===");

        for (int i = 0; i < block.structure.Length; i++)
        {
            Vector2Int coords = block.structure[i];
            Debug.Log($"Processing structure[{i}]: coords=({coords.x}, {coords.y})");

            if (block.transform.GetChild(i).name == "Block tile")
            {
                // Get the source BlockTile. This will get a SpecialBlockTile if one exists,
                // because SpecialBlockTile IS-A BlockTile.
                BlockTile sourceBlockTile = block.transform.GetChild(i).GetComponent<BlockTile>();

                if (sourceBlockTile == null)
                {
                    Debug.LogError($"Child {i} '{block.transform.GetChild(i).name}' has no BlockTile (or SpecialBlockTile) component!");
                    continue;
                }

                // DEBUG: Check if it's a special tile by casting
                SpecialBlockTile specialCheck = sourceBlockTile as SpecialBlockTile;
                Debug.Log($"Child {i} '{block.transform.GetChild(i).name}' has SpecialBlockTile: {specialCheck != null}");
                if (specialCheck != null)
                {
                    Debug.Log($"  -> gemSprite assigned: {specialCheck.gemSprite != null}, sprite name: {(specialCheck.gemSprite != null ? specialCheck.gemSprite.name : "null")}");
                }

                int boardX = start.x + coords.x;
                int boardY = start.y + coords.y;

                Debug.Log($"Calling SpawnBlockTile for child {i} at board position ({boardX}, {boardY})");

                // Spawn NEW board tile (as child of board) WITH special component transfer
                // BoardManager.SpawnBlockTile will handle the logic of creating the correct tile type.
                BlockTile boardTile = BoardManager.ins.SpawnBlockTile(boardX, boardY, sourceBlockTile);

                // The boardTile is already assigned in SpawnBlockTile, but let's be explicit
                BoardManager.ins.boardBlocks[boardX, boardY] = boardTile;
            }
        }
    }

    private Vector2Int RoundVector2(Vector2 v)
    {
        return new Vector2Int((int)(v.x + 0.5f), (int)(v.y + 0.5f));
    }

    private Vector3 BlockPosition(Vector2 o, Vector2 s)
    {
        Vector3 off = Vector3.zero;

        if (s.x % 2 == 1)
            off.x = 0.5f;
        if (s.y % 2 == 1)
            off.y = 0.5f;

        return new Vector3((int)(o.x + 0.5f) + (int)(s.x / 2), (int)(o.y + 0.5f) + (int)(s.y / 2), -1) + off;
    }

    private bool IsInRange(Vector2 o, Vector2 e)
    {
        return BoardManager.ins.IsInRange(o, e);
    }

    private bool IsEmpty(Block b, Vector2 o)
    {
        return BoardManager.ins.IsEmpty(b, o);
    }

    private void RemoveAllHighlights()
    {
        if (!GameManager.ins.gameOver)
            foreach (BlockTile b in BoardManager.ins.boardBlocks)
                if (b)
                    b.Fade(0.2f, b.defaultColor);

        for (int i = 0; i < 9; i++)
        {
            if (highlightedTiles[i])
            {
                highlightedTiles[i].color = BoardManager.ins.boardColor;
                highlightedTiles[i] = null;
            }
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
            ResetBlock();
    }

    private void MoveDraggedBlock()
    {
        draggedBlock.Scale(false, 0.2f);
        draggedBlock.Move(0.25f, draggedBlock.basePosition);
        draggedBlock.ChangeColor(draggedBlock.defaultColor);
    }

    private void ResetDraggedBlock()
    {
        startPos = Vector3.zero;
        draggedBlock = null;
        RemoveAllHighlights();
    }
}