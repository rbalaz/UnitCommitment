using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitCommitment.Evolution_algorithm_blocks;
using UnitCommitment.Representation;

namespace UnitCommitment.Utilities
{
    static class StaticOperations
    {
        public static List<Machine> GenerateMachineTemplate(int scale)
        {
            Machine first = new Machine(150, 455, 8, 8, 1000, 16.19, 0.00048, 9000, 4500, 5);
            Machine second = new Machine(150, 455, 8, 8, 970, 17.26, 0.00031, 10000, 5000, 5);
            Machine third = new Machine(25, 162, 6, 6, 450, 19.7, 0.00398, 1800, 900, 4);
            Machine fourth = new Machine(20, 130, 5, 5, 680, 16.5, 0.00211, 1120, 560, 4);
            Machine fifth = new Machine(20, 130, 5, 5, 700, 16.6, 0.002, 1100, 550, 4);
            Machine sixth = new Machine(20, 80, 3, 3, 370, 22.26, 0.00712, 340, 170, 2);
            Machine seventh = new Machine(25, 85, 3, 3, 480, 27.74, 0.00079, 520, 260, 2);
            Machine eigth = new Machine(10, 55, 1, 1, 660, 25.92, 0.00413, 60, 30, 0);
            Machine ninth = new Machine(10, 55, 1, 1, 665, 27.27, 0.00222, 60, 30, 0);
            Machine tenth = new Machine(10, 55, 1, 1, 670, 27.79, 0.00173, 60, 30, 0);

            List<Machine> machineListTemplate = new List<Machine>(10)
            {
                first,
                second,
                third,
                fourth,
                fifth,
                sixth,
                seventh,
                eigth,
                ninth,
                tenth
            };

            List<Machine> machineList = new List<Machine>(scale * 10);
            foreach (Machine machine in machineListTemplate)
            {
                for (int i = 0; i < scale; i++)
                {
                    Machine machineClone = Machine.Clone(machine);
                    machineList.Add(machineClone);
                }
            }

            return machineList;
        }

        public static List<int> GenerateInitialMachinesState(int scale)
        {
            List<int> initialStateTemplate = new List<int>
            {
                //First machine state
                8,
                //Second machine state
                8,
                //Third machine state
                -6,
                //Fourth machine state
                -5,
                //Fifth machine state
                -5,
                //Sixth machine state
                -3,
                //Seventh machine state
                -3,
                //Eigth machine state
                -1,
                //Ninth machine state
                -1,
                //Tenth machine state
                -1
            };

            List<int> initialState = new List<int>(10 * scale);
            foreach (int state in initialStateTemplate)
            {
                for (int i = 0; i < scale; i++)
                {
                    initialState.Add(state);
                }
            }

            return initialState;
        }

        public static List<int> GenerateMaximumHourlyChanges(int scale)
        {
            List<int> maximumHourlyChangesTemplate = new List<int>();
            maximumHourlyChangesTemplate.AddRange(new List<int> { 160, 160 });
            maximumHourlyChangesTemplate.AddRange(new List<int> { 100, 100, 100 });
            maximumHourlyChangesTemplate.AddRange(new List<int> { 60, 60 });
            maximumHourlyChangesTemplate.AddRange(new List<int> { 40, 40, 40 });

            List<int> maximumHourlyChanges = new List<int>(10 * scale);
            foreach (int change in maximumHourlyChangesTemplate)
            {
                for (int i = 0; i < scale; i++)
                {
                    maximumHourlyChanges.Add(change);
                }
            }

            return maximumHourlyChanges;
        }

        public static List<int> GenerateHourlyLoads(int scale)
        {
            List<int> hourlyLoadsTemplate = new List<int> { 700,750,850,950,1000,1100,1150,1200,1300,1400,1450,1500,1400,
                1300,1200,1050,1000,1100,1200,1400,1300,1100,900,800};

            List<int> hourlyLoads = hourlyLoadsTemplate.Select(item => item * scale).ToList();

            return hourlyLoads;
        }

        public static bool ValidateIndividual(Individual individual)
        {
            // 1. check if power values are fulfilled
            List<int> hourlyLoads = GenerateHourlyLoads(individual.Schedule[0].Count / 10);
            for (int i = 0; i < individual.Schedule.Count; i++)
            {
                double totalPower = individual.Schedule[i].Sum(machine => machine.CurrentOutputPower);
                if (totalPower - hourlyLoads[i] > 0.001)
                    return false;
            }

            List<int> maximumChanges = GenerateMaximumHourlyChanges(individual.Schedule[0].Count / 10);

            // 2.) check if power value change boundaries were not broken
            for (int i = 0; i < individual.Schedule.Count - 1; i++)
            {
                for (int j = 0; j < individual.Schedule[i].Count; j++)
                {
                    Machine predecessor = individual.Schedule[i][j];
                    Machine successor = individual.Schedule[i + 1][j];

                    if (predecessor.CurrentOutputPower > 0 && successor.CurrentOutputPower > 0)
                    {
                        double powerChange = Math.Abs(predecessor.CurrentOutputPower - successor.CurrentOutputPower);
                        if (powerChange - maximumChanges[j] > 0.001)
                            return false;
                    }
                }
            }

            // 3.) check if some machine was not started or terminated too early
            for (int i = 0; i < individual.Schedule.Count - 1; i++)
            {
                for (int j = 0; j < individual.Schedule[i].Count; j++)
                {
                    Machine predecessor = individual.Schedule[i][j];
                    Machine successor = individual.Schedule[i + 1][j];

                    if (predecessor.CurrentOutputPower == 0 && successor.CurrentOutputPower > 0)
                    {
                        if (Math.Abs(predecessor.CurrentStatus) < predecessor.MinimumDowntime)
                            return false;
                    }

                    if (predecessor.CurrentOutputPower > 0 && successor.CurrentOutputPower == 0)
                    {
                        if (Math.Abs(predecessor.CurrentStatus) < predecessor.MinimumUptime)
                            return false;
                    }
                }
            }

            // 4.) check if some machine fell under lowest power limit or over the highest power limit
            for (int i = 0; i < individual.Schedule.Count; i++)
            {
                for (int j = 0; j < individual.Schedule[i].Count; j++)
                {
                    Machine machine = individual.Schedule[i][j];
                    if (machine.CurrentOutputPower > 0)
                    {
                        if (machine.CurrentOutputPower > machine.MaximumOutputPower)
                            return false;
                        if (machine.CurrentOutputPower < machine.MinimumOutputPower)
                            return false;
                    }
                }
            }

            return true;
        }

