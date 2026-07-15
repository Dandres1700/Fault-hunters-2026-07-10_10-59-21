using System;
using UnityEngine;

[Serializable]
public sealed class MissionStory
{
    public int index;
    public string country;
    public string operation;
    public string bossName;
    public string threatLevel;
    public string objective;
    public string briefing;
    public string recoveredFragment;
    public string conclusion;

    public string DisplayTitle => $"{country.ToUpperInvariant()} // {operation}";
}

/// <summary>
/// Biblia narrativa jugable. Cada entrada alimenta el mapa, el briefing
/// y la pantalla de victoria sin duplicar textos entre escenas.
/// </summary>
public static class GameStoryDatabase
{
    private static readonly MissionStory[] missions =
    {
        new MissionStory()
        {
            index = 0,
            country = "Egipto",
            operation = "PROTOCOLO KHEPRI",
            bossName = "ANUBIS.EXE",
            threatLevel = "AMENAZA II",
            objective = "Localizar la Falla bajo la meseta y estabilizar el nodo solar.",
            briefing = "Las redes eléctricas de El Cairo repiten una secuencia de 4.600 años. Una entidad con forma de guardián funerario está convirtiendo monumentos, drones y señales urbanas en bloques de código negro. El patrón no es una infección: parece una puerta.",
            recoveredFragment = "FRAGMENTO 01 // «EL NÚCLEO NO ESTÁ EN UN LUGAR. ESTÁ DEBAJO DE TODAS LAS REGLAS.»",
            conclusion = "ANUBIS.EXE fue desactivado. Antes de colapsar, transmitió coordenadas hacia un nodo del Pacífico. La firma tiene caracteres japoneses que todavía no existían cuando el fragmento fue escrito."
        },
        new MissionStory()
        {
            index = 1,
            country = "Japón",
            operation = "SOMBRA 404",
            bossName = "KAGE-404",
            threatLevel = "AMENAZA III",
            objective = "Romper el bucle de sincronización que mantiene a Tokio congelada en microsegundos.",
            briefing = "Miles de dispositivos marcan la misma hora imposible. La Falla se manifiesta como un oni de neón que salta entre pantallas, semáforos y trenes. Cada ataque predice el movimiento del Cazador antes de que ocurra.",
            recoveredFragment = "FRAGMENTO 02 // «PARA REESCRIBIR EL MUNDO, PRIMERO HAY QUE ENSEÑARLE A REPETIRSE.»",
            conclusion = "El bucle terminó, pero el último eco de KAGE-404 mostró una cordillera atravesada por una red de luz. El siguiente pulso nace en la mitad del mundo."
        },
        new MissionStory()
        {
            index = 2,
            country = "Ecuador",
            operation = "NODO MITAD DEL MUNDO",
            bossName = "CÓNDOR.NULL",
            threatLevel = "AMENAZA III",
            objective = "Proteger el nodo ecuatorial y cerrar la tormenta de datos sobre los Andes.",
            briefing = "Una Falla alada absorbe comunicaciones desde Quito hasta la Amazonía. Sus plumas son fragmentos de mapas satelitales y su sombra altera la gravedad digital del sistema. La Agencia Nexo cree que el ecuador funciona como una línea de depuración del planeta.",
            recoveredFragment = "FRAGMENTO 03 // «LAS FRONTERAS SON CAPAS. LOS NODOS ANTIGUOS FUERON CONSTRUIDOS PARA ATRAVESARLAS.»",
            conclusion = "CÓNDOR.NULL liberó un mapa de nodos anteriores a la red moderna. Uno de ellos despierta bajo una ciudad levantada sobre otra ciudad: México."
        },
        new MissionStory()
        {
            index = 3,
            country = "México",
            operation = "SERPIENTE DE ESPEJOS",
            bossName = "QUETZAL.GLITCH",
            threatLevel = "AMENAZA IV",
            objective = "Separar la memoria histórica de la corrupción que la está usando como arma.",
            briefing = "La Falla adopta la forma de una serpiente emplumada compuesta por obsidiana, anuncios rotos y recuerdos digitalizados. Cada fase del combate cambia la arquitectura a su alrededor, como si varias épocas intentaran ocupar el mismo espacio.",
            recoveredFragment = "FRAGMENTO 04 // «NO CREAMOS LAS FALLAS. LAS FALLAS SON LOS ANTICUERPOS DEL SISTEMA.»",
            conclusion = "La Agencia Nexo ocultó información. Las criaturas no intentan destruir el mundo: responden a una alteración más profunda. Alguien está obligando al sistema a defenderse."
        },
        new MissionStory()
        {
            index = 4,
            country = "Francia",
            operation = "MERIDIANO ROTO",
            bossName = "GÁRGOLA PRIME",
            threatLevel = "AMENAZA V",
            objective = "Recuperar el reloj maestro antes de que la corrupción borre la secuencia temporal global.",
            briefing = "París pierde segundos completos. La Falla vive entre piedra y datos, vigilando un reloj que no existe en ningún plano de la ciudad. Sus pulsos coinciden con transmisiones secretas de la propia Agencia Nexo.",
            recoveredFragment = "FRAGMENTO 05 // «AUTOR DE LA REESCRITURA: DIRECTOR NEXO. ACCESO CONCEDIDO AL NÚCLEO CERO.»",
            conclusion = "La verdad queda expuesta: el director de los Cazadores inició la reescritura para crear un mundo sin azar. El último destino no pertenece a ningún país. Está en la capa que sostiene a todos."
        },
        new MissionStory()
        {
            index = 5,
            country = "Núcleo Cero",
            operation = "REGLA FINAL",
            bossName = "EL ARQUITECTO",
            threatLevel = "COLAPSO GLOBAL",
            objective = "Entrar al origen del sistema y decidir qué reglas merece conservar el mundo.",
            briefing = "Todos los fragmentos forman una clave. El Cazador atraviesa la red invisible y llega a un espacio donde ciudades, climas y memorias existen como procesos editables. El Arquitecto espera con una propuesta: eliminar el caos, aunque también desaparezca la libertad.",
            recoveredFragment = "ARCHIVO COMPLETO // «UN MUNDO PERFECTO NO NECESITA CAZADORES. UN MUNDO VIVO, SÍ.»",
            conclusion = "El núcleo vuelve a aceptar la incertidumbre. Las Fallas dejan de crecer, pero no desaparecen: ahora son señales de que el mundo sigue vivo. El Cazador novato se convierte en guardián de una verdad que ningún sistema puede controlar por completo."
        }
    };

    public static int Count => missions.Length;

    public static MissionStory Get(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, missions.Length - 1);
        return missions[safeIndex];
    }
}
