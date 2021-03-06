﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace efi {
    [Flags]
    enum ExitWindows : uint { Reboot = 0x02 }

    [Flags]
    enum ShutdownReason : uint {
        MajorOther = 0x00000000,
        MinorOther = 0x00000000,
        FlagPlanned = 0x80000000
    }

    class NativeMethods {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern uint GetFirmwareEnvironmentVariable([MarshalAs(UnmanagedType.LPWStr)] string lpName, [MarshalAs(UnmanagedType.LPWStr)] string lpGuid, byte[] pBuffer, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetFirmwareEnvironmentVariable([MarshalAs(UnmanagedType.LPWStr)] string lpName, [MarshalAs(UnmanagedType.LPWStr)] string lpGuid, byte[] pValue, uint nSize);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref PrivilegeHelper.TokenPrivelege newst, int len, IntPtr prev, IntPtr relen);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ExitWindowsEx(ExitWindows uFlags, ShutdownReason dwReason);
    }

    class NoBootNextException : Exception { }

    class EFIEnvironment {
        private const string EFI_GLOBAL_VARIABLE = "{8BE4DF61-93CA-11D2-AA0D-00E098032B8C}";
        private const string EFI_TEST_VARIABLE = "{00000000-0000-0000-0000-000000000000}";
        private const string EFI_BOOT_ORDER = "BootOrder";
        private const string EFI_BOOT_CURRENT = "BootCurrent";
        private const string EFI_BOOT_NEXT = "BootNext";
        private const string LOAD_OPTION_FORMAT = "Boot{0:X4}";

        public static bool IsSupported() {
            if (NativeMethods.GetFirmwareEnvironmentVariable(string.Empty, EFI_TEST_VARIABLE, null, 0) == 0) return Marshal.GetLastWin32Error() == 998;
            return true;
        }

        public static uint ReadVariable(string name, out byte[] buffer) {
            buffer = new byte[10000];
            return NativeMethods.GetFirmwareEnvironmentVariable(name, EFI_GLOBAL_VARIABLE, buffer, (uint)buffer.Length);
        }

        public static BootEntry[] GetEntries() {
            var length = (int)ReadVariable(EFI_BOOT_ORDER, out var buffer);
            var entries = new List<BootEntry>();
            ushort currentEntryId = GetBootCurrent();
            bool hasBootNext = false;
            ushort nextEntryId = 0;
            try {
                nextEntryId = GetBootNext();
                hasBootNext = true;
            } catch (NoBootNextException) { }
            return Enumerable.Range(0, length / 2).Select(x => BitConverter.ToUInt16(buffer, x * 2)).Select(optionId => new BootEntry() {
                Id = optionId,
                Description = GetDescription(optionId),
                IsCurrent = currentEntryId == optionId,
                IsBootNext = hasBootNext ? nextEntryId == optionId : optionId == BitConverter.ToUInt16(buffer, 0), // Is BootNext or if none, is default
            }).ToArray();
        }

        public static string GetDescription(ushort optionId) {
            var length = (int)ReadVariable(string.Format(LOAD_OPTION_FORMAT, optionId), out var buffer);
            return new string(Encoding.Unicode.GetString(buffer, 6, length).TakeWhile(x => x != 0).ToArray());
        }

        public static ushort GetBootCurrent() {
            uint length = ReadVariable(EFI_BOOT_CURRENT, out var buffer);
            if (length == 0) throw new Exception("Error" + Marshal.GetLastWin32Error());
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static ushort GetBootNext() {
            uint length = ReadVariable(EFI_BOOT_NEXT, out var buffer);
            if (length == 0) {
                var err = Marshal.GetLastWin32Error();
                throw err == 203 ? new NoBootNextException() : new Exception("Error" + err);
            }
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static void SetVariable(string name, byte[] buffer) {
            bool result = NativeMethods.SetFirmwareEnvironmentVariable(name, EFI_GLOBAL_VARIABLE, buffer, (uint)buffer.Length);
            if (!result) throw new InvalidOperationException("Unable to set variable");
        }

        public static void SetBootNext(ushort entryId) {
            SetVariable(EFI_BOOT_NEXT, BitConverter.GetBytes(entryId));
        }

        public static void SetBootNext(BootEntry entry) {
            SetBootNext(entry.Id);
        }
    }

    class BootEntry {
        public ushort Id;
        public string Description;
        public bool IsCurrent;
        public bool IsBootNext;
    }

    class PrivilegeHelper {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TokenPrivelege {
            public int Count;
            public long Luid;
            public int Attr;
        }

        private const string SE_SYSTEM_ENVIRONMENT_NAME = "SeSystemEnvironmentPrivilege";
        private const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        internal const int SE_PRIVILEGE_ENABLED = 0x00000002;
        internal const int TOKEN_QUERY = 0x00000008;
        internal const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;

        public static void ObtainPrivileges(string privilege) {
            var hToken = IntPtr.Zero;
            if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref hToken)) throw new InvalidOperationException("OpenProcessToken failed!");

            var tp = new TokenPrivelege { Count = 1, Luid = 0, Attr = SE_PRIVILEGE_ENABLED };
            if (!NativeMethods.LookupPrivilegeValue(null, privilege, ref tp.Luid)) throw new InvalidOperationException("LookupPrivilegeValue failed!");
            if (!NativeMethods.AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)) throw new InvalidOperationException("AdjustTokenPrivileges failed!");
        }

        public static void ObtainSystemPrivileges() {
            ObtainPrivileges(SE_SYSTEM_ENVIRONMENT_NAME);
            ObtainPrivileges(SE_SHUTDOWN_NAME);
        }
    }
}