        public static bool ValidateIndividualWithGroups(Individual individual)
        {
            // 1. check if power values are fulfilled
            List<int> hourlyLoads = GenerateHourlyLoads(individual.Schedule[0].Count / 10);
            for (int i = 0; i < individual.Schedule.Count; i++)
            {
                double totalPower = individual.Schedule[i].Sum(machine => machine.CurrentOutputPower);
                if (totalPower - hourlyLoads[i] > 0.001)
                    return false;
            }

            List<int> maximumChanges = GenerateMaximumHourlyChanges(individual.Schedule[0].Count / 10);

            // 2.) check if power value change boundaries were not broken
            for (int i = 0; i < individual.Schedule.Count - 1; i++)
            {
                for (int j = 0; j < individual.Schedule[i].Count; j++)
                {
                    Machine predecessor = individual.Schedule[i][j];
                    Machine successor = individual.Schedule[i + 1][j];

                    if (predecessor.CurrentOutputPower > 0 && successor.CurrentOutputPower > 0)
                    {
                        double powerChange = Math.Abs(predecessor.CurrentOutputPower - successor.CurrentOutputPower);
                        if (powerChange - maximumChanges[j] > 0.001)
                        {
                            //Representation.PrintSchedule(individual.Schedule);
                            return false;
                        }
                    }
                }
            }

            // 3.) check if some machine was not started or terminated too early
            for (int i = 0; i < individual.Schedule.Count - 1; i++)
            {
                for (int j = 0; j < individual.Schedule[i].Count; j++)
                {
                    Machine predecessor = individual.Schedule[i][j];
                    Machine successor = individual.Schedule[i + 1][j];

                    if (predecessor.CurrentOutputPower == 0 && successor.CurrentOutputPower > 0)
                    {
                        if (Math.Abs(predecessor.CurrentStatus) < predecessor.MinimumDowntime)
                            return false;
                    }

                    if (predecessor.CurrentOutputPower > 0 && successor.CurrentOutputPower == 0)
                    {
                        if (Math.Abs(predecessor.CurrentStatus) < predecessor.MinimumUptime)
                            return false;
                    }
                }
            }

            // 4.) check if some machine fell under lowest power limit or over the highest power limit
            for (int i = 0; i < individual.Schedule.Count; i++)
            {
                for (int j = 0; j < individual.Schedule[i].Count; j++)
                {
                    Machine machine = individual.Schedule[i][j];
                    if (machine.CurrentOutputPower > 0)
                    {
                        if (machine.CurrentOutputPower > machine.MaximumOutputPower)
                            return false;
                        if (machine.CurrentOutputPower < machine.MinimumOutputPower)
                            return false;
                    }
                }
            }

            // 5.) check if groups are always fully started/terminated
            List<Group> groups = individual.groups;
            for (int i = 0; i < individual.Schedule.Count; i++)
            {
                for (int j = 0; j < groups.Count; j++)
                {
                    bool started = individual.Schedule[i][groups[j].grouppedMachines[0]].CurrentOutputPower > 0;
                    for (int k = 1; k < groups[j].grouppedMachines.Count; k++)
                    {
                        bool currentStarted = individual.Schedule[i][groups[j].grouppedMachines[k]].CurrentOutputPower > 0;
                        if (started != currentStarted)
                            return false;
                    }
                }
            }

            return true;
        }

        public static List<Machine> CloneMachineList(List<Machine> machineList)
        {
            List<Machine> newList = new List<Machine>(machineList.Count);

            foreach (Machine machine in machineList)
            {
                Machine clone = Machine.Clone(machine);
                newList.Add(clone);
            }

            return newList;
        }

        public static bool SpecialValidation(List<Machine> current, List<Machine> previous)
        {
            List<int> maximumHourlyChanges = GenerateMaximumHourlyChanges(current.Count / 10);
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].CurrentOutputPower > 0 && previous[i].CurrentOutputPower > 0)
                {
                    double hourlyChange = Math.Abs(current[i].CurrentOutputPower - previous[i].CurrentOutputPower);

                    if (hourlyChange - maximumHourlyChanges[i] > 0.001)
                        return false;
                }
            }

            return true;
        }

        public static bool ValidateHour(List<Machine> hour, int index)
        {
            List<int> hourlyLoadSchedule = GenerateHourlyLoads(hour.Count / 10);

            double hourPowerOutput = hour.Sum(item => item.CurrentOutputPower);

            if (hourPowerOutput - hourlyLoadSchedule[index] > 0.001)
                return false;
            else
                return true;
        }
    }
}
