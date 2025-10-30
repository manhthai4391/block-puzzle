using UnityEngine;
using System.Collections.Generic;

public class SpecialTileTracker : MonoBehaviour
{
    public static SpecialTileTracker ins;

    [Header("Statistics")]
    public int totalGemsDestroyed = 0;
    public int totalStarsDestroyed = 0;
    public int totalDiamondsDestroyed = 0;

    // Dictionary to track all special tile types
    private Dictionary<SpecialBlockTile.SpecialTileType, int> specialTileCount =
        new Dictionary<SpecialBlockTile.SpecialTileType, int>();

    // Events for other systems to subscribe to
    public System.Action<SpecialBlockTile.SpecialTileType> OnSpecialTileDestroyedEvent;

    private void Awake()
    {
        if (ins == null)
        {
            ins = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeTracker();
        LoadStats();
    }

    private void InitializeTracker()
    {
        // Initialize all tile types
        foreach (SpecialBlockTile.SpecialTileType type in System.Enum.GetValues(typeof(SpecialBlockTile.SpecialTileType)))
        {
            if (!specialTileCount.ContainsKey(type))
            {
                specialTileCount[type] = 0;
            }
        }
    }

    public void OnSpecialTileDestroyed(SpecialBlockTile.SpecialTileType tileType)
    {
        // Increment the counter
        if (specialTileCount.ContainsKey(tileType))
        {
            specialTileCount[tileType]++;
        }
        else
        {
            specialTileCount[tileType] = 1;
        }

        // Update specific counters for easy access
        switch (tileType)
        {
            case SpecialBlockTile.SpecialTileType.Gem:
                totalGemsDestroyed++;
                break;
            case SpecialBlockTile.SpecialTileType.Star:
                totalStarsDestroyed++;
                break;
            case SpecialBlockTile.SpecialTileType.Diamond:
                totalDiamondsDestroyed++;
                break;
        }

        Debug.Log($"Total {tileType}s destroyed: {specialTileCount[tileType]}");

        // Trigger event for other systems
        OnSpecialTileDestroyedEvent?.Invoke(tileType);

        // Save the stats
        SaveStats();
    }

    public int GetDestroyedCount(SpecialBlockTile.SpecialTileType tileType)
    {
        return specialTileCount.ContainsKey(tileType) ? specialTileCount[tileType] : 0;
    }

    public int GetTotalGemsDestroyed()
    {
        return totalGemsDestroyed;
    }

    private void SaveStats()
    {
        PlayerPrefs.SetInt("TotalGemsDestroyed", totalGemsDestroyed);
        PlayerPrefs.SetInt("TotalStarsDestroyed", totalStarsDestroyed);
        PlayerPrefs.SetInt("TotalDiamondsDestroyed", totalDiamondsDestroyed);
        PlayerPrefs.Save();
    }

    private void LoadStats()
    {
        totalGemsDestroyed = PlayerPrefs.GetInt("TotalGemsDestroyed", 0);
        totalStarsDestroyed = PlayerPrefs.GetInt("TotalStarsDestroyed", 0);
        totalDiamondsDestroyed = PlayerPrefs.GetInt("TotalDiamondsDestroyed", 0);

        specialTileCount[SpecialBlockTile.SpecialTileType.Gem] = totalGemsDestroyed;
        specialTileCount[SpecialBlockTile.SpecialTileType.Star] = totalStarsDestroyed;
        specialTileCount[SpecialBlockTile.SpecialTileType.Diamond] = totalDiamondsDestroyed;
    }

    public void ResetStats()
    {
        totalGemsDestroyed = 0;
        totalStarsDestroyed = 0;
        totalDiamondsDestroyed = 0;

        foreach (var key in new List<SpecialBlockTile.SpecialTileType>(specialTileCount.Keys))
        {
            specialTileCount[key] = 0;
        }

        SaveStats();
    }
}