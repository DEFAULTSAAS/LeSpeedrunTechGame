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
    private float _jumpCurveMaxX = 1.0f;
    private bool _isFirstJumpUpdate = false;
    
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
            _remainingJumpTime = JumpTime;
            _jumpCurveMaxX = JumpCurve.keys[JumpCurve.keys.Length - 1].time;
            _isFirstJumpUpdate = true;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (_remainingJumpTime > 0.0f && !_isFirstJumpUpdate)
        {
            float t = (JumpTime - _remainingJumpTime) / JumpTime;
            float acceleration = CalcCurveAcceleration(JumpCurve, _jumpCurveMaxX, t, dt / JumpTime);
            
            float force = acceleration * JumpHeight;
            force /= JumpTime * JumpTime;
            force -= Physics.gravity.y;

            _rigidbody.AddForce(Vector3.up * force, ForceMode.Acceleration);
            _remainingJumpTime -= dt;   
        }
        else if (_isFirstJumpUpdate)
        {
            _isFirstJumpUpdate = false;
            float firstCurveHeight = JumpCurve.Evaluate(dt / JumpTime) * JumpHeight;
            float speed = firstCurveHeight / dt;
            
            speed -= Physics.gravity.y * dt;
            _rigidbody.AddForce(Vector3.up * speed, ForceMode.VelocityChange);
        }
    }

    public static float CalcCurveAcceleration(AnimationCurve inCurve, float inMaxX, float inT, float inH)
    {
        float tPlusH = inT + inH;
        float tPlus2H = inT + (2.0f * inH);

        // Have to do extra faff if tPlus2H is > 1.0f,
        // so do below as a backup.
        if (tPlus2H > inMaxX)
        {
            return CalcCurveAcceleration(inCurve, inMaxX, inMaxX - (2.0f * inH), inH);
        }

        float fOfTPlus2H = inCurve.Evaluate(tPlus2H);
        float fOfTPlusH = inCurve.Evaluate(tPlusH);
        float fOfT = inCurve.Evaluate(inT);
        
        float result = (fOfTPlus2H - (2.0f * fOfTPlusH) + fOfT) / (inH * inH);
        return result;
    }
}
