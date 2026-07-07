using System;
using UnityEngine;

[Serializable]
public struct TileData
{
    public int TileID;
    public float TileHeight;
    public float TileYAngle;
    public bool HasEnemies;
}

public class TileManager : MonoBehaviour
{
    public int NumberOfTilesToWin {get; private set;}
    public Transform Player;
    public Transform WinLocation;
    public GameObject[] TilePrefabs;
    public TileData[] LevelLayout;
    public Vector2Int GridSize;
    public float LoadDeLoadDistance = 200.0f;
    public float DeLoadTime = 5.0f;

    private Tile[] _tiles;

    private Vector3[] _tilePoss;
    private bool[] _tileLoaded;
    private bool[] _tileCompleted;
    private float[] _tileOutOfRangeTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _tiles = new Tile[LevelLayout.Length];
        _tilePoss = new Vector3[LevelLayout.Length];
        _tileLoaded = new bool[LevelLayout.Length];
        _tileCompleted = new bool[LevelLayout.Length];
        _tileOutOfRangeTime = new float[LevelLayout.Length];

        for (int i = 0; i < LevelLayout.Length; i++)
        {
            Vector3 spawnPos = new Vector3(transform.position.x + (i % GridSize.x) * 65.0f, LevelLayout[i].TileHeight, transform.position.z + (i / GridSize.y) * 65.0f);
            _tilePoss[i] = spawnPos;

            if (TilePrefabs[LevelLayout[i].TileID].GetComponent<Tile>().EnemySpawners.Length != 0)
                NumberOfTilesToWin++;
            //Instantiate(TilePrefabs[LevelLayout[i].TileID], spawnPos, Quaternion.AngleAxis(LevelLayout[i].TileYAngle, Vector3.up));        
        }
        Debug.Log(NumberOfTilesToWin);
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;

        int tileCompleteCounter = 0;
        for (int i = 0; i < LevelLayout.Length; i++)
        {
            if (Vector3.Distance(Player.position, _tilePoss[i]) < LoadDeLoadDistance && !_tileLoaded[i])
            {
                GameObject gameObj = Instantiate(TilePrefabs[LevelLayout[i].TileID], _tilePoss[i], Quaternion.AngleAxis(LevelLayout[i].TileYAngle, Vector3.up));
                _tiles[i] = gameObj.GetComponent<Tile>();
                _tiles[i].ParentTileManager = this;
                _tiles[i].GridPos = _tileCompleted[i] ? -1 : i;
                _tileLoaded[i] = true;
            }
            if(Vector3.Distance(Player.position, _tilePoss[i]) > LoadDeLoadDistance && _tileLoaded[i])
                _tileOutOfRangeTime[i] += dt;

            if (_tileOutOfRangeTime[i] > DeLoadTime && _tileLoaded[i])
            {
                _tiles[i].IsBeingDestroyed = true;
                Destroy(_tiles[i].gameObject);
                _tileLoaded[i] = false;
                _tileOutOfRangeTime[i] = 0.0f;
            }

            if (_tileCompleted[i])
                tileCompleteCounter++;
        }

        if (tileCompleteCounter >= NumberOfTilesToWin)
        {
            Debug.Log("You Win!");
            Player.transform.position = WinLocation.position;
            NumberOfTilesToWin = 1000; 
        }
    }

    public void OnTileComplete(int inGridPos)
    {
        _tileCompleted[inGridPos] = true;
        Debug.Log("Tile finished!");   
    }
}
