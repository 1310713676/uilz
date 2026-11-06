using System.Linq;
using DefenseCounter.Core;
using DefenseCounter.UnityIntegration;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DefenseCounter.EditorTools
{
    /// <summary>
    /// 云构建入口。GameCI 的 buildMethod 会直接调用 BuildAndroid()。
    ///
    /// 全自动流程（不需要手动在编辑器里点任何东西）：
    ///   1. 生成 20 个配置资产（幂等）
    ///   2. 程序化创建 Battle 场景（相机 + 地面 + 控制器）
    ///   3. 配置 PlayerSettings（包名 / IL2CPP / ARM64 / minSdk 24）
    ///   4. BuildPipeline.BuildPlayer
    /// </summary>
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/Battle.unity";
        private const string OutputApk = "build/Android/DefenseCounter.apk";

        [MenuItem("DefenseCounter/Build Android APK (CI)")]
        public static void BuildAndroid()
        {
            Debug.Log("[Build] 开始");

            GameDatabase db = ConfigSeederEditor.SeedAll("Assets/Data");
            if (db == null)
            {
                throw new System.Exception("[Build] 配置资产生成失败");
            }

            EnsureScene(db);
            ConfigurePlayerSettings();

            AssetDatabase.SaveAssets();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputApk,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception("[Build] 失败: " + summary.result
                                           + " 错误数=" + summary.totalErrors
                                           + " 日志=" + summary.outputPath);
            }

            Debug.Log("[Build] 成功  大小=" + (summary.totalSize / (1024 * 1024)) + "MB"
                      + "  用时=" + summary.totalTime.TotalSeconds.ToString("0") + "s");
        }

        // ---------- 场景 ----------

        private static void EnsureScene(GameDatabase db)
        {
            EnsureFolder("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 相机：正交俯视（塔防视角）
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camGo.transform.position = new Vector3(0f, 40f, 0f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 地面：Cube + BoxCollider，射线拾取需要
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(64f, 1f, 64f);

            // 对局控制器
            var battleGo = new GameObject("BattleController");
            var battle = battleGo.AddComponent<BattleSceneController>();
            battle.database = db;
            battle.autoTick = true;
            battle.drawGizmos = true;

            // UI 接线（引用留空 → 只有 Gizmos，不会崩）
            var hookGo = new GameObject("UIHook");
            hookGo.AddComponent<BattleUIHook>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log("[Build] 场景已生成: " + ScenePath);
        }

        // ---------- PlayerSettings ----------

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "DefenseCounter";
            PlayerSettings.productName = "Defense Counter";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.defensecounter.game");

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            // 低强度裁剪 + link.xml，避免 Logic 层被 IL2CPP 删类型
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            Debug.Log("[Build] PlayerSettings 已配置");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int idx = path.LastIndexOf('/');
            if (idx <= 0)
            {
                return;
            }

            string parent = path.Substring(0, idx);
            string leaf = path.Substring(idx + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}