using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GearPotion.EditorTools
{
    /// <summary>
    /// 원클릭(또는 batchmode) 셋업: 데이터 에셋 · 메인 씬 · 빌드 설정.
    /// 메뉴 Gear &amp; Potion → Setup Project. 다시 실행해도 안전하다.
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/Atelier.unity";
        const string BuildPath = "Builds/Windows/GearPotion.exe";
        public const string Version = "0.6.0";

        [MenuItem("Gear & Potion/Setup Project")]
        public static void Setup()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var dir in new[] { "Data/Ingredients", "Data/Machines", "Data/Potions", "Data/Customers", "Materials", "Scenes" })
                Directory.CreateDirectory(Path.Combine(Root, dir));
            AssetDatabase.Refresh();

            var material = CreateMaterial("M_SpriteUnlit", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            var db = DefaultContent.Upsert<GameDatabase>($"{Root}/Data/GameDatabase.asset", _ => { });
            db.ingredients = DefaultContent.Ingredients(Root);
            db.machines = DefaultContent.Machines(Root);
            db.potions = DefaultContent.Potions(Root, out var murky);
            db.murkyPotion = murky;
            db.customers = DefaultContent.Customers(Root);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            camGo.transform.position = new Vector3(7f, 4.5f, -10f);
            camGo.AddComponent<AudioListener>();

            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            var so = new SerializedObject(gm);
            so.FindProperty("database").objectReferenceValue = db;
            so.FindProperty("spriteMaterial").objectReferenceValue = material;
            so.FindProperty("litMaterial").objectReferenceValue = CreateMaterial("M_SpriteLit", "Universal Render Pipeline/2D/Sprite-Lit-Default");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Gear & Potion setup complete: {db.ingredients.Count} ingredients, {db.machines.Count} machines, {db.potions.Count} potions, {db.customers.Count} customers");
        }

        [MenuItem("Gear & Potion/Build Windows")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath)!);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[Build] {BuildPath} {report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize}");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        /// <summary>batchmode 진입점: 셋업 후 빌드.</summary>
        public static void SetupAndBuild()
        {
            Setup();
            BuildWindows();
        }

        static Material CreateMaterial(string name, string shaderName)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            var shader = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "GearPotion";
            PlayerSettings.productName = "Gear & Potion";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.GearPotion.GearPotion");
        }
    }
}
