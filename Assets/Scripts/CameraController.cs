using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.InputSystem;

public struct CameraOrbitInfo
{
    public Vector3 Pos;
    public Vector3 Norm;
    public Vector3 Tgnt;
}

public class CameraController : MonoBehaviour
{
    public Transform Target;
    
    public Vector2 LookSpeedFPS = new (10.0f, 10.0f);
    public Vector2 LookSpeedTPS = new (10.0f, 10.0f);
    public Vector2 InputAxisFactorsFPS = new Vector2(1.0f, -1.0f); // For adjust look inversion and such.
    public Vector2 InputAxisFactorsTPS = new Vector2(-1.0f, 1.0f);

    public float MoveSpeed = 10.0f;
    public float MoveSpeedRampUp = 1.0f;
    public float TargetOrbitRadius = 4.0f;
    public float CollisionSphereRadius = 0.5f;
    public float MoveInterpolationSpeed = 1.0f;
    public float LookInterpolationSpeed = 1.0f;
    public float CameraOrbitTangentFactor = 0.4f;
    
    public bool EnableFreeFly;
    public bool FollowTarget;

    private InputAction MoveInputAction;
    private InputAction LookInputAction;
    private Vector2 LookInputAcc;

    CameraOrbitInfo TargetCameraOrbit;
    CameraOrbitInfo CurrCameraOrbit;

    private Quaternion CameraOrientation = Quaternion.identity; // Orientation of camera around the player.
    private Spline CameraSpline = new Spline();
    private float CameraSplineSampleT;

    private float CurrCameraSplineLength;
    private float CurrCameraOrbitRadius;
    private bool TooCloseToPlayer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        MoveInputAction = InputSystem.actions.FindAction("Move");
        LookInputAction = InputSystem.actions.FindAction("Look");
        CurrCameraOrbitRadius = TargetOrbitRadius;
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;

        if (EnableFreeFly)
        {
            UpdateFreeCamera(dt);
            return;
        }
        if (!Target)
            return;

        Vector2 lookInput = LookInputAction.ReadValue<Vector2>() * InputAxisFactorsTPS;
        lookInput *= LookSpeedTPS * dt;
        LookInputAcc += lookInput;

        CameraSplineSampleT += MoveInterpolationSpeed * dt;
        transform.position = CameraSpline.EvaluatePosition(Mathf.Clamp01(CameraSplineSampleT));
        transform.rotation = Quaternion.LookRotation((Target.position - transform.position).normalized, Vector3.up);
    }

    void FixedUpdate()
    {
        if (!Target || EnableFreeFly)
        {
            LookInputAcc = Vector2.zero;
            return;
        }

        CurrCameraOrbit.Norm = (transform.position - Target.position).normalized;
        CurrCameraOrbit.Pos = Target.position + (CurrCameraOrbitRadius * CurrCameraOrbit.Norm);

        CameraOrientation = Quaternion.AngleAxis(LookInputAcc.x, Vector3.up) * Quaternion.AngleAxis(LookInputAcc.y, CameraOrientation * Vector3.right) * CameraOrientation;
        TargetCameraOrbit.Norm = CameraOrientation * -Vector3.forward;
        TargetCameraOrbit.Pos = Target.position + (TargetOrbitRadius * TargetCameraOrbit.Norm);

        Vector3 PosDelta = TargetCameraOrbit.Pos - CurrCameraOrbit.Pos;
        float TgntLength = PosDelta.magnitude * CameraOrbitTangentFactor;

        CurrCameraOrbit.Tgnt = Vector3.ProjectOnPlane(PosDelta, CurrCameraOrbit.Norm).normalized;
        TargetCameraOrbit.Tgnt = Vector3.ProjectOnPlane(PosDelta, TargetCameraOrbit.Norm).normalized;

        CameraSpline.Clear();
        CameraSpline.Add(CurrCameraOrbit.Pos);
        CameraSpline.Add(Target.position + Vector3.Slerp(CurrCameraOrbit.Norm, TargetCameraOrbit.Norm, 0.5f) * CurrCameraOrbitRadius);
        CameraSpline.Add(TargetCameraOrbit.Pos);

        for (int i = 0; i < 64; i++)
        {
            Vector3 startPos = CameraSpline.EvaluatePosition(i / 64.0f);
            Vector3 endPos = CameraSpline.EvaluatePosition((i + 1) / 64.0f);

            Debug.DrawLine(startPos, endPos, Color.red);
        }       
        Debug.DrawLine(TargetCameraOrbit.Pos, TargetCameraOrbit.Pos + (Vector3.up * 3.0f), Color.darkBlue);

        CameraSplineSampleT = 0.0f;
        //CurrCameraSplineLength = CurveUtility.ApproximateLength(CameraSpline);
        LookInputAcc = Vector2.zero;
    }

    void UpdateFreeCamera(float inDeltaTime)
    {
        Vector2 lookInput = LookInputAction.ReadValue<Vector2>() * InputAxisFactorsFPS;
        lookInput *= LookSpeedFPS * inDeltaTime;
        transform.rotation = Quaternion.AngleAxis(lookInput.x, Vector3.up) * Quaternion.AngleAxis(lookInput.y, transform.right) * transform.rotation;

        Vector2 moveInput = MoveInputAction.ReadValue<Vector2>();
        Vector3 forwardDir = transform.forward * moveInput.y + transform.right * moveInput.x;
        
        forwardDir.Normalize();
        transform.position += forwardDir * MoveSpeed * inDeltaTime;
    }

    static Vector3 CustomLerp(Vector3 inPosOne, Vector3 inPosTwo, float inDelta, float inMinDist = 0.001f)
    {
        Vector3 toTwo = inPosTwo - inPosOne;
        float toTwoDist = toTwo.magnitude;
        
        if (inDelta >= toTwoDist || toTwoDist <= inMinDist)
            return inPosTwo;

        toTwo.Normalize();
        return inPosOne + (toTwo * inDelta);
    }
}
