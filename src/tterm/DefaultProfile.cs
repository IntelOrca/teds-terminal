using System.IO;
using static System.Environment;

namespace tterm
{
    internal static class DefaultProfile
    {
        public static Profile Get()
        {
            var profile = new Profile()
            {
                Command = GetDefaultCommand(),
                CurrentWorkingDirectory = GetDefaultWorkingDirectory()
            };
            return profile;
        }

        private static string GetDefaultCommand()
        {
            string result = null;
            if (GetEnvironmentVariable(EnvironmentVariables.COMSPEC) != null)
            {
                result = "%COMSPEC%";
            }
            else
            {
                result = Path.Combine(GetFolderPath(SpecialFolder.System), "cmd.exe");
            }
            return result;
        }

        private static string GetDefaultWorkingDirectory()
        {
            string result;
            if (GetEnvironmentVariable(EnvironmentVariables.HOMEDRIVE) != null &&
                GetEnvironmentVariable(EnvironmentVariables.HOMEPATH) != null)
            {
                result = string.Format("%{0}%%{1}%", EnvironmentVariables.HOMEDRIVE, EnvironmentVariables.HOMEPATH);
            }
            else
            {
                result = GetFolderPath(SpecialFolder.UserProfile);
            }
            return result;
        }
    }
}
