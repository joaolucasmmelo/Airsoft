using UnityEditor;
using UnityEngine;

namespace Airsoft
{
    /// <summary>Gera o executável do jogo (menu Airsoft → Gerar executável).</summary>
    public static class BuildDoJogo
    {
        [MenuItem("Airsoft/Gerar executável (Linux)", false, 60)]
        public static void BuildLinux()
        {
            BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/SampleScene.unity" },
                "Build/Linux/Airsoft.x86_64",
                BuildTarget.StandaloneLinux64,
                BuildOptions.None);
        }

        [MenuItem("Airsoft/Gerar executável (Windows)", false, 61)]
        public static void BuildWindows()
        {
            BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/SampleScene.unity" },
                "Build/Windows/Airsoft.exe",
                BuildTarget.StandaloneWindows64,
                BuildOptions.None);
        }
    }
}
