using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Engine.Interfaces;
using System.Runtime.InteropServices;


#if WINDOWS
using System.Management;
using Microsoft.Win32;
#else
using System.IO;
#endif


namespace Client.Engine.Http.Providers
{
    public class UserAgentProvider : IUserAgentProvider
    {
        private readonly string _userAgent;
        private readonly string _clientVersion;
        public UserAgentProvider(IConfiguration configuration)
        {
            _clientVersion = configuration.GetValue("ClientVersion", "0.0.0");
            _userAgent = BuildUserAgent();
        }
        public string GetUserAgent() => _userAgent;
        private string BuildUserAgent()
        {
            string os = RuntimeInformation.OSDescription;
            string arch = RuntimeInformation.OSArchitecture.ToString();
            string model = GetDeviceModel();
            return $"DataGuardClient/{_clientVersion} ({os}; {arch}; {model})";
        }
        private string GetDeviceModel()
        {
            try
            {
                #if WINDOWS
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SystemInformation");
                if (key != null)
                {
                    var manufacturer = key.GetValue("SystemManufacturer")?.ToString()?.Trim();
                    var model = key.GetValue("SystemProductName")?.ToString()?.Trim();

                    if (!string.IsNullOrEmpty(manufacturer) || !string.IsNullOrEmpty(model))
                    {
                        return $"{manufacturer} {model}".Trim();
                    }
                }
                throw new Exception("WIN: Failed to get device model");
                #elif LINUX
                if (File.Exists("/sys/class/dmi/id/product_name"))
                {
                    string vendor = File.Exists("/sys/class/dmi/id/sys_vendor")
                        ? File.ReadAllText("/sys/class/dmi/id/sys_vendor").Trim()
                        : "";
                    string model = File.ReadAllText("/sys/class/dmi/id/product_name").Trim();
                    
                    return $"{vendor} {model}".Trim();
                }
                throw new Exception("LIN: Failed to get device model");
                #elif MACOS
                var psi = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.model", // Возвращает модель, например "MacBookPro18,2"
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(1000); // Ограничиваем время ожидания одной секундой
                    return process.StandardOutput.ReadToEnd().Trim();
                }
                throw new Exception("MAC: Failed to get device model");
                #else
                throw new Exception("Unknown OS");
                #endif
            }
            catch(Exception ex)
            {
                switch(ex.Message.Substring(0, 3))
                {
                    case "WIN": return "Windows";
                    case "LIN": return "Linux";
                    case "MAC": return "MacOS";
                    default: return "Unknown";
                }
                
            }
        }

    }
}