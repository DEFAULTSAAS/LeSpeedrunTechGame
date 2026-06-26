using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerInputActionTypes
{
    None,
    Move,
    Jump,
    Crouch,
    Strafing,
    Swing,
    Fire
}

public enum PlayerActions
{
    None,
    Jump,
    LongJump,
    Backflip,
    LeftSide,
    RightSide,
    ForwardLeftSide,
    ForwardRightSide,
    Charge,
    Fire,
    Swing
}

public struct InputActionState
{
    public PlayerInputActionTypes ActionType;
    public float ActionTime;
    public Vector2 AxisValue;
    public bool HasChanged;
    public bool IsPersistent;
}

public class PlayerInputSystem : MonoBehaviour
{
    public static PlayerInputSystem MainPISInstance;

    // The amount of time in seconds, before a input action state is removed and no longer processed for player actions.
    public float InputActionStateRemovalDelay = 0.0167f; 
    public float InputActionStateChangeNotedTime = 0.5f;
    public string MoveInputActionName = "Move";
    public string JumpInputActionName = "Jump";
    public string CrouchInputActionName = "Crouch";
    public string StrafingInputActionName = "Sprint";

    public PlayerInputActionTypes[] PersistentInputActions = new PlayerInputActionTypes[]{PlayerInputActionTypes.Move};

    private Dictionary<string, PlayerInputActionTypes> _actionNameToActionType = new();
    private Dictionary<PlayerInputActionTypes, float> _currActionsTriggerTime = new();
    private Dictionary<PlayerInputActionTypes, float> _prevActionsTriggerTime = new();
    private List<Tuple<PlayerInputActionTypes[], Func<List<InputActionState>, PlayerActions>>> _inputActionsToPlayerAction = new(); 
    private List<PlayerInputActionTypes> _playerInputActionTypes = new();
    private List<InputActionState> _inputActionStates = new(); 
    private List<PlayerActions> _playerActionBuffer = new();
    
