using System;
using System.Reflection;
using Microsoft.Win32.TaskScheduler;

namespace bootnext {
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
}
