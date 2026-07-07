using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public int GridPos {get; set;}
    public bool IsBeingDestroyed {get; set;}

    public EnemySpawner[] EnemySpawners;
    public TileManager ParentTileManager;
    public List<EnemySpawner> EnemySpawnersList {get; set;}

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (GridPos >= 0)
        {
            EnemySpawnersList = new(EnemySpawners);
            foreach (EnemySpawner enemySpawner in EnemySpawnersList)
                enemySpawner.SpawnEnemy();   
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnEnemyDestroyed(int inID)
    {
        for (int i = 0; i < EnemySpawnersList.Count; i++)
        {
            if (EnemySpawnersList[i].UniqueID == inID)
            {
                EnemySpawnersList.RemoveAt(i);
                break;   
            }
        }

        if (EnemySpawnersList.Count == 0)
            ParentTileManager.OnTileComplete(GridPos);
    }
}
