using System;
using System.Collections.Generic;
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
    public MouseLockingManager GeneralMouseLockingManager;
    public GameManager GeneralGameManager;
    public Transform Player;
    public Transform WinLocation;
    public GameObject[] TilePrefabs;
    public TileData[] LevelLayout;
    public Vector2Int GridSize;
    public float LoadDeLoadDistance = 200.0f;
    public float DeLoadTime = 5.0f;

    private Tile[] _tiles;
    private Dictionary<int, bool> _enemyTiles = new();

    private Vector3[] _tilePoss;
    private bool[] _tileLoaded;
    private bool[] _tileBeingLoaded;
    private bool[] _tileCompleted;
    private float[] _tileOutOfRangeTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _tiles = new Tile[LevelLayout.Length];
        _tilePoss = new Vector3[LevelLayout.Length];
        _tileLoaded = new bool[LevelLayout.Length];
        _tileBeingLoaded = new bool[LevelLayout.Length];
        _tileCompleted = new bool[LevelLayout.Length];
        _tileOutOfRangeTime = new float[LevelLayout.Length];

        for (int i = 0; i < LevelLayout.Length; i++)
        {
            Vector3 spawnPos = new Vector3(transform.position.x + (i % GridSize.x) * 65.0f, LevelLayout[i].TileHeight, transform.position.z + (i / GridSize.y) * 65.0f);
            _tilePoss[i] = spawnPos;

            if (TilePrefabs[LevelLayout[i].TileID].GetComponent<Tile>().EnemySpawners.Length != 0)
            {
                NumberOfTilesToWin++;
                _enemyTiles[i] = true;   
            }
            else
                _enemyTiles[i] = false;
            //Instantiate(TilePrefabs[LevelLayout[i].TileID], spawnPos, Quaternion.AngleAxis(LevelLayout[i].TileYAngle, Vector3.up));        
        }
        Debug.Log(NumberOfTilesToWin);
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        float currMinPlayerDistance = float.PositiveInfinity;

        int tileCompleteCounter = 0;
        int indexPlayerIsClosestTo = 0;
        for (int i = 0; i < LevelLayout.Length; i++)
        {
            float playerDistance = Vector3.Distance(Player.position, _tilePoss[i]);
            if (playerDistance < LoadDeLoadDistance && !_tileLoaded[i] && !_tileBeingLoaded[i])
            {
                AsyncInstantiateOperation<GameObject> gameObj = 
                InstantiateAsync(TilePrefabs[LevelLayout[i].TileID], 1, _tilePoss[i], Quaternion.AngleAxis(LevelLayout[i].TileYAngle, Vector3.up));
                
                _tileBeingLoaded[i] = true;
                int tileIndex = i; // Needed for lambda capture, i may increment before tile has finished loading.

                gameObj.completed += _ =>
                {
                    _tiles[tileIndex] = gameObj.Result[0].GetComponent<Tile>();
                    _tiles[tileIndex].ParentTileManager = this;
                    _tiles[tileIndex].GridPos = _tileCompleted[tileIndex] ? -1 : tileIndex;
                    _tileLoaded[tileIndex] = true;
                    _tileBeingLoaded[tileIndex] = false;  
                };
            }
            if(playerDistance > LoadDeLoadDistance && _tileLoaded[i])
                _tileOutOfRangeTime[i] += dt;

            if (_tileOutOfRangeTime[i] > DeLoadTime && _tileLoaded[i])
            {
                _tiles[i].IsBeingDestroyed = true;
                Destroy(_tiles[i].gameObject);
                _tileLoaded[i] = false;
                _tileOutOfRangeTime[i] = 0.0f;
            }

            GeneralMouseLockingManager.TileImages[i].color = _enemyTiles[i] ? Color.white : Color.gray;
            if (playerDistance < currMinPlayerDistance)
            {
                currMinPlayerDistance = playerDistance;
                indexPlayerIsClosestTo = i;
            }

            if (_tileCompleted[i])
            {
                tileCompleteCounter++;
                GeneralMouseLockingManager.TileImages[i].color = Color.green;
            }
        }
        GeneralMouseLockingManager.TileImages[indexPlayerIsClosestTo].color = _tileCompleted[indexPlayerIsClosestTo] ? Color.darkGreen : Color.blue;

        if (tileCompleteCounter >= NumberOfTilesToWin && !GeneralGameManager.PlayerHasFinished)
        {
            Debug.Log("You Win!");
            GeneralGameManager.PlayerHasFinished = true;
            Player.transform.position = WinLocation.position; 
        }
    }

    public int GetTilesCompleted()
    {
        int tileCompleteCounter = 0;
        for (int i = 0; i < LevelLayout.Length; i++)
        {
            if (_tileCompleted[i])
                tileCompleteCounter++;
        }

        return tileCompleteCounter;
    }

    public void OnTileComplete(int inGridPos)
    {
        _tileCompleted[inGridPos] = true;
        Debug.Log("Tile finished!");   
    }
}
