using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoPileCap.Services
{
    /// <summary>
    /// Silent background auto-updater for BIM Plus.
    /// Checks for updates when Revit opens, downloads the latest DLL silently,
    /// and replaces the file for the next Revit session without any popups or notifications.
    /// </summary>
    public static class AutoUpdateService
    {
        // Change this URL to your own online update manifest (GitHub, web server, or cloud storage)
        public const string UpdateManifestUrl = "https://raw.githubusercontent.com/BIMPlus-Smart/BIM-Plus/main/update_info.json";

        public class UpdateInfo
        {
            public string Version { get; set; } = "";
            public string DllUrl { get; set; } = "";
            public string Notes { get; set; } = "";
        }

        /// <summary>
        /// Runs in background. Checks remote version, downloads new DLL, and silently replaces the local file.
        /// Guaranteed 100% silent - never throws errors or displays any message boxes.
        /// </summary>
        public static async Task CheckAndApplyUpdateSilentlyAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(UpdateManifestUrl) || UpdateManifestUrl.Contains("your-"))
                    return;

                string currentDllPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(currentDllPath) || !File.Exists(currentDllPath))
                    return;

                string pluginDir = Path.GetDirectoryName(currentDllPath);
                if (string.IsNullOrEmpty(pluginDir))
                    return;

                // 1. Delete previous .old file if it was released by Revit
                string oldDllPath = Path.Combine(pluginDir, "AutoPileCap.dll.old");
                if (File.Exists(oldDllPath))
                {
                    try { File.Delete(oldDllPath); } catch { /* in use or locked */ }
                }

                // 2. Query remote version manifest
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "BIMPlus-AutoUpdater");

                string jsonContent = await client.GetStringAsync(UpdateManifestUrl).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(jsonContent))
                    return;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(jsonContent, options);

                if (updateInfo == null || string.IsNullOrWhiteSpace(updateInfo.Version) || string.IsNullOrWhiteSpace(updateInfo.DllUrl))
                    return;

                // 3. Compare remote version with current version
                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
                if (!Version.TryParse(updateInfo.Version, out Version? remoteVersion))
                    return;

                if (remoteVersion <= currentVersion)
                {
                    // Already up to date
                    return;
                }

                // 4. Download new DLL to temporary file
                string tempDllPath = Path.Combine(pluginDir, "AutoPileCap.dll.tmp");
                byte[] newDllBytes = await client.GetByteArrayAsync(updateInfo.DllUrl).ConfigureAwait(false);

                // Basic validation: Revit plugins are at least ~500KB
                if (newDllBytes == null || newDllBytes.Length < 100 * 1024)
                    return;

                await File.WriteAllBytesAsync(tempDllPath, newDllBytes).ConfigureAwait(false);

                // 5. Safely replace current DLL using Windows atomic rename
                // Windows allows renaming an executing DLL.
                if (File.Exists(oldDllPath))
                {
                    try { File.Delete(oldDllPath); } catch { }
                }

                File.Move(currentDllPath, oldDllPath, true);
                File.Move(tempDllPath, currentDllPath, true);

                // Done silently! Next time Revit opens, it loads the new updated version automatically.
            }
            catch
            {
                // Silently ignore all network/file errors so Revit never crashes
            }
        }
    }
}
