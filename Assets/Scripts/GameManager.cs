using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public bool PlayerHasFinished {get; set;} = false;
    public PlayerController Player;
    public TextMeshPro[] TimerText;
    public TextMeshPro CounterText;
    public float PlayerPosCheckDelta = 1.0f;

    private InputAction _resetPlayerPosInputAction;
    private InputAction _resetPlayerHealthInputAction;
    private int _numPosResets;
    private int _numHealthResets;
    private bool _hasPlayerStarted;
    private float _timePlayerStarted;
    private float _fakeTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _resetPlayerPosInputAction = InputSystem.actions.FindAction("ResetPos");
        _resetPlayerHealthInputAction = InputSystem.actions.FindAction("ResetHealth");
        
        AsyncInstantiateOperation.SetIntegrationTimeMS(1.0f);
    }

    Vector3 _lastValidPlayerPos;
    float _currPlayerPosCheckTime;
    bool _resetPlayerPos = false;
    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        if (!PlayerHasFinished && _hasPlayerStarted)
            _fakeTime = Time.time;

        if (_currPlayerPosCheckTime > PlayerPosCheckDelta)
        {
            _currPlayerPosCheckTime = 0.0f;
            bool isValidPos = Physics.Raycast(Player.transform.position, Vector3.down, 3.0f);

            if (isValidPos)
                _lastValidPlayerPos = Player.transform.position;
        }
        _currPlayerPosCheckTime += dt;

        if (_resetPlayerPosInputAction.WasPerformedThisFrame() && Player.GetPlayerHealth() > 0.0f)
        {
            _numPosResets++;   
            _resetPlayerPos = true;
        }
        if (_resetPlayerHealthInputAction.WasPerformedThisFrame() && Player.GetPlayerHealth() > 0.0f)
        {
            _numHealthResets++;
            Player.ResetPlayerHealth();   
        }
        
        TimeSpan time = TimeSpan.FromSeconds(_fakeTime - _timePlayerStarted);
        string timeStr = time.ToString(@"hh\:mm\:ss\:fff");
        foreach (TextMeshPro timerText in TimerText)
        {
            timerText.text = timeStr;
        }
        CounterText.text = $"Health Resets: {_numHealthResets}\nPosition Resets: {_numPosResets}";
    }

    void FixedUpdate()
    {
        if (_resetPlayerPos)
        {
            Rigidbody playerRigidbody = Player.GetPlayerRigidbody();
            if (Mathf.Abs(playerRigidbody.linearVelocity.y) >= 20.0f)
            {
                Vector3 currLinVel = playerRigidbody.linearVelocity; currLinVel.y = 0.0f;
                playerRigidbody.linearVelocity = currLinVel;
            }
            
            Player.transform.position = _lastValidPlayerPos;
            _resetPlayerPos = false;   
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() && !_hasPlayerStarted)
        {
            _timePlayerStarted = Time.time;
            _hasPlayerStarted = true;   
        }
    }
}
