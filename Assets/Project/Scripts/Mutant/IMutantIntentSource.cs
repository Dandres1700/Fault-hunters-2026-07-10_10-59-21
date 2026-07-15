using UnityEngine;

/// <summary>
/// Fuente desacoplada de intenciones. Una futura CPU puede implementar esta
/// interfaz sin cambiar el motor, el combate ni el Animator del Mutant.
/// </summary>
public interface IMutantIntentSource
{
    Vector2 Move { get; }
    Vector2 Look { get; }
    bool SprintHeld { get; }
    bool IsUsingGamepad { get; }

    bool ConsumeJump();
    bool ConsumeCrouch();
    bool ConsumeAttack();
}
