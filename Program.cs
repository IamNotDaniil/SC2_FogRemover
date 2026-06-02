using System;
using System.Diagnostics;
using System.Threading;

namespace SC2FogRemover
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║     SC2 Fog of War Remover v1.0       ║");
            Console.WriteLine("║     Full Map Reveal - DirectX 11      ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();
            
            // Ждем запуска StarCraft II
            Process[] processes;
            do
            {
                Console.WriteLine("[WAIT] Waiting for StarCraft II (SC2_x64.exe)...");
                processes = Process.GetProcessesByName("SC2_x64");
                if (processes.Length == 0)
                    Thread.Sleep(2000);
            } while (processes.Length == 0);
            
            Console.WriteLine($"[INFO] Found SC2_x64.exe (PID: {processes[0].Id})");
            Console.WriteLine("[INFO] Removing Fog of War...");
            Console.WriteLine();
            
            try
            {
                // Метод 1: Прямой патчинг памяти
                var fogRemover = new FogRemover();
                fogRemover.RemoveFogOfWar();
                
                // Метод 2: Hook DirectX 11
                var dxHook = new DirectXHook();
                dxHook.RemoveFog();
                dxHook.ForceNoFogViaD3D();
                
                Console.WriteLine();
                Console.WriteLine("[SUCCESS] Fog of War has been REMOVED!");
                Console.WriteLine("[INFO] You can now see the entire map.");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.WriteLine("[INFO] Make sure you run this program as Administrator");
                Console.ReadKey();
            }
        }
    }
}
