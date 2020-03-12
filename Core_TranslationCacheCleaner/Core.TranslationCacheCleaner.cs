﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using XUnity.AutoTranslator.Plugin.Core;
using GeBoCommon.AutoTranslation;
using KeyboardShortcut = BepInEx.Configuration.KeyboardShortcut;
using GeBoCommon;
using System.ComponentModel;
using KKAPI.Utilities;
using System.Collections;
using System.Diagnostics;

namespace TranslationCacheCleanerPlugin
{
    [BepInDependency(GeBoAPI.GUID, GeBoAPI.Version)]
    [BepInDependency(XUnity.AutoTranslator.Plugin.Core.Constants.PluginData.Identifier)]
    [BepInPlugin(GUID, PluginName, Version)]
    public partial class TranslationCacheCleaner
    {
        public const string GUID = "com.gebo.bepinex.translationcachecleaner";
        public const string PluginName = "Translation Cache Cleaner";
        public const string Version = "0.5.0";

        private const float notifySeconds = 10f;
        private const float yieldSeconds = 0.1f;

        internal static new ManualLogSource Logger;

        public static ConfigEntry<KeyboardShortcut> CleanCacheHotkey { get; private set; }

        private static bool cleaningActive = false;
        private string latestBackup = null;

        internal void Awake()
        {
            Logger = base.Logger;

            CleanCacheHotkey = Config.Bind("Keyboard Shortcuts", "Clean Cache Hotkey", new KeyboardShortcut(KeyCode.F6, KeyCode.LeftShift), "Pressing this will attempt to clean your autotranslation cache.");
        }

        internal void Update()
        {
            if (!cleaningActive && CleanCacheHotkey.Value.IsPressed())
            {
                try
                {
                    cleaningActive = true;
                    StartCoroutine(CoroutineUtils.ComposeCoroutine(
                        CleanTranslationCacheCoroutine(),
                        PostCleanupCoroutine(),
                        CoroutineUtils.CreateCoroutine(() => cleaningActive = false)));
                    //CleanTranslationCache();
                }
                finally
                {
                    //cleaningActive = false;
                }
            }
        }

        private string AutoTranslationsFilePath => GeBoAPI.Instance.AutoTranslationHelper.GetAutoTranslationsFilePath();

        private void ReloadTranslations()
        {
            GeBoAPI.Instance.AutoTranslationHelper.ReloadTranslations();
        }
        private static string GetWorkFileName(string path, string prefix, string extension)
        {
            string timestamp = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
            string result = string.Empty;
            for (int i = 0; string.IsNullOrEmpty(result) || File.Exists(result); i++)
            {
                result = Path.Combine(path, string.Join(".", new string[] { prefix, PluginName, timestamp, i.ToString(), extension }));
            }
            return result;
        }

        private static void MoveReplaceFile(string source, string destination)
        {
            string removeFile = null;
            if (File.Exists(destination))
            {
                removeFile = GetWorkFileName(Path.GetDirectoryName(destination), Path.GetFileName(destination), "remove");
                File.Move(destination, removeFile);
            }
            File.Move(source, destination);
            if (removeFile != null)
            {
                File.Delete(removeFile);
            }
        }

