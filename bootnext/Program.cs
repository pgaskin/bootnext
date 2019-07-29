using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace bootnext {
    static class Program {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        [STAThread]
        static int Main(string[] args) {
            bool console = args.Length > 0;
            try { Init(); } catch (Exception ex) {
                if (console)
                    Console.WriteLine("Error: {0}", ex.ToString());
                else
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            return console ? MainConsole(args) : MainUI();
        }

        static void Init() {
            efi.PrivilegeHelper.ObtainSystemPrivileges();
            if (!efi.EFIEnvironment.IsSupported())
                throw new Exception("EFI not supported");
        }

        static int MainUI() {
            FreeConsole();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BootNextTray());
            return 0;
        }

        static int MainConsole(string[] args) {
            try {
                switch ((args[0] ?? "").ToUpper()) {
                    case "/INSTALL":
                        Console.WriteLine("Installing task");
                        new BootNextTask().Install();
                        return 0;
                    case "/UNINSTALL":
                        Console.WriteLine("Removing task");
                        new BootNextTask().Uninstall();
                        return 0;
                    case "/LIST":
                        foreach (efi.BootEntry entry in efi.EFIEnvironment.GetEntries())
                            Console.WriteLine("{0:X4} {1,-7} {2,4} {3}", entry.Id, entry.IsCurrent ? "CURRENT" : "", entry.IsBootNext ? "NEXT" : "", entry.Description);
                        return 0;
                    default:
                        Console.WriteLine("bootnext [/? | /INSTALL | /UNINSTALL | /LIST]");
                        return 1;
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
