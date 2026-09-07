#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class HierarchyOrganizerUpdater
{
    public const string Version = "1.7.0";
    const string Repo = "Blaubluter/HierarchyOrganizerUnity6.6";
    const long MaxZipBytes = 10 * 1024 * 1024;
    static UnityWebRequest request;
    static Action completed;
    static string status;

    [Serializable] sealed class Release
    {
        public string tag_name;
        public string body;
        public bool draft;
        public bool prerelease;
        public Asset[] assets;
    }
    [Serializable] sealed class Asset
    {
        public string name;
        public string browser_download_url;
        public string digest;
        public long size;
    }

    [MenuItem("Window/Hierarchy Organizer Updates/Nach Updates suchen")]
    public static void CheckForUpdates()
    {
        if (request != null) { Show("Eine Update-Anfrage läuft bereits."); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        { Show("Bitte den Play-Modus und die Kompilierung zuerst beenden."); return; }
        Begin("https://api.github.com/repos/" + Repo + "/releases/latest", "Suche nach Updates …", () =>
        {
            var release = JsonUtility.FromJson<Release>(request.downloadHandler.text);
            if (release == null || release.draft || release.prerelease ||
                !System.Version.TryParse((release.tag_name ?? "").TrimStart('v', 'V'), out var latest))
                throw new InvalidDataException("Die Release-Versionsnummer ist ungültig.");
            if (latest <= new System.Version(Version)) { Show("Version " + Version + " ist aktuell."); return; }
            var asset = release.assets?.FirstOrDefault(a => a.name == "HierarchyOrganizer-" + latest + ".zip");
            if (asset == null || asset.size <= 0 || asset.size > MaxZipBytes ||
                !Uri.TryCreate(asset.browser_download_url, UriKind.Absolute, out var url) ||
                url.Scheme != "https" || url.Host != "github.com" ||
                !url.AbsolutePath.StartsWith("/" + Repo + "/releases/download/", StringComparison.Ordinal))
                throw new InvalidDataException("Dieses Release enthält kein passendes Update-ZIP.");
            string notes = release.body ?? "";
            if (notes.Length > 2200) notes = notes.Substring(0, 2200) + "…";
            if (EditorUtility.DisplayDialog("Hierarchy Organizer Update",
                "Installiert: " + Version + "\nVerfügbar: " + latest + "\n\n" + notes +
                "\n\nDie bisherigen Dateien werden gesichert. Unity kompiliert anschließend neu.", "Installieren", "Abbrechen"))
                EditorApplication.delayCall += () => Download(asset, latest.ToString());
        });
    }

    static void Download(Asset asset, string expectedVersion)
    {
        Begin(asset.browser_download_url, "Update herunterladen …", () =>
        {
            byte[] bytes = request.downloadHandler.data;
            if (bytes.LongLength != asset.size || bytes.LongLength > MaxZipBytes)
                throw new InvalidDataException("Die Download-Größe stimmt nicht mit dem Release überein.");
            if (string.IsNullOrEmpty(asset.digest) || !asset.digest.StartsWith("sha256:", StringComparison.Ordinal))
                throw new InvalidDataException("Dem Release fehlt die SHA-256-Prüfsumme. Bitte das ZIP auf GitHub neu hochladen.");
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                if (!string.Equals(actual, asset.digest.Substring(7), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Die Prüfsumme des Downloads stimmt nicht.");
            }
            var files = ReadPackage(bytes);
            string updaterCode = System.Text.Encoding.UTF8.GetString(files["HierarchyOrganizerUpdater.cs"]);
            var versionMatch = System.Text.RegularExpressions.Regex.Match(updaterCode, "public const string Version = \"([^\"]+)\"");
            if (!versionMatch.Success || versionMatch.Groups[1].Value != expectedVersion)
                throw new InvalidDataException("Die Version im Paket stimmt nicht mit dem Release überein.");
            EditorApplication.delayCall += () =>
            {
                try { Install(files); }
                catch (Exception ex) { Show("Update nicht installiert: " + ex.Message); }
            };
        });
    }

    static void Begin(string url, string message, Action done)
    {
        if (request != null) return;
        request = UnityWebRequest.Get(url);
        request.timeout = 90;
        request.SetRequestHeader("User-Agent", "HierarchyOrganizer/" + Version);
        status = message;
        completed = done;
        request.SendWebRequest();
        EditorApplication.update += Poll;
        AssemblyReloadEvents.beforeAssemblyReload += Cancel;
    }

    static void Poll()
    {
        if (request == null) return;
        if (EditorUtility.DisplayCancelableProgressBar("Hierarchy Organizer", status, Mathf.Max(0, request.downloadProgress)))
        { Cancel(); return; }
        if (request.downloadedBytes > (ulong)MaxZipBytes) { Cancel(); Show("Download zu groß."); return; }
        if (!request.isDone) return;
        EditorUtility.ClearProgressBar();
        try
        {
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException(request.responseCode == 404
                    ? "Kein öffentliches Release gefunden. Prüfe die Repository-Sichtbarkeit und veröffentliche ein Release."
                    : "GitHub-Anfrage fehlgeschlagen (" + request.responseCode + "): " + request.error);
            completed?.Invoke();
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { Cancel(); }
    }

    static void Cancel()
    {
        EditorApplication.update -= Poll;
        AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
        if (request != null) { request.Abort(); request.Dispose(); request = null; }
        completed = null;
        EditorUtility.ClearProgressBar();
    }

    // Only these files may be installed. Never extract arbitrary archive paths.
    public static Dictionary<string, byte[]> ReadPackage(byte[] bytes)
    {
        const string prefix = "Assets/JustDoIt_Tools/HierarchyOrganizer/";
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "HierarchyOrganizerWindow.cs", "HierarchyOrganizerWindow.cs.meta",
            "HierarchyOrganizerSceneData.cs", "HierarchyOrganizerSceneData.cs.meta",
            "HierarchyOrganizerUpdater.cs", "HierarchyOrganizerUpdater.cs.meta"
        };
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var stream = new MemoryStream(bytes))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            if (zip.Entries.Count > 50) throw new InvalidDataException("Zu viele Dateien im Update.");
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                total += entry.Length;
                if (entry.Length < 0 || total > MaxZipBytes) throw new InvalidDataException("Entpacktes Update zu groß.");
                string path = entry.FullName;
                if (path.Contains("\\") || path.StartsWith("/") || path.Split('/').Any(p => p == ".." || p == "."))
                    throw new InvalidDataException("Ungültiger Pfad im Update.");
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string name = path.Substring(prefix.Length);
                if (!allowed.Contains(name)) throw new InvalidDataException("Unbekannte Update-Datei: " + name);
                if (files.ContainsKey(name)) throw new InvalidDataException("Doppelte Update-Datei: " + name);
                using (var input = entry.Open())
                using (var output = new MemoryStream())
                {
                    input.CopyTo(output);
                    files.Add(name, output.ToArray());
                }
            }
        }
        foreach (string name in allowed)
            if (!files.ContainsKey(name) || files[name].Length == 0)
                throw new InvalidDataException("Update unvollständig: " + name);
        return files;
    }

    static void Install(Dictionary<string, byte[]> files)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Unity ist beschäftigt. Bitte später erneut versuchen.");
        var scripts = AssetDatabase.FindAssets("HierarchyOrganizerUpdater t:MonoScript")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileName(p) == "HierarchyOrganizerUpdater.cs").ToArray();
        if (scripts.Length != 1 || !scripts[0].StartsWith("Assets/", StringComparison.Ordinal))
            throw new InvalidOperationException("Der Installationsordner ist nicht eindeutig unter Assets auffindbar.");
        string target = Path.GetFullPath(Path.GetDirectoryName(scripts[0]));
        if (!File.Exists(Path.Combine(target, "HierarchyOrganizerWindow.cs")) ||
            !File.Exists(Path.Combine(target, "HierarchyOrganizerSceneData.cs")))
            throw new InvalidOperationException("Die bestehende Installation ist unvollständig.");
        string backup = Path.GetFullPath(Path.Combine("Library", "HierarchyOrganizerUpdates", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(backup);
        var originals = new Dictionary<string, byte[]>();
        foreach (string name in files.Keys)
        {
            string path = Path.Combine(target, name);
            byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
            originals[name] = original;
            if (original != null) File.WriteAllBytes(Path.Combine(backup, name), original);
        }
        File.WriteAllText(Path.Combine(backup, "RESTORE.txt"), "Restore the backed-up files to:\n" + target);
        EditorApplication.LockReloadAssemblies();
        bool editing = false;
        try
        {
            AssetDatabase.StartAssetEditing();
            editing = true;
            foreach (var item in files)
            {
                // Keep existing GUIDs, so scene references survive the update.
                if (item.Key.EndsWith(".meta", StringComparison.Ordinal) && originals[item.Key] != null) continue;
                File.WriteAllBytes(Path.Combine(target, item.Key), item.Value);
            }
        }
        catch
        {
            foreach (var item in originals)
            {
                string path = Path.Combine(target, item.Key);
                if (item.Value == null) { if (File.Exists(path)) File.Delete(path); }
                else File.WriteAllBytes(path, item.Value);
            }
            throw;
        }
        finally
        {
            try { if (editing) AssetDatabase.StopAssetEditing(); }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh();
            }
        }
        Debug.Log("[HierarchyOrganizer] Update-Dateien installiert. Sicherung: " + backup);
        Show("Update-Dateien installiert. Unity kompiliert jetzt neu.\n\nSicherung: " + backup);
    }

    static void Show(string message) => EditorUtility.DisplayDialog("Hierarchy Organizer", message, "OK");
}
#endif
