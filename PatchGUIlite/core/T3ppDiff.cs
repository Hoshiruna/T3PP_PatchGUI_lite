using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PatchGUIlite.Core
{
    public static class T3ppDiff
    {
        // ---------------------
        // P/Invoke bindings
        // ---------------------

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate void NativeLogCb(int level, string msg);

        private static class Native
        {
#if DEBUG
            private const string DllName = "T3ppNativelite_Debug_x64.dll";
#else
            private const string DllName = "T3ppNativelite_Release_x64.dll";
#endif

            static Native()
            {
                NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, ResolveNativeDll);
            }

            private static IntPtr ResolveNativeDll(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
            {
                if (!string.Equals(libraryName, DllName, StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                string? processPath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    string? processDir = Path.GetDirectoryName(processPath);
                    if (!string.IsNullOrWhiteSpace(processDir))
                    {
                        string nativePath = Path.Combine(processDir, DllName);
                        if (File.Exists(nativePath))
                            return NativeLibrary.Load(nativePath);
                    }
                }

                return IntPtr.Zero;
            }

            [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
            internal static extern int t3pp_apply_patch_from_file(
                string patch_file,
                string target_root,
                NativeLogCb? logger,
                HashMismatchNativeCb? mismatchCb,
                int dry_run);

            [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
            internal static extern int t3pp_create_patch_from_dirs(
                string old_dir,
                string new_dir,
                string output_file,
                NativeLogCb? logger);
        }

        public static Action<string>? DebugLog;

        // ---------------------
        // Apply patch
        // ---------------------
        public static int ApplyPatchToDirectory(
            string patchFile,
            string targetRoot,
            Action<string>? logger,
            bool dryRun,
            Action<string, string>? hashMismatchHandler = null)
        {
            if (string.IsNullOrWhiteSpace(patchFile))
                throw new ArgumentNullException(nameof(patchFile));
            if (string.IsNullOrWhiteSpace(targetRoot))
                throw new ArgumentNullException(nameof(targetRoot));

            NativeLogCb? cb = null;
            HashMismatchNativeCb? mismatchCb = null;

            if (logger != null || DebugLog != null)
            {
                cb = (level, msg) =>
                {
                    try
                    {
                        string prefix = level switch
                        {
                            0 => "[INFO] ",
                            1 => "[WARN] ",
                            _ => "[ERROR] "
                        };
                        if (logger != null) logger($"{prefix}{msg}");
                        else DebugLog?.Invoke($"{prefix}{msg}");
                    }
                    catch
                    {
                        // Swallow logging errors to keep native call unaffected.
                    }
                };
            }

            if (hashMismatchHandler != null)
            {
                HashUtility.SetHashMismatchHandler(hashMismatchHandler);
                mismatchCb = HashUtility.GetNativeMismatchCallback();
            }

            int rc = Native.t3pp_apply_patch_from_file(
                patchFile,
                targetRoot,
                cb,
                mismatchCb,
                dryRun ? 1 : 0);

            if (hashMismatchHandler != null)
            {
                HashUtility.SetHashMismatchHandler(null);
            }

            return rc;
        }

        // ---------------------
        // Create diff
        // ---------------------
        public static void CreateDirectoryDiff(
            string oldDir,
            string newDir,
            string outputFile)
        {
            if (string.IsNullOrWhiteSpace(oldDir))
                throw new ArgumentNullException(nameof(oldDir));
            if (string.IsNullOrWhiteSpace(newDir))
                throw new ArgumentNullException(nameof(newDir));
            if (string.IsNullOrWhiteSpace(outputFile))
                throw new ArgumentNullException(nameof(outputFile));

            NativeLogCb? cb = null;
            if (DebugLog != null)
            {
                cb = (level, msg) =>
                {
                    try
                    {
                        DebugLog?.Invoke($"[NATIVE-{level}] {msg}");
                    }
                    catch { }
                };
            }

            int rc = Native.t3pp_create_patch_from_dirs(
                oldDir,
                newDir,
                outputFile,
                cb);

            if (rc == 1)
            {
                // 1 = no changes found
                throw new InvalidOperationException("No file changes were found; no patch was generated.");
            }

            if (rc != 0)
            {
                throw new InvalidOperationException($"Patch generation failed with native error code: {rc}");
            }
        }
    }
}
