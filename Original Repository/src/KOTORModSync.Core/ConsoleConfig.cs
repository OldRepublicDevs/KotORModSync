// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Drawing;
using System.Runtime.InteropServices;

using KOTORModSync.Core.Utility;
namespace KOTORModSync.Core
{
    public partial class ConsoleConfig
    {
        private const uint ENABLE_QUICK_EDIT = 0x0040;
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetStdHandle")]
        private static extern IntPtr GetStdHandleNative(int nStdHandle);
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetConsoleMode")]
        private static extern bool GetConsoleModeNative(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetConsoleMode")]
        private static extern bool SetConsoleModeNative(IntPtr hConsoleHandle, uint dwMode);
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetConsoleCtrlHandler")]
        private static extern bool SetConsoleCtrlHandlerNative(HandlerRoutine Handler, bool Add);
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        public static IntPtr GetStdHandle(int nStdHandle)
        {
            if (UtilityHelper.GetOperatingSystem() != OSPlatform.Windows)
            {
                Logger.LogWarning("Attempted to get standard handle on a non-Windows platform.");
                return IntPtr.Zero;
            }

            IntPtr handle = GetStdHandleNative(nStdHandle);
            if (handle == IntPtr.Zero || handle == InvalidHandleValue)
            {
                Logger.LogError($"Failed to retrieve console handle for descriptor {nStdHandle}.");
                return IntPtr.Zero;
            }

            return handle;
        }

        public static bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode)
        {
            lpMode = 0;
            if (UtilityHelper.GetOperatingSystem() != OSPlatform.Windows)
            {
                Logger.LogWarning("Attempted to get console mode on a non-Windows platform.");
                return false;
            }

            if (hConsoleHandle == IntPtr.Zero || hConsoleHandle == InvalidHandleValue)
            {
                Logger.LogError("Invalid console handle passed to GetConsoleMode.");
                return false;
            }

            return GetConsoleModeNative(hConsoleHandle, out lpMode);
        }

        public static bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode)
        {
            if (UtilityHelper.GetOperatingSystem() != OSPlatform.Windows)
            {
                Logger.LogWarning("Attempted to set console mode on a non-Windows platform.");
                return false;
            }

            if (hConsoleHandle == IntPtr.Zero || hConsoleHandle == InvalidHandleValue)
            {
                Logger.LogError("Invalid console handle passed to SetConsoleMode.");
                return false;
            }

            bool success = SetConsoleModeNative(hConsoleHandle, dwMode);
            if (!success)
            {
                Logger.LogError("SetConsoleMode call failed.");
            }

            return success;
        }

        public static bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add)
        {
            if (UtilityHelper.GetOperatingSystem() != OSPlatform.Windows)
            {
                Logger.LogWarning("Attempted to modify console control handler on a non-Windows platform.");
                return false;
            }

            if (Handler is null)
            {
                throw new ArgumentNullException(nameof(Handler));
            }

            bool success = SetConsoleCtrlHandlerNative(Handler, Add);
            if (!success)
            {
                Logger.LogError("SetConsoleCtrlHandler call failed.");
            }

            return success;
        }
        public enum CtrlTypes
        {
            CTRL_CLOSE_EVENT = 2,
        }
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);
        private const int MF_BYCOMMAND = 0x00000000;
        public const int SC_CLOSE = 0xF060;
        [DllImport("user32.dll", EntryPoint = "DeleteMenu")]
        private static extern int DeleteMenuNative(IntPtr hMenu, int nPosition, int wFlags);
        private static int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags)
        {
            if (UtilityHelper.GetOperatingSystem() != OSPlatform.Windows)
            {
                Logger.LogWarning("Attempted to delete a system menu on a non-Windows platform.");
                return 0;
            }

            return DeleteMenuNative(hMenu, nPosition, wFlags);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        public static void DisableConsoleCloseButton() => DeleteMenu(GetSystemMenu(GetConsoleWindow(), bRevert: false), SC_CLOSE, MF_BYCOMMAND);
        public static void EnableCloseButton()
        {
            _ = GetSystemMenu(GetConsoleWindow(), bRevert: true);
            IntPtr systemMenuHandle = GetSystemMenu(GetConsoleWindow(), bRevert: false);
            if (systemMenuHandle == IntPtr.Zero)
            {
                Logger.LogWarning("Unable to retrieve system menu handle while enabling console close button.");
            }
        }
        public static void DisableQuickEdit()
        {
            try
            {
                IntPtr consoleHandle = GetStdHandle(-10);
                if (!GetConsoleMode(consoleHandle, out uint consoleMode))
                {
                    Logger.LogWarning("Could not get current console mode. You can ignore this warning if you're piping your terminal to something other than cmd.exe and powershell.");
                    return;
                }
                consoleMode &= ~ENABLE_QUICK_EDIT;
                if (!SetConsoleMode(consoleHandle, consoleMode))
                {
                    Logger.LogError("Could not set console mode on console handle");
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct ConsoleScreenBufferInfoEx
        {
            public uint CbSize;
            public ConsoleCoordinate DwSize;
            public ConsoleCoordinate DwCursorPosition;
            public ushort Attributes;
            public SmallRect Window;
            public ConsoleCoordinate DwMaximumWindowSize;
            public ushort PopupAttributes;
            public bool FullscreenSupported;
            public ColorRef Black;
            public ColorRef DarkBlue;
            public ColorRef DarkGreen;
            public ColorRef DarkCyan;
            public ColorRef DarkRed;
            public ColorRef DarkMagenta;
            public ColorRef DarkYellow;
            public ColorRef Gray;
            public ColorRef DarkGray;
            public ColorRef Blue;
            public ColorRef Green;
            public ColorRef Cyan;
            public ColorRef Red;
            public ColorRef Magenta;
            public ColorRef Yellow;
            public ColorRef White;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct ConsoleCoordinate
        {
            public short X;
            public short Y;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct SmallRect
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct ColorRef
        {
            public uint ColorDword;
            public ColorRef(Color color) =>
                ColorDword = color.R + ((uint)color.G << 8) + ((uint)color.B << 16);
            public Color GetSystemColor() =>
                Color.FromArgb(
                    (int)(0x000000FFU & ColorDword),
                    (int)(0x0000FF00U & ColorDword) >> 8,
                    (int)(0x00FF0000U & ColorDword) >> 16
                );
        }
    }
}
