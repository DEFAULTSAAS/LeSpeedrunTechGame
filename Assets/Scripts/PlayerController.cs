using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public enum JumpModes
{
    None,
    Acceleration,
    HeightDelta
}

[Serializable]
public enum JumpTypes
{
    None = 0,
    Single = 35,
    Double = 34,
    Back = 4,
    Side = 8,
    Long = 16,
    Slam = 32,
    Bonk = 64
}

[Serializable]
public class JumpParams
{
    public bool IsVerticalJump = true;
    public bool IsHorizontalJump = false;
    public bool UseLocalAxis = false;
    public bool IncludeGravityOnDecent;
    
    public float JumpHeight;
    public float JumpWidth;
    public float VerticalJumpDuration;
    public float HorizontalJumpDuration;
    
    public AnimationCurve JumpCurve;
    public Vector3 JumpAxis = Vector3.up;
    public JumpModes JumpMode;
    public JumpTypes JumpType;
    
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
}

public struct JumpDurationParams
{
    public JumpModes JumpMode {get; private set;}
    public JumpTypes JumpType {get; private set;}

    public float CurrVertJumpTime {get; private set;}
    public float RemainingVertJumpTime {get; private set;}
    public float CurrHoriJumpTime {get; private set;}
    public float RemainingHoriJumpTime {get; private set;}

    public void FromJumpParams(JumpParams inJumpParams)
    {
        JumpMode = inJumpParams.JumpMode;
        JumpType = inJumpParams.JumpType;

        CurrVertJumpTime = 0.0f;
        RemainingVertJumpTime = inJumpParams.VerticalJumpDuration;

        CurrHoriJumpTime = 0.0f;
        RemainingHoriJumpTime = inJumpParams.HorizontalJumpDuration;
    }

    public void JumpDurationTick(float inDT)
    {
        CurrVertJumpTime += inDT;
        RemainingVertJumpTime -= inDT;

        CurrHoriJumpTime += inDT;
        RemainingHoriJumpTime -= inDT;
    }

    // We return the maximum time fo the two different types of jump,
    // as code generally cares when the jump stops as a whole.
    public float GetJumpTime()
    {
        return Mathf.Max(CurrVertJumpTime, CurrHoriJumpTime);
    }

    // This will return the maximum amount of time remaining
    // between the two different types of jump.
    public float GetRemainingJumpTime()
    {
        return Mathf.Max(RemainingVertJumpTime, RemainingHoriJumpTime);
    }

    public void Reset()
    {
        JumpMode = JumpModes.None;
        JumpType = JumpTypes.None;

        CurrVertJumpTime = float.PositiveInfinity;
        RemainingVertJumpTime = 0.0f;

        CurrHoriJumpTime = float.PositiveInfinity;
        RemainingHoriJumpTime = 0.0f;
    }
}

public class JumpManager
{
    public Vector2 PrevVel = new();
    public JumpDurationParams CurrJump = new();

    public bool StartJump(JumpParams inJumpParams)
    {
        if (CurrJump.JumpType == JumpTypes.None || 
           (CurrJump.JumpType != inJumpParams.JumpType && (CurrJump.JumpType & inJumpParams.JumpType) > 0))
        {
            CurrJump.FromJumpParams(inJumpParams);
            return true;
        }

        return false;
    }

    public void StopJump(Rigidbody inRigidbody)
    {
        CurrJump.Reset();
        if (inRigidbody != null && CurrJump.JumpMode == JumpModes.HeightDelta)
        {
            inRigidbody.AddForce(Vector3.up * -PrevVel.y, ForceMode.VelocityChange);
            inRigidbody.AddForce(inRigidbody.transform.right * -PrevVel.x, ForceMode.VelocityChange);   
        }
    }
}

public class PlayerController : MonoBehaviour
{
    public PlayerColAnimManager PCAM;
    public CameraController PlayerCamera;
    
    public float WalkMoveSpeed = 3.0f;
    public float JumpMoveSpeedFactor = 0.9f;
    public float InAirMoveSpeedFactor = 0.2f;
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

    private JumpManager _jumpManager = new();
    private JumpParams _currJumpParams = new();
    private Rigidbody _rigidbody;
    private Vector2 _moveInputAcc;

    private Vector3 _playerMoveDir;
    private Vector3 _playerLookDir;
    private Vector3 _playerLookDirXZ;
    private Vector3 _playerInputDir;
    private Vector3 _desiredMoveDir;
    private Vector3 _momentumDir;
    
    private float _momentumMag;
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
        
        _moveInputAction.started += PlayerInputSystem.MainPISInstance.HandleInputCallback;
        _moveInputAction.performed += PlayerInputSystem.MainPISInstance.HandleInputCallback;
        _moveInputAction.canceled += PlayerInputSystem.MainPISInstance.HandleInputCallback;

