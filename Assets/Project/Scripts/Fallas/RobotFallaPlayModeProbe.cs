using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RobotFallaPlayModeProbe : MonoBehaviour
{
    [Serializable]
    private sealed class ValidationReport
    {
        public string timestamp;
        public bool completed;
        public bool playerDamageIndependent;
        public bool mutantDamageIndependent;
        public bool playerDeathIndependent;
        public bool mutantDeathIndependent;
        public bool robotDamageIndependent;
        public bool robotDeathIndependent;
        public bool robotTargetsOnlyPlayer;
        public bool mutantMovedTowardPlayer;
        public bool mutantAttackStateObserved;
        public bool mutantAttackedPlayer;
        public float minimumMutantDistance;
        public bool crawlerAttackedPlayer;
        public bool explosiveAttackedPlayer;
        public bool generatorCreatedRobots;
        public bool noPurpleBlobHierarchy;
        public bool noMissingRuntimeReferences;
        public string[] notes;
    }

    [SerializeField, Min(2f)] private float observationSeconds = 12f;

    private readonly List<string> notes = new List<string>();
    private bool mutantHitObserved;
    private bool crawlerHitObserved;
    private bool explosiveHitObserved;

    private IEnumerator Start()
    {
        ValidationReport report = new ValidationReport
        {
            timestamp = DateTime.Now.ToString("O")
        };

        yield return null;
        CazadorStats player = FindAnyObjectByType<CazadorStats>();
        MutantStats mutant = FindAnyObjectByType<MutantStats>();
        MutantEnemyIntentSource ai = FindAnyObjectByType<MutantEnemyIntentSource>();
        FallaCore[] robots = FindObjectsByType<FallaCore>();
        FallaGenerator generator = FindAnyObjectByType<FallaGenerator>();

        report.noMissingRuntimeReferences = player != null && mutant != null && ai != null &&
                                            robots.Length >= 3 && generator != null;
        if (!report.noMissingRuntimeReferences)
        {
            notes.Add(
                $"Referencias: Cazador={player != null}, Mutant={mutant != null}, AI={ai != null}, Robots={robots.Length}, Generador={generator != null}");
            Finish(report);
            yield break;
        }

        player.OnImpactoRecibido += OnPlayerImpact;
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        yield return null;

        float initialMutantHealth = mutant.VidaActual;
        player.RecibirImpacto(new DamageInfo(7f, player.transform.position,
            Vector3.forward, gameObject));
        report.playerDamageIndependent = Approximately(mutant.VidaActual, initialMutantHealth);
        player.ReiniciarStats();

        float initialPlayerHealth = player.VidaActual;
        mutant.RecibirImpacto(new DamageInfo(7f, mutant.transform.position,
            Vector3.back, gameObject));
        report.mutantDamageIndependent = Approximately(player.VidaActual, initialPlayerHealth);
        mutant.Reiniciar();

        FallaCore testRobot = robots[0];
        Dictionary<FallaCore, float> otherRobotHealth = robots
            .Where(robot => robot != testRobot)
            .ToDictionary(robot => robot, robot => robot.VidaActual);
        initialPlayerHealth = player.VidaActual;
        initialMutantHealth = mutant.VidaActual;
        testRobot.RecibirDano(1f);
        report.robotDamageIndependent =
            otherRobotHealth.All(pair => Approximately(pair.Key.VidaActual, pair.Value)) &&
            Approximately(player.VidaActual, initialPlayerHealth) &&
            Approximately(mutant.VidaActual, initialMutantHealth);

        GameObject tempPlayerObject = new GameObject("TempPlayerStatsValidation");
        CazadorStats tempPlayer = tempPlayerObject.AddComponent<CazadorStats>();
        GameObject tempMutantObject = new GameObject("TempMutantStatsValidation");
        MutantStats tempMutant = tempMutantObject.AddComponent<MutantStats>();
        tempPlayer.RecibirDano(tempPlayer.VidaMaxima + 1f);
        report.playerDeathIndependent = !tempPlayer.EstaVivo && tempMutant.EstaVivo;
        tempPlayer.ReiniciarStats();
        tempMutant.RecibirDano(tempMutant.VidaMaxima + 1f);
        report.mutantDeathIndependent = !tempMutant.EstaVivo && tempPlayer.EstaVivo;
        Destroy(tempPlayerObject);
        Destroy(tempMutantObject);

        GameObject robotClone = Instantiate(
            testRobot.gameObject,
            new Vector3(40f, 0f, 40f),
            Quaternion.identity
        );
        FallaCore cloneCore = robotClone.GetComponent<FallaCore>();
        cloneCore.KillImmediately();
        report.robotDeathIndependent = player.EstaVivo && mutant.EstaVivo;

        report.noPurpleBlobHierarchy = robots.All(robot =>
            FindChild(robot.transform, "ManchaPrincipal") == null &&
            FindChild(robot.transform, "Nucleo") == null &&
            robot.GetComponentInChildren<RobotFallaAnimationAdapter>(true) != null);

        Time.timeScale = previousTimeScale;
        Vector3 startMutantPosition = mutant.transform.position;
        float startDistance = Vector3.Distance(startMutantPosition, player.transform.position);
        float elapsed = 0f;
        while (elapsed < observationSeconds * 0.65f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (player.VidaActual < player.VidaMaxima * 0.35f)
            {
                player.ReiniciarStats();
            }
            yield return null;
        }

        report.crawlerAttackedPlayer = crawlerHitObserved;
        report.explosiveAttackedPlayer = explosiveHitObserved;
        report.generatorCreatedRobots = generator.CantidadActiva > 0 ||
                                        FindObjectsByType<FallaCore>().Length >
                                        robots.Length;
        report.robotTargetsOnlyPlayer = FindObjectsByType<FallaCore>()
            .Where(robot => robot != null && robot.EstaViva && robot.Objetivo != null)
            .All(robot => CombatTargeting.IsCazador(
                robot.Objetivo.GetComponentInParent<IRecibeImpacto>()));

        // Aisla al Mutant para que la invulnerabilidad producida por robots no
        // oculte un impacto valido de su hitbox.
        foreach (FallaCore robot in FindObjectsByType<FallaCore>())
        {
            if (robot != null)
            {
                robot.gameObject.SetActive(false);
            }
        }
        player.ReiniciarStats();
        MutantCombat mutantCombat = mutant.GetComponent<MutantCombat>();
        report.minimumMutantDistance = Vector3.Distance(
            mutant.transform.position, player.transform.position);
        elapsed = 0f;
        while (elapsed < observationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float distance = Vector3.Distance(mutant.transform.position, player.transform.position);
            report.minimumMutantDistance = Mathf.Min(report.minimumMutantDistance, distance);
            report.mutantAttackStateObserved |= mutantCombat != null && mutantCombat.EstaAtacando;
            if (player.VidaActual < player.VidaMaxima * 0.35f)
            {
                player.ReiniciarStats();
            }
            yield return null;
        }

        float endDistance = Vector3.Distance(mutant.transform.position, player.transform.position);
        report.mutantMovedTowardPlayer = endDistance < startDistance - 0.5f ||
                                        report.minimumMutantDistance < startDistance - 0.5f;
        report.mutantAttackedPlayer = mutantHitObserved;

        player.OnImpactoRecibido -= OnPlayerImpact;
        report.completed = true;
        Finish(report);
    }

    private void OnPlayerImpact(DamageInfo impact)
    {
        if (impact.Fuente == null)
        {
            return;
        }
        MutantStats mutantSource = impact.Fuente.GetComponentInParent<MutantStats>();
        if (mutantSource != null)
        {
            mutantHitObserved = true;
            return;
        }
        FallaCore robotSource = impact.Fuente.GetComponentInParent<FallaCore>();
        if (robotSource == null)
        {
            return;
        }
        if (robotSource.Tipo == FallaType.Rastrera)
        {
            crawlerHitObserved = true;
        }
        else if (robotSource.Tipo == FallaType.Explosiva)
        {
            explosiveHitObserved = true;
        }
    }

    private void Finish(ValidationReport report)
    {
        report.notes = notes.ToArray();
        string directory = Path.Combine(Application.dataPath, "Project", "Validation");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "RobotFallaPlayModeReport.json");
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log(
            $"ROBOT_FALLA_PLAYMODE_REPORT:{JsonUtility.ToJson(report)}",
            this
        );
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) < 0.001f;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChild(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
