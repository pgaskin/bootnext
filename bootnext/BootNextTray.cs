using System;
using System.Linq;
using System.Windows.Forms;

namespace bootnext {
    class BootNextTray : ApplicationContext {
        private readonly NotifyIcon trayIcon;

        public BootNextTray() {
            trayIcon = new NotifyIcon() {
                Icon = Environment.OSVersion.ToString().Contains(" 10") ? Properties.Resources.AppIconWhite : Properties.Resources.AppIconColor,
                Visible = true,
                ContextMenu = new ContextMenu(efi.EFIEnvironment.GetEntries().Select(entry =>
                    new MenuItem(entry.Description + (entry.IsCurrent ? " (current)" : ""), BootNext) { Checked = entry.IsBootNext, Tag = entry.Id }
                ).Concat(new MenuItem[] {
                    new MenuItem("Restart", Restart) { BarBreak = true },
                    new MenuItem("Exit", Exit),
                }).ToArray()),
            };
        }

        void BootNext(object sender, EventArgs e) {
            ushort id = (sender as MenuItem)?.Tag as ushort? ?? throw new Exception("sender tag not set");
            try {
                efi.EFIEnvironment.SetBootNext(id);
            } catch (Exception ex) {
                trayIcon.ShowBalloonTip(3000, "Error changing BootNext", ex.ToString(), ToolTipIcon.Error);
                return;
            }
            foreach (MenuItem other in trayIcon.ContextMenu.MenuItems)
                other.Checked = (other.Tag as ushort?) == id;
        }

        void Restart(object sender, EventArgs e) {
            if (MessageBox.Show("Are you sure you want to restart?", "Restart", MessageBoxButtons.YesNoCancel).Equals(DialogResult.Yes))
                if (!efi.NativeMethods.ExitWindowsEx(efi.ExitWindows.Reboot, efi.ShutdownReason.MajorOther | efi.ShutdownReason.MinorOther | efi.ShutdownReason.FlagPlanned))
                    trayIcon.ShowBalloonTip(3000, "Shutdown failed", "Unknown error.", ToolTipIcon.Error);
        }

        void Exit(object sender, EventArgs e) {
            trayIcon.Visible = false;
            Application.Exit();
        }
    }
}
