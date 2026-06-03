using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SC2FogRemover
{
    class Program
    {
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
        
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
        
        static void Main(string[] args)
        {
            Console.Title = "SC2 Fog Remover v2.0";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==========================================");
            Console.WriteLine("   SC2 Fog of War Remover v2.0");
            Console.WriteLine("   Full Map Reveal");
            Console.WriteLine("==========================================");
            Console.ResetColor();
            Console.WriteLine();
            
            // Проверка прав
            if (!IsAdministrator())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Run as Administrator!");
                Console.ResetColor();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            
            // Поиск SC2 процесса
            Process gameProcess = null;
            string[] processNames = { "SC2_x64", "SC2", "StarCraft II", "StarCraft II_x64" };
            
            Console.WriteLine("[INFO] Looking for StarCraft II...");
            
            for (int i = 0; i < 30; i++)
            {
                foreach (string name in processNames)
                {
                    Process[] procs = Process.GetProcessesByName(name);
                    if (procs.Length > 0)
                    {
                        gameProcess = procs[0];
                        break;
                    }
                }
                if (gameProcess != null) break;
                Thread.Sleep(1000);
                Console.Write(".");
            }
            
            Console.WriteLine();
            
            if (gameProcess == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] StarCraft II not found!");
                Console.WriteLine("[INFO] Make sure the game is running.");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Found SC2 (PID: {gameProcess.Id})");
            Console.ResetColor();
            Console.WriteLine();
            
            // Сворачиваем консоль
            ShowWindow(GetConsoleWindow(), SW_MINIMIZE);
            
            try
            {
                var remover = new FogRemover(gameProcess);
                int patches = remover.RemoveFog();
                
                ShowWindow(GetConsoleWindow(), SW_RESTORE);
                
                Console.WriteLine();
                if (patches > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("==========================================");
                    Console.WriteLine($"   SUCCESS! {patches} patches applied!");
                    Console.WriteLine("   Fog of War has been REMOVED!");
                    Console.WriteLine("==========================================");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[FAILED] No patches were applied.");
                    Console.WriteLine("[INFO] Game version may be incompatible.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                ShowWindow(GetConsoleWindow(), SW_RESTORE);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();
            }
            
            Console.WriteLine();
            Console.WriteLine("Press Enter to exit and restore fog...");
            Console.ReadLine();
        }
        
        static bool IsAdministrator()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }
    }
}
