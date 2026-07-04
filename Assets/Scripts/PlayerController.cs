using System;
using Unity.VisualScripting;
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
    Single = 1 | 32,
    Double = 2 | 1 | 32,
    Back = 4 | 32,
    Side = 8 | 32,
    Long = 16,
    Slam = 32,
    Charge = 64 | 256 | unchecked((int)0x80000000),
    Swing = 128 | 1 | 4 | 8 | 16 | 32 | unchecked((int)0x80000000), // Make negative.
    Bonk = 256
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
    
    public int MaxChainJumps = 1;
    
    public float JumpHeight;
    public float JumpWidth;
    public float VerticalJumpDuration;
    public float HorizontalJumpDuration;
    public float MinChainJumpDelay = float.PositiveInfinity; // Amount of time that must pass after the first jump.
    public float MaxChainJumpDelay = 0.0f;
    
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

    public int MaxChainJumps {get; private set;}
    public int NumJumps;

    public float MinChainJumpDelay {get; private set;}
    public float MaxChainJumpDelay {get; private set;}

    public float CurrVertJumpTime {get; private set;}
    public float RemainingVertJumpTime {get; private set;}
    public float CurrHoriJumpTime {get; private set;}
    public float RemainingHoriJumpTime {get; private set;}

    public void FromJumpParams(JumpParams inJumpParams)
    {
        JumpModes = inJumpParams.JumpModes;
        JumpType = inJumpParams.JumpType;

        MaxChainJumps = inJumpParams.MaxChainJumps;
        MinChainJumpDelay = inJumpParams.MinChainJumpDelay;
        MaxChainJumpDelay = inJumpParams.MaxChainJumpDelay;

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
        
        MaxChainJumps = 0;
        NumJumps = 0;

        MinChainJumpDelay = float.PositiveInfinity;
        MaxChainJumpDelay = 0.0f;

        CurrVertJumpTime = float.NegativeInfinity;
        RemainingVertJumpTime = 0.0f;

        CurrHoriJumpTime = float.NegativeInfinity;
        RemainingHoriJumpTime = 0.0f;
    }
}

public class JumpManager
{
    public Vector2 PrevVel = new();
    public Vector2 ProjectedMagnitude = new();
    public JumpDurationParams CurrJump = new();

    public Vector3 PrevChosenYAxis;
    public Vector3 PrevChosenXAxis;

    public Vector3 ChosenYAxis;
    public Vector3 ChosenXAxis;

    public int NumJumps { get; private set; }

    public bool StartJump(JumpParams inJumpParams)
    {
        // Bit of a hack, but there's no quick and easy way to prevent this.
        // Normal checks below will falsely return true for this combo,
        // due to the bitwise & succeeding as you can slam with both.
        if (CurrJump.JumpType == JumpTypes.Side && inJumpParams.JumpType == JumpTypes.Double)
            return false;

        if (CurrJump.JumpType == JumpTypes.None || 
           ((CurrJump.JumpType != inJumpParams.JumpType || (int)CurrJump.JumpType < 0) && 
           ((int)CurrJump.JumpType & unchecked(0x7FFFFFFF) & (int)inJumpParams.JumpType) != 0 &&
           (CurrJump.NumJumps == 0 || 
           (CurrJump.NumJumps < CurrJump.MaxChainJumps && 
           CurrJump.GetJumpTime() >= CurrJump.MinChainJumpDelay && 
           CurrJump.GetJumpTime() <= CurrJump.MaxChainJumpDelay))))
        {
            CurrJump.FromJumpParams(inJumpParams);
            NumJumps++;
            
            CurrJump.NumJumps = NumJumps;
            return true;
        }

        return false;
    }

    public void StopJump(Rigidbody inRigidbody, bool inKeepPrevYVel = false, bool inKeepPrevXVel = false)
    {
        CurrJump.Reset();
        ProjectedMagnitude = Vector2.one * float.PositiveInfinity;

        if (inRigidbody != null && CurrJump.JumpModes.Y == JumpMethods.HeightDelta && !inKeepPrevYVel)
            inRigidbody.AddForce(ChosenYAxis * -PrevVel.y, ForceMode.VelocityChange);
        
        if (inRigidbody != null && CurrJump.JumpModes.X == JumpMethods.HeightDelta && !inKeepPrevXVel)
            inRigidbody.AddForce(ChosenXAxis * -PrevVel.x, ForceMode.VelocityChange);   
    }

    public void ResetNumJumps()
    {
        NumJumps = 0;
    }
}

public class PlayerController : MonoBehaviour
{
    public PlayerColAnimManager PCAM;
    public CameraController PlayerCamera;
    public CameraPivot PlayerCameraPivot;
    public BombLauncher PlayerBombLauncher;
    
    public float WalkMoveSpeed = 3.0f;
    public float JumpMoveSpeedFactor = 0.9f;
    public float InAirMoveSpeedFactor = 0.2f;
    public float PlayerFaceDirRotSpeedFactor = 2.0f;
    public float ForwardSideFlipSpeedReduceFactor = 2.0f;
    public float DirChangeRate = 2.0f;
    // How much the current horizontal velocity effects how quickly the player direction is changed.
    public float DirChangeRateVelFactor = 1.0f;
    public float DirChangeRateStrafingFactor = 10.0f;
    public float DirChangeRateSlidingFactor = 0.05f;
    public float DirChangeRateInAirFactor = 0.2f;

    public JumpParams FirstJumpParams;
    public JumpParams SecondJumpParams;
    public JumpParams LongJumpJumpParams;
    public JumpParams BackflipJumpParams;
    public JumpParams LeftSideFlipJumpParams;
    public JumpParams ChargeJumpParams;
    public JumpParams SwingJumpParams;
    public JumpParams SlamJumpParams;

    public float FarOffGroundDistance = 2.0f;
    public float OnGroundRadiusReduction = 0.01f;
    public float OnGroundDistanceIncrease = 0.025f;
    public float GroundSnapDistance = 0.05f;
    public float GroundSnapSpeed = 10.0f;

    private BlasterWeapon _blasterWeapon;

    private InputAction _moveInputAction;
    private InputAction _jumpInputAction;
    private InputAction _crouchInputAction;
    private InputAction _strafeInputAction;
    private InputAction _swingInputAction;
    private InputAction _attackInputAction;
    private InputAction _aimInputAction;
    private PlayerInputSystem _PIS;

    private JumpManager _jumpManager = new();
    private JumpParams _currJumpParams = new();
    private JumpParams _nextJumpParams = new();
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
    private float _farOffGroundCastDist;

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
        
        _blasterWeapon = GetComponent<BlasterWeapon>();
        _blasterWeapon.ProjectileSpawnPoint = transform;

        _playerRaycastMask = GameUtils.CollisionLayerToRaycastMask(LayerMask.NameToLayer("Player"));
        _farOffGroundCastDist = (PCAM.MainPlayerCollider.height / 2.0f) + FarOffGroundDistance;
        _onGroundSphereCastDist = (PCAM.MainPlayerCollider.height / 2.0f) - PCAM.MainPlayerCollider.radius + OnGroundRadiusReduction;
        _onGroundSphereCastDist += OnGroundDistanceIncrease; 
        // Little bit of leeway meaning the player is just slightly above the ground when the game thinks they're on the ground.

        _rigidbody = GetComponent<Rigidbody>();
        if (!_rigidbody)
        {
            throw new NullReferenceException("PlayerController could not get a rigidbody!");
        }
        
        _moveInputAction = InputSystem.actions.FindAction("Move");
        _jumpInputAction = InputSystem.actions.FindAction("Jump");
        _crouchInputAction = InputSystem.actions.FindAction("Crouch");
        _strafeInputAction = InputSystem.actions.FindAction("Sprint");
        _swingInputAction = InputSystem.actions.FindAction("Swing");
        _attackInputAction = InputSystem.actions.FindAction("Attack");
        _aimInputAction = InputSystem.actions.FindAction("Aim");

