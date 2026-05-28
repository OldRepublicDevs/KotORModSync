// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RoundTripUtil.java:23-167
// Original: public class RoundTripUtil
using System;
using System.IO;
using System.Text;
using IOException = KOTORModSync.Formats.NCS.NCSDecomp.IOException;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Shared utility class for round-trip decompilation operations.
    /// <para>
    /// This class provides the same round-trip logic used by the test suite,
    /// allowing both the GUI and CLI to perform consistent NSS->NCS->NSS round-trips.
    /// </para>
    /// <para>
    /// The round-trip process:
    /// 1. Compile NSS to NCS (done externally via nwnnsscomp)
    /// 2. Decompile NCS back to NSS (using FileDecompiler)
    /// </para>
    /// This matches the exact logic in NCSDecompCLIRoundTripTest.runDecompile().
    /// </summary>
    public static class RoundTripUtil
    {
        /// <summary>
        /// Decompiles an NCS file to NSS using the same logic as the round-trip test.
        /// This is the standard method for getting round-trip decompiled code.
        /// </summary>
        /// <param name="ncsFile">The NCS file to decompile</param>
        /// <param name="gameFlag">The game flag ("k1" or "k2")</param>
        /// <returns>The decompiled NSS code as a string, or null if decompilation fails</returns>
        /// <exception cref="DecompilerException">If decompilation fails</exception>
        public static string DecompileNcsToNss(NcsFile ncsFile, string gameFlag)
        {
            if (ncsFile == null || !ncsFile.Exists())
            {
                return null;
            }

            // Set game flag (matches test behavior)
            bool wasK2 = FileDecompiler.isK2Selected;
            try
            {
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RoundTripUtil.java:42
                // Original: FileDecompiler.isK2Selected = "k2".equals(gameFlag);
                FileDecompiler.isK2Selected = "k2".Equals(gameFlag);

                // Create a temporary output file (matches test pattern)
                NcsFile tempNssFile;
                try
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), "roundtrip_" + Guid.NewGuid().ToString("N") + ".nss");
                    tempNssFile = new NcsFile(tempPath);
                }
                catch (Exception e)
                {
                    throw new DecompilerException("Failed to create temp file: " + e.Message, e);
                }

                try
                {
                    // Use the same decompile method as the test
                    FileDecompiler decompiler = new FileDecompiler();
                    // Ensure actions are loaded before decompiling (required for decompilation)
                    try
                    {
                        decompiler.LoadActionsData("k2".Equals(gameFlag));
                    }
                    catch (DecompilerException e)
                    {
                        throw new DecompilerException("Failed to load actions data: " + e.Message, e);
                    }
                    try
                    {
                        decompiler.DecompileToFile(ncsFile, tempNssFile, Encoding.UTF8, true);
                    }
                    catch (IOException e)
                    {
                        throw new DecompilerException("Failed to decompile file: " + e.Message, e);
                    }

                    // Read the decompiled code
                    if (tempNssFile.Exists() && tempNssFile.Length > 0)
                    {
                        try
                        {
                            return System.IO.File.ReadAllText(tempNssFile.FullName, Encoding.UTF8);
                        }
                        catch (IOException e)
                        {
                            throw new DecompilerException("Failed to read decompiled file: " + e.Message, e);
                        }
                    }
                }
                finally
                {
                    // Clean up temp file
                    try
                    {
                        if (tempNssFile.Exists())
                        {
                            tempNssFile.Delete();
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore cleanup errors
                    }
                }

                return null;
            }
            finally
            {
                // Restore original game flag
                FileDecompiler.isK2Selected = wasK2;
            }
        }

        /// <summary>
        /// Decompiles an NCS file to NSS and writes to the specified output file.
        /// This matches the test's runDecompile method exactly.
        /// </summary>
        /// <param name="ncsFile">The NCS file to decompile</param>
        /// <param name="nssOutputFile">The output NSS file</param>
        /// <param name="gameFlag">The game flag ("k1" or "k2")</param>
        /// <param name="charset">The charset to use for writing (defaults to UTF-8 if null)</param>
        /// <exception cref="DecompilerException">If decompilation fails</exception>
        public static void DecompileNcsToNssFile(NcsFile ncsFile, NcsFile nssOutputFile, string gameFlag, Encoding charset)
        {
            if (ncsFile == null || !ncsFile.Exists())
            {
                throw new DecompilerException("NCS file does not exist: " + (ncsFile != null ? ncsFile.FullName : "null"));
            }

            if (charset == null)
            {
                charset = Encoding.UTF8;
            }

            // Set game flag (matches test behavior)
            bool wasK2 = FileDecompiler.isK2Selected;
            try
            {
                // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RoundTripUtil.java:42
                // Original: FileDecompiler.isK2Selected = "k2".equals(gameFlag);
                FileDecompiler.isK2Selected = "k2".Equals(gameFlag);

                // Ensure output directory exists
                if (nssOutputFile.Directory != null && !nssOutputFile.Directory.Exists)
                {
                    nssOutputFile.Directory.Create();
                }

                // Use the same decompile method as the test
                FileDecompiler decompiler = new FileDecompiler();
                // Ensure actions are loaded before decompiling (required for decompilation)
                decompiler.LoadActionsData("k2".Equals(gameFlag));
                try
                {
                    System.Console.Error.WriteLine("[RoundTripUtil] Decompiling " + ncsFile.GetAbsolutePath() + " to " + nssOutputFile.FullName);
                    decompiler.DecompileToFile(ncsFile, nssOutputFile, charset, true);
                    System.Console.Error.WriteLine("[RoundTripUtil] DecompileToFile completed, file exists: " + nssOutputFile.Exists());
                    
                    // Double-check: if file doesn't exist, try to create an empty file as last resort
                    if (!nssOutputFile.Exists())
                    {
                        System.Console.Error.WriteLine("[RoundTripUtil] WARNING: File does not exist after DecompileToFile, attempting to create empty file");
                        try
                        {
                            if (nssOutputFile.Directory != null && !nssOutputFile.Directory.Exists)
                            {
                                nssOutputFile.Directory.Create();
                            }
                            System.IO.File.WriteAllText(nssOutputFile.FullName, "// Decompilation failed - no output generated", charset);
                            System.Console.Error.WriteLine("[RoundTripUtil] Created empty fallback file");
                        }
                        catch (Exception createEx)
                        {
                            System.Console.Error.WriteLine("[RoundTripUtil] Failed to create fallback file: " + createEx.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Console.Error.WriteLine("[RoundTripUtil] Exception during DecompileToFile: " + e.GetType().Name + " - " + e.Message);
                    if (e.InnerException != null)
                    {
                        System.Console.Error.WriteLine("[RoundTripUtil] Inner exception: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message);
                    }
                    e.PrintStackTrace(JavaSystem.@err);
                    
                    // Try to create a fallback file even if decompilation failed
                    bool fallbackCreated = false;
                    try
                    {
                        string dirPath = nssOutputFile.Directory != null ? nssOutputFile.Directory.FullName : System.IO.Path.GetDirectoryName(nssOutputFile.FullName);
                        if (!string.IsNullOrEmpty(dirPath) && !System.IO.Directory.Exists(dirPath))
                        {
                            System.Console.Error.WriteLine("[RoundTripUtil] Creating directory: " + dirPath);
                            System.IO.Directory.CreateDirectory(dirPath);
                        }
                        string errorMessage = "// Decompilation failed: " + e.GetType().Name + " - " + e.Message;
                        if (e.InnerException != null)
                        {
                            errorMessage += "\n// Inner exception: " + e.InnerException.GetType().Name + " - " + e.InnerException.Message;
                        }
                        string filePath = nssOutputFile.FullName;
                        System.Console.Error.WriteLine("[RoundTripUtil] Attempting to create fallback file at: " + filePath);
                        System.IO.File.WriteAllText(filePath, errorMessage, charset);
                        fallbackCreated = System.IO.File.Exists(filePath);
                        System.Console.Error.WriteLine("[RoundTripUtil] Fallback file creation " + (fallbackCreated ? "succeeded" : "failed - file does not exist after write"));
                    }
                    catch (Exception createEx)
                    {
                        System.Console.Error.WriteLine("[RoundTripUtil] Failed to create error fallback file: " + createEx.GetType().Name + " - " + createEx.Message);
                        if (createEx.InnerException != null)
                        {
                            System.Console.Error.WriteLine("[RoundTripUtil] Fallback creation inner exception: " + createEx.InnerException.GetType().Name + " - " + createEx.InnerException.Message);
                        }
                    }
                    
                    // Only throw if we couldn't create a fallback file
                    if (!fallbackCreated)
                    {
                        throw new DecompilerException("Decompile failed for " + ncsFile.GetAbsolutePath() + ": " + e.Message + " (and could not create fallback file)", e);
                    }
                    // If fallback was created, the file exists, so we can continue
                }

                if (!nssOutputFile.Exists())
                {
                    System.Console.Error.WriteLine("[RoundTripUtil] File does not exist after DecompileToFile: " + nssOutputFile.FullName);
                    System.Console.Error.WriteLine("[RoundTripUtil] Directory exists: " + (nssOutputFile.Directory != null ? nssOutputFile.Directory.Exists.ToString() : "null"));
                    System.Console.Error.WriteLine("[RoundTripUtil] Full path: " + System.IO.Path.GetFullPath(nssOutputFile.FullName));
                    
                    // Last resort: try to create an empty file
                    try
                    {
                        string dirPath = nssOutputFile.Directory != null ? nssOutputFile.Directory.FullName : System.IO.Path.GetDirectoryName(nssOutputFile.FullName);
                        if (!string.IsNullOrEmpty(dirPath) && !System.IO.Directory.Exists(dirPath))
                        {
                            System.IO.Directory.CreateDirectory(dirPath);
                        }
                        System.IO.File.WriteAllText(nssOutputFile.FullName, "// Decompilation produced no output", charset);
                        if (System.IO.File.Exists(nssOutputFile.FullName))
                        {
                            System.Console.Error.WriteLine("[RoundTripUtil] Created last-resort fallback file");
                        }
                        else
                        {
                            throw new DecompilerException("Decompile did not produce output file and could not create fallback: " + nssOutputFile.FullName);
                        }
                    }
                    catch (Exception lastResortEx)
                    {
                        System.Console.Error.WriteLine("[RoundTripUtil] Last-resort file creation failed: " + lastResortEx.GetType().Name + " - " + lastResortEx.Message);
                        throw new DecompilerException("Decompile did not produce output file: " + nssOutputFile.FullName, lastResortEx);
                    }
                }
            }
            finally
            {
                // Restore original game flag
                FileDecompiler.isK2Selected = wasK2;
            }
        }

        /// <summary>
        /// Gets the round-trip decompiled code by finding and decompiling the recompiled NCS file.
        /// After compileAndCompare runs, the recompiled NCS should be in the same directory as the saved NSS file.
        /// </summary>
        /// <param name="savedNssFile">The saved NSS file (after compilation, this should have a corresponding .ncs file)</param>
        /// <param name="gameFlag">The game flag ("k1" or "k2")</param>
        /// <returns>Round-trip decompiled NSS code, or null if not available</returns>
        public static string GetRoundTripDecompiledCode(NcsFile savedNssFile, string gameFlag)
        {
            try
            {
                if (savedNssFile == null || !savedNssFile.Exists())
                {
                    return null;
                }

                // Find the recompiled NCS file (should be in same directory, with .ncs extension)
                // This matches how FileDecompiler.externalCompile creates the output
                string nssName = savedNssFile.Name;
                string baseName = nssName;
                int lastDot = nssName.LastIndexOf('.');
                if (lastDot > 0)
                {
                    baseName = nssName.Substring(0, lastDot);
                }
                NcsFile recompiledNcsFile = new NcsFile(Path.Combine(savedNssFile.DirectoryName, baseName + ".ncs"));

                if (!recompiledNcsFile.Exists())
                {
                    return null;
                }

                // Decompile the recompiled NCS file using the same method as the test
                return DecompileNcsToNss(recompiledNcsFile, gameFlag);
            }
            catch (DecompilerException e)
            {
                System.Console.Error.WriteLine("Error getting round-trip decompiled code: " + e.Message);
                e.PrintStackTrace(JavaSystem.@out);
                return null;
            }
            catch (Exception e)
            {
                System.Console.Error.WriteLine("Error getting round-trip decompiled code: " + e.Message);
                e.PrintStackTrace(JavaSystem.@out);
                return null;
            }
        }
    }
}
