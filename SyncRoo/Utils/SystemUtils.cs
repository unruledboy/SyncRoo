using System.Security.Principal;

namespace SyncRoo.Utils
{
    public static class SystemUtils
    {
        public static bool IsAdministrator()
        {
            if (OperatingSystem.IsWindows())
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return false;
        }
    }
}
