using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace SC2FogRemover
{
    public unsafe class DirectXHook : IDisposable
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);
        
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }
        
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private Process _gameProcess;
        private IntPtr _gameHandle;
        private bool _fogRemoved = false;
        
        public DirectXHook()
        {
            _gameProcess = Process.GetProcessesByName("SC2_x64")[0];
            _gameHandle = _gameProcess.Handle;
        }
        
        public void RemoveFog()
        {
            Console.WriteLine("[DirectXHook] Searching for fog shader...");
            
            // Метод 1: Поиск сигнатуры в Game.dll
            IntPtr gameBase = GetModuleHandle("Game.dll");
            if (gameBase != IntPtr.Zero)
            {
                PatchFogShader(gameBase);
            }
            
            // Метод 2: Поиск сигнатуры в Engine.dll
            IntPtr engineBase = GetModuleHandle("Engine.dll");
            if (engineBase != IntPtr.Zero)
            {
                PatchFogShader(engineBase);
            }
            
            // Метод 3: Поиск сигнатуры в SC2_x64.exe
            IntPtr exeBase = _gameProcess.MainModule.BaseAddress;
            PatchFogShader(exeBase);
            
            Console.WriteLine($"[DirectXHook] Fog removal: {(_fogRemoved ? "SUCCESS" : "FAILED")}");
        }
        
        private void PatchFogShader(IntPtr baseAddress)
        {
            if (baseAddress == IntPtr.Zero) return;
            
            // Сигнатура для fog enable/disable (версия 5.0.13)
            byte[][] signatures = new byte[][]
            {
                // Сигнатура 1: JE -> JMP для fog culling
                new byte[] { 0x74, 0x15, 0x48, 0x8B, 0xCB, 0xE8 },
                // Сигнатура 2: fog render condition
                new byte[] { 0x80, 0x3D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x74, 0x10 },
                // Сигнатура 3: fog of war check
                new byte[] { 0x48, 0x8B, 0x4D, 0x08, 0x80, 0xB9, 0x00, 0x00, 0x00, 0x00, 0x00, 0x74 },
                // Сигнатура 4: shader constant for fog
                new byte[] { 0xC7, 0x45, 0x00, 0x00, 0x00, 0x00, 0x00, 0x8B, 0x45, 0x00, 0x85 }
            };
            
            for (int i = 0; i < 0x10000000; i += 0x1000)
            {
                foreach (byte[] sig in signatures)
                {
                    IntPtr addr = FindSignature(baseAddress, i, 0x1000, sig);
                    if (addr != IntPtr.Zero)
                    {
                        ApplyPatch(addr, sig);
                    }
                }
            }
        }
        
        private IntPtr FindSignature(IntPtr baseAddr, int startOffset, int searchSize, byte[] signature)
        {
            for (int i = 0; i < searchSize - signature.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < signature.Length; j++)
                {
                    byte expected = signature[j];
                    if (expected == 0x00 && j > 2) continue; // Wildcard для неизвестных байтов
                    
                    byte actual = Marshal.ReadByte(baseAddr, startOffset + i + j);
                    if (expected != 0x00 && actual != expected)
                    {
                        found = false;
                        break;
                    }
                }
                
                if (found)
                {
                    return IntPtr.Add(baseAddr, startOffset + i);
                }
            }
            return IntPtr.Zero;
        }
        
        private void ApplyPatch(IntPtr address, byte[] originalSignature)
        {
            uint oldProtect;
            VirtualProtect(address, (uint)originalSignature.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
            
            // Замена условного перехода на безусловный (JE -> JMP)
            if (originalSignature[0] == 0x74)
            {
                Marshal.WriteByte(address, 0xEB); // JMP вместо JE
                _fogRemoved = true;
                Console.WriteLine($"[DirectXHook] Patched JE->JMP at 0x{address.ToInt64():X}");
            }
            // Принудительная установка fog enable = false
            else if (originalSignature[0] == 0x80 && originalSignature[7] == 0x74)
            {
                // XOR EAX,EAX + NOP
                Marshal.WriteByte(address, 0x31); // XOR
                Marshal.WriteByte(IntPtr.Add(address, 1), 0xC0); // EAX,EAX
                for (int i = 2; i < 8; i++)
                    Marshal.WriteByte(IntPtr.Add(address, i), 0x90); // NOP
                _fogRemoved = true;
                Console.WriteLine($"[DirectXHook] Patched fog enable at 0x{address.ToInt64():X}");
            }
            // Принудительный return (ret)
            else
            {
                Marshal.WriteByte(address, 0xC3); // RET
                _fogRemoved = true;
                Console.WriteLine($"[DirectXHook] Patched with RET at 0x{address.ToInt64():X}");
            }
            
            VirtualProtect(address, (uint)originalSignature.Length, oldProtect, out _);
        }
        
        // Альтернативный метод: через D3D11 Device context
        public void ForceNoFogViaD3D()
        {
            try
            {
                IntPtr hWnd = FindWindow("BlizzardStartupClass", "StarCraft II");
                if (hWnd == IntPtr.Zero) return;
                
                GetWindowRect(hWnd, out RECT rect);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                
                // Создаем свое устройство для перехвата
                var swapChainDesc = new SwapChainDescription()
                {
                    BufferCount = 2,
                    ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                    IsWindowed = true,
                    OutputHandle = hWnd,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.Discard,
                    Usage = Usage.RenderTargetOutput
                };
                
                Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport, swapChainDesc, out Device device, out SwapChain swapChain);
                
                // Устанавливаем BlendState который игнорирует туман
                var blendDesc = new BlendStateDescription();
                blendDesc.RenderTargets[0].IsBlendEnabled = false;
                blendDesc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
                blendDesc.AlphaToCoverageEnable = false;
                blendDesc.IndependentBlendEnable = false;
                
                BlendState noFogBlend = new BlendState(device, blendDesc);
                device.ImmediateContext.OutputMerger.SetBlendState(noFogBlend, new SharpDX.Mathematics.Interop.RawColor4(1, 1, 1, 1), -1);
                
                Console.WriteLine("[DirectXHook] D3D11 BlendState override applied");
                _fogRemoved = true;
                
                swapChain.Dispose();
                device.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DirectXHook] D3D method failed: {ex.Message}");
            }
        }
        
        public bool IsFogRemoved() => _fogRemoved;
        
        public void Dispose()
        {
            _gameProcess?.Dispose();
        }
    }
}
