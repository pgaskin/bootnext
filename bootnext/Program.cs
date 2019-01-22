using System;
using System.Linq;
using System.Windows.Forms;

namespace bootnext {
    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BootNextTray());
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
