using Medallion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitCommitment.Representation;
using UnitCommitment.Utilities;

namespace UnitCommitment.Evolution_algorithm_blocks
{
    public class Initialisation
    {
        private int populationCount;
        private Random random;
        private List<int> maximumHourChanges;
        private int scale;

        public Initialisation(int populationCount, int scale)
        {
            this.populationCount = populationCount;
            this.scale = scale;
            random = new Random();
            maximumHourChanges = StaticOperations.GenerateMaximumHourlyChanges(scale);
        }

        #region Main initialisation workflow
        public List<Individual> GenerateInitialPopulation()
        {
            List<Individual> initialPopulation = new List<Individual>();
            for (int i = 0; i < populationCount; i++)
            {
                List<List<Machine>> schedule = new List<List<Machine>>(24);
                for (int j = 0; j < 24; j++)
                {
                    schedule.Add(StaticOperations.GenerateMachineTemplate(scale));
                }

                List<int> initialState = StaticOperations.GenerateInitialMachinesState(scale);
                List<int> hourlyPowerDemand = StaticOperations.GenerateHourlyLoads(scale);
                List<int> maximumHourlyChanges = StaticOperations.GenerateMaximumHourlyChanges(scale);

                List<List<Machine>> scheduleBackup = BackupSchedule(schedule);
                // 1.) Randomly generate first hour schedule
                schedule[0] = RandomMachineHourScheduleGeneration(schedule[0], hourlyPowerDemand[0]);

                ReviseMachineStatus(schedule[0], initialState);
                for (int j = 1; j < 24; j++)
                {
                    schedule[j] = RandomMachineHourScheduleGenerationWithReference(schedule[j - 1], hourlyPowerDemand[j], out bool success);
                    ReviseMachineStatus(schedule[j], schedule[j - 1]);
                    if (success == false)
                    {
                        j = 0;
                        RestoreScheduleFromBackup(schedule, scheduleBackup);
                        schedule[0] = RandomMachineHourScheduleGeneration(schedule[0], hourlyPowerDemand[0]);
                        double hourPower = schedule[0].Sum(item => item.CurrentOutputPower);
                        ReviseMachineStatus(schedule[0], initialState);
                    }
                }
                Individual representation = new Individual(schedule);
                initialPopulation.Add(representation);
                Console.WriteLine("Initial population count: " + i);
            }

            return initialPopulation;
        }

        private List<Machine> RandomMachineHourScheduleGeneration(List<Machine> hour, int load)
        {
            List<int> stoppedMachines = Enumerable.Range(0, scale * 10).ToList();
            // Turn on machines so that:
            // a) maximum achievable load is higher than demand
            // b) minimum achievable load is lower than demand
            // This allows random corrections to achieve exactly the value of load
            List<Machine> hourClone = StaticOperations.CloneMachineList(hour);
            int totalMaximumPower = 0;
            int totalMinimumPower = 0;
            while (true)
            {
                stoppedMachines.Shuffle();
                int startingMachineIndex = stoppedMachines[0];
                stoppedMachines.RemoveAt(0);

                hourClone[startingMachineIndex].CurrentOutputPower = hourClone[startingMachineIndex].MaximumOutputPower;
                totalMaximumPower += hourClone[startingMachineIndex].MaximumOutputPower;
                totalMinimumPower += hourClone[startingMachineIndex].MinimumOutputPower;

                if (totalMaximumPower >= load)
                {
                    if (totalMinimumPower <= load)
                        break;
                    else
                    {
                        stoppedMachines = Enumerable.Range(0, scale * 10).ToList();
                        hourClone = StaticOperations.CloneMachineList(hour);
                    }
                }
            }

            double totalLoad = totalMaximumPower;
            // Randomly reduce current power output to suffice power demand
            while (totalLoad > load)
            {
                List<int> adjustableMachineIndices = GetLowerableMachineIndices(hourClone);
                adjustableMachineIndices.Shuffle();
                int index = adjustableMachineIndices[0];

                double change = random.NextDouble() * (totalLoad - load);
                if (totalLoad - load < 1)
                    change = totalLoad - load;

                double maximumAllowedChange = hourClone[index].CurrentOutputPower - hourClone[index].MinimumOutputPower;
                change = change > maximumAllowedChange ? maximumAllowedChange : change;

                hourClone[index].CurrentOutputPower -= change;
                totalLoad -= change;
            }

            return hourClone;
        }

