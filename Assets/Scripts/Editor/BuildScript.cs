using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DungeonGame.EditorTools
{
    /// <summary>
    /// ビルド用のエディタスクリプト。メニュー「DungeonGame/WebGLビルド」か、コマンドラインから実行する。
    ///   unity build . --target WebGL --execute-method DungeonGame.EditorTools.BuildScript.BuildWebGL
    /// 出力先:Build/WebGL
    /// </summary>
    public static class BuildScript
    {
        public const string WebGLOutput = "Build/WebGL";

        [MenuItem("DungeonGame/WebGLビルド")]
        public static void BuildWebGL()
        {
            // どのWebサーバーでも動くよう、gzip圧縮+ブラウザ側での展開(フォールバック)にする
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.productName = "ゆうしゃとまおう";

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };

            Directory.CreateDirectory(WebGLOutput);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WebGLOutput,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[BuildScript] WebGLビルド結果: {summary.result} サイズ {summary.totalSize / 1024 / 1024}MB 時間 {summary.totalTime}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
