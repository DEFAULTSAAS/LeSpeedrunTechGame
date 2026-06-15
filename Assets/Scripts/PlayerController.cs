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
    
    public float WalkMoveSpeed = 3.0f;
    public float DirChangeRate = 2.0f;
    // How much the current horizontal velocity effects how quickly the player direction is changed.
    public float DirChangeRateVelFactor = 1.0f;

    public JumpParams FirstJumpParams;
    public JumpParams SecondJumpParams;
    
    public float MinSecondJumpDelay = 0.1f; // Amount of time that must pass after the first jump.
    public float MaxSecondJumpDelay = 0.6f;

    public float OnGroundRadiusReduction = 0.01f;
    public float GroundSnapDistance = 0.05f;
    public float GroundSnapSpeed = 10.0f;

    private InputAction _moveInputAction;
    private InputAction _jumpInputAction;

    private JumpParams _currJumpParams;
    private Rigidbody _rigidbody;
    private Vector2 _moveInputAcc;

    private Vector3 _playerMoveDir;
    private Vector3 _playerLookDir;
    private Vector3 _playerLookDirXZ;
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
        _onGroundSphereCastDist += 0.01f;

        _rigidbody = GetComponent<Rigidbody>();
        if (!_rigidbody)
        {
            throw new NullReferenceException("PlayerController could not get a rigidbody!");
        }
        
        _moveInputAction = InputSystem.actions.FindAction("Move");
        _jumpInputAction = InputSystem.actions.FindAction("Jump");

        FirstJumpParams.AdjustJumpCurve();
        SecondJumpParams.AdjustJumpCurve();
        Debug.Log(PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction);
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        Vector2 moveInput = _moveInputAction.ReadValue<Vector2>();
        _moveInputAcc += moveInput;

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
        Quaternion cameraYRot = CameraController.GetRotationAroundAxis(PlayerCamera.GetCameraOrientation(), Vector3.up); 

        _moveInputAcc.Normalize();
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

        _playerLookDir = PlayerCamera.GetCameraOrientation() * Vector3.forward;
        _playerLookDirXZ = cameraYRot * Vector3.forward;
        _playerInputDir = (_moveInputAcc.y * _playerLookDirXZ) + (_moveInputAcc.x * (cameraYRot * Vector3.right));
        _playerInputDir.Normalize();
        _desiredMoveDir = _playerInputDir; 
        // By default the desired move dir is just the input dir, but this can change based on different actions.

        Vector3 dirDiff = _desiredMoveDir - _playerMoveDir;
        float currMoveSpeed = WalkMoveSpeed;
        float dirDiffMag = dirDiff.magnitude; 
        float dirChangeMag = DirChangeRate * dt;
        
        dirChangeMag = (dirChangeMag > dirDiffMag) ? dirDiffMag : dirChangeMag;
        _playerMoveDir += dirDiff.normalized * dirChangeMag;

        Vector3 targetVelXZ = _playerMoveDir * currMoveSpeed;
        Vector3 currVel = _rigidbody.linearVelocity; currVel.y = 0.0f;
        if (_isOnGround) 
            _rigidbody.AddForce(targetVelXZ - currVel, ForceMode.VelocityChange);
        else if (_isFirstJumpUpdate)
        {
            if (targetVelXZ == Vector3.zero)
                targetVelXZ = currVel.normalized * currMoveSpeed;
            _rigidbody.AddForce(targetVelXZ - currVel, ForceMode.VelocityChange);
        }

        _moveInputAcc = Vector2.zero;
        if (_remainingJumpTime > 0.0f && !_isFirstJumpUpdate)
        {
            float t = _currJumpTime / _currJumpParams.JumpDuration;
            float adjustedDT = dt / _currJumpParams.JumpDuration;
            
            float acceleration = CalcCurveAcceleration(_currJumpParams.JumpCurve, 1.0f, t, adjustedDT);
            float heightDelta = CalcCurveVelocity(_currJumpParams.JumpCurve, t, adjustedDT);

            float force = acceleration * _currJumpParams.JumpHeight;
            force /= _currJumpParams.JumpDuration * _currJumpParams.JumpDuration;
            force -= (_currJumpParams.IncludeGravityOnDecent && heightDelta < 0.0f) ? 0.0f : Physics.gravity.y;
            // We need to take gravity into account, to ensure the jump height curve is followed properly.
            // This is due to the jump height curve not taking gravity into account itself.

            currVel = new Vector3(0.0f, _rigidbody.linearVelocity.y, 0.0f);
            currVel.y += force;

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

    public static float CalcCurveVelocity(AnimationCurve inCurve, float inT, float inH)
    {
        float fOfT = inCurve.Evaluate(inT);
        float fOfTPlusH = inCurve.Evaluate(inT + inH);
        float fOfTSubH = inCurve.Evaluate(inT - inH);

        if ((inT - inH) < 0.0f)
            return (fOfTPlusH - fOfT) / inH;
        else if ((inT + inH) > 1.0f)
            return (fOfT - fOfTSubH) / inH;

        return (fOfTPlusH - fOfTSubH) / (2.0f * inH);
    }

    public static float CalcCurveAcceleration(AnimationCurve inCurve, float inMaxX, float inT, float inH)
    {
        float vOfT = CalcCurveVelocity(inCurve, inT, inH);
        float vOfTPlusH = CalcCurveVelocity(inCurve, inT + inH, inH);
        float vOfTSubH = CalcCurveVelocity(inCurve, inT - inH, inH);
        
        if ((inT - inH) < 0.0f)
            return (vOfTPlusH - vOfT) / inH;
        else if ((inT + inH) > 1.0f)
            return (vOfT - vOfTSubH) / inH;

        return (vOfTPlusH - vOfTSubH) / (2.0f * inH);
        
        // float tPlusH = inT + inH;
        // float tPlus2H = inT + (2.0f * inH);

        // // Have to do extra faff if tPlus2H is > 1.0f,
        // // so do below as a backup.
        // if (tPlus2H > inMaxX)
        // {
        //     return CalcCurveAcceleration(inCurve, inMaxX, inMaxX - (2.0f * inH), inH);
        // }

        // float fOfTPlus2H = inCurve.Evaluate(tPlus2H);
        // float fOfTPlusH = inCurve.Evaluate(tPlusH);
        // float fOfT = inCurve.Evaluate(inT);
        
        // float result = (fOfTPlus2H - (2.0f * fOfTPlusH) + fOfT) / (inH * inH);
        // return result;
    }
}
