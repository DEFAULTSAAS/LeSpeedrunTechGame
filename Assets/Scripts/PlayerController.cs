using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public enum JumpMethods
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
    Swing = 64,
    Bonk = 128
}

[Serializable]
public struct JumpParamsTuple<T>
{
    public T Y;
    public T X;

    public JumpParamsTuple(T inY = default, T inX = default) 
    { 
        Y = inY;
        X = inX;  
    }
}

[Serializable]
public class JumpParams
{
    public bool IsVerticalJump = true;
    public bool IsHorizontalJump = false;
    public bool AllowsMidAirControl = true;
    public JumpParamsTuple<bool> UseLocalAxes = new(false, true);
    public bool IncludeGravityOnDecent = false;
    public bool HittingGroundCancelsJump = true;
    
    public float JumpHeight;
    public float JumpWidth;
    public float VerticalJumpDuration;
    public float HorizontalJumpDuration;
    
    public JumpParamsTuple<AnimationCurve> JumpCurves;
    public JumpParamsTuple<Vector3> JumpAxes = new(Vector3.up, Vector3.right);
    public JumpParamsTuple<JumpMethods> JumpModes;
    public JumpTypes JumpType;
    
    public void AdjustJumpCurve()
    {
        AdjustJumpCurve(ref JumpCurves.Y);
        AdjustJumpCurve(ref JumpCurves.X);
    }

    public static void AdjustJumpCurve(ref AnimationCurve inJumpCurve)
    {
        Keyframe[] adjustedKeys = inJumpCurve.keys;
        float jumpCurveMax = inJumpCurve.keys[inJumpCurve.keys.Length - 1].time;
        float invJumpCurveMaxX = 1.0f / jumpCurveMax;
        for (int i = 0; i < adjustedKeys.Length; i++)
        {
            adjustedKeys[i].inTangent *= jumpCurveMax;
            adjustedKeys[i].outTangent *= jumpCurveMax;
            adjustedKeys[i].time *= invJumpCurveMaxX;
        }
        inJumpCurve = new (adjustedKeys);
    }

    public JumpParams ShallowCopy()
    {
        return (JumpParams)MemberwiseClone();
    }
}

public struct JumpDurationParams
{
    public JumpParamsTuple<JumpMethods> JumpModes {get; private set;}
    public JumpTypes JumpType {get; private set;}

    public float CurrVertJumpTime {get; private set;}
    public float RemainingVertJumpTime {get; private set;}
    public float CurrHoriJumpTime {get; private set;}
    public float RemainingHoriJumpTime {get; private set;}

