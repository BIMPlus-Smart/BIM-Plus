using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AutoPileCap.Services
{
    public static class LicenseManager
    {
        // 256-bit cryptographic salt for key generation (masked byte array)
        private static readonly byte[] KeySalt = new byte[] 
        { 
            0x50, 0x68, 0x65, 0x61, 0x72, 0x61, 0x6B, 0x42, 
            0x49, 0x4D, 0x50, 0x6C, 0x75, 0x73, 0x32, 0x30, 
            0x32, 0x36, 0x53, 0x65, 0x63, 0x75, 0x72, 0x65, 
            0x4B, 0x65, 0x79, 0x21, 0x23, 0x24, 0x25, 0x26 
        };

        public static string GetMachineId()
        {
            string machineGuid = "";
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("MachineGuid");
                        if (val != null)
                        {
                            machineGuid = val.ToString().ToUpper();
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(machineGuid))
            {
                machineGuid = Environment.MachineName.ToUpper();
            }
            
            // Return a shorter, friendlier ID
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(machineGuid));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("X2"));
                }
                string fullId = builder.ToString();
                return $"{fullId.Substring(0, 4)}-{fullId.Substring(4, 4)}-{fullId.Substring(8, 4)}"; // e.g. ABCD-1234-EF56
            }
        }

        public static string GenerateActivationCode(string machineId)
        {
            if (string.IsNullOrWhiteSpace(machineId)) return string.Empty;

            string cleanId = machineId.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
            using (var hmac = new HMACSHA256(KeySalt))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(cleanId));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 6; i++) // 6 bytes = 12 hex characters
                {
                    sb.Append(hash[i].ToString("X2"));
                }
                string fullCode = sb.ToString();
                // Format: XXXX-XXXX-XXXX (exact same length as Machine ID)
                return $"{fullCode.Substring(0, 4)}-{fullCode.Substring(4, 4)}-{fullCode.Substring(8, 4)}";
            }
        }

        public static bool ValidateActivationCode(string machineId, string activationCode)
        {
            if (string.IsNullOrWhiteSpace(machineId) || string.IsNullOrWhiteSpace(activationCode))
                return false;

            string expected = GenerateActivationCode(machineId).Replace("-", "").ToUpperInvariant();
            string actual = activationCode.Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();

            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLicensed()
        {
            string savedCode = GetSavedLicenseCode();
            if (string.IsNullOrWhiteSpace(savedCode)) return false;

            return ValidateActivationCode(GetMachineId(), savedCode);
        }

        public static void SaveLicenseCode(string code)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\AutoPileCap"))
                {
                    key.SetValue("ActivationCode", code);
                }
            }
            catch { }
        }

        public static string GetSavedLicenseCode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\AutoPileCap"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("ActivationCode");
                        if (val != null)
                        {
                            return val.ToString();
                        }
                    }
                }
            }
            catch { }
            return string.Empty;
        }
        
        public static bool VerifyLicense(Autodesk.Revit.UI.UIApplication uiapp)
        {
            if (IsLicensed())
            {
                return true;
            }
            
            // Show Activation Window
            var window = new UI.LicenseWindow(uiapp);
            window.ShowDialog();
            
            return IsLicensed();
        }
    }
}
