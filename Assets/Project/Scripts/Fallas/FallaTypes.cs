using System;

public enum FallaType
{
    Rastrera,
    Explosiva,
    Generadora,
    Adherida
}

public enum FallaState
{
    Inactiva,
    Alerta,
    Persiguiendo,
    Atacando,
    Herida,
    Muerta
}

public enum FallaCoreVisibility
{
    SiempreVisible,
    DuranteAtaque,
    AlRecibirDano,
    TrasDeteccion,
    ReveladoExterno
}

public interface IFallaAttack
{
    bool IsRunning { get; }
    void BeginAttack(FallaCore owner, UnityEngine.Transform target);
    void CancelAttack();
}

