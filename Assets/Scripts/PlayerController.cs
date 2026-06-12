using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class JumpParams
{
    public bool IncludeGravityOnDecent;
    public float JumpHeight;
    public float JumpDuration;
    public AnimationCurve JumpCurve;

    public void AdjustJumpCurve()
    {
        Keyframe[] adjustedKeys = JumpCurve.keys;
        float jumpCurveMax = JumpCurve.keys[JumpCurve.keys.Length - 1].time;
        float invJumpCurveMaxX = 1.0f / jumpCurveMax;
        for (int i = 0; i < adjustedKeys.Length; i++)
        {
            adjustedKeys[i].inTangent *= jumpCurveMax;
            adjustedKeys[i].outTangent *= jumpCurveMax;
            adjustedKeys[i].time *= invJumpCurveMaxX;
        }
        JumpCurve = new (adjustedKeys);
    }
};

public class PlayerController : MonoBehaviour
{
    public PlayerColAnimManager PCAM;
    public CameraController PlayerCamera;
    public JumpParams FirstJumpParams;
    public JumpParams SecondJumpParams;
    
    public float MinSecondJumpDelay = 0.1f; // Amount of time that must pass after the first jump.
    public float MaxSecondJumpDelay = 0.6f;

    public float OnGroundRadiusReduction = 0.01f;
    public float GroundSnapDistance = 0.05f;
    public float GroundSnapSpeed = 10.0f;

    private JumpParams _currJumpParams;
    private InputAction _jumpInputAction;
    private Rigidbody _rigidbody;

    private Vector3 _playerMoveDir;
    private Vector3 _playerLookDir;
    private Vector3 _playerInputDir;
    private Vector3 _desiredMoveDir;
    private Vector3 _momentumDir;
    
    private float _momentumMag;
    private float _currJumpTime;
    private float _remainingJumpTime;
    private float _onGroundSphereCastDist;

    private int _playerRaycastMask = 0;

    private bool _isFirstJumpUpdate = false;
    private bool _isOnGround = false;
    private bool _isInGroundSnapRange = false;

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
        
        _playerRaycastMask = GameUtils.CollisionLayerToRaycastMask(LayerMask.NameToLayer("Player"));
        _onGroundSphereCastDist = (PCAM.MainPlayerCollider.height / 2.0f) - PCAM.MainPlayerCollider.radius + OnGroundRadiusReduction;

        _rigidbody = GetComponent<Rigidbody>();
        if (!_rigidbody)
        {
            throw new NullReferenceException("PlayerController could not get a rigidbody!");
        }
        _jumpInputAction = InputSystem.actions.FindAction("Jump");

        FirstJumpParams.AdjustJumpCurve();
        SecondJumpParams.AdjustJumpCurve();
    }

    // Update is called once per frame
    void Update()
    {
        if (_jumpInputAction.WasPerformedThisFrame())
        {
            bool jumpTriggered = false;
            if (_isOnGround && _remainingJumpTime == 0.0f)
            {
                _currJumpParams = FirstJumpParams;
                jumpTriggered = true;
            }
            else if (_currJumpParams != SecondJumpParams && 
                     _remainingJumpTime > 0.0f && 
                     _currJumpTime > MinSecondJumpDelay && 
                     _currJumpTime < MaxSecondJumpDelay)
            {
                _currJumpParams = SecondJumpParams;
                jumpTriggered = true;
            }

            if (jumpTriggered)
            {
                _remainingJumpTime = _currJumpParams.JumpDuration;
                _isFirstJumpUpdate = true;
                _currJumpTime = 0.0f;
            }
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (_isOnGround && _currJumpTime > 0.1f)
        {
            _remainingJumpTime = 0.0f;
            _currJumpTime = 0.0f;
        }
        _isOnGround = Physics.SphereCast(transform.position, 
                                         PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction, 
                                         -Vector3.up, 
                                         out RaycastHit hit, 
                                         _onGroundSphereCastDist, 
                                         _playerRaycastMask, 
                                         QueryTriggerInteraction.Ignore);
        
        RaycastHit groundSnapRange;
        _isInGroundSnapRange = Physics.SphereCast(transform.position, 
                                                  PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction, 
                                                  -Vector3.up, 
                                                  out groundSnapRange, 
                                                  _onGroundSphereCastDist + GroundSnapDistance,
                                                  _playerRaycastMask, 
                                                  QueryTriggerInteraction.Ignore);
        if (!_isOnGround && _isInGroundSnapRange && _rigidbody.linearVelocity.y < 0.0f)
        {
            Vector3 currLinVel = _rigidbody.linearVelocity;
            currLinVel.y -= GroundSnapSpeed * dt;

            _rigidbody.linearVelocity = currLinVel;
        }

        if (_remainingJumpTime > 0.0f && !_isFirstJumpUpdate)
        {
            float t = _currJumpTime / _currJumpParams.JumpDuration;
            float adjustedDT = dt / _currJumpParams.JumpDuration;
            
            float acceleration = CalcCurveAcceleration(_currJumpParams.JumpCurve, 1.0f, t, adjustedDT);
            float heightDelta =  _currJumpParams.JumpCurve.Evaluate(Mathf.Clamp01(t + adjustedDT)) - 
                                 _currJumpParams.JumpCurve.Evaluate(t);

            float force = acceleration * _currJumpParams.JumpHeight;
            force /= _currJumpParams.JumpDuration * _currJumpParams.JumpDuration;
            force -= (_currJumpParams.IncludeGravityOnDecent && heightDelta < 0.0f) ? 0.0f : Physics.gravity.y;
            // We need to take gravity into account, to ensure the jump height curve is followed properly.
            // This is due to the jump height curve not taking gravity into account itself.

            _rigidbody.AddForce(Vector3.up * force, ForceMode.Acceleration);
            _remainingJumpTime -= dt;   
            _currJumpTime += dt;
        }
        else if (_isFirstJumpUpdate)
        {
            _isOnGround = false;
            _isFirstJumpUpdate = false;
            float firstCurveHeight = _currJumpParams.JumpCurve.Evaluate(dt / _currJumpParams.JumpDuration) * 
                                     _currJumpParams.JumpHeight;
            float speed = firstCurveHeight / dt;
            
            speed -= _rigidbody.linearVelocity.y;
            speed = (speed < 0.0f) ? 0.0f : speed;

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
