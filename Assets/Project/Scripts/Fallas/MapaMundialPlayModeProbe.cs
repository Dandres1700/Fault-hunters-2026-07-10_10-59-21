using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class MapaMundialPlayModeProbe : MonoBehaviour
{
    [Serializable]
    private sealed class Report
    {
        public string timestamp;
        public bool completed;
        public bool loadedFromMenu;
        public bool cazadorMoved;
        public bool cazadorAttacked;
        public bool mutantMoved;
        public bool mutantAttackedCazador;
        public bool crawlerAttackedCazador;
        public bool explosiveAttackedCazador;
        public bool mutantSummonedRobots;
        public bool robotsTargetOnlyCazador;
        public bool damageIndependent;
        public bool deathIndependent;
        public bool uniqueActorsAndSystems;
        public bool noPurpleVisuals;
        public bool noMissingReferences;
        public float initialMutantDistance;
        public float minimumMutantDistance;
        public int initialRobotCount;
        public int maximumRobotCount;
        public string[] notes;
    }

    private const string ReportPath =
        "Assets/Project/Validation/MapaMundialPlayModeReport.json";
    [SerializeField, Min(5f)] private float observationSeconds = 18f;
    private readonly List<string> notes = new List<string>();
    private bool mutantHit;
    private bool crawlerHit;
    private bool explosiveHit;
    private bool runtimeReferenceError;

    private IEnumerator Start()
    {
        Report report = new Report { timestamp = DateTime.Now.ToString("O") };
        Application.logMessageReceived += OnLog;
        yield return null;

        report.loadedFromMenu = PlayerPrefs.GetInt("MapaMundialValidation.LoadedFromMenu", 0) == 1;
        CazadorStats hunter = FindAnyObjectByType<CazadorStats>();
        MutantStats mutant = FindAnyObjectByType<MutantStats>();
        MutantEnemyIntentSource ai = FindAnyObjectByType<MutantEnemyIntentSource>();
        MutantFallaController summoner = FindAnyObjectByType<MutantFallaController>();
        MutantCombat mutantCombat = FindAnyObjectByType<MutantCombat>();
        CazadorInputReader input = FindAnyObjectByType<CazadorInputReader>();
        CazadorCombat hunterCombat = FindAnyObjectByType<CazadorCombat>();

        FallaCore[] initialRobots = FindObjectsByType<FallaCore>();
        report.initialRobotCount = initialRobots.Length;
        report.maximumRobotCount = initialRobots.Length;
        report.noMissingReferences = hunter != null && mutant != null && ai != null &&
                                     summoner != null && mutantCombat != null && input != null &&
                                     hunterCombat != null &&
                                     initialRobots.Length >= 3;
        if (!report.noMissingReferences)
        {
            notes.Add("Faltan actores o referencias principales en MapaMundial.");
            Finish(report);
            yield break;
        }

        typeof(CazadorStats).GetField("vidaMaxima",
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hunter, 1000f);
        hunter.ReiniciarStats();
        hunter.OnImpactoRecibido += OnHunterImpact;

        ai.enabled = false;
        summoner.enabled = false;
        foreach (FallaCore robot in initialRobots)
        {
            robot.enabled = false;
        }

        report.uniqueActorsAndSystems = ValidateUniqueSystems();
        report.noPurpleVisuals = !FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .Any(item => item.name.Contains("ManchaPrincipal") ||
                         item.name.StartsWith("Extension_") ||
                         item.name == "Nucleo");
        report.robotsTargetOnlyCazador = initialRobots.All(robot =>
            robot.Objetivo == null || robot.Objetivo == hunter.transform ||
            (robot.Objetivo != null && robot.Objetivo.IsChildOf(hunter.transform)));

        Vector3 hunterStart = hunter.transform.position;
        SetAutoProperty(input, "Move", Vector2.up);
        yield return new WaitForSeconds(0.8f);
        SetAutoProperty(input, "Move", Vector2.zero);
        report.cazadorMoved = Vector3.Distance(hunterStart, hunter.transform.position) > 0.15f;

        CazadorStateController hunterState = hunter.GetComponent<CazadorStateController>();
        float groundedWait = 0f;
        while (groundedWait < 2f && hunterState != null && !hunterState.CanAttack)
        {
            groundedWait += Time.deltaTime;
            yield return null;
        }
        FieldInfo attackRequest = typeof(CazadorInputReader).GetField(
            "attackRequested", BindingFlags.Instance | BindingFlags.NonPublic);
        attackRequest?.SetValue(input, true);
        float attackWait = 0f;
        while (attackWait < 1f && !hunterCombat.EstaAtacando)
        {
            attackWait += Time.deltaTime;
            yield return null;
        }
        report.cazadorAttacked = hunterCombat.EstaAtacando;

        ai.enabled = true;
        ai.SetTarget(hunter);
        summoner.enabled = true;
        foreach (FallaCore robot in initialRobots)
        {
            robot.enabled = true;
            robot.SetTarget(hunter.transform);
        }

        report.initialMutantDistance = HorizontalDistance(mutant.transform, hunter.transform);
        report.minimumMutantDistance = report.initialMutantDistance;
        Vector3 mutantStart = mutant.transform.position;
        bool mutantAttackState = false;
        int initialSummoned = summoner.CantidadActiva;
        float elapsed = 0f;
        while (elapsed < observationSeconds && hunter.EstaVivo && mutant.EstaVivo)
        {
            elapsed += Time.deltaTime;
            report.minimumMutantDistance = Mathf.Min(report.minimumMutantDistance,
                HorizontalDistance(mutant.transform, hunter.transform));
            mutantAttackState |= ai.Estado == MutantEnemyAIState.Attack ||
                                 mutantCombat.EstaAtacando;
            report.maximumRobotCount = Mathf.Max(report.maximumRobotCount,
                FindObjectsByType<FallaCore>().Length);
            yield return null;
        }

        report.mutantMoved = Vector3.Distance(mutantStart, mutant.transform.position) > 0.5f &&
                             report.minimumMutantDistance < report.initialMutantDistance - 0.5f;
        report.mutantAttackedCazador = mutantAttackState && mutantHit;
        report.crawlerAttackedCazador = crawlerHit;
        report.explosiveAttackedCazador = explosiveHit;
        report.mutantSummonedRobots = summoner.CantidadActiva > initialSummoned ||
                                     report.maximumRobotCount > report.initialRobotCount;
        report.robotsTargetOnlyCazador &= FindObjectsByType<FallaCore>().All(robot =>
            robot.Objetivo == null ||
            robot.Objetivo == hunter.transform || robot.Objetivo.IsChildOf(hunter.transform));

        yield return VerifyIndependentHealth(report, hunter, mutant);
        report.noMissingReferences &= !runtimeReferenceError;
        hunter.OnImpactoRecibido -= OnHunterImpact;
        Application.logMessageReceived -= OnLog;
        Finish(report);
    }

    private IEnumerator VerifyIndependentHealth(
        Report report, CazadorStats hunter, MutantStats mutant)
    {
        hunter.ReiniciarStats();
        mutant.Reiniciar();
        FallaCore[] robots = FindObjectsByType<FallaCore>()
            .Where(robot => robot.EstaViva).ToArray();
        if (robots.Length < 2)
        {
            notes.Add("No quedaron dos robots vivos para la prueba de independencia.");
            yield break;
        }

        float mutantBefore = mutant.VidaActual;
        float robotBefore = robots[0].VidaActual;
        hunter.RecibirDano(3f);
        yield return null;
        bool hunterDamageOnly = mutant.VidaActual == mutantBefore &&
                                robots[0].VidaActual == robotBefore;

        float hunterBefore = hunter.VidaActual;
        robotBefore = robots[0].VidaActual;
        mutant.RecibirDano(3f);
        yield return null;
        bool mutantDamageOnly = hunter.VidaActual == hunterBefore &&
                                robots[0].VidaActual == robotBefore;

        hunterBefore = hunter.VidaActual;
        mutantBefore = mutant.VidaActual;
        float otherRobotBefore = robots[1].VidaActual;
        robots[0].RecibirDano(3f);
        yield return null;
        bool robotDamageOnly = hunter.VidaActual == hunterBefore &&
                               mutant.VidaActual == mutantBefore &&
                               robots[1].VidaActual == otherRobotBefore;
        report.damageIndependent = hunterDamageOnly && mutantDamageOnly && robotDamageOnly;

        hunter.ReiniciarStats();
        mutant.Reiniciar();
        hunter.RecibirDano(hunter.VidaMaxima * 2f);
        yield return null;
        bool hunterDeathOnly = !hunter.EstaVivo && mutant.EstaVivo && robots[1].EstaViva;

        hunter.ReiniciarStats();
        mutant.Reiniciar();
        mutant.RecibirDano(mutant.VidaMaxima * 2f);
        yield return null;
        bool mutantDeathOnly = !mutant.EstaVivo && hunter.EstaVivo && robots[1].EstaViva;
        report.deathIndependent = hunterDeathOnly && mutantDeathOnly;
    }

    private void OnHunterImpact(DamageInfo info)
    {
        if (info.Fuente == null)
        {
            return;
        }
        MutantStats sourceMutant = info.Fuente.GetComponentInParent<MutantStats>();
        if (sourceMutant != null)
        {
            mutantHit = true;
            return;
        }
        FallaCore robot = info.Fuente.GetComponentInParent<FallaCore>();
        if (robot == null)
        {
            return;
        }
        crawlerHit |= robot.Tipo == FallaType.Rastrera;
        explosiveHit |= robot.Tipo == FallaType.Explosiva;
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception &&
            (condition.Contains("NullReferenceException") ||
             condition.Contains("MissingReferenceException")))
        {
            runtimeReferenceError = true;
            notes.Add(condition);
        }
    }

    private static bool ValidateUniqueSystems()
    {
        int hunters = FindObjectsByType<CazadorStats>().Length;
        int mutants = FindObjectsByType<MutantStats>().Length;
        int inputs = FindObjectsByType<PlayerInput>(FindObjectsInactive.Include)
            .Count(value => value.enabled);
        int cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include)
            .Count(value => value.enabled);
        int listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include)
            .Count(value => value.enabled);
        int systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include)
            .Count(value => value.enabled);
        return hunters == 1 && mutants == 1 && inputs == 1 && cameras == 1 &&
               listeners == 1 && systems <= 1;
    }

    private static void SetAutoProperty(object target, string property, object value)
    {
        target.GetType().GetField($"<{property}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
    }

    private static float HorizontalDistance(Transform a, Transform b)
    {
        Vector3 delta = a.position - b.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void Finish(Report report)
    {
        report.completed = report.loadedFromMenu && report.cazadorMoved &&
            report.cazadorAttacked && report.mutantMoved && report.mutantAttackedCazador &&
            report.crawlerAttackedCazador && report.explosiveAttackedCazador &&
            report.mutantSummonedRobots && report.robotsTargetOnlyCazador &&
            report.damageIndependent && report.deathIndependent &&
            report.uniqueActorsAndSystems && report.noPurpleVisuals &&
            report.noMissingReferences;
        report.notes = notes.ToArray();
        string fullPath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
        Debug.Log($"Validacion MapaMundial terminada. Exito={report.completed}. Reporte: {ReportPath}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
