using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitCommitment
{
    class Program
    {
        static void Main(string[] args)
        {
            int initialPopulationCount = 0;
            while (true)
            {
                Console.Write("Number of individuals: ");
                string initialPopulationString = Console.ReadLine();
                if (int.TryParse(initialPopulationString, out initialPopulationCount))
                    break;
            }
            int evolutionCycles = 0;
            while (true)
            {
                Console.Write("Evolution cycles: ");
                string evolutionCyclesString = Console.ReadLine();
                if (int.TryParse(evolutionCyclesString, out evolutionCycles))
                    break;
            }
            int mode = 0;
            while (true)
            {
                Console.WriteLine("Choose mode: ");
                Console.WriteLine("0 - no grouping.");
                Console.WriteLine("1 - default grouping.");
                Console.WriteLine("2 - random grouping.");
                Console.Write("Your choice: ");
                string modeString = Console.ReadLine();
                if (int.TryParse(modeString, out mode))
                    break;
            }

            int scale = 0;
            if (mode == 0 || mode == 2)
            {
                while (true)
                {
                    Console.Write("Choose replication factor: ");
                    string scaleString = Console.ReadLine();
                    if (int.TryParse(scaleString, out scale))
                        break;
                }
            }
            else
                scale = 1;

            Evolution evolution = new Evolution(evolutionCycles, initialPopulationCount, mode, scale);
            if (mode == 0)
                evolution.RealiseEvolution();
            else
                evolution.RealiseGroupEvolution();
        }
    }
}
