using System;
using System.Globalization;
using System.IO;

namespace AionCL {
    /// Writes the ShugoConsole configuration consumed by the optional client DLL.
    /// The DLL is loaded by the game from bin64/version.dll; no helper process is
    /// launched and no registry state is required.
    public static class ShugoConsoleIntegration {
        public static void Configure(double fov, double cameraDistance) {
            if (fov < 60 || fov > 170) throw new ArgumentOutOfRangeException("fov");
            if (cameraDistance < 5 || cameraDistance > 50) throw new ArgumentOutOfRangeException("cameraDistance");
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShugoConsole");
            Directory.CreateDirectory(directory);
            string content = "g_minFov = " + fov.ToString("0.###", CultureInfo.InvariantCulture) + Environment.NewLine +
                "g_camMax = " + cameraDistance.ToString("0.###", CultureInfo.InvariantCulture) + Environment.NewLine;
            File.WriteAllText(Path.Combine(directory, "config.toml"), content);
        }
    }
}