    void Awake()
    {
        if (MainPISInstance == null)
            MainPISInstance = this;
        else if (MainPISInstance != this)
            Destroy(gameObject);

        _actionNameToActionType[MoveInputActionName] = PlayerInputActionTypes.Move;
        _actionNameToActionType[JumpInputActionName] = PlayerInputActionTypes.Jump;
        _actionNameToActionType[CrouchInputActionName] = PlayerInputActionTypes.Crouch;
        _actionNameToActionType[StrafingInputActionName] = PlayerInputActionTypes.Strafing;

        foreach (PlayerInputActionTypes playerInputActionType in Enum.GetValues(typeof(PlayerInputActionTypes)))
        {
            _currActionsTriggerTime[playerInputActionType] = 0.0f;
            _prevActionsTriggerTime[playerInputActionType] = 0.0f;   
        }

        _inputActionsToPlayerAction.Add(new(new[]{PlayerInputActionTypes.Jump}, (_) => {return PlayerActions.Jump;}));
        _inputActionsToPlayerAction.Add(new(new[]{PlayerInputActionTypes.Swing}, (_) => {return PlayerActions.Swing;}));
        _inputActionsToPlayerAction.Add(new(new[]{PlayerInputActionTypes.Fire}, (_) => {return PlayerActions.Fire;}));
        
        _inputActionsToPlayerAction.Add(new(new[]{PlayerInputActionTypes.Crouch}, 
                                                  (states) => {
            if ((_currActionsTriggerTime[PlayerInputActionTypes.Crouch] - 
                 _prevActionsTriggerTime[PlayerInputActionTypes.Crouch]) < 0.2f)
                return PlayerActions.Charge;
            return PlayerActions.None;                                              
        }));

        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move, 
                                                   PlayerInputActionTypes.Jump, 
                                                   PlayerInputActionTypes.Crouch}, 
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x != 0.0f || state.AxisValue.y != 0.0f)
                return PlayerActions.LongJump;
            return PlayerActions.None;                                           
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move, 
                                                   PlayerInputActionTypes.Jump, 
                                                   PlayerInputActionTypes.Strafing}, 
                                                   (states) => {
            if (_inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)].AxisValue.y < 0.0f)
                return PlayerActions.Backflip;
            return PlayerActions.None;                                           
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Crouch},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x < 0.0f && state.AxisValue.y == 0.0f && 
               (Time.time - _currActionsTriggerTime[PlayerInputActionTypes.Move]) < 0.2f)
                return PlayerActions.LeftSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Strafing},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x < 0.0f && state.AxisValue.y == 0.0f)
                return PlayerActions.LeftSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Crouch},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x > 0.0f && state.AxisValue.y == 0.0f && 
               (Time.time - _currActionsTriggerTime[PlayerInputActionTypes.Move]) < 0.2f)
                return PlayerActions.RightSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Strafing}, 
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x > 0.0f && state.AxisValue.y == 0.0f)
                return PlayerActions.RightSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Crouch},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x < 0.0f && state.AxisValue.y > 0.0f && state.HasChanged)
                return PlayerActions.ForwardLeftSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Strafing}, 
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x < 0.0f && state.AxisValue.y > 0.0f)
                return PlayerActions.ForwardLeftSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Crouch}, 
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x > 0.0f && state.AxisValue.y > 0.0f && state.HasChanged)
                return PlayerActions.ForwardRightSide;
            return PlayerActions.None;                                       
        }));
        _inputActionsToPlayerAction.Add(new(new []{PlayerInputActionTypes.Move,
                                                   PlayerInputActionTypes.Jump,
                                                   PlayerInputActionTypes.Strafing},
                                                   (states) => {
            InputActionState state = _inputActionStates[_playerInputActionTypes.BinarySearch(PlayerInputActionTypes.Move)];
            if (state.AxisValue.x > 0.0f && state.AxisValue.y > 0.0f)
                return PlayerActions.ForwardRightSide;
            return PlayerActions.None;                                       
        }));
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public bool GetIfInputPresent(PlayerInputActionTypes inInput)
    {
        int index = _playerInputActionTypes.BinarySearch(inInput);
        return index >= 0;
    }

    public ref readonly List<PlayerInputActionTypes> GetPlayerInputActionTypes()
    {
        return ref _playerInputActionTypes;
    }

    public ref readonly List<InputActionState> GetInputActionStates()
    {
        return ref _inputActionStates;
    }

    public ref readonly List<PlayerActions> GetPlayerActions()
    {
        return ref _playerActionBuffer;
    }

    public PlayerActions GetPlayerAction(int inIndex)
    {
        if (inIndex >= _playerActionBuffer.Count)
            return PlayerActions.None;

        return _playerActionBuffer[inIndex];
    }

    public void ProcessInputActionStates()
    {
        _playerActionBuffer.Clear();
        _playerInputActionTypes.Clear();
        _inputActionStates.Sort((a, b) => {return a.ActionType.CompareTo(b.ActionType);});

        foreach (InputActionState state in _inputActionStates)
            _playerInputActionTypes.Add(state.ActionType);

        foreach (var dualAction in _inputActionsToPlayerAction)
        {
            if (_playerInputActionTypes.Count < dualAction.Item1.Length)
                continue;

            int counter = 0;
            for (int i = 0; i < dualAction.Item1.Length; i++)
            {
                if (GetIfInputPresent(dualAction.Item1[i]))
                    counter++;
            }

            PlayerActions playerAction = dualAction.Item2(_inputActionStates);
            if (counter == dualAction.Item1.Length && playerAction != PlayerActions.None)
                _playerActionBuffer.Add(playerAction);
        }
        _playerActionBuffer.Distinct();
        _playerActionBuffer.Sort((a, b) => { return b.CompareTo(a); });

        for (int i = 0; i < _inputActionStates.Count; i++)
        {
            InputActionState inputActionState = _inputActionStates[i];
            if ((Time.time - inputActionState.ActionTime) > InputActionStateChangeNotedTime)
            {
                inputActionState.HasChanged = false;
                _inputActionStates[i] = inputActionState;   
            }
        }

        _inputActionStates.RemoveAll(inputActionState => (Time.time - inputActionState.ActionTime) > 
                                     InputActionStateRemovalDelay && !inputActionState.IsPersistent);

        // if (_playerActionBuffer.Count > 0)
        // {
        //     Debug.Log(_playerActionBuffer[0]);
        // }
        // if (_playerInputActionTypes.Count > 0)
        //     Debug.Log(string.Join(',', _playerInputActionTypes));   
    }

    public void HandleInputCallback(InputAction.CallbackContext context)
    {
        PlayerInputActionTypes actionType = PlayerInputActionTypes.None;
        if (_actionNameToActionType.ContainsKey(context.action.name))
            actionType = _actionNameToActionType[context.action.name];
        else
            return;

        switch(context.phase)
        {
            case InputActionPhase.Performed:
            {
                bool actionStateFound = false;
                for (int i = 0; i < _inputActionStates.Count; i++)
                {
                    if (_inputActionStates[i].ActionType != actionType)
                        continue;
                    actionStateFound = true;
                    
                    _prevActionsTriggerTime[actionType] = _currActionsTriggerTime[actionType];
                    _currActionsTriggerTime[actionType] = Time.time;

                    InputActionState inputActionState = _inputActionStates[i];
                    inputActionState.ActionTime = Time.time;
                    inputActionState.AxisValue = (context.action.expectedControlType == "Vector2") ? 
                                                  context.action.ReadValue<Vector2>() : Vector2.one * float.NegativeInfinity;
                    inputActionState.HasChanged = GetIsPersistent(actionType);

                    _inputActionStates[i] = inputActionState;
                }

                if (!actionStateFound)
                {
                    _prevActionsTriggerTime[actionType] = _currActionsTriggerTime[actionType];
                    _currActionsTriggerTime[actionType] = Time.time;

                    InputActionState inputActionState;
                    inputActionState.ActionType = actionType;
                    inputActionState.ActionTime = Time.time;
                    inputActionState.AxisValue = (context.action.expectedControlType == "Vector2") ? 
                                                  context.action.ReadValue<Vector2>() : Vector2.one * float.NegativeInfinity;
                    inputActionState.HasChanged = false;
                    inputActionState.IsPersistent = GetIsPersistent(actionType);

                    _inputActionStates.Add(inputActionState);
                }
            } break;
            case InputActionPhase.Canceled:
            {
                for (int i = 0; i < _inputActionStates.Count; i++)
                {
                    if (_inputActionStates[i].ActionType == actionType)
                    {
                        // We don't want to remove the action straight away,
                        // as we want it to be 'buffered' until after its action time has expired.
                        InputActionState inputActionState = _inputActionStates[i];
                        inputActionState.IsPersistent = false;
                        _inputActionStates[i] = inputActionState;
                        
                        break;   
                    }    
                }
            } break;
        }
    }

    private bool GetIsPersistent(PlayerInputActionTypes inActionType)
    {
        return PersistentInputActions.Contains(inActionType);
    }
}