        private List<Machine> RandomMachineHourScheduleGenerationWithReference(List<Machine> previous, int load,
            out bool success)
        {
            // Current hour schedule uses reference from previous hour schedule
            List<Machine> hourClone = StaticOperations.CloneMachineList(previous);
            double previousTotalLoad = previous.Sum(machine => machine.CurrentOutputPower);

            // If power demand decreased
            bool currentSuccess = false;
            if (load < previousTotalLoad)
            {
                // Check if power demand is feasable with current machine setup
                double totalMinimumPower = CalculateMinimumAchievablePower(previous, maximumHourChanges);
                double totalMaximumPower = CalculateMaximumAchievablePower(previous, maximumHourChanges);
                if (!(totalMaximumPower > load && totalMinimumPower < load))
                {
                    List<int> stoppableMachineIndices = GetStoppableMachineIndices(previous);
                    List<int> machinesToStop = CalculateOptimalTerminateSequence(totalMaximumPower, totalMinimumPower,
                        stoppableMachineIndices, hourClone, load);
                    if (machinesToStop != null)
                    {
                        foreach (int startIndex in machinesToStop)
                        {
                            hourClone[startIndex].CurrentOutputPower = 0;
                        }
                        currentSuccess = true;
                    }
                    else
                        currentSuccess = false;
                }
                else
                    currentSuccess = true;
            }
            // If power demand increased
            else
            {
                // Check if power demand is feasable with current machine setup
                double totalMinimumPower = CalculateMinimumAchievablePower(previous, maximumHourChanges);
                double totalMaximumPower = CalculateMaximumAchievablePower(previous, maximumHourChanges);
                if (!(totalMaximumPower > load && totalMinimumPower < load))
                {
                    List<int> startableMachineIndices = GetStartableMachineIndices(previous);
                    List<int> machinesToStart = CalculateOptimalStartSequence(totalMaximumPower, totalMinimumPower,
                        startableMachineIndices, hourClone, load);
                    if (machinesToStart != null)
                    {
                        foreach (int startIndex in machinesToStart)
                        {
                            hourClone[startIndex].CurrentOutputPower = hourClone[startIndex].MaximumOutputPower;
                        }
                        currentSuccess = true;
                    }
                    else
                        currentSuccess = false;
                }
                else
                    currentSuccess = true;
            }
            if (currentSuccess == false)
            {
                success = false;
                return hourClone;
            }

            double totalLoad = hourClone.Sum(machine => machine.CurrentOutputPower);
            if (totalLoad > load)
            {
                while (totalLoad > load)
                {
                    double change = random.NextDouble() * (totalLoad - load);
                    if (totalLoad - load < 1)
                        change = totalLoad - load;
                    List<int> lowerableMachineIndices = GetLowerableMachineIndices(hourClone, previous);
                    lowerableMachineIndices.Shuffle();
                    int index = lowerableMachineIndices[0];
                    double maximumAllowedDecrease = GetMaximumMachineDecrease(hourClone[index], previous[index], maximumHourChanges[index]);
                    change = change > maximumAllowedDecrease ? maximumAllowedDecrease : change;
                    totalLoad -= change;
                    hourClone[index].CurrentOutputPower -= change;
                }
            }
            else
            {
                while (totalLoad < load)
                {
                    double change = random.NextDouble() * (load - totalLoad);
                    if (load - totalLoad < 1)
                        change = load - totalLoad;
                    List<int> incresableMachineIndices = GetIncreasableMachineIndices(hourClone, previous);
                    incresableMachineIndices.Shuffle();
                    int index = incresableMachineIndices[0];
                    double maximumAllowedIncrease = GetMaximumMachineIncrease(hourClone[index], previous[index], maximumHourChanges[index]);
                    change = change > maximumAllowedIncrease ? maximumAllowedIncrease : change;
                    totalLoad += change;
                    hourClone[index].CurrentOutputPower += change;
                }
            }

            success = totalLoad == load;
            return hourClone;
        }

