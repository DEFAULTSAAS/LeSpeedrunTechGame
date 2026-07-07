using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    static int UniqueIDCounter;
    public int UniqueID {get; set;}
    public Tile ParentTile;
    public GameObject EnemyPrefab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UniqueID = UniqueIDCounter++;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SpawnEnemy()
    {
        GameObject enemyObj = Instantiate(EnemyPrefab, transform.position, transform.rotation);
        enemyObj.transform.SetParent(ParentTile.transform);
        IEnemy enemy = enemyObj.GetComponent<IEnemy>();
        enemy.CurrEnemySpawner = this;
    }

    public void OnEnemyDestroyed()
    {
        if (!ParentTile.IsBeingDestroyed)
            ParentTile.OnEnemyDestroyed(UniqueID);
    }
}