        _PIS = PlayerInputSystem.MainPISInstance;
        _moveInputAction.performed += _PIS.HandleInputCallback;
        _moveInputAction.canceled += _PIS.HandleInputCallback;

        _jumpInputAction.performed += _PIS.HandleInputCallback;
        _jumpInputAction.canceled += _PIS.HandleInputCallback;
       
        _crouchInputAction.performed += _PIS.HandleInputCallback;
        _crouchInputAction.canceled += _PIS.HandleInputCallback;

        _strafeInputAction.performed += _PIS.HandleInputCallback;
        _strafeInputAction.canceled += _PIS.HandleInputCallback;

        _swingInputAction.performed += _PIS.HandleInputCallback;
        _swingInputAction.canceled += _PIS.HandleInputCallback;

        FirstJumpParams.AdjustJumpCurve();
        SecondJumpParams.AdjustJumpCurve();
        LongJumpJumpParams.AdjustJumpCurve();
        BackflipJumpParams.AdjustJumpCurve();
        LeftSideFlipJumpParams.AdjustJumpCurve();
        ChargeJumpParams.AdjustJumpCurve();
        SwingJumpParams.AdjustJumpCurve();
        SlamJumpParams.AdjustJumpCurve();
        //Debug.Log(PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction);
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        Vector2 moveInput = _moveInputAction.ReadValue<Vector2>();
        _moveInputAcc += moveInput;