        private void ReviseMachineStatus(List<Machine> hour, List<int> previousStates)
        {
            for (int i = 0; i < hour.Count; i++)
            {
                if (hour[i].CurrentOutputPower > 0)
                {
                    if (previousStates[i] > 0)
                    {
                        hour[i].CurrentStatus = previousStates[i] + 1;
                    }
                    else
                    {
                        hour[i].CurrentStatus = 1;
                    }
                }
                else
                {
                    if (previousStates[i] < 0)
                    {
                        hour[i].CurrentStatus = previousStates[i] - 1;
                    }
                    else
                    {
                        hour[i].CurrentStatus = -1;
                    }
                }
            }
        }

        private void ReviseMachineStatus(List<Machine> current, List<Machine> previous)
        {
            List<int> previousStates = previous.Select(item => item.CurrentStatus).ToList();
            ReviseMachineStatus(current, previousStates);
        }
        #endregion

        #region Utilities
        private List<int> GetStoppableMachineIndices(List<Machine> previous)
        {
            List<int> stoppableMachineIndices = new List<int>();
            for (int i = 0; i < previous.Count; i++)
            {
                if (previous[i].CurrentStatus >= previous[i].MinimumUptime)
                    stoppableMachineIndices.Add(i);
            }

            return stoppableMachineIndices;
        }

        private List<int> GetStartableMachineIndices(List<Machine> previous)
        {
            List<int> startableMachineIndices = new List<int>();
            for (int i = 0; i < previous.Count; i++)
            {
                if (previous[i].CurrentOutputPower == 0 &&
                    Math.Abs(previous[i].CurrentStatus) >= previous[i].MinimumDowntime)
                {
                    startableMachineIndices.Add(i);
                }
            }

            return startableMachineIndices;
        }

        private List<int> GetNotRunningMachineIndices(List<Machine> template)
        {
            List<int> notRunningMachineIndices = new List<int>();
            for (int i = 0; i < template.Count; i++)
            {
                if (template[i].CurrentOutputPower == 0)
                    notRunningMachineIndices.Add(i);
            }

            return notRunningMachineIndices;
        }

        private List<int> GetLowerableMachineIndices(List<Machine> template)
        {
            List<int> adjustableMachineIndices = new List<int>();
            for (int i = 0; i < template.Count; i++)
            {
                if (template[i].CurrentOutputPower > template[i].MinimumOutputPower)
                    adjustableMachineIndices.Add(i);
            }

            return adjustableMachineIndices;
        }

        private List<int> GetLowerableMachineIndices(List<Machine> template, List<Machine> previous)
        {
            List<int> adjustableMachineIndices = new List<int>();
            for (int i = 0; i < template.Count; i++)
            {
                if (template[i].CurrentOutputPower > template[i].MinimumOutputPower && template[i].CurrentOutputPower > 0)
                {
                    if (previous[i].CurrentOutputPower == 0)
                        adjustableMachineIndices.Add(i);
                    else if (Math.Abs(template[i].CurrentOutputPower - previous[i].CurrentOutputPower) < maximumHourChanges[i])
                        adjustableMachineIndices.Add(i);
                }
            }
            return adjustableMachineIndices;
        }

        private List<int> GetIncreasableMachineIndices(List<Machine> template, List<Machine> previous)
        {
            List<int> adjustableMachineIndices = new List<int>();
            for (int i = 0; i < template.Count; i++)
            {
                if (template[i].CurrentOutputPower < template[i].MaximumOutputPower && template[i].CurrentOutputPower > 0)
                {
                    if (previous[i].CurrentOutputPower == 0)
                        adjustableMachineIndices.Add(i);
                    else if (Math.Abs(template[i].CurrentOutputPower - previous[i].CurrentOutputPower) < maximumHourChanges[i])
                    {
                        adjustableMachineIndices.Add(i);
                    }
                }
            }
            return adjustableMachineIndices;
        }

