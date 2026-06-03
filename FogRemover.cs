using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace SC2FogRemover
{
    public class FogRemover
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);
        
        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);
        
        [DllImport("kernel32.dll")]
        static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);
        
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);
        
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        
        private IntPtr _processHandle;
        private Process _gameProcess;
        
        public FogRemover(Process gameProcess)
        {
            _gameProcess = gameProcess;
            _processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, gameProcess.Id);
            
            if (_processHandle == IntPtr.Zero)
                throw new Exception("Failed to open process. Run as Administrator!");
        }
        
        public int RemoveFog()
        {
            int applied = 0;
            Console.WriteLine("[FogRemover] Scanning memory patterns...");
            
            // Сигнатуры для патчинга
            var patches = new List<(byte[] pattern, byte[] replace, string name)>
            {
                (new byte[] { 0x74, 0x15, 0x48, 0x8B, 0xCB }, new byte[] { 0xEB, 0x15, 0x48, 0x8B, 0xCB }, "JE->JMP (fog culling)"),
                (new byte[] { 0x74, 0x2A, 0x48, 0x8B, 0x0D }, new byte[] { 0xEB, 0x2A, 0x48, 0x8B, 0x0D }, "JE->JMP (render bypass)"),
                (new byte[] { 0x84, 0xC0, 0x74, 0x20, 0x48 }, new byte[] { 0x84, 0xC0, 0xEB, 0x20, 0x48 }, "TEST->JMP (fow check)"),
                (new byte[] { 0x80, 0x3D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x74 }, new byte[] { 0xC6, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0xEB }, "Force fog disable")
            };
            
            foreach (var patch in patches)
            {
                IntPtr addr = FindPattern(patch.pattern);
                if (addr != IntPtr.Zero)
                {
                    uint oldProtect;
                    VirtualProtectEx(_processHandle, addr, (uint)patch.replace.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    
                    IntPtr written;
                    if (WriteProcessMemory(_processHandle, addr, patch.replace, patch.replace.Length, out written))
                    {
                        applied++;
                        Console.WriteLine($"  [+] {patch.name}");
                    }
                    
                    VirtualProtectEx(_processHandle, addr, (uint)patch.replace.Length, oldProtect, out oldProtect);
                }
                else
                {
                    Console.WriteLine($"  [-] {patch.name} - not found");
                }
            }
            
            return applied;
        }
        
        private IntPtr FindPattern(byte[] pattern)
        {
            IntPtr baseAddr = _gameProcess.MainModule.BaseAddress;
            int size = _gameProcess.MainModule.ModuleMemorySize;
            
            for (int i = 0; i < size - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (pattern[j] == 0x00 && j > 0 && pattern[j-1] == 0x3D) continue;
                    
                    byte[] buf = new byte[1];
                    IntPtr read;
                    ReadProcessMemory(_processHandle, IntPtr.Add(baseAddr, i + j), buf, 1, out read);
                    
                    if (buf[0] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return IntPtr.Add(baseAddr, i);
            }
            return IntPtr.Zero;
        }
        
        ~FogRemover()
        {
            if (_processHandle != IntPtr.Zero) CloseHandle(_processHandle);
        }
    }
}
