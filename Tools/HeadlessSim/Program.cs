using System;
namespace RTS.Headless
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            RTS.Sim.Catalog.EnsureBuilt();
            Console.WriteLine($"Units {RTS.Sim.Catalog.Units.Count} Buildings {RTS.Sim.Catalog.Buildings.Count}");
            return 0;
        }
    }
}
