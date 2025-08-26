// Assets/Editor/ProjectStructureWizard.cs
// Menu: Tools/YGO/Structure Project
// - Dry Run: logs what would happen
// - Apply: creates folders, asmdefs, and moves files (GUID-safe)

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ProjectStructureWizard : EditorWindow
{
    private const string Root = "Assets/Script";

    // -------- Configure your target layout (folders) --------
    // Left = subfolder under Assets/Scripts, Right = asmdef name (null = none)
    private static readonly (string path, string asmdef)[] Folders =
    {
        ("Runtime/Core",                      "YGO.Runtime.Core"),
        ("Runtime/Installers",                null),
        ("Runtime/Duel/Board",                null),
        ("Runtime/Duel/Rules",                null),
        ("Runtime/Duel/Turn",                 null),
        ("Runtime/Duel/Chain",                null),
        ("Runtime/Duel/Actions/Base",         "YGO.Actions"),
        ("Runtime/Duel/Actions/Impl",         null),
        ("Runtime/Duel/Systems",              null),
        ("Runtime/Duel/Battle",               "YGO.Battle"),
        ("Runtime/Duel/Targeting",            null),
        ("Runtime/Data/Card",                 null),
        ("Runtime/Data/Tags",                 null),

        ("Presentation/3D",                   null),
        ("Presentation/UI/HUD",               null),
        ("Presentation/UI/Hand",              null),
        ("Presentation/UI/Modals",            null),
        ("Presentation/UI/Inspectors",        null),
        ("Presentation/UI/Flow",              null),
        ("Presentation/UI/Overlays",          null),
        ("Presentation/ZonesView",            null),
        ("Presentation/Views.Shared",         "YGO.Presentation.Views"),
        ("Presentation/FX",                   null),
        ("Presentation/Services",             "YGO.Presentation.Services"),

        ("AI",                                "YGO.AI"),
        ("Samples.Demo",                      null),
    };

    // -------- Map filenames (no extension) to target subfolders --------
    // If a name appears in multiple buckets, first match wins.
    private static readonly Dictionary<string, string> FileToFolder = new(StringComparer.OrdinalIgnoreCase)
    {
        // 3D & Presentation
        ["Board3DLayout"] = "Presentation/3D",
        ["SpawnManager3D"] = "Presentation/3D",
        ["Card3DView"] = "Presentation/3D",
        ["BattleAnimationController3D"] = "Presentation/3D",
        ["PlayerAvatar3D"] = "Presentation/3D",
        ["AvatarLocatorService"] = "Presentation/3D",
        ["BillboardUI"] = "Presentation/3D",

        // UI – HUD / Hand / Modals / Flow / Overlays / Shared
        ["LifePointsHUD"] = "Presentation/UI/HUD",
        ["DuelHud"] = "Presentation/UI/HUD",

        ["HandView"] = "Presentation/UI/Hand",
        ["PlayerHandView"] = "Presentation/UI/Hand",
        ["HandSpawner"] = "Presentation/UI/Hand",
        ["HandSummonController"] = "Presentation/UI/Hand",

        ["SummonChoicePopup"] = "Presentation/UI/Modals",
        ["ZoneSelectionPanel"] = "Presentation/UI/Modals",
        ["PromptModal"] = "Presentation/UI/Modals",
        ["CardInspectorPanel"] = "Presentation/UI/Inspectors",
        ["HintToast"] = "Presentation/UI/Inspectors",

        ["PhaseRibbon"] = "Presentation/UI/Flow",
        ["PhaseManager"] = "Presentation/UI/Flow",
        ["PhaseAutoSkipper"] = "Presentation/UI/Flow",
        ["PriorityManager"] = "Presentation/UI/Flow",
        ["InputRouter"] = "Presentation/UI/Flow",

        ["TargetingOverlay"] = "Presentation/UI/Overlays",
        ["HoverTooltipController"] = "Presentation/UI/Overlays",

        ["ZoneView"] = "Presentation/ZonesView",
        ["GraveyardView"] = "Presentation/ZonesView",
        ["BanishedView"] = "Presentation/ZonesView",
        ["ChainView"] = "Presentation/ZonesView",

        ["CardView"] = "Presentation/Views.Shared",
        ["CardViewDemoBinder"] = "Presentation/Views.Shared",
        ["SimpleCardArtProvider"] = "Presentation/Views.Shared",

        ["BattleVfxController"] = "Presentation/FX",
        ["ChainVfxController"] = "Presentation/FX",

        ["AttackCommandService"] = "Presentation/Services",
        ["SummonCommandService"] = "Presentation/Services",
        ["InputLockService"] = "Presentation/Services",
        ["AttackController"] = "Presentation/Services", // if it's more service-y than view

        // Runtime / Duel / Board
        ["BoardManager"] = "Runtime/Duel/Board",
        ["Zone"] = "Runtime/Duel/Board",
        ["StateQuery"] = "Runtime/Duel/Board",

        // Rules, Turn, Chain
        ["RuleSet"] = "Runtime/Duel/Rules",
        ["RuleAdapters"] = "Runtime/Duel/Rules",
        ["TimingTable"] = "Runtime/Duel/Rules",

        ["TurnManager"] = "Runtime/Duel/Turn",

        ["ChainManager"] = "Runtime/Duel/Chain",
        ["ChainLink"] = "Runtime/Duel/Chain",

        // Actions
        ["GameActionBase"] = "Runtime/Duel/Actions/Base",
        ["ActionFactory"] = "Runtime/Duel/Actions/Base",
        ["ActionQueue"] = "Runtime/Duel/Actions/Base",
        ["ActionUtil"] = "Runtime/Duel/Actions/Base",
        ["ActivateEffectAction"] = "Runtime/Duel/Actions/Impl",
        ["DeclareAttackAction"] = "Runtime/Duel/Actions/Impl",
        ["ResolveDamageStepAction"] = "Runtime/Duel/Actions/Impl",
        ["NormalSummonAction"] = "Runtime/Duel/Actions/Impl",
        ["SetCardAction"] = "Runtime/Duel/Actions/Impl",
        ["ChangePositionAction"] = "Runtime/Duel/Actions/Impl",
        ["EndPhaseAction"] = "Runtime/Duel/Actions/Impl",
        ["EndTurnAction"] = "Runtime/Duel/Actions/Impl",
        ["PassPriorityAction"] = "Runtime/Duel/Actions/Impl",
        ["ConcedeAction"] = "Runtime/Duel/Actions/Impl",
        ["PlayerActionHandler"] = "Runtime/Duel/Actions/Impl",

        // Systems
        ["DrawSystem"] = "Runtime/Duel/Systems",
        ["DiscardSystem"] = "Runtime/Duel/Systems",
        ["DestructionSystem"] = "Runtime/Duel/Systems",
        ["ConditionSystem"] = "Runtime/Duel/Systems",
        ["CostSystem"] = "Runtime/Duel/Systems",
        ["SummonValidator"] = "Runtime/Duel/Systems",

        // Battle
        ["BattleManager"] = "Runtime/Duel/Battle",
        ["BattleTriggerSystem"] = "Runtime/Duel/Battle",
        ["DamageCalculator"] = "Runtime/Duel/Battle",
        ["DirectAttackValidator"] = "Runtime/Duel/Battle",
        ["CardBattlerAdapter"] = "Runtime/Duel/Battle",
        ["IBattlerResolver"] = "Runtime/Duel/Battle",
        ["DefaultBattlerResolver"] = "Runtime/Duel/Battle",

        // Targeting
        ["TargetingService"] = "Runtime/Duel/Targeting",
        ["BoardQueryAdapter"] = "Runtime/Duel/Targeting",

        // Data
        ["Card"] = "Runtime/Data/Card",
        ["CardDefinition"] = "Runtime/Data/Card",
        ["CardDatabase"] = "Runtime/Data/Card",
        ["CardFactory"] = "Runtime/Data/Card",
        ["CardIndex"] = "Runtime/Data/Card",
        ["RuntimeCardIndex"] = "Runtime/Data/Card",

        ["ArchetypeTag"] = "Runtime/Data/Tags",
        ["AttributeTag"] = "Runtime/Data/Tags",
        ["TypeTag"] = "Runtime/Data/Tags",
        ["CounterTag"] = "Runtime/Data/Tags",
        ["Token"] = "Runtime/Data/Tags",

        // Core / Installer
        ["ServiceLocator"] = "Runtime/Core",
        ["DeterministicRng"] = "Runtime/Core",
        ["DuelLogger"] = "Runtime/Core",
        ["DuelState"] = "Runtime/Core",
        ["GameConfig"] = "Runtime/Core",
        ["DuelInstaller"] = "Runtime/Installers",

        // AI & Demo
        ["AIController"] = "AI",
        ["DuelRecorder"] = "Samples.Demo",
        ["DuelReplay"] = "Samples.Demo",
        ["Pay1000Draw1"] = "Samples.Demo",
        ["Player"] = "Samples.Demo",
        ["SimpleCardStatProvider"] = "Samples.Demo",

        // Misc that appeared in your list
        ["CardMover"] = "Samples.Demo",
        ["PhaseAutoSkipper"] = "Presentation/UI/Flow",
        ["LifePointsHUD"] = "Presentation/UI/HUD",
        ["BillboardUI"] = "Presentation/3D",
        ["InputLockService"] = "Presentation/Services",
    };

    // Optional: assembly references graph (asmdef → references)
    private static readonly Dictionary<string, string[]> AsmdefRefs = new()
    {
        ["YGO.Battle"] = new[] { "YGO.Runtime.Core" },
        ["YGO.Actions"] = new[] { "YGO.Runtime.Core" },
        ["YGO.Presentation.Views"] = new[] { "YGO.Runtime.Core", "YGO.Battle" },
        ["YGO.Presentation.Services"] = new[] { "YGO.Runtime.Core", "YGO.Battle" },
        ["YGO.AI"] = new[] { "YGO.Runtime.Core" },
    };

    private bool _dryRun = true;
    private Vector2 _scroll;

    [MenuItem("Tools/YGO/Structure Project")]
    private static void Open() => GetWindow<ProjectStructureWizard>("YGO Structure");

    private void OnGUI()
    {
        GUILayout.Label("Project Structure Wizard", EditorStyles.boldLabel);
        _dryRun = EditorGUILayout.Toggle("Dry Run (log only)", _dryRun);

        if (GUILayout.Button(_dryRun ? "Simulate" : "Apply Structure"))
        {
            try
            {
                ApplyStructure(_dryRun);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        GUILayout.Space(8);
        GUILayout.Label("Notes", EditorStyles.miniBoldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(140));
        EditorGUILayout.HelpBox(
            "- Commit your repo first!\n" +
            "- The wizard will create folders, asmdefs, and move scripts by filename.\n" +
            "- Update the FileToFolder map above if something lands in the wrong place.\n" +
            "- Re-run with Dry Run ON to preview changes.", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private static void ApplyStructure(bool dryRun)
    {
        EnsureFolder("Assets");
        EnsureFolder(Root);

        // 1) Create folders + asmdefs
        foreach (var (sub, asm) in Folders)
        {
            var full = PathCombine(Root, sub);
            EnsureFolder(full);

            if (!string.IsNullOrEmpty(asm))
                EnsureAsmdef(full, asm, AsmdefRefs.TryGetValue(asm, out var refs) ? refs : Array.Empty<string>(), dryRun);
        }

        // 2) Find all C# scripts under Assets (excluding Packages)
        var guids = AssetDatabase.FindAssets("t:MonoScript");
        int moveCount = 0, skipCount = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
            if (!path.StartsWith("Assets/")) continue; // ignore Packages, etc.

            var fn = Path.GetFileNameWithoutExtension(path);
            if (!FileToFolder.TryGetValue(fn, out var targetSub)) { skipCount++; continue; }

            var targetDir = PathCombine(Root, targetSub);
            var targetPath = PathCombine(targetDir, Path.GetFileName(path));

            if (path == targetPath) { skipCount++; continue; } // already in place

            if (dryRun)
            {
                Debug.Log($"[DRY] Move: {path} -> {targetPath}");
            }
            else
            {
                EnsureFolder(targetDir);
                var result = AssetDatabase.MoveAsset(path, targetPath);
                if (string.IsNullOrEmpty(result))
                    moveCount++;
                else
                    Debug.LogError($"Move failed: {path} -> {targetPath}\n{result}");
            }
        }

        if (!dryRun) AssetDatabase.Refresh();

        Debug.Log(dryRun
            ? $"[DRY] Structure simulated. (would move ~{moveCount} files; skipped ~{skipCount})"
            : $"Structure applied. Moved {moveCount} files; skipped {skipCount}.");
    }

    // ---------- helpers ----------

    private static void EnsureFolder(string fullPath)
    {
        var parts = fullPath.Split('/');
        var cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{cur}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(cur, parts[i]);
            }
            cur = next;
        }
    }

    private static string PathCombine(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        return a.TrimEnd('/') + "/" + b.TrimStart('/');
    }

    private static void EnsureAsmdef(string folder, string name, string[] references, bool dryRun)
    {
        var path = PathCombine(folder, name + ".asmdef");
        if (File.Exists(path)) return;

        var asmdef = new AsmDefJson
        {
            name = name,
            allowUnsafeCode = false,
            autoReferenced = true,
            includePlatforms = new string[0],
            references = references?.Select(r => new AsmRef { reference = r }).ToArray() ?? Array.Empty<AsmRef>(),
            defineConstraints = Array.Empty<string>(),
            versionDefines = Array.Empty<AsmVersionDefine>(),
            noEngineReferences = false
        };

        var json = JsonUtility.ToJson(asmdef, true);
        if (dryRun)
        {
            Debug.Log($"[DRY] Create asmdef: {path}\n{json}");
            return;
        }

        File.WriteAllText(path, json);
        AssetDatabase.ImportAsset(path);
        Debug.Log($"Created asmdef: {path}");
    }

    // minimal asmdef JSON types
    [Serializable] private class AsmDefJson
    {
        public string name;
        public bool allowUnsafeCode;
        public bool autoReferenced;
        public string[] includePlatforms;
        public AsmRef[] references;
        public string[] defineConstraints;
        public AsmVersionDefine[] versionDefines;
        public bool noEngineReferences;
    }
    [Serializable] private class AsmRef { public string reference; }
    [Serializable] private class AsmVersionDefine
    {
        public string name;
        public string expression;
        public bool define;
    }
}
#endif