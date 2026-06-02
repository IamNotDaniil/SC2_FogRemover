using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SC2FogRemover
{
    public class FogRemover
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);
        
        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);
        
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);
        
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);
        
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        
        private IntPtr _processHandle;
        private Process _gameProcess;
        private bool _patched = false;
        
        public FogRemover()
        {
            _gameProcess = Process.GetProcessesByName("SC2_x64")[0];
            _processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, _gameProcess.Id);
        }
        
        public void RemoveFogOfWar()
        {
            Console.WriteLine("[FogRemover] Starting fog removal...");
            
            // Патч #1: Fog of War culling function
            PatchPattern(new byte[] { 0x48, 0x85, 0xC0, 0x74, 0x10, 0x48, 0x8B, 0x40 }, 
                        new byte[] { 0x48, 0x85, 0xC0, 0xEB, 0x10, 0x48, 0x8B, 0x40 });
            
            // Патч #2: IsFogEnabled check
            PatchPattern(new byte[] { 0x80, 0x3D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x74, 0x0C },
                        new byte[] { 0xC6, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0xEB, 0x0C });
            
            // Патч #3: Fog render bypass
            PatchPattern(new byte[] { 0x74, 0x2A, 0x48, 0x8B, 0x0D, 0x00, 0x00, 0x00, 0x00, 0x48, 0x85, 0xC9 },
                        new byte[] { 0xEB, 0x2A, 0x48, 0x8B, 0x0D, 0x00, 0x00, 0x00, 0x00, 0x48, 0x85, 0xC9 });
            
            // Патч #4: Force fog disabled in shader constant
            PatchPattern(new byte[] { 0xC7, 0x45, 0xF8, 0x01, 0x00, 0x00, 0x00, 0x8B, 0x45, 0xF8 },
                        new byte[] { 0xC7, 0x45, 0xF8, 0x00, 0x00, 0x00, 0x00, 0x8B, 0x45, 0xF8 });
            
            if (_patched)
                Console.WriteLine("[FogRemover] SUCCESS - Fog of War disabled!");
            else
                Console.WriteLine("[FogRemover] WARNING - No patterns found. Game version may be different.");
        }
        
        private void PatchPattern(byte[] pattern, byte[] replacement)
        {
            IntPtr address = FindPattern(pattern);
            if (address != IntPtr.Zero)
            {
                uint oldProtect;
                VirtualProtect(address, (uint)replacement.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                
                IntPtr bytesWritten;
                WriteProcessMemory(_processHandle, address, replacement, replacement.Length, out bytesWritten);
                
                VirtualProtect(address, (uint)replacement.Length, oldProtect, out _);
                
                _patched = true;
                Console.WriteLine($"[FogRemover] Patched at 0x{address.ToInt64():X}");
            }
        }
        
        private IntPtr FindPattern(byte[] pattern)
        {
            IntPtr baseAddress = _gameProcess.MainModule.BaseAddress;
            int moduleSize = _gameProcess.MainModule.ModuleMemorySize;
            
            for (int i = 0; i < moduleSize - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (pattern[j] == 0x00 && j > 0 && pattern[j-1] == 0x3D) continue; // wildcard
                    
                    byte[] buffer = new byte[1];
                    IntPtr bytesRead;
                    ReadProcessMemory(_processHandle, IntPtr.Add(baseAddress, i + j), buffer, 1, out bytesRead);
                    
                    if (buffer[0] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                
                if (found)
                    return IntPtr.Add(baseAddress, i);
            }
            return IntPtr.Zero;
        }
        
        ~FogRemover()
        {
            if (_processHandle != IntPtr.Zero)
                CloseHandle(_processHandle);
        }
    }
}