        FirstJumpParams.AdjustJumpCurve();
        SecondJumpParams.AdjustJumpCurve();
        //Debug.Log(PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction);
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
            if (_isOnGround && _jumpManager.CurrJump.GetRemainingJumpTime() == 0.0f)
            {
                _currJumpParams = FirstJumpParams;
                jumpTriggered = _jumpManager.StartJump(_currJumpParams);
            }
            else if (_currJumpParams != SecondJumpParams && 
                     _jumpManager.CurrJump.GetRemainingJumpTime() > 0.0f && 
                     _jumpManager.CurrJump.GetJumpTime() > MinSecondJumpDelay && 
                     _jumpManager.CurrJump.GetJumpTime() < MaxSecondJumpDelay)
            {
                _currJumpParams = SecondJumpParams;
                jumpTriggered = _jumpManager.StartJump(_currJumpParams);
            }

            if (jumpTriggered)
                _isFirstJumpUpdate = true;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Quaternion cameraYRot = CameraController.GetRotationAroundAxis(PlayerCamera.GetCameraOrientation(), Vector3.up); 

        _moveInputAcc.Normalize();
        if (_isOnGround && _jumpManager.CurrJump.GetJumpTime() > 0.1f)
        {
            _jumpManager.StopJump(_rigidbody);
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

        float currMoveSpeed = WalkMoveSpeed;
        if (!_isOnGround && !_isFirstJumpUpdate)
        {
            currMoveSpeed *= JumpMoveSpeedFactor;
        }

        Vector3 dirDiff = _desiredMoveDir - _playerMoveDir;
        float dirDiffMag = dirDiff.magnitude; 
        float dirChangeMag = DirChangeRate * dt;
        
        dirChangeMag = (dirChangeMag > dirDiffMag) ? dirDiffMag : dirChangeMag;
        _playerMoveDir += dirDiff.normalized * dirChangeMag;

        Vector3 targetVelXZ = _playerMoveDir * currMoveSpeed;
        Vector3 currVel = _rigidbody.linearVelocity; currVel.y = 0.0f;

        if (targetVelXZ == Vector3.zero && _jumpManager.CurrJump.JumpType == JumpTypes.Double)
            targetVelXZ = currVel.normalized * currMoveSpeed;
        _rigidbody.AddForce(targetVelXZ - currVel, ForceMode.VelocityChange);

        _moveInputAcc = Vector2.zero;
        if (_currJumpParams.IsVerticalJump && _jumpManager.CurrJump.RemainingVertJumpTime > 0.0f && !_isFirstJumpUpdate)
        {
            float t = _jumpManager.CurrJump.CurrVertJumpTime / _currJumpParams.VerticalJumpDuration;
            float adjustedDT = dt / _currJumpParams.VerticalJumpDuration;
            
            float acceleration = CalcCurveAcceleration(_currJumpParams.JumpCurve, t, adjustedDT);
            float heightDelta = CalcCurveVelocity(_currJumpParams.JumpCurve, t, adjustedDT);
            
            float velocity = heightDelta * _currJumpParams.JumpHeight / _currJumpParams.VerticalJumpDuration; 
            float gravity = (_currJumpParams.IncludeGravityOnDecent && heightDelta < 0.0f) ? 0.0f : Physics.gravity.y;

            acceleration *= _currJumpParams.JumpHeight;
            acceleration /= _currJumpParams.VerticalJumpDuration * _currJumpParams.VerticalJumpDuration;
            acceleration -= gravity;
            // We need to take gravity into account, to ensure the jump height curve is followed properly.
            // This is due to the jump height curve not taking gravity into account itself.

            if (_currJumpParams.JumpMode == JumpModes.Acceleration)
                _rigidbody.AddForce(Vector3.up * acceleration, ForceMode.Acceleration);
            else
            {
                _rigidbody.AddForce(Vector3.up * (velocity - (gravity * dt)), ForceMode.VelocityChange);
                _rigidbody.AddForce(Vector3.up * -_jumpManager.PrevVel.y, ForceMode.VelocityChange);   
            }
            
            _jumpManager.PrevVel.y = velocity;
            _jumpManager.CurrJump.JumpDurationTick(dt);
        }
        else if (_currJumpParams.IsVerticalJump && _isFirstJumpUpdate)
        {
            if (_isOnGround)
                _jumpManager.PrevVel.y = -(Physics.gravity.y * dt);
            else
                _jumpManager.PrevVel.y += Physics.gravity.y * dt;
                
            _isOnGround = false;
            _isFirstJumpUpdate = false;
            
            if (_currJumpParams.JumpMode == JumpModes.Acceleration)
            {
                float adjustedDT = dt / _currJumpParams.VerticalJumpDuration;
                float firstCurveHeight = CalcCurveVelocity(_currJumpParams.JumpCurve, 0.0f, adjustedDT) * _currJumpParams.JumpHeight;
                float speed = firstCurveHeight / _currJumpParams.VerticalJumpDuration;
                
                speed -= _rigidbody.linearVelocity.y;
                speed = (speed < 0.0f) ? 0.0f : speed;

                speed += Physics.gravity.y * dt;
                _rigidbody.AddForce(Vector3.up * speed, ForceMode.VelocityChange);   
            }
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

    public static float CalcCurveAcceleration(AnimationCurve inCurve, float inT, float inH)
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
