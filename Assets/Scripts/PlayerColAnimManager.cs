using System;
using UnityEngine;

public class PlayerColAnimManager : MonoBehaviour
{
    public CapsuleCollider MainPlayerCollider;
    public Animator PlayerAnimator;
    public Quaternion PlayerFaceDirRot {
        get
        {
            return transform.rotation;
        } 
        set 
        {
            transform.rotation = value;    
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!MainPlayerCollider)
            MainPlayerCollider = GetComponent<CapsuleCollider>();
        if (!MainPlayerCollider)
            throw new NullReferenceException("Could not get the main player collider!");

        if (!PlayerAnimator)
            PlayerAnimator = GetComponentInChildren<Animator>();
        if (!PlayerAnimator)
            throw new NullReferenceException("Could not get the player animator!");
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        
    }
}