        public IEnumerator CleanTranslationCacheCoroutine()
        {
            var reloadCoroutine = CoroutineUtils.CreateCoroutine(() => { }, ReloadTranslations);
            float cutoff = Time.realtimeSinceStartup + yieldSeconds;
            float notifyTime = Time.realtimeSinceStartup + notifySeconds;
            Logger.LogMessage("Attempting to clean translation cache, please be patient...");
            var cache = GeBoAPI.Instance.AutoTranslationHelper.DefaultCache;

            if (cache == null)
            {
                Logger.LogError("Unable to access translation cache");
                yield break;
            }

            Dictionary<string, string> translations = GeBoAPI.Instance.AutoTranslationHelper.GetTranslations();
            Logger.LogWarning($"{translations} {translations?.Count}");
            List<Regex> regexes = new List<Regex>();

            HashSet<string> tmp = GeBoAPI.Instance.AutoTranslationHelper.GetRegisteredRegexes();//(HashSet<string>)cache?.GetType().GetField("_registeredRegexes", AccessTools.all)?.GetValue(cache);
            if (tmp != null)
            {
                regexes.AddRange(tmp.Select((s) => new Regex(s)));
            }
            tmp = GeBoAPI.Instance.AutoTranslationHelper.GetRegisteredSplitterRegexes();//(HashSet<string>)cache?.GetType().GetField("_registeredSplitterRegexes", AccessTools.all)?.GetValue(cache);
            if (tmp != null)
            {
                regexes.AddRange(tmp.Select((s) => new Regex(s)));
            }

            string newFile = GetWorkFileName(Path.GetDirectoryName(AutoTranslationsFilePath), Path.GetFileName(AutoTranslationsFilePath), "new");
            string backupFile = GetWorkFileName(Path.GetDirectoryName(AutoTranslationsFilePath), Path.GetFileName(AutoTranslationsFilePath), "bak");
            MoveReplaceFile(AutoTranslationsFilePath, backupFile);
            latestBackup = backupFile;
            Logger.LogInfo("Reloading translations without existing cache file");
            yield return StartCoroutine(reloadCoroutine);
            Logger.LogInfo("Reloading done");

            char[] splitter = { '=' };
            int changed = 0;
            using (var outStream = File.Open(newFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(outStream, Encoding.UTF8))
            using (var inStream = File.Open(backupFile, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(inStream, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var now = Time.realtimeSinceStartup;
                    if (now > notifyTime)
                    {
                        Logger.LogMessage("Cleaning translation cache...");
                        notifyTime = now + notifySeconds;
                    }
                    if (now > cutoff)
                    {
                        cutoff = now + yieldSeconds;
                        yield return null;
                    }
                    string[] parts = line.Split(splitter, StringSplitOptions.None);
                    if (parts.Length == 2 && !parts[0].StartsWith("//", StringComparison.InvariantCulture))
                    {
                        if (translations.ContainsKey(parts[0]))
                        {
                            Logger.LogInfo($"Removing cached line (static match): {line.TrimEnd()}");
                            changed++;
                            continue;
                        }
                        if (regexes.Any((r) => r.IsMatch(parts[0])))
                        {
                            Logger.LogInfo($"Removing cached line (regex match): {line.TrimEnd()}");
                            changed++;
                            continue;
                        }
                    }
                    writer.WriteLine(line);
                }
            }
            yield return null;
            if (changed > 0)
            {
                Logger.LogMessage($"Done. Removed {changed} entries from cache. Reloading translations.");
                MoveReplaceFile(newFile, AutoTranslationsFilePath);
            }
            else
            {
                Logger.LogMessage("Done. No changes made. Restoring/reloading translations");
                MoveReplaceFile(backupFile, AutoTranslationsFilePath);
            }
            latestBackup = null;
            yield return StartCoroutine(reloadCoroutine);
        }

        public IEnumerator PostCleanupCoroutine()
        {
            if (!latestBackup.IsNullOrWhiteSpace() && File.Exists(latestBackup))
            {
                Logger.LogWarning("Something unexpected happened. Restoring previous translation cache.");
                MoveReplaceFile(latestBackup, AutoTranslationsFilePath);
                yield return StartCoroutine(CoroutineUtils.CreateCoroutine(ReloadTranslations));
            }
        }

        public void CleanTranslationCache()
        {
            Logger.LogMessage("Attempting to clean translation cache, please be patient...");
            var cache = GeBoAPI.Instance.AutoTranslationHelper.DefaultCache;

            if (cache == null)
            {
                Logger.LogError("Unable to access translation cache");
                return;
            }

            Dictionary<string, string> translations = GeBoAPI.Instance.AutoTranslationHelper.GetTranslations();
            Logger.LogWarning($"{translations} {translations?.Count}");
            /*
            (Dictionary<string, string>)cache?.GetType().GetField("_translations", AccessTools.all)?.GetValue(cache) ??
            new Dictionary<string, string>();
            */
            List<Regex> regexes = new List<Regex>();

            HashSet<string> tmp = GeBoAPI.Instance.AutoTranslationHelper.GetRegisteredRegexes();//(HashSet<string>)cache?.GetType().GetField("_registeredRegexes", AccessTools.all)?.GetValue(cache);
            if (tmp != null)
            {
                regexes.AddRange(tmp.Select((s) => new Regex(s)));
            }
            tmp = GeBoAPI.Instance.AutoTranslationHelper.GetRegisteredSplitterRegexes();//(HashSet<string>)cache?.GetType().GetField("_registeredSplitterRegexes", AccessTools.all)?.GetValue(cache);
            if (tmp != null)
            {
                regexes.AddRange(tmp.Select((s) => new Regex(s)));
            }

            string newFile = GetWorkFileName(Path.GetDirectoryName(AutoTranslationsFilePath), Path.GetFileName(AutoTranslationsFilePath), "new");
            string backupFile = GetWorkFileName(Path.GetDirectoryName(AutoTranslationsFilePath), Path.GetFileName(AutoTranslationsFilePath), "bak");
            MoveReplaceFile(AutoTranslationsFilePath, backupFile);
            Logger.LogInfo("Reloading translations without existing cache file");
            ReloadTranslations();

            bool completed = false;

            try
            {
                char[] splitter = { '=' };
                int changed = 0;
                using (var outStream = File.Open(newFile, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(outStream, Encoding.UTF8))
                using (var inStream = File.Open(backupFile, FileMode.Open, FileAccess.Read))
                using (var reader = new StreamReader(inStream, Encoding.UTF8))
                {
                    Logger.LogWarning($"{outStream}, {writer}, {inStream}, {reader}");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split(splitter, StringSplitOptions.None);
                        if (parts.Length == 2 && !parts[0].StartsWith("//", StringComparison.InvariantCulture))
                        {
                            if (translations.ContainsKey(parts[0]))
                            {
                                Logger.LogInfo($"Removing cached line (static match): {line.TrimEnd()}");
                                changed++;
                                continue;
                            }
                            if (regexes.Any((r) => r.IsMatch(parts[0])))
                            {
                                Logger.LogInfo($"Removing cached line (regex match): {line.TrimEnd()}");
                                changed++;
                                continue;
                            }
                        }
                        writer.WriteLine(line);
                    }
                }

                if (changed > 0)
                {
                    Logger.LogMessage($"Done. Removed {changed} entries from cache. Reloading translations.");
                    MoveReplaceFile(newFile, AutoTranslationsFilePath);
                }
                else
                {
                    Logger.LogMessage("Done. No changes made. Restoring/reloading translations");
                    MoveReplaceFile(backupFile, AutoTranslationsFilePath);
                }
                ReloadTranslations();
                completed = true;
            }
            catch (Exception e)
            {
                Logger.LogFatal(e.Message);
                Logger.LogError(e.StackTrace);
                throw;
            }
            finally
            {
                if (!completed)
                {
                    if (File.Exists(backupFile))
                    {
                        Logger.LogWarning("Something unexpected happened. Restoring previous translation cache.");
                        MoveReplaceFile(backupFile, AutoTranslationsFilePath);
                        ReloadTranslations();
                    }
                }
            }
        }
    }
}
