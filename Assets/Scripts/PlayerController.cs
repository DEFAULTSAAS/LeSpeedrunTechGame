using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public PlayerColAnimManager PCAM;
    private Vector3 playerMoveDir;
    private Vector3 playerLookDir;
    private Vector3 playerInputDir;
    private Vector3 desiredMoveDir;
    private Vector3 momentumDir;
    private float momentumMag;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!PCAM)
        {
            PCAM = GetComponent<PlayerColAnimManager>();
        }
        if (!PCAM)
        {
            throw new NullReferenceException("Could not get instance of Player Collision Animation Manager!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