        private double GetMaximumMachineDecrease(Machine machine, Machine previous, double maximumHourlyChange)
        {
            double maximumPossibleChange = machine.CurrentOutputPower - machine.MinimumOutputPower;
            double maximumAllowedChange = maximumHourlyChange - Math.Abs(machine.CurrentOutputPower - previous.CurrentOutputPower);

            if (previous.CurrentOutputPower == 0)
                maximumAllowedChange = maximumPossibleChange;

            return maximumAllowedChange < maximumPossibleChange ? maximumAllowedChange : maximumPossibleChange;
        }

        private double GetMaximumMachineIncrease(Machine machine, Machine previous, double maximumHourlyChange)
        {
            double maximumPossibleChange = machine.MaximumOutputPower - machine.CurrentOutputPower;
            double maximumAllowedChange = maximumHourlyChange - Math.Abs(machine.CurrentOutputPower - previous.CurrentOutputPower);

            if (previous.CurrentOutputPower == 0)
                maximumAllowedChange = maximumPossibleChange;

            return maximumAllowedChange < maximumPossibleChange ? maximumAllowedChange : maximumPossibleChange;
        }

        private List<List<Machine>> BackupSchedule(List<List<Machine>> schedule)
        {
            List<List<Machine>> scheduleClone = new List<List<Machine>>();
            foreach (List<Machine> hour in schedule)
            {
                List<Machine> hourClone = StaticOperations.CloneMachineList(hour);
                scheduleClone.Add(hourClone);
            }

            return scheduleClone;
        }

        private double CalculateMaximumAchievablePower(List<Machine> previous, List<int> maximumHourlyChanges)
        {
            double totalMaximumOutputPower = 0;
            for (int i = 0; i < previous.Count; i++)
            {
                if (previous[i].CurrentOutputPower == 0)
                    continue;

                double maximumPossibleOutput = previous[i].MaximumOutputPower;
                double maximumAllowedOutput = previous[i].CurrentOutputPower + maximumHourlyChanges[i];

                if (maximumAllowedOutput < maximumPossibleOutput)
                    totalMaximumOutputPower += maximumAllowedOutput;
                else
                    totalMaximumOutputPower += maximumPossibleOutput;
            }

            return totalMaximumOutputPower;
        }

        private double CalculateMaximumAchievablePower(List<Machine> previous, List<int> maximumHourlyChanges,
            List<int> stoppedMachineIndices)
        {
            double totalMaximumOutputPower = 0;
            for (int i = 0; i < previous.Count; i++)
            {
                if (stoppedMachineIndices.Contains(i))
                    continue;

                if (previous[i].CurrentOutputPower == 0)
                    continue;

                double maximumPossibleOutput = previous[i].MaximumOutputPower;
                double maximumAllowedOutput = previous[i].CurrentOutputPower + maximumHourlyChanges[i];

                if (maximumAllowedOutput < maximumPossibleOutput)
                    totalMaximumOutputPower += maximumAllowedOutput;
                else
                    totalMaximumOutputPower += maximumPossibleOutput;
            }

            return totalMaximumOutputPower;
        }

        private double CalculateMinimumAchievablePower(List<Machine> previous, List<int> maximumHourlyChanges)
        {
            double totalMinimumOutputPower = 0;
            for (int i = 0; i < previous.Count; i++)
            {
                if (previous[i].CurrentOutputPower == 0)
                    continue;

                double minimumPossibleOutput = previous[i].MinimumOutputPower;
                double minimumAllowedOutput = previous[i].CurrentOutputPower - maximumHourlyChanges[i];

                if (minimumAllowedOutput > minimumPossibleOutput)
                    totalMinimumOutputPower += minimumAllowedOutput;
                else
                    totalMinimumOutputPower += minimumPossibleOutput;
            }

            return totalMinimumOutputPower;
        }

