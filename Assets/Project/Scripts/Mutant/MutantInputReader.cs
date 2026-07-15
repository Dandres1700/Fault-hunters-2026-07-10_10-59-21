using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class MutantInputReader : MonoBehaviour, IMutantIntentSource
{
    private const string PlayerMapName = "Player";
    private const string GamepadSchemeName = "Gamepad";

    private PlayerInput playerInput;
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction attackAction;
    private bool callbacksRegistered;
    private bool inputEnabled = true;
    private bool jumpRequested;
    private bool crouchRequested;
    private bool attackRequested;

    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool IsUsingGamepad => playerInput != null &&
                                  string.Equals(
                                      playerInput.currentControlScheme,
                                      GamepadSchemeName,
                                      StringComparison.Ordinal
                                  );

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        CacheActions();
    }

    private void OnEnable()
    {
        RegisterCallbacks();
        ApplyInputState();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
        playerMap?.Disable();
        ClearInput();
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;
        ApplyInputState();
        if (!value)
        {
            ClearInput();
        }
    }

    public bool ConsumeJump() => Consume(ref jumpRequested);
    public bool ConsumeCrouch() => Consume(ref crouchRequested);
    public bool ConsumeAttack() => Consume(ref attackRequested);

    private void CacheActions()
    {
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogError(
                "MutantInputReader necesita Player2Controller.inputactions en PlayerInput.",
                this
            );
            return;
        }

        playerMap = playerInput.actions.FindActionMap(PlayerMapName, false);
        if (playerMap == null)
        {
            Debug.LogError("Player2Controller no contiene el Action Map 'Player'.", this);
            return;
        }

        moveAction = FindRequiredAction("Move");
        lookAction = FindRequiredAction("Look");
        jumpAction = FindRequiredAction("Jump");
        sprintAction = FindRequiredAction("Sprint");
        crouchAction = FindRequiredAction("Crouch");
        attackAction = FindRequiredAction("Attack");

        if (moveAction == null || lookAction == null || jumpAction == null ||
            sprintAction == null || crouchAction == null || attackAction == null)
        {
            playerMap = null;
        }
    }

    private InputAction FindRequiredAction(string actionName)
    {
        InputAction action = playerMap.FindAction(actionName, false);
        if (action == null)
        {
            Debug.LogError(
                $"Falta la accion Player/{actionName} en Player2Controller.inputactions.",
                this
            );
        }

        return action;
    }

    private void RegisterCallbacks()
    {
        if (callbacksRegistered || playerMap == null)
        {
            return;
        }

        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        lookAction.performed += OnLook;
        lookAction.canceled += OnLook;
        jumpAction.performed += OnJump;
        sprintAction.performed += OnSprint;
        sprintAction.canceled += OnSprint;
        crouchAction.performed += OnCrouch;
        attackAction.performed += OnAttack;
        callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (!callbacksRegistered)
        {
            return;
        }

        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;
        lookAction.performed -= OnLook;
        lookAction.canceled -= OnLook;
        jumpAction.performed -= OnJump;
        sprintAction.performed -= OnSprint;
        sprintAction.canceled -= OnSprint;
        crouchAction.performed -= OnCrouch;
        attackAction.performed -= OnAttack;
        callbacksRegistered = false;
    }

    private void ApplyInputState()
    {
        if (!isActiveAndEnabled || playerMap == null)
        {
            return;
        }

        if (inputEnabled)
        {
            playerMap.Enable();
        }
        else
        {
            playerMap.Disable();
        }
    }

    private void ClearInput()
    {
        Move = Vector2.zero;
        Look = Vector2.zero;
        SprintHeld = false;
        jumpRequested = false;
        crouchRequested = false;
        attackRequested = false;
    }

    private static bool Consume(ref bool request)
    {
        bool value = request;
        request = false;
        return value;
    }

    private void OnMove(InputAction.CallbackContext context) =>
        Move = context.ReadValue<Vector2>();

    private void OnLook(InputAction.CallbackContext context) =>
        Look = context.ReadValue<Vector2>();

    private void OnJump(InputAction.CallbackContext context) => jumpRequested = true;

    private void OnSprint(InputAction.CallbackContext context) =>
        SprintHeld = context.ReadValueAsButton();

    private void OnCrouch(InputAction.CallbackContext context) => crouchRequested = true;

    private void OnAttack(InputAction.CallbackContext context) => attackRequested = true;
}
