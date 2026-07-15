using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class MutantControlMode : MonoBehaviour
{
    [Tooltip("Si esta activo, Mutant toma Player2Controller y su camara al iniciar.")]
    [SerializeField] private bool controlHumanoActivo = true;
    [Tooltip("Evita que otro PlayerInput responda al mismo teclado o mando.")]
    [SerializeField] private bool desactivarOtrosPlayerInput = true;
    [Tooltip("Garantiza que solamente la camara del personaje seleccionado renderice y escuche audio.")]
    [SerializeField] private bool desactivarOtrasCamaras = true;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private MutantInputReader inputReader;
    [SerializeField] private MutantCameraController cameraController;
    [SerializeField] private Camera mutantCamera;
    [SerializeField] private AudioListener audioListener;

    private readonly List<PlayerInput> inputsDesactivados = new List<PlayerInput>();
    private readonly List<Behaviour> lectoresDesactivados = new List<Behaviour>();
    private readonly List<Camera> camarasDesactivadas = new List<Camera>();
    private readonly List<AudioListener> listenersDesactivados = new List<AudioListener>();

    public bool ControlHumanoActivo => controlHumanoActivo;

    private void Awake()
    {
        playerInput ??= GetComponent<PlayerInput>();
        inputReader ??= GetComponent<MutantInputReader>();
        cameraController ??= GetComponentInChildren<MutantCameraController>(true);
        mutantCamera ??= GetComponentInChildren<Camera>(true);
        audioListener ??= GetComponentInChildren<AudioListener>(true);
        ApplyMode();
    }

    public void SetControlHumano(bool value)
    {
        if (controlHumanoActivo == value)
        {
            ApplyMode();
            return;
        }

        controlHumanoActivo = value;
        ApplyMode();
    }

    private void ApplyMode()
    {
        if (controlHumanoActivo && desactivarOtrosPlayerInput)
        {
            DisableOtherPlayerInputs();
        }
        else if (!controlHumanoActivo)
        {
            RestoreOtherPlayerInputs();
        }

        if (controlHumanoActivo && desactivarOtrasCamaras)
        {
            DisableOtherCameras();
        }
        else if (!controlHumanoActivo)
        {
            RestoreOtherCameras();
        }

        if (playerInput != null)
        {
            playerInput.enabled = controlHumanoActivo;
        }

        inputReader?.SetInputEnabled(controlHumanoActivo);
        if (cameraController != null)
        {
            cameraController.enabled = controlHumanoActivo;
        }

        if (mutantCamera != null)
        {
            mutantCamera.enabled = controlHumanoActivo;
        }

        if (audioListener != null)
        {
            audioListener.enabled = controlHumanoActivo;
        }
    }

    private void DisableOtherPlayerInputs()
    {
        RestoreOtherPlayerInputs();
        PlayerInput[] allInputs = FindObjectsByType<PlayerInput>(
            FindObjectsInactive.Exclude
        );
        foreach (PlayerInput candidate in allInputs)
        {
            if (candidate == playerInput || !candidate.enabled)
            {
                continue;
            }

            candidate.enabled = false;
            inputsDesactivados.Add(candidate);

            foreach (MonoBehaviour behaviour in candidate.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.enabled &&
                    behaviour.GetType().Name.EndsWith("InputReader"))
                {
                    behaviour.enabled = false;
                    lectoresDesactivados.Add(behaviour);
                }
            }
        }
    }

    private void RestoreOtherPlayerInputs()
    {
        foreach (PlayerInput candidate in inputsDesactivados)
        {
            if (candidate != null)
            {
                candidate.enabled = true;
            }
        }

        inputsDesactivados.Clear();

        foreach (Behaviour reader in lectoresDesactivados)
        {
            if (reader != null)
            {
                reader.enabled = true;
            }
        }

        lectoresDesactivados.Clear();
    }

    private void DisableOtherCameras()
    {
        RestoreOtherCameras();
        foreach (Camera candidate in FindObjectsByType<Camera>(
                     FindObjectsInactive.Exclude))
        {
            if (candidate != mutantCamera && candidate.enabled)
            {
                candidate.enabled = false;
                camarasDesactivadas.Add(candidate);
            }
        }

        foreach (AudioListener candidate in FindObjectsByType<AudioListener>(
                     FindObjectsInactive.Exclude))
        {
            if (candidate != audioListener && candidate.enabled)
            {
                candidate.enabled = false;
                listenersDesactivados.Add(candidate);
            }
        }
    }

    private void RestoreOtherCameras()
    {
        foreach (Camera candidate in camarasDesactivadas)
        {
            if (candidate != null)
            {
                candidate.enabled = true;
            }
        }

        camarasDesactivadas.Clear();
        foreach (AudioListener candidate in listenersDesactivados)
        {
            if (candidate != null)
            {
                candidate.enabled = true;
            }
        }

        listenersDesactivados.Clear();
    }

    private void OnDisable()
    {
        RestoreOtherPlayerInputs();
        RestoreOtherCameras();
    }
}