        private double CalculateMinimumAchievablePower(List<Machine> previous, List<int> maximumHourlyChanges,
            List<int> stoppedMachineIndices)
        {
            double totalMinimumOutputPower = 0;
            for (int i = 0; i < previous.Count; i++)
            {
                if (stoppedMachineIndices.Contains(i))
                    continue;

                if (previous[i].CurrentOutputPower == 0)
                    continue;

                double minimumPossibleOutput = previous[i].MinimumOutputPower;
                double minimumAllowedOutput = previous[i].CurrentOutputPower - maximumHourlyChanges[i];

                if (minimumAllowedOutput > minimumPossibleOutput)
                    totalMinimumOutputPower += minimumAllowedOutput;
                else
                    totalMinimumOutputPower += minimumPossibleOutput;
            }

            return totalMinimumOutputPower;
        }

        private void RestoreScheduleFromBackup(List<List<Machine>> schedule, List<List<Machine>> backup)
        {
            for (int i = 0; i < backup.Count; i++)
            {
                List<Machine> machineListClone = StaticOperations.CloneMachineList(backup[i]);
                schedule[i] = machineListClone;
            }
        }
        #endregion

        #region Recursive optimal group assembling
        private List<int> CalculateOptimalStartSequence(double maximumOutputPower, double minimumOutputPower,
            List<int> startableMachineIndices, List<Machine> hour, int load)
        {
            if (startableMachineIndices.Count == 0)
                return null;

            // 1.) Iterate over machines to find if it won't provide desired status
            List<int> deletees = new List<int>();
            foreach (int index in startableMachineIndices)
            {
                double currentMaximumOutputPower = maximumOutputPower + hour[index].MaximumOutputPower;
                double currentMinimumOutputPower = minimumOutputPower + hour[index].MinimumOutputPower;
                if (currentMaximumOutputPower >= load && currentMinimumOutputPower <= load)
                    return new List<int> { index };
                if (currentMinimumOutputPower > load)
                    deletees.Add(index);
            }

            // If code reaches this place, it means a single machine cannot provide desired status
            // 2.) Remove all machines that will go over the desired status on their own
            foreach (int index in deletees)
            {
                startableMachineIndices.Remove(index);
            }

            List<List<int>> viableCombinations = new List<List<int>>();
            foreach (int index in startableMachineIndices)
            {
                viableCombinations.Add(new List<int> { index });
            }

            return RecursiveCalculateOptimalStartSequence(maximumOutputPower, minimumOutputPower,
                viableCombinations, startableMachineIndices, hour, load);
        }

        private List<int> RecursiveCalculateOptimalStartSequence(double maximumOutputPower, double minimumOutputPower,
            List<List<int>> viableCombinations, List<int> startableMachineIndices, List<Machine> hour, int load)
        {
            if (viableCombinations.Count == 0)
                return null;

            List<List<int>> newViableCombinations = new List<List<int>>();
            foreach (List<int> viableCombination in viableCombinations)
            {
                int groupMaximumOutputPower = 0;
                int groupMinimumOutputPower = 0;
                foreach (int index in viableCombination)
                {
                    groupMaximumOutputPower += hour[index].MaximumOutputPower;
                    groupMinimumOutputPower += hour[index].MinimumOutputPower;
                }
                for (int i = 0; i < startableMachineIndices.Count; i++)
                {
                    if (viableCombination.Contains(startableMachineIndices[i]))
                        continue;

                    double currentMaximumOutputPower = groupMaximumOutputPower + 
                        hour[startableMachineIndices[i]].MaximumOutputPower + maximumOutputPower;
                    double currentMinimumOutputPower = groupMinimumOutputPower + 
                        hour[startableMachineIndices[i]].MinimumOutputPower + minimumOutputPower;

                    if (currentMaximumOutputPower >= load && currentMinimumOutputPower <= load)
                    {
                        viableCombination.Add(startableMachineIndices[i]);
                        return viableCombination;
                    }

                    if (currentMaximumOutputPower < load)
                    {
                        List<int> newViableCombination = new List<int>();
                        newViableCombination.AddRange(viableCombination);
                        newViableCombination.Add(startableMachineIndices[i]);
                        newViableCombinations.Add(newViableCombination);
                    }
                }
            }

            return RecursiveCalculateOptimalStartSequence(maximumOutputPower, minimumOutputPower, newViableCombinations,
                startableMachineIndices, hour, load);
        }