        _PIS.ProcessInputActionStates();
        if (_attackInputAction.IsPressed() && _isStrafing)
        {
            // Vector3 trajectoryStartPoint = Vector3.Project(transform.position - PlayerCamera.transform.position, PlayerCamera.transform.forward);
            // Vector3 cameraCentre = PlayerCamera.GetCamera().ViewportToWorldPoint(new Vector2(0.5f, 0.5f));
            
            // RaycastHit enemyHit;
            // bool aimingAtEnemy = Physics.SphereCast(cameraCentre, 0.33f, PlayerCamera.transform.forward, out enemyHit, 1000.0f, 1 << LayerMask.NameToLayer("Enemy"));
            // Debug.Log(aimingAtEnemy);

            // if (!aimingAtEnemy)
            //     ((IWeapon)_blasterWeapon).TryFire(PlayerCamera.transform.forward, PlayerCamera.transform.position + trajectoryStartPoint, PlayerCamera.transform.forward);
            // else
            //     ((IWeapon)_blasterWeapon).TryFire(Vector3.zero, Vector3.zero, Vector3.zero, enemyHit.transform);

            Vector3 cameraCentre = PlayerCamera.GetCamera().ViewportToWorldPoint(new Vector2(0.5f, 0.5f));
            Vector3 hitPoint = Vector3.one * float.PositiveInfinity;
            
            RaycastHit groundHit;
            bool aimingAtGround = Physics.Raycast(cameraCentre, PlayerCamera.transform.forward, out groundHit, 1000.0f, _playerRaycastMask);

            if (aimingAtGround)
                hitPoint = groundHit.point;

            ((IWeapon)PlayerBombLauncher).TryFire(hitPoint, Vector3.one * 3.0f, Vector3.zero);
        }
        else if (_attackInputAction.IsPressed())
        {
            Vector3 hitPoint = Vector3.one * float.PositiveInfinity;
            ((IWeapon)PlayerBombLauncher).TryFire(hitPoint, Vector3.one * 3.0f, Vector3.zero);
        }
    }

    private float _currMoveSpeed = 1.0f;
    private float _currDirChangeRate = 1.0f;
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Quaternion cameraYRot = CameraController.GetRotationAroundAxis(PlayerCamera.GetCameraOrientation(), Vector3.up); 

        _moveInputAcc.Normalize();
        _isOnGround = Physics.SphereCast(transform.position, 
                                         PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction, 
                                         -Vector3.up, 
                                         out RaycastHit _, 
                                         _onGroundSphereCastDist, 
                                         _playerRaycastMask, 
                                         QueryTriggerInteraction.Ignore);
        
        bool fakeIsOnGround = false;
        bool stopCharge = _jumpManager.CurrJump.JumpType == JumpTypes.Charge && _attackInputAction.IsPressed();
        bool isFarOffGround = !Physics.Raycast(transform.position, -Vector3.up, _farOffGroundCastDist, _playerRaycastMask, QueryTriggerInteraction.Ignore);
        
        if ((_isOnGround && _currJumpParams.HittingGroundCancelsJump && _jumpManager.CurrJump.GetJumpTime() > 0.1f) || stopCharge)
        {
            _jumpManager.StopJump(_rigidbody, stopCharge, stopCharge);
            _jumpManager.ResetNumJumps();

            if (isFarOffGround)
            {
                Vector3 currLinVel = _rigidbody.linearVelocity; currLinVel.y = 0.0f;
                _currDirChangeRate = DirChangeRate * DirChangeRateInAirFactor;
                _playerMoveDir = currLinVel.normalized;
                _currMoveSpeed = currLinVel.magnitude;
            }
        }
        else if (_jumpManager.CurrJump.GetRemainingJumpTime() < 0.0f)
        {
            if (_jumpManager.CurrJump.JumpType == JumpTypes.Charge)
                fakeIsOnGround = true;
            _jumpManager.StopJump(_rigidbody, !_isOnGround || fakeIsOnGround, !_isOnGround || fakeIsOnGround);

            if (isFarOffGround)
            {
                Vector3 currLinVel = _rigidbody.linearVelocity; currLinVel.y = 0.0f;
                _currDirChangeRate = DirChangeRate * DirChangeRateInAirFactor;
                _playerMoveDir = currLinVel.normalized;
                _currMoveSpeed = currLinVel.magnitude;
            }
        }   
        
        if (_isOnGround && _jumpManager.CurrJump.GetJumpTime() <= 0.0f)
            _jumpManager.ResetNumJumps();
        
        _isStrafing = _isOnGround && _PIS.GetIfInputPresent(PlayerInputActionTypes.Strafing);
        _isOnGround = _isOnGround ? _isOnGround : fakeIsOnGround || _jumpManager.CurrJump.JumpType == JumpTypes.Swing && _jumpManager.NumJumps < 2;

        PlayerCameraPivot.InStrafeMode = _isStrafing;
        if (_isOnGround && _aimInputAction.IsPressed() && _jumpManager.CurrJump.GetRemainingJumpTime() == 0.0f)
        {
            if (stopCharge)
            {
                Vector3 currLinVel = _rigidbody.linearVelocity; currLinVel.y = 0.0f;
                _currDirChangeRate = DirChangeRate * DirChangeRateInAirFactor;
                _playerMoveDir = currLinVel.normalized;
                _currMoveSpeed = currLinVel.magnitude;
            }

            PlayerCamera.EnableFreeFly = true;
            PlayerCamera.CanMove = false;
        }
        else
        {
            PlayerCamera.EnableFreeFly = false;
            PlayerCamera.CanMove = true;
        }

        if (_PIS.GetPlayerInputActionTypes().Count > 0)
        {
            bool jumpTriggered = false;
            bool noJumpInput = false;
            float factor = 1.0f;

            if (_isOnGround || (!_isOnGround && _jumpManager.CurrJump.NumJumps > 0))
            {
                switch (_PIS.GetPlayerAction(0))
                {
                    case PlayerActions.LongJump: _nextJumpParams = LongJumpJumpParams; break;
                    case PlayerActions.Backflip: _nextJumpParams = BackflipJumpParams; break;
                    case PlayerActions.ForwardLeftSide:
                    {
                        LeftSideFlipJumpParams.JumpAxes.X.x = -0.707f;
                        LeftSideFlipJumpParams.JumpAxes.X.z = 0.707f;
                        _nextJumpParams = LeftSideFlipJumpParams.ShallowCopy();
                        _nextJumpParams.JumpWidth *= 1.414f;

                        Vector3 currLinVel = _rigidbody.linearVelocity; currLinVel.y = 0.0f;
                        float currMoveSpeed = currLinVel.magnitude;
                        float jumpDurationReductionFactor = (currMoveSpeed > WalkMoveSpeed) ? 
                        (WalkMoveSpeed / currMoveSpeed) * ForwardSideFlipSpeedReduceFactor : 1.0f;

                        factor = jumpDurationReductionFactor;
                        factor = _nextJumpParams.VerticalJumpDuration / (_nextJumpParams.VerticalJumpDuration * factor);
                        factor = factor < 1.0f ? 1.0f : factor;

                        _nextJumpParams.VerticalJumpDuration *=  jumpDurationReductionFactor;
                        _nextJumpParams.HorizontalJumpDuration *= jumpDurationReductionFactor;
                    } break;
                    case PlayerActions.LeftSide:
                    {
                        LeftSideFlipJumpParams.JumpAxes.X.x = -1.0f;
                        LeftSideFlipJumpParams.JumpAxes.X.y = 0.0f; 
                        _nextJumpParams = LeftSideFlipJumpParams; 
                    } break;
                    case PlayerActions.ForwardRightSide:
                    {
                        LeftSideFlipJumpParams.JumpAxes.X.x = 0.707f;
                        LeftSideFlipJumpParams.JumpAxes.X.z = 0.707f;
                        _nextJumpParams = LeftSideFlipJumpParams.ShallowCopy();
                        _nextJumpParams.JumpWidth *= 1.414f;

                        Vector3 currLinVel = _rigidbody.linearVelocity; currLinVel.y = 0.0f;
                        float currMoveSpeed = currLinVel.magnitude;
                        float jumpDurationReductionFactor = (currMoveSpeed > WalkMoveSpeed) ? 
                        (WalkMoveSpeed / currMoveSpeed) * ForwardSideFlipSpeedReduceFactor : 1.0f;

                        factor = jumpDurationReductionFactor;
                        factor = _nextJumpParams.VerticalJumpDuration / (_nextJumpParams.VerticalJumpDuration * factor);
                        factor = factor < 1.0f ? 1.0f : factor;

                        _nextJumpParams.VerticalJumpDuration *=  jumpDurationReductionFactor;
                        _nextJumpParams.HorizontalJumpDuration *= jumpDurationReductionFactor;
                    } break;
                    case PlayerActions.RightSide:
                    {
                        LeftSideFlipJumpParams.JumpAxes.X.x = 1.0f;
                        LeftSideFlipJumpParams.JumpAxes.X.y = 0.0f; 
                        _nextJumpParams = LeftSideFlipJumpParams;
                    } break;
                    case PlayerActions.Charge:
                    {
                        _nextJumpParams = ChargeJumpParams;
                    } break;
                    case PlayerActions.Swing:
                    {
                        if (_isOnGround)
                        {
                            _nextJumpParams = SwingJumpParams;
                        }
                        else
                        {
                            _nextJumpParams = SlamJumpParams;   
                        }
                    } break;
                    case PlayerActions.Jump:
                    {
                        if (_jumpManager.CurrJump.NumJumps == 0)
                            _nextJumpParams = FirstJumpParams;
                        else
                        {
                            _nextJumpParams = SecondJumpParams;   
                        }
                    } break;
                    default: noJumpInput = true; break;
                }

                jumpTriggered = !noJumpInput ? _jumpManager.StartJump(_nextJumpParams) : false;
                _currJumpParams = jumpTriggered ? _nextJumpParams : _currJumpParams;
            }
            if (jumpTriggered)
            {
                _isFirstJumpUpdate = true;
                // if (_playerMoveDir == Vector3.zero && _currJumpParams.JumpType == JumpTypes.Swing)
                // {
                //     _playerMoveDir = PCAM.transform.forward;   
                // }

                switch (_PIS.GetPlayerAction(0))
                {
                    case PlayerActions.Charge:
                    {
                        Vector3 currLinVel = _rigidbody.linearVelocity; currLinVel.y = 0.0f;
                        if (currLinVel.magnitude > WalkMoveSpeed)
                            _rigidbody.AddForce((currLinVel.normalized * WalkMoveSpeed) - currLinVel, ForceMode.VelocityChange);
                    } break;
                }

                switch (_currJumpParams.JumpType)
                {
                    case JumpTypes.Double:
                    {
                        PCAM.PlayerAnimator.ResetControllerState();
                        PCAM.PlayerAnimator.Play("DoubleJumpRoll");
                    } break;
                    case JumpTypes.Side:
                    {
                        PCAM.PlayerAnimator.ResetControllerState();
                        switch (_PIS.GetPlayerAction(0))
                        {
                            case PlayerActions.ForwardLeftSide:
                                PCAM.PlayerAnimator.SetFloat("PlaybackSpeed", factor); goto case PlayerActions.LeftSide;
                            case PlayerActions.LeftSide:
                                PCAM.PlayerAnimator.Play("LeftSideJumpRoll"); break;
                            case PlayerActions.ForwardRightSide:
                                PCAM.PlayerAnimator.SetFloat("PlaybackSpeed", factor); goto case PlayerActions.RightSide;
                            case PlayerActions.RightSide:
                                PCAM.PlayerAnimator.Play("RightSideJumpRoll"); break;
                        }
                    } break;
                    case JumpTypes.Back:
                    {
                        PCAM.PlayerAnimator.ResetControllerState();
                        PCAM.PlayerAnimator.Play("BackflipJumpRoll");
                    } break;
                    case JumpTypes.Swing:
                    {
                        PCAM.PlayerAnimator.ResetControllerState();
                        PCAM.PlayerAnimator.Play("WrenchSwingAnim");
                    } break;
                    case JumpTypes.Slam:
                    {
                        PCAM.PlayerAnimator.ResetControllerState();
                        PCAM.PlayerAnimator.Play("WrenchSlamAnim");
                    } break;
                }
            }
        }

        _isInGroundSnapRange = Physics.SphereCast(transform.position, 
                                                  PCAM.MainPlayerCollider.radius - OnGroundRadiusReduction, 
                                                  -Vector3.up, 
                                                  out RaycastHit _, 
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

        bool isMoveInput = false;
        if ((_isOnGround && _PIS.GetPlayerInputActionTypes().BinarySearch(PlayerInputActionTypes.Move) >= 0 && 
            _PIS.GetPlayerInputActionTypes().BinarySearch(PlayerInputActionTypes.Jump) < 0 &&
            _PIS.GetPlayerInputActionTypes().BinarySearch(PlayerInputActionTypes.Crouch) < 0) ||
            _PIS.GetPlayerInputActionTypes().BinarySearch(PlayerInputActionTypes.Strafing) >= 0 ||
            (!_isOnGround && _currJumpParams.AllowsMidAirControl && _desiredMoveDir != Vector3.zero))
        {
            isMoveInput = true;
            _currMoveSpeed = WalkMoveSpeed;
            _currDirChangeRate = DirChangeRate * (_isStrafing ? DirChangeRateStrafingFactor : 1.0f);   
        }
        
        float moveSpeed = _currMoveSpeed;
        if (!_isOnGround && !_isFirstJumpUpdate && _jumpManager.CurrJump.GetRemainingJumpTime() > 0.0f)
        {
            moveSpeed *= JumpMoveSpeedFactor;
        }
        else if (!_isOnGround && isMoveInput && _jumpManager.CurrJump.GetRemainingJumpTime() <= 0.0f)
        {
            moveSpeed *= InAirMoveSpeedFactor;
        }

        Vector3 dirDiff = _desiredMoveDir - _playerMoveDir;
        float dirDiffMag = dirDiff.magnitude; 
        
        _playerFaceTurnRate = _currDirChangeRate * PlayerFaceDirRotSpeedFactor;
        float dirChangeMag = _currDirChangeRate * dt;

        dirChangeMag = (dirChangeMag > dirDiffMag) ? dirDiffMag : dirChangeMag;
        _playerMoveDir += dirDiff.normalized * dirChangeMag;

        Vector3 targetVelXZ = _playerMoveDir * moveSpeed;
        Vector3 currVel = _rigidbody.linearVelocity; currVel.y = 0.0f;

        if (targetVelXZ == Vector3.zero && _jumpManager.CurrJump.JumpType == JumpTypes.Double)
            targetVelXZ = currVel.normalized * moveSpeed;

        if ((_jumpManager.CurrJump.GetRemainingJumpTime() <= 0.0f) || 
            (_jumpManager.CurrJump.GetRemainingJumpTime() > 0.0f && _currJumpParams.AllowsMidAirControl))
        {
            _rigidbody.AddForce(targetVelXZ - currVel, ForceMode.VelocityChange);
        }

        if (!(_jumpManager.CurrJump.GetRemainingJumpTime() > 0.0f && !_currJumpParams.AllowsMidAirControl) && _desiredMoveDir != Vector3.zero)
        {
            _prevTargetVelXZNorm = (targetVelXZ.sqrMagnitude > 0.001f) ? targetVelXZ.normalized : _prevTargetVelXZNorm;
            _prevTargetVelXZNorm = _PIS.GetIfInputPresent(PlayerInputActionTypes.Strafing) ? _playerLookDirXZ : _prevTargetVelXZNorm;

            Quaternion currRot = CameraController.GetRotationAroundAxis(PCAM.transform.rotation, Vector3.up);
            Quaternion targetRot = Quaternion.FromToRotation(Vector3.forward, _prevTargetVelXZNorm);
            float dirDist = Mathf.Acos(Mathf.Clamp(Vector3.Dot(PCAM.transform.forward, _prevTargetVelXZNorm), -1.0f, 1.0f));

            _playerFaceDirRot = Quaternion.Lerp(currRot, targetRot, (_playerFaceTurnRate * dt) / dirDist);
            _playerFaceDirRot = CameraController.GetRotationAroundAxis(_playerFaceDirRot, Vector3.up);
            PCAM.PlayerFaceDirRot = _playerFaceDirRot;   
        }
        if (_jumpManager.CurrJump.GetJumpTime() == 0.0f && _jumpManager.CurrJump.JumpType == JumpTypes.Swing)
        {
            PCAM.PlayerFaceDirRot = Quaternion.FromToRotation(PCAM.transform.forward, _desiredMoveDir) * PCAM.PlayerFaceDirRot;
            if (_desiredMoveDir == Vector3.zero)
                _desiredMoveDir = PCAM.transform.forward;

            _rigidbody.AddForce((_desiredMoveDir * currVel.magnitude) - currVel, ForceMode.VelocityChange);
            _prevTargetVelXZNorm = PCAM.transform.forward;   
        }

        #region Jumping
        _jumpManager.PrevChosenYAxis = _jumpManager.ChosenYAxis;
        _jumpManager.PrevChosenXAxis = _jumpManager.ChosenXAxis;
        
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

        if (_currJumpParams.IsHorizontalJump && _jumpManager.CurrJump.RemainingHoriJumpTime > 0.0f && !_isFirstJumpUpdate)
        {
            float t = _jumpManager.CurrJump.CurrHoriJumpTime/ _currJumpParams.HorizontalJumpDuration;
            float adjustedDT = dt / _currJumpParams.HorizontalJumpDuration;
            
            float acceleration = CalcCurveAcceleration(_currJumpParams.JumpCurves.X, t, adjustedDT);
            float widthDelta = CalcCurveVelocity(_currJumpParams.JumpCurves.X, t, adjustedDT);
            float velocity = widthDelta * _currJumpParams.JumpWidth / _currJumpParams.HorizontalJumpDuration; 
            
            float velProjMagNow = Vector3.Project(_rigidbody.linearVelocity, _jumpManager.PrevChosenXAxis).magnitude; 
            float newVelProjMag = Vector3.Project(_rigidbody.linearVelocity, _jumpManager.ChosenXAxis).magnitude;

            acceleration *= _currJumpParams.JumpWidth;
            acceleration /= _currJumpParams.HorizontalJumpDuration * _currJumpParams.HorizontalJumpDuration;
            // We need to take gravity into account, to ensure the jump height curve is followed properly.
            // This is due to the jump height curve not taking gravity into account itself.

            if (_currJumpParams.JumpModes.X == JumpMethods.Acceleration)
                _rigidbody.AddForce(_jumpManager.ChosenXAxis * acceleration, ForceMode.Acceleration);
            else
            {
                float velProjMagDiff = _jumpManager.ProjectedMagnitude.x - velProjMagNow;
                float prevVelCompensation = -_jumpManager.PrevVel.x;

                if (_jumpManager.ProjectedMagnitude.x != float.PositiveInfinity && 
                    velProjMagDiff > 0.001f && 
                    velProjMagNow < _jumpManager.ProjectedMagnitude.x)
                {

                    prevVelCompensation += velProjMagDiff;
                    if (prevVelCompensation > 0.0f)
                        prevVelCompensation = 0.0f;
                }
                _rigidbody.AddForce(_jumpManager.ChosenXAxis * velocity, ForceMode.VelocityChange);

                _jumpManager.ProjectedMagnitude.x = newVelProjMag + velocity + prevVelCompensation;
                _rigidbody.AddForce(_jumpManager.ChosenXAxis * prevVelCompensation, ForceMode.VelocityChange);   
            }
            
            _jumpManager.PrevVel.x = velocity;
            _jumpManager.CurrJump.JumpDurationTick(dt);
        }
        else if (_currJumpParams.IsHorizontalJump && _isFirstJumpUpdate)
        {
            if (_isOnGround)
                _jumpManager.PrevVel.x = 0.0f;
            
            _isOnGround = !_currJumpParams.IsVerticalJump;
            _isFirstJumpUpdate = false;

            float adjustedDT = dt / _currJumpParams.HorizontalJumpDuration;
            float firstCurveWidth = CalcCurveVelocity(_currJumpParams.JumpCurves.X, 0.0f, adjustedDT) * _currJumpParams.JumpWidth;
            float speed = firstCurveWidth / _currJumpParams.HorizontalJumpDuration;
            
            float velProjMag = Vector3.Project(_rigidbody.linearVelocity, _jumpManager.ChosenXAxis).magnitude;
            speed -= velProjMag;
            speed = (speed < 0.0f) ? 0.0f : speed;
            
            _jumpManager.ProjectedMagnitude.x = float.PositiveInfinity;
            if (_currJumpParams.JumpModes.X == JumpMethods.Acceleration)
                _rigidbody.AddForce(_jumpManager.ChosenXAxis * speed, ForceMode.VelocityChange);   
        }
        #endregion
    }

    public void RedirectPlayerVelocity()
    {
        Vector3 currVel = _rigidbody.linearVelocity;
        Vector3 currVelXZ = currVel; currVelXZ.y = 0.0f;
        Vector3 currVelXZNorm = currVelXZ.normalized;

        Vector3 newVec = Quaternion.Lerp(Quaternion.FromToRotation(Vector3.forward, currVelXZNorm), 
                                         Quaternion.FromToRotation(currVelXZNorm, Vector3.up), 0.75f) * currVelXZ;
        newVec.y += currVel.y;
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