    public void FromJumpParams(JumpParams inJumpParams)
    {
        JumpModes = inJumpParams.JumpModes;
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
        JumpModes = new(JumpMethods.None, JumpMethods.None);
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

    public Vector3 ChosenYAxis;
    public Vector3 ChosenXAxis;

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
        if (inRigidbody != null && CurrJump.JumpModes.Y == JumpMethods.HeightDelta)
            inRigidbody.AddForce(ChosenYAxis * -PrevVel.y, ForceMode.VelocityChange);
        
        if (inRigidbody != null && CurrJump.JumpModes.X == JumpMethods.HeightDelta)
            inRigidbody.AddForce(ChosenXAxis * -PrevVel.x, ForceMode.VelocityChange);   
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
    public float DirChangeRateStrafingFactor = 10.0f;

    public JumpParams FirstJumpParams;
    public JumpParams SecondJumpParams;
    public JumpParams LongJumpJumpParams;
    public JumpParams BackflipJumpParams;
    public JumpParams LeftSideFlipJumpParams;

    
    public float MinSecondJumpDelay = 0.1f; // Amount of time that must pass after the first jump.
    public float MaxSecondJumpDelay = 0.6f;

    public float OnGroundRadiusReduction = 0.01f;
    public float GroundSnapDistance = 0.05f;
    public float GroundSnapSpeed = 10.0f;

    private InputAction _moveInputAction;
    private InputAction _jumpInputAction;
    private InputAction _crouchInputAction;
    private InputAction _strafeInputAction;
    private PlayerInputSystem _PIS;

    private JumpManager _jumpManager = new();
    private JumpParams _currJumpParams = new();
    private Rigidbody _rigidbody;
    private Vector2 _moveInputAcc;

    private Quaternion _playerFaceDirRot;

    private Vector3 _playerMoveDir;
    private Vector3 _playerLookDir;
    private Vector3 _playerLookDirXZ;
    private Vector3 _playerInputDir;
    private Vector3 _desiredMoveDir;
    private Vector3 _momentumDir;
    private Vector3 _prevTargetVelXZNorm = Vector3.forward;
    
    private float _playerFaceTurnRate;
    private float _momentumMag;
    private float _onGroundSphereCastDist;

    private int _playerRaycastMask = 0;

    private bool _isFirstJumpUpdate = false;
    private bool _isOnGround = false;
    private bool _isInGroundSnapRange = false;
    private bool _isStrafing = false;

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
        _crouchInputAction = InputSystem.actions.FindAction("Crouch");
        _strafeInputAction = InputSystem.actions.FindAction("Sprint");
        
        _PIS = PlayerInputSystem.MainPISInstance;
        _moveInputAction.performed += _PIS.HandleInputCallback;
        _moveInputAction.canceled += _PIS.HandleInputCallback;

        _jumpInputAction.performed += _PIS.HandleInputCallback;
        _jumpInputAction.canceled += _PIS.HandleInputCallback;
       
        _crouchInputAction.performed += _PIS.HandleInputCallback;
        _crouchInputAction.canceled += _PIS.HandleInputCallback;

        _strafeInputAction.performed += _PIS.HandleInputCallback;
        _strafeInputAction.canceled += _PIS.HandleInputCallback;

        FirstJumpParams.AdjustJumpCurve();
        SecondJumpParams.AdjustJumpCurve();
        LongJumpJumpParams.AdjustJumpCurve();
        BackflipJumpParams.AdjustJumpCurve();
        LeftSideFlipJumpParams.AdjustJumpCurve();
        //Debug.Log(PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction);
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        Vector2 moveInput = _moveInputAction.ReadValue<Vector2>();
        _moveInputAcc += moveInput;

        _PIS.ProcessInputActionStates();

        if (_PIS.GetPlayerInputActionTypes().Contains(PlayerInputActionTypes.Jump))
        {
            bool jumpTriggered = false;
            if (_isOnGround && _jumpManager.CurrJump.GetRemainingJumpTime() == 0.0f)
            {
                switch (_PIS.GetPlayerAction(0))
                {
                    case PlayerActions.LongJump: _currJumpParams = LongJumpJumpParams; break;
                    case PlayerActions.Backflip: _currJumpParams = BackflipJumpParams; break;
                    case PlayerActions.ForwardLeftSide:
                    {
                        LeftSideFlipJumpParams.JumpAxes.X.x = -0.707f;
                        LeftSideFlipJumpParams.JumpAxes.X.z = 0.707f;
                        _currJumpParams = LeftSideFlipJumpParams.ShallowCopy();
                        _currJumpParams.JumpWidth *= 1.414f;
                    } break;
                    case PlayerActions.LeftSide:
                    {
                        LeftSideFlipJumpParams.JumpAxes.X.x = -1.0f;
                        LeftSideFlipJumpParams.JumpAxes.X.y = 0.0f; 
                        _currJumpParams = LeftSideFlipJumpParams; 
                    } break;
                    case PlayerActions.ForwardRightSide:
                    {
                        LeftSideFlipJumpParams.JumpAxes.X.x = 0.707f;
                        LeftSideFlipJumpParams.JumpAxes.X.z = 0.707f;
                        _currJumpParams = LeftSideFlipJumpParams.ShallowCopy();
                        _currJumpParams.JumpWidth *= 1.414f;
                    } break;
                    case PlayerActions.RightSide:
                    {
                        LeftSideFlipJumpParams.JumpAxes.X.x = 1.0f;
                        LeftSideFlipJumpParams.JumpAxes.X.y = 0.0f; 
                        _currJumpParams = LeftSideFlipJumpParams;
                    } break;
                    default: _currJumpParams = FirstJumpParams; break;
                }
                jumpTriggered = _jumpManager.StartJump(_currJumpParams);
            }
            else if (_currJumpParams != SecondJumpParams && 
                     _jumpManager.CurrJump.GetRemainingJumpTime() > 0.0f && 
                     _jumpManager.CurrJump.GetJumpTime() > MinSecondJumpDelay && 
                     _jumpManager.CurrJump.GetJumpTime() < MaxSecondJumpDelay)
            {
                jumpTriggered = _jumpManager.StartJump(SecondJumpParams);
                _currJumpParams = jumpTriggered ? SecondJumpParams : _currJumpParams;
            }

            if (jumpTriggered)
            {
                _isFirstJumpUpdate = true;   
            }
        }
    }

    int _frameSinceJumpStopped = 0;
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Quaternion cameraYRot = CameraController.GetRotationAroundAxis(PlayerCamera.GetCameraOrientation(), Vector3.up); 

        _moveInputAcc.Normalize();
        _isOnGround = Physics.SphereCast(transform.position, 
                                         PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction, 
                                         -Vector3.up, 
                                         out RaycastHit hit, 
                                         _onGroundSphereCastDist, 
                                         _playerRaycastMask, 
                                         QueryTriggerInteraction.Ignore);
        
        if (_isOnGround && _currJumpParams.HittingGroundCancelsJump && _jumpManager.CurrJump.GetJumpTime() > 0.1f)
        {
            _jumpManager.StopJump(_rigidbody);
            _frameSinceJumpStopped++;
        }
        _isStrafing = _isOnGround && _PIS.GetPlayerInputActionTypes().Contains(PlayerInputActionTypes.Strafing);
        
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
        Debug.Log(_isOnGround);

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
        float dirChangeMag = DirChangeRate * (_isStrafing ? DirChangeRateStrafingFactor : 1.0f);
        
        _playerFaceTurnRate = dirChangeMag;
        dirChangeMag *= dt;

        dirChangeMag = (dirChangeMag > dirDiffMag) ? dirDiffMag : dirChangeMag;
        _playerMoveDir += dirDiff.normalized * dirChangeMag;

        Vector3 targetVelXZ = _playerMoveDir * currMoveSpeed;
        Vector3 currVel = _rigidbody.linearVelocity; currVel.y = 0.0f;

        if (targetVelXZ == Vector3.zero && _jumpManager.CurrJump.JumpType == JumpTypes.Double)
            targetVelXZ = currVel.normalized * currMoveSpeed;
        if ((_jumpManager.CurrJump.GetRemainingJumpTime() <= 0.0f) || 
            (_jumpManager.CurrJump.GetRemainingJumpTime() > 0.0f && _currJumpParams.AllowsMidAirControl))
            _rigidbody.AddForce(targetVelXZ - currVel, ForceMode.VelocityChange);

        if (!(_jumpManager.CurrJump.GetRemainingJumpTime() > 0.0f && !_currJumpParams.AllowsMidAirControl))
        {
            _prevTargetVelXZNorm = (targetVelXZ.sqrMagnitude > 0.001f) ? targetVelXZ.normalized : _prevTargetVelXZNorm;
            _prevTargetVelXZNorm = _isStrafing ? _playerLookDirXZ : _prevTargetVelXZNorm;

            _playerFaceDirRot = Quaternion.RotateTowards(CameraController.GetRotationAroundAxis(PCAM.transform.rotation, Vector3.up), 
                                                         Quaternion.FromToRotation(Vector3.forward, _prevTargetVelXZNorm), 
                                                         _playerFaceTurnRate);
            PCAM.PlayerFaceDirRot = _playerFaceDirRot;   
        }

        _jumpManager.ChosenYAxis = _currJumpParams.JumpAxes.Y;
        _jumpManager.ChosenXAxis = _currJumpParams.JumpAxes.X;

        if (_currJumpParams.UseLocalAxes.Y)
        {
            _jumpManager.ChosenYAxis = (PCAM.transform.forward * _currJumpParams.JumpAxes.Y.z) + 
                                       (PCAM.transform.right * _currJumpParams.JumpAxes.Y.x) + 
                                       (PCAM.transform.up * _currJumpParams.JumpAxes.Y.y); 
        }
        if (_currJumpParams.UseLocalAxes.X)
        {
            _jumpManager.ChosenXAxis = (PCAM.transform.forward * _currJumpParams.JumpAxes.X.z) + 
                                       (PCAM.transform.right * _currJumpParams.JumpAxes.X.x) + 
                                       (PCAM.transform.up * _currJumpParams.JumpAxes.X.y);
        }

        _moveInputAcc = Vector2.zero;
        if (_currJumpParams.IsVerticalJump && _jumpManager.CurrJump.RemainingVertJumpTime > 0.0f && !_isFirstJumpUpdate)
        {
            _frameSinceJumpStopped = 0;
            float t = _jumpManager.CurrJump.CurrVertJumpTime / _currJumpParams.VerticalJumpDuration;
            float adjustedDT = dt / _currJumpParams.VerticalJumpDuration;
            
            float acceleration = CalcCurveAcceleration(_currJumpParams.JumpCurves.Y, t, adjustedDT);
            float heightDelta = CalcCurveVelocity(_currJumpParams.JumpCurves.Y, t, adjustedDT);
            
            float velocity = heightDelta * _currJumpParams.JumpHeight / _currJumpParams.VerticalJumpDuration; 
            float gravity = (_currJumpParams.IncludeGravityOnDecent && heightDelta < 0.0f) ? 0.0f : Physics.gravity.y;

            acceleration *= _currJumpParams.JumpHeight;
            acceleration /= _currJumpParams.VerticalJumpDuration * _currJumpParams.VerticalJumpDuration;
            acceleration -= gravity;
            // We need to take gravity into account, to ensure the jump height curve is followed properly.
            // This is due to the jump height curve not taking gravity into account itself.

            if (_currJumpParams.JumpModes.Y == JumpMethods.Acceleration)
                _rigidbody.AddForce(_jumpManager.ChosenYAxis * acceleration, ForceMode.Acceleration);
            else
            {
                _rigidbody.AddForce(_jumpManager.ChosenYAxis * (velocity - (gravity * dt)), ForceMode.VelocityChange);
                _rigidbody.AddForce(_jumpManager.ChosenYAxis * -_jumpManager.PrevVel.y, ForceMode.VelocityChange);   
            }
            
            _jumpManager.PrevVel.y = velocity;
            if (!_currJumpParams.IsHorizontalJump) {_jumpManager.CurrJump.JumpDurationTick(dt);}
        }
        else if (_currJumpParams.IsVerticalJump && _isFirstJumpUpdate)
        {
            if (_isOnGround)
                _jumpManager.PrevVel.y = -(Physics.gravity.y * dt);
            else
                _jumpManager.PrevVel.y += Physics.gravity.y * dt;
                
            _isOnGround = _currJumpParams.IsHorizontalJump;
            _isFirstJumpUpdate = _currJumpParams.IsHorizontalJump;
            
            if (_currJumpParams.JumpModes.Y == JumpMethods.Acceleration)
            {
                float adjustedDT = dt / _currJumpParams.VerticalJumpDuration;
                float firstCurveHeight = CalcCurveVelocity(_currJumpParams.JumpCurves.Y, 0.0f, adjustedDT) * _currJumpParams.JumpHeight;
                float speed = firstCurveHeight / _currJumpParams.VerticalJumpDuration;
                
                speed -= Vector3.Project(Vector3.one * _rigidbody.linearVelocity.y, _jumpManager.ChosenYAxis).magnitude;
                speed = (speed < 0.0f) ? 0.0f : speed;

                speed += Physics.gravity.y * dt;
                _rigidbody.AddForce(_jumpManager.ChosenYAxis * speed, ForceMode.VelocityChange);
            }
        }
        Debug.Log(_frameSinceJumpStopped);

        if (_currJumpParams.IsHorizontalJump && _jumpManager.CurrJump.RemainingHoriJumpTime > 0.0f && !_isFirstJumpUpdate)
        {
            float t = _jumpManager.CurrJump.CurrHoriJumpTime/ _currJumpParams.HorizontalJumpDuration;
            float adjustedDT = dt / _currJumpParams.HorizontalJumpDuration;
            
            float acceleration = CalcCurveAcceleration(_currJumpParams.JumpCurves.X, t, adjustedDT);
            float widthDelta = CalcCurveVelocity(_currJumpParams.JumpCurves.X, t, adjustedDT);
            float velocity = widthDelta * _currJumpParams.JumpWidth / _currJumpParams.HorizontalJumpDuration; 

            acceleration *= _currJumpParams.JumpWidth;
            acceleration /= _currJumpParams.HorizontalJumpDuration * _currJumpParams.HorizontalJumpDuration;
            // We need to take gravity into account, to ensure the jump height curve is followed properly.
            // This is due to the jump height curve not taking gravity into account itself.

            if (_currJumpParams.JumpModes.X == JumpMethods.Acceleration)
                _rigidbody.AddForce(_jumpManager.ChosenXAxis * acceleration, ForceMode.Acceleration);
            else
            {
                _rigidbody.AddForce(_jumpManager.ChosenXAxis * velocity, ForceMode.VelocityChange);
                _rigidbody.AddForce(_jumpManager.ChosenXAxis * -_jumpManager.PrevVel.x, ForceMode.VelocityChange);   
            }
            
            _jumpManager.PrevVel.x = velocity;
            _jumpManager.CurrJump.JumpDurationTick(dt);
        }
        else if (_currJumpParams.IsHorizontalJump && _isFirstJumpUpdate)
        {
            if (_isOnGround)
                _jumpManager.PrevVel.x = 0.0f;
            
            _isOnGround = false;
            _isFirstJumpUpdate = false;

            if (_currJumpParams.JumpModes.X == JumpMethods.Acceleration)
            {
                float adjustedDT = dt / _currJumpParams.HorizontalJumpDuration;
                float firstCurveWidth = CalcCurveVelocity(_currJumpParams.JumpCurves.X, 0.0f, adjustedDT) * _currJumpParams.JumpWidth;
                float speed = firstCurveWidth / _currJumpParams.HorizontalJumpDuration;
                
                speed -= Vector3.Project(_rigidbody.linearVelocity, _jumpManager.ChosenXAxis).magnitude;
                speed = (speed < 0.0f) ? 0.0f : speed;
                _rigidbody.AddForce(_jumpManager.ChosenXAxis * speed, ForceMode.VelocityChange);   
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
