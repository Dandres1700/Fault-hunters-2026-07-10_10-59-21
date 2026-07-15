# Cazadores de Fallas — Guía de historia y audio

## Qué se encontró al revisar el proyecto

- El proyecto usa **Unity 6.0.5 (6000.5.3f1)**.
- Las escenas principales ya estaban agregadas al Build Settings: `MenuPrincipal`, `Prologo`, `MapaMundial`, `SampleScene` y `Opciones`.
- `SampleScene` ya contiene una instancia del prefab `Cazador` y una del prefab `Mutant`.
- La escena `Prologo` solo tenía cámara y luz, por lo que no existía una presentación narrativa jugable.
- El proyecto únicamente tenía dos audios: música de menú y clic de botón.
- La arquitectura de jugador, mutante, daño y combate está separada correctamente, pero varios sistemas generales siguen vacíos (`GameManager`, `SaveSystem`, `HUDManager`, `FallaBoss`, `FallaBossPhase`, etc.).

## Qué se agregó

### 1. Sistema central de audio

Archivo principal:

`Assets/Project/Scripts/Audio/GameAudioManager.cs`

Funciones:

- Persiste entre escenas.
- Cambia música y ambiente automáticamente según la escena.
- Usa un pequeño pool de `AudioSource` 3D para evitar crear y destruir objetos por cada sonido.
- Reproduce variaciones aleatorias de pasos, ataques y daño.
- Mantiene los sonidos de interfaz en 2D.
- Respeta el volumen general que ya controla `AudioListener.volume` desde la escena de opciones.

### 2. Audio automático para el Cazador

`CazadorAudioController.cs`

Incluye:

- Pasos al caminar.
- Pasos más rápidos al correr.
- Salto.
- Aterrizaje.
- Balanceo del arma al iniciar cada ataque del combo.
- Sonido de impacto al recibir daño.
- Voz/efecto de dolor.
- Muerte.

El componente se agrega automáticamente al objeto que tenga `CazadorController`, por lo que no hay que modificar el prefab manualmente.

### 3. Audio automático para la Falla/Mutante

`MutantAudioController.cs`

Incluye:

- Rugido al aparecer.
- Pasos pesados.
- Ataques.
- Impacto y daño.
- Muerte.
- Efecto de Falla derrotada.

El componente se agrega automáticamente a objetos que tengan `MutantStats`.

### 4. Sonidos de interfaz

`UISoundFeedback.cs` y `AutoAudioInstaller.cs`

Todos los controles `Selectable` reciben automáticamente sonido de hover y clic. Los botones que ya tengan `SonidoClickBoton` se omiten para evitar reproducir el clic dos veces.

### 5. Prólogo jugable

`PrologoController.cs`

La escena vacía de prólogo ahora genera en tiempo de ejecución:

- Presentación de la red invisible.
- Explicación de las Fallas.
- Introducción de la Agencia Nexo y los Cazadores.
- Primera misión del jugador.
- Revelación del patrón oculto.
- Destino inicial: Egipto, Operación Protocolo Khepri.
- Texto progresivo, transiciones, efectos de glitch, música y controles por teclado o botón.

Controles:

- `Espacio`, `Enter` o `Flecha derecha`: avanzar.
- `Escape`: saltar el prólogo.

### 6. Campaña y progresión

`GameStoryDatabase.cs` contiene seis capítulos:

1. Egipto — Protocolo Khepri — `ANUBIS.EXE`
2. Japón — Sombra 404 — `KAGE-404`
3. Ecuador — Nodo Mitad del Mundo — `CÓNDOR.NULL`
4. México — Serpiente de Espejos — `QUETZAL.GLITCH`
5. Francia — Meridiano Roto — `GÁRGOLA PRIME`
6. Núcleo Cero — Regla Final — `EL ARQUITECTO`

Cada misión tiene:

- País y operación.
- Nombre del jefe.
- Nivel de amenaza.
- Objetivo.
- Briefing.
- Fragmento recuperado.
- Consecuencia narrativa.

