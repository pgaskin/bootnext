# bootnext
Windows tray icon to set the EFI BootNext option. Binaries can be downloaded [here](https://ci.appveyor.com/project/geek1011/bootnext/build/artifacts).

![](screenshot.png)

**Running automatically on login:**
1. Open `Task Scheduler`.
2. Press `Create Task`.
3. Name the task `bootnext` (or whatever you want to call it).
4. Check `Run only when user is logged on`, and choose your user.
5. Check `Run with highest privileges`.
6. Under `Triggers`, press `New...`, choose `At log on`, then tick `Specific user` and choose your user.
7. Under `Actions`, add `bootnext.exe` (or whatever you downloaded/built it as).
8. Under `Conditions` untick `Start the task only when the computer is on AC power`.
9. Under `Settings`, untick `Stop the task if it runs longer than:`.

Alternatively, you can create the task automatically by running `bootnext /install` from a command prompt as an administrator. To remove it, run `bootnext /uninstall`.