        private List<int> CalculateOptimalTerminateSequence(double maximumOutputPower, double minimumOutputPower,
            List<int> stoppableMachineIndices, List<Machine> hour, int load)
        {
            if (stoppableMachineIndices.Count == 0)
                return null;

            // 1.) Iterate over machines to find if it won't provide desired status
            List<int> deletees = new List<int>();
            foreach (int index in stoppableMachineIndices)
            {
                double currentMaximumOutputPower = maximumOutputPower - hour[index].MaximumOutputPower;
                double currentMinimumOutputPower = minimumOutputPower - hour[index].MinimumOutputPower;
                if (currentMaximumOutputPower >= load && currentMinimumOutputPower <= load)
                    return new List<int> { index };
                if (currentMaximumOutputPower < load)
                    deletees.Add(index);
            }

            // If code reaches this place, it means a single machine cannot provide desired status
            // 2.) Remove all machines that will go over the desired status on their own
            foreach (int index in deletees)
            {
                stoppableMachineIndices.Remove(index);
            }

            List<List<int>> viableCombinations = new List<List<int>>();
            foreach (int index in stoppableMachineIndices)
            {
                viableCombinations.Add(new List<int> { index });
            }

            return RecursiveCalculateOptimalTerminateSequence(maximumOutputPower, minimumOutputPower,
                viableCombinations, stoppableMachineIndices, hour, load);
        }

        private List<int> RecursiveCalculateOptimalTerminateSequence(double maximumOutputPower, double minimumOutputPower,
            List<List<int>> viableCombinations, List<int> stoppableMachineIndices, List<Machine> hour, int load)
        {
            if (viableCombinations.Count == 0)
                return null;

            List<List<int>> newViableCombinations = new List<List<int>>();
            foreach (List<int> viableCombination in viableCombinations)
            {
                int groupMaximumOutputPower = 0;
                int groupMinimumOutputPower = 0;
                foreach (int index in viableCombination)
                {
                    groupMaximumOutputPower += hour[index].MaximumOutputPower;
                    groupMinimumOutputPower += hour[index].MinimumOutputPower;
                }
                for (int i = 0; i < stoppableMachineIndices.Count; i++)
                {
                    if (viableCombination.Contains(stoppableMachineIndices[i]))
                        continue;

                    double currentMaximumOutputPower = maximumOutputPower - 
                        (groupMaximumOutputPower + hour[stoppableMachineIndices[i]].MaximumOutputPower);
                    double currentMinimumOutputPower = minimumOutputPower - 
                        (groupMinimumOutputPower + hour[stoppableMachineIndices[i]].MinimumOutputPower);

                    if (currentMaximumOutputPower >= load && currentMinimumOutputPower <= load)
                    {
                        viableCombination.Add(stoppableMachineIndices[i]);
                        return viableCombination;
                    }

                    if (currentMinimumOutputPower > load)
                    {
                        List<int> newViableCombination = new List<int>();
                        newViableCombination.AddRange(viableCombination);
                        newViableCombination.Add(stoppableMachineIndices[i]);
                        newViableCombinations.Add(newViableCombination);
                    }
                }
            }

            return RecursiveCalculateOptimalStartSequence(maximumOutputPower, minimumOutputPower, newViableCombinations,
                stoppableMachineIndices, hour, load);
        }
        #endregion
    }
}