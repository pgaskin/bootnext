using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32.TaskScheduler;

namespace bootnext {
    static class Program {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        [STAThread]
        static int Main(string[] args) {
            return args.Length > 0 ? MainConsole(args) : MainUI();
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
                    default:
                        Console.WriteLine("bootnext [/? | /INSTALL | /UNINSTALL]");
                        return 1;
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }
    }

    class BootNextTask {
        private TaskService svc = new TaskService();

        public void Uninstall() {
            using (var task = svc.GetTask(@"\bootnext"))
                task?.Stop();
            svc.RootFolder.DeleteTask("bootnext", false);
        }

        public void Install(bool run = true) {
            Uninstall();

            using (TaskDefinition task = svc.NewTask()) {
                task.RegistrationInfo.Description = "Starts the bootnext tray icon on login";
                task.RegistrationInfo.Version = Assembly.GetExecutingAssembly().GetName().Version;

                task.Principal.GroupId = "S-1-5-32-544"; // Administrators
                task.Principal.RunLevel = TaskRunLevel.Highest;
                task.Triggers.Add(new LogonTrigger());
                task.Actions.Add(new ExecAction(Assembly.GetEntryAssembly().Location));

                task.Settings.Hidden = false;
                task.Settings.AllowDemandStart = true;
                task.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                task.Settings.MultipleInstances = TaskInstancesPolicy.StopExisting;

                task.Settings.DisallowStartIfOnBatteries = false;
                task.Settings.StopIfGoingOnBatteries = false;
                task.Settings.AllowHardTerminate = false;
                task.Settings.StartWhenAvailable = false;
                task.Settings.RunOnlyIfNetworkAvailable = false;
                task.Settings.WakeToRun = false;

                svc.RootFolder.RegisterTaskDefinition(@"bootnext", task);
            }

            if (run)
                using (var task = svc.GetTask(@"\bootnext"))
                    task?.Run();
        }
    }

    class BootNextTray : ApplicationContext {
        private NotifyIcon trayIcon;

        public BootNextTray() {
            try {
                efi.PrivilegeHelper.ObtainSystemPrivileges();
            } catch (Exception ex) {
                MessageBox.Show("Error obtaining privileges: " + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!efi.EFIEnvironment.IsSupported()) {
                MessageBox.Show("UEFI not supported", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            foreach (efi.BootEntry entry in efi.EFIEnvironment.GetEntries()) {
                Console.WriteLine("{0:X4} {1}{2}{3}", entry.Id, entry.IsCurrent ? "CURRENT " : "        ", entry.IsBootNext ? "NEXT " : "     ", entry.Description);
            }

            trayIcon = new NotifyIcon() {
                Icon = Environment.OSVersion.ToString().Contains(" 10") ? Properties.Resources.AppIconWhite : Properties.Resources.AppIconColor,
                Visible = true,
                ContextMenu = new ContextMenu(efi.EFIEnvironment.GetEntries().Select(entry => new MenuItem(entry.Description + (entry.IsCurrent ? " (current)" : ""), (sender, e) => {
                    try {
                        efi.EFIEnvironment.SetBootNext(entry);
                    } catch (Exception ex) {
                        trayIcon.ShowBalloonTip(3000, "Error changing BootNext", ex.ToString(), ToolTipIcon.Error);
                        return;
                    }
                    foreach (MenuItem i in trayIcon.ContextMenu.MenuItems) i.Checked = false;
                    ((MenuItem)sender).Checked = true;
                    trayIcon.ShowBalloonTip(3000, "Changed BootNext", entry.Description, ToolTipIcon.Info);
                }) {
                    Checked = entry.IsBootNext,
                }).Concat(new MenuItem[] {
                    new MenuItem("Restart", (sender, e) => {
                        if (MessageBox.Show("Are you sure you want to restart?", "Restart", MessageBoxButtons.YesNoCancel).Equals(DialogResult.Yes)) {
                            if (!efi.NativeMethods.ExitWindowsEx(efi.ExitWindows.Reboot, efi.ShutdownReason.MajorOther | efi.ShutdownReason.MinorOther | efi.ShutdownReason.FlagPlanned)) {
                                trayIcon.ShowBalloonTip(3000, "Shutdown failed", "Unknown error.", ToolTipIcon.Error);
                            }
                        }
                    }) {
                        BarBreak = true,
                    },
                    new MenuItem("Exit", (sender, e) => {
                        trayIcon.Visible = false;
                        Application.Exit();
                    }),
                }).ToArray()),
            };
        }
    }
}