`GameProgress.cs` guarda en `PlayerPrefs`:

- Misión actual.
- Misión más alta desbloqueada.
- Fragmentos obtenidos.
- Si el prólogo ya se completó.

Al pulsar **Nueva partida**, el progreso de esta campaña se reinicia.

### 7. Mapa mundial narrativo

`WorldMapStoryController.cs`

La escena `MapaMundial` recibe una tarjeta de misión con:

- Nodo seleccionado.
- Estado de la Falla.
- Objetivo hostil.
- Objetivo de misión.
- Navegación entre nodos desbloqueados.
- Botón para iniciar o repetir la misión.

Controles:

- `←` y `→`: cambiar nodo.
- `Enter`: iniciar misión.

### 8. Briefing y resultado de misión

`MissionFlowController.cs`

Al entrar a `SampleScene`:

- El juego se pausa.
- Aparece el informe de campo.
- Se muestran jefe, objetivo y contexto.
- Al iniciar, se reactiva el combate y suena el efecto de misión.
- Al morir el `Mutant`, aparece el fragmento recuperado.
- Se guarda el progreso y se desbloquea el siguiente capítulo.

## Audios provisionales incluidos

Se generaron 34 clips originales de prueba dentro de:

`Assets/Project/Resources/Audio/`

Carpetas:

- `Music`
- `Ambience`
- `SFX/UI`
- `SFX/Player`
- `SFX/Player/Footsteps`
- `SFX/Boss`
- `SFX/Boss/Footsteps`
- `SFX/World`

Son audios sintéticos provisionales creados para que el juego funcione y tenga respuesta sonora desde la primera prueba. Para producción conviene reemplazarlos por grabaciones o librerías profesionales.

## Cómo reemplazar un audio sin tocar código

La forma más sencilla es mantener el mismo nombre y ruta. Ejemplo:

1. Elimina o reemplaza:
   `Assets/Project/Resources/Audio/SFX/Player/jump.wav`
2. Coloca el nuevo archivo con el mismo nombre:
   `jump.wav`
3. Vuelve a Unity y espera a que termine la importación.
4. Ejecuta el juego.

El sistema lo cargará automáticamente.

## Prueba recomendada

1. Abre el proyecto en Unity 6.0.5.
2. Espera a que importe los nuevos scripts y WAV.
3. Revisa la consola. No debe haber errores rojos.
4. Ejecuta desde `MenuPrincipal`.
5. Pulsa `Nueva partida`.
6. Recorre el prólogo.
7. Selecciona Egipto en el mapa.
8. Inicia la misión.
9. Comprueba pasos, salto, aterrizaje, ataques, impactos, daño y muerte.
10. Derrota al Mutant y verifica el fragmento recuperado.

También puedes ejecutar:

`Tools > Cazadores de Fallas > Validar historia y audio`

La herramienta revisa escenas y recursos principales.

## Recomendaciones para la siguiente etapa

1. Crear un `AudioMixer` con buses separados: Master, Music, Ambience, SFX, UI y Voices.
2. Cambiar el slider general por sliders independientes para música y efectos.
3. Añadir tipos de superficie para pasos: piedra, arena, metal, madera, agua y terreno corrupto.
4. Reemplazar la detección temporal de pasos por Animation Events cuando las animaciones finales estén cerradas.
5. Añadir voces o radio de la Agencia Nexo a los briefings.
6. Crear un modelo, arena y patrón de ataque distinto para cada Falla.
7. Implementar los scripts vacíos de `FallaBoss` y `FallaBossPhase` como sistema real de fases.
8. Crear una escena de Archivos de Falla que lea los fragmentos guardados por `GameProgress`.

## Nota de diseño

El sistema se instaló mediante controladores automáticos para evitar editar manualmente cada escena y prefab. Así, la integración actual es rápida y segura. Cuando el proyecto avance a producción, conviene convertir las configuraciones de audio y misión en `ScriptableObject` para que diseñadores puedan modificar datos desde el Inspector sin editar código.
