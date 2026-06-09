using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PlayerColAnimManager PCAM;
    public CameraController PlayerCamera;
    public AnimationCurve JumpCurve;
    
    public float JumpTime = 1.5f;
    public float JumpHeight = 2.0f; 

    private Rigidbody _rigidbody;

    private Vector3 playerMoveDir;
    private Vector3 playerLookDir;
    private Vector3 playerInputDir;
    private Vector3 desiredMoveDir;
    private Vector3 momentumDir;
    
    private float _momentumMag;
    private float _remainingJumpTime;
    
    private InputAction _jumpInputAction;

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

        _rigidbody = GetComponent<Rigidbody>();
        if (!_rigidbody)
        {
            throw new NullReferenceException("PlayerController could not get a rigidbody!");
        }

        _jumpInputAction = InputSystem.actions.FindAction("Jump");
    }

    // Update is called once per frame
    void Update()
    {
        if (_remainingJumpTime <= 0.0f && _jumpInputAction.WasPerformedThisFrame())
        {
            Debug.Log("Jump!");
            _remainingJumpTime = JumpTime;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (_remainingJumpTime > 0.0f)
        {
            _rigidbody.AddForce(((Vector3.up * 9.81f) + (Vector3.up * JumpCurve.Evaluate((JumpTime - _remainingJumpTime) / JumpTime) * JumpHeight)) * dt, ForceMode.Impulse);
            _remainingJumpTime -= dt;   
        }
    }
}
