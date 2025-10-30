using UnityEngine;

// Inherit from BlockTile
public class SpecialBlockTile : BlockTile
{
    [Header("Special Tile Settings")]
    public SpecialTileType tileType = SpecialTileType.Gem;
    public Sprite gemSprite; // Assign a gem sprite in the inspector

    private bool hasBeenDestroyed = false;

    public enum SpecialTileType
    {
        Gem,
        Star,
        Diamond,
        // Add more types as needed
    }

    // The base BlockTile.Awake() will run automatically.
    // We use Start() to apply our custom sprite *after* Awake runs.
    private void Start()
    {
        // Apply the gem sprite if assigned
        ApplyGemSprite();
    }

    private void ApplyGemSprite()
    {
        if (gemSprite != null)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = gemSprite;
            }
        }
    }

    public void OnDestroyed()
    {
        if (hasBeenDestroyed) return;

        hasBeenDestroyed = true;

        // Notify the tracker
        if (SpecialTileTracker.ins != null)
        {
            SpecialTileTracker.ins.OnSpecialTileDestroyed(tileType);
        }

        Debug.Log($"Special tile {tileType} destroyed!");
    }

    public SpecialTileType GetTileType()
    {
        return tileType;
    }
}