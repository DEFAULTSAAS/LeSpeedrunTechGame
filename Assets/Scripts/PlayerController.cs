using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PlayerColAnimManager PCAM;
    public CameraController PlayerCamera;
    public AnimationCurve FirstJumpCurve;
    public AnimationCurve SecondJumpCurve;
    
    public float FirstJumpTime = 1.5f;
    public float FirstJumpHeight = 2.0f; 
    
    public float SecondJumpTime = 1.5f;
    public float SecondJumpHeight = 2.0f;

    public float OnGroundRadiusReduction = 0.01f; 

    private InputAction _jumpInputAction;
    private Rigidbody _rigidbody;

    private Vector3 playerMoveDir;
    private Vector3 playerLookDir;
    private Vector3 playerInputDir;
    private Vector3 desiredMoveDir;
    private Vector3 momentumDir;
    
    private float _momentumMag;
    private float _remainingJumpTime;
    private float _onGroundSphereCastDist;
    private bool _isFirstJumpUpdate = false;
    private bool _isOnGround = false;
    
    [SerializeField] private AnimationCurve _firstJumpCurve;
    [SerializeField] private AnimationCurve _secondJumpCurve;

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
        _onGroundSphereCastDist = (PCAM.MainPlayerCollider.height / 2.0f) - PCAM.MainPlayerCollider.radius + OnGroundRadiusReduction;

        _rigidbody = GetComponent<Rigidbody>();
        if (!_rigidbody)
        {
            throw new NullReferenceException("PlayerController could not get a rigidbody!");
        }
        _jumpInputAction = InputSystem.actions.FindAction("Jump");

        Keyframe[] adjustedKeys = FirstJumpCurve.keys;
        float jumpCurveMax = FirstJumpCurve.keys[FirstJumpCurve.keys.Length - 1].time;
        float invJumpCurveMaxX = 1.0f / jumpCurveMax;
        for (int i = 0; i < adjustedKeys.Length; i++)
        {
            adjustedKeys[i].inTangent *= jumpCurveMax;
            adjustedKeys[i].outTangent *= jumpCurveMax;
            adjustedKeys[i].time *= invJumpCurveMaxX;
        }
        _firstJumpCurve = new (adjustedKeys);

        adjustedKeys = SecondJumpCurve.keys;
        jumpCurveMax = SecondJumpCurve.keys[SecondJumpCurve.length - 1].time;
        invJumpCurveMaxX = 1.0f / jumpCurveMax;
        for (int i = 0; i < adjustedKeys.Length; i++)
        {
            adjustedKeys[i].inTangent *= jumpCurveMax;
            adjustedKeys[i].outTangent *= jumpCurveMax;
            adjustedKeys[i].time *= invJumpCurveMaxX;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_isOnGround && _remainingJumpTime <= 0.0f && _jumpInputAction.WasPerformedThisFrame())
        {
            _remainingJumpTime = FirstJumpTime;
            _isFirstJumpUpdate = true;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float _timeSinceFirstJump = FirstJumpTime - _remainingJumpTime;

        if (_remainingJumpTime > 0.0f && !_isFirstJumpUpdate)
        {
            float t = _timeSinceFirstJump / FirstJumpTime;
            float acceleration = CalcCurveAcceleration(_firstJumpCurve, 1.0f, t, dt / FirstJumpTime);
            
            float force = acceleration * FirstJumpHeight;
            force /= FirstJumpTime * FirstJumpTime;
            force -= Physics.gravity.y;

            _rigidbody.AddForce(Vector3.up * force, ForceMode.Acceleration);
            _remainingJumpTime -= dt;   
        }
        else if (_isFirstJumpUpdate)
        {
            _isOnGround = false;
            _isFirstJumpUpdate = false;
            float firstCurveHeight = _firstJumpCurve.Evaluate(dt / FirstJumpTime) * FirstJumpHeight;
            float speed = firstCurveHeight / dt;
            
            speed -= Physics.gravity.y * dt;
            _rigidbody.AddForce(Vector3.up * speed, ForceMode.VelocityChange);
        }

        if (_isOnGround && _timeSinceFirstJump > 0.01f)
        {
            _remainingJumpTime = 0.0f;
        }
        _isOnGround = Physics.SphereCast(transform.position, 
                                         PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction, 
                                         -Vector3.up, 
                                         out RaycastHit hit, 
                                         _onGroundSphereCastDist, 
                                         ~(1 << LayerMask.NameToLayer("Player")), 
                                         QueryTriggerInteraction.Ignore);
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
