using Medallion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitCommitment.Representation;
using UnitCommitment.Utilities;

namespace UnitCommitment.Evolution_algorithm_blocks
{
    public class GroupInitialisation
    {
        private int populationCount;
        private int scale;
        private Random random;
        private List<int> maximumHourChanges;

        public GroupInitialisation(int populationCount, int scale)
        {
            this.populationCount = populationCount;
            this.scale = scale;
            random = new Random();
        }

        #region Main initialisaton methods
        public List<Individual> GenerateGroupInitialPopulation()
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
                maximumHourChanges = StaticOperations.GenerateMaximumHourlyChanges(scale);

                Group baseGroup = new Group(new List<int> { 0, 1 });
                Group intermittentGroup = new Group(new List<int> { 2, 3, 4 });
                Group semiPeakGroup = new Group(new List<int> { 5, 6 });
                Group peakGroup = new Group(new List<int> { 7, 8, 9});
                List<Group> groups = new List<Group> { baseGroup, intermittentGroup, semiPeakGroup, peakGroup };
                List<List<Machine>> scheduleBackup = BackupSchedule(schedule);

                schedule[0] = RandomMachineHourGrouppedScheduleInitialisation(schedule[0], groups, hourlyPowerDemand[0]);
                ReviseMachineStatus(schedule[0], initialState);

                for (int j = 1; j < 24; j++)
                {
                    schedule[j] = RandomMachineHourGrouppedScheduleInitialisationWithReference(schedule[j], schedule[j - 1], groups, hourlyPowerDemand[j], out bool success);
                    ReviseMachineStatus(schedule[j], schedule[j - 1]);
                    if (success == false)
                    {
                        j = 0;
                        RestoreScheduleFromBackup(schedule, scheduleBackup);
                        schedule[0] = RandomMachineHourGrouppedScheduleInitialisation(schedule[0], groups, hourlyPowerDemand[0]);
                        ReviseMachineStatus(schedule[0], initialState);
                    }
                }

                Individual representation = new Individual(schedule)
                {
                    groups = groups
                };
                initialPopulation.Add(representation);
            }

            return initialPopulation;
        }

        public List<Individual> GenerateRandomGroupInitialPopulation()
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
                maximumHourChanges = StaticOperations.GenerateMaximumHourlyChanges(scale);

                List<Group> randomGroups = GenerateRandomGroup(scale);

                List<List<Machine>> scheduleBackup = BackupSchedule(schedule);

                schedule[0] = RandomMachineHourGrouppedScheduleInitialisation(schedule[0], randomGroups, hourlyPowerDemand[0]);
                ReviseMachineStatus(schedule[0], initialState);

                for (int j = 1; j < 24; j++)
                {
                    schedule[j] = RandomMachineHourGrouppedScheduleInitialisationWithReference(schedule[j], schedule[j - 1], randomGroups, hourlyPowerDemand[j], out bool success);
                    ReviseMachineStatus(schedule[j], schedule[j - 1]);
                    if (success == false)
                    {
                        j = 0;
                        RestoreScheduleFromBackup(schedule, scheduleBackup);
                        randomGroups = GenerateRandomGroup(scale);
                        schedule[0] = RandomMachineHourGrouppedScheduleInitialisation(schedule[0], randomGroups, hourlyPowerDemand[0]);
                        ReviseMachineStatus(schedule[0], initialState);
                    }
                }

                Individual representation = new Individual(schedule)
                {
                    groups = randomGroups
                };
                initialPopulation.Add(representation);
            }

            return initialPopulation;
        }
        #endregion

        #region Hour mutation changes
        public List<Machine> RandomMachineHourGrouppedScheduleInitialisation(List<Machine> hourSchedule, List<Group> groups, int load)
        {
            List<Machine> hourScheduleClone = StaticOperations.CloneMachineList(hourSchedule);
            List<int> notRunningGroupIndices = new List<int>();
            for (int i = 0; i < groups.Count; i++)
            {
                notRunningGroupIndices.Add(i);
            }

            double maximumTotalLoad = 0;
            double minimumTotalLoad = 0;
            while (true)
            {
                notRunningGroupIndices.Shuffle();
                int index = notRunningGroupIndices[0];
                notRunningGroupIndices.RemoveAt(0);

                // Start groups
                for (int i = 0; i < groups[index].grouppedMachines.Count; i++)
                {
                    int machineIndex = groups[index].grouppedMachines[i];
                    hourScheduleClone[machineIndex].CurrentOutputPower = hourScheduleClone[machineIndex].MaximumOutputPower;
                    maximumTotalLoad += hourScheduleClone[machineIndex].MaximumOutputPower;
                    minimumTotalLoad += hourScheduleClone[machineIndex].MinimumOutputPower;
                }

                if (maximumTotalLoad >= load)
                {
                    if (minimumTotalLoad <= load)
                        break;
                    else
                    {
                        hourScheduleClone = StaticOperations.CloneMachineList(hourSchedule);
                        for (int i = 0; i < groups.Count; i++)
                        {
                            notRunningGroupIndices.Add(i);
                        }
                    }
                }
            }

            double totalLoad = maximumTotalLoad;
            while (totalLoad > load)
            {
                List<int> adjustableMachineIndices = GetLowerableMachineIndices(hourScheduleClone);
                adjustableMachineIndices.Shuffle();
                int index = adjustableMachineIndices[0];

                double change = random.NextDouble() * (totalLoad - load);
                if (totalLoad - load < 1)
                    change = totalLoad - load;

                double maximumAllowedChange = hourScheduleClone[index].CurrentOutputPower - hourScheduleClone[index].MinimumOutputPower;
                change = change > maximumAllowedChange ? maximumAllowedChange : change;

                hourScheduleClone[index].CurrentOutputPower -= change;
                totalLoad -= change;
            }

            return hourScheduleClone;
        }

        private List<Machine> RandomMachineHourGrouppedScheduleInitialisationWithReference(
            List<Machine> hourSchedule, List<Machine> previous, List<Group> groups, int load, out bool success)
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
                List<int> machineGroupsToStop = null;
                if (!(totalMaximumPower > load && totalMinimumPower < load))
                {
                    List<int> stoppableMachineIndices = GetStoppableMachineGroupIndices(previous, groups);
                    if (stoppableMachineIndices.Count == 0)
                    {
                        success = false;
                        return hourClone;
                    }
                    IEnumerable<IEnumerable<int>> permutations = GenerateAllPermutations(stoppableMachineIndices, stoppableMachineIndices.Count);
                    List<List<int>> permutationLists = new List<List<int>>();
                    foreach (IEnumerable<int> permutation in permutations)
                    {
                        permutationLists.Add(permutation.ToList());
                    }
                    for (int i = 0; i < permutationLists.Count; i++)
                    {
                        double currentTotalMinimumPower = totalMinimumPower;
                        double currentTotalMaximumPower = totalMaximumPower;
                        machineGroupsToStop = new List<int>();
                        currentSuccess = false;
                        for (int j = 0; j < permutationLists[i].Count; j++)
                        {
                            int chosenIndex = permutationLists[i][j];
                            machineGroupsToStop.Add(chosenIndex);
                            currentTotalMaximumPower = CalculateMaximumAchievablePower(previous, maximumHourChanges, machineGroupsToStop);
                            currentTotalMinimumPower = CalculateMinimumAchievablePower(previous, maximumHourChanges, machineGroupsToStop);
                            if (currentTotalMinimumPower < load && currentTotalMaximumPower > load)
                            {
                                currentSuccess = true;
                                break;
                            }
                        }
                        if (currentSuccess == true)
                        {
                            foreach (int stopGroup in machineGroupsToStop)
                            {
                                foreach (int stop in groups[stopGroup].grouppedMachines)
                                {
                                    hourClone[stop].CurrentOutputPower = 0;
                                }
                            }
                            totalMaximumPower = currentTotalMaximumPower;
                            totalMinimumPower = currentTotalMinimumPower;
                            break;
                        }
                    }
                    if (StaticOperations.SpecialValidation(hourClone, previous) == false)
                        throw new NotSupportedException();
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
                List<int> machineGroupsToStart = null;
                if (!(totalMaximumPower > load && totalMinimumPower < load))
                {
                    List<int> startableMachineIndices = GetStartableMachineGroupIndices(previous, groups);
                    if (startableMachineIndices.Count == 0)
                    {
                        success = false;
                        return hourClone;
                    }
                    IEnumerable<IEnumerable<int>> permutations = GenerateAllPermutations(startableMachineIndices, startableMachineIndices.Count);
                    List<List<int>> permutationLists = new List<List<int>>();
                    foreach (IEnumerable<int> permutation in permutations)
                    {
                        permutationLists.Add(permutation.ToList());
                    }
                    for (int i = 0; i < permutationLists.Count; i++)
                    {
                        double currentTotalMinimumPower = totalMinimumPower;
                        double currentTotalMaximumPower = totalMaximumPower;
                        machineGroupsToStart = new List<int>();
                        currentSuccess = false;
                        for (int j = 0; j < permutationLists[i].Count; j++)
                        {
                            int chosenIndex = permutationLists[i][j];
                            machineGroupsToStart.Add(chosenIndex);
                            currentTotalMaximumPower += CalculateMaximumGroupPower(hourClone, groups[chosenIndex]);
                            currentTotalMinimumPower += CalculateMinimumGroupPower(hourClone, groups[chosenIndex]);
                            if (currentTotalMinimumPower < load && currentTotalMaximumPower > load)
                            {
                                currentSuccess = true;
                                break;
                            }
                        }
                        if (currentSuccess == true)
                        {
                            foreach (int startGroup in machineGroupsToStart)
                            {
                                foreach (int start in groups[startGroup].grouppedMachines)
                                {
                                    hourClone[start].CurrentOutputPower = hourClone[start].MaximumOutputPower;
                                }
                            }
                            totalMaximumPower = currentTotalMaximumPower;
                            totalMinimumPower = currentTotalMinimumPower;
                            break;
                        }
                    }
                    if (StaticOperations.SpecialValidation(hourClone, previous) == false)
                        throw new NotSupportedException();
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
        #endregion

        #region Status revision
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

        private List<Group> GenerateRandomGroup(int scale)
        {
            List<int> indices = Enumerable.Range(0, scale * 10).ToList();
            // First group may have 1 - 7 members, n1 members
            // Second group may have 1 - (10 - n1)/3, n2 members
            // Third group may have 1 - (10 - n1 - n2)/2, n3 members
            // Fourth group may have 1 - (10 - n1 - n2 - n3), n4 members
            indices.Shuffle();
            int firstGroupCount = random.Next(1, 10 * scale - 2);
            int secondGroupCount = random.Next(1, (10 * scale - firstGroupCount) / 3);
            int thirdGroupCount = random.Next(1, (10 * scale - firstGroupCount - secondGroupCount) / 2);
            int fourthGroupCount = 10 * scale - firstGroupCount - secondGroupCount - thirdGroupCount;
            List<int> firstGroupMachines = new List<int>();
            for (int i = 0; i < firstGroupCount; i++)
            {
                int index = random.Next(0, indices.Count);
                firstGroupMachines.Add(indices[index]);
                indices.RemoveAt(index);
            }
            List<int> secondGroupMachines = new List<int>();
            for (int i = 0; i < secondGroupCount; i++)
            {
                int index = random.Next(0, indices.Count);
                secondGroupMachines.Add(indices[index]);
                indices.RemoveAt(index);
            }
            List<int> thirdGroupMachines = new List<int>();
            for (int i = 0; i < thirdGroupCount; i++)
            {
                int index = random.Next(0, indices.Count);
                thirdGroupMachines.Add(indices[index]);
                indices.RemoveAt(index);
            }
            List<int> fourthGroupMachines = new List<int>();
            for (int i = 0; i < fourthGroupCount; i++)
            {
                int index = random.Next(0, indices.Count);
                fourthGroupMachines.Add(indices[index]);
                indices.RemoveAt(index);
            }

            Group firstGroup = new Group(firstGroupMachines);
            Group secondGroup = new Group(secondGroupMachines);
            Group thirdGroup = new Group(thirdGroupMachines);
            Group fourthGroup = new Group(fourthGroupMachines);

            return new List<Group> { firstGroup, secondGroup, thirdGroup, fourthGroup };
        }

        #region Mutation utilities
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

        private List<int> GetStoppableMachineGroupIndices(List<Machine> previous, List<Group> groups)
        {
            List<int> runningGroupIndices = new List<int>();
            for (int i = 0; i < groups.Count; i++)
            {
                int index = groups[i].grouppedMachines[0];
                if (previous[index].CurrentOutputPower > 0)
                    runningGroupIndices.Add(i);

            }
            if (runningGroupIndices.Count == 0)
                return new List<int>();

            List<int> stoppableMachineGroupIndices = new List<int>();
            for (int i = 0; i < runningGroupIndices.Count; i++)
            {
                bool startable = true;
                for (int j = 0; j < groups[runningGroupIndices[i]].grouppedMachines.Count; j++)
                {
                    int index = groups[runningGroupIndices[i]].grouppedMachines[j];
                    if (previous[index].CurrentStatus < previous[index].MinimumUptime)
                    {
                        startable = false;
                        break;
                    }
                }
                if (startable)
                    stoppableMachineGroupIndices.Add(runningGroupIndices[i]);
            }

            return stoppableMachineGroupIndices;
        }

        private List<int> GetStartableMachineGroupIndices(List<Machine> previous, List<Group> groups)
        {
            List<int> notRunningGroupIndices = new List<int>();
            for (int i = 0; i < groups.Count; i++)
            {
                int index = groups[i].grouppedMachines[0];
                if (previous[index].CurrentOutputPower == 0)
                    notRunningGroupIndices.Add(i);

            }
            if (notRunningGroupIndices.Count == 0)
                return new List<int>();

            List<int> startableMachineGroupIndices = new List<int>();
            for (int i = 0; i < notRunningGroupIndices.Count; i++)
            {
                bool startable = true;
                for (int j = 0; j < groups[notRunningGroupIndices[i]].grouppedMachines.Count; j++)
                {
                    int index = groups[notRunningGroupIndices[i]].grouppedMachines[j];
                    if (Math.Abs(previous[index].CurrentStatus) < previous[index].MinimumDowntime)
                    {
                        startable = false;
                        break;
                    }
                }
                if (startable)
                    startableMachineGroupIndices.Add(notRunningGroupIndices[i]);
            }

            return startableMachineGroupIndices;
        }

        private IEnumerable<IEnumerable<int>> GenerateAllPermutations(List<int> template, int length)
        {
            if (length <= 0)
                throw new NullReferenceException();

            if (length == 1) return template.Select(t => new int[] { t });

            return GenerateAllPermutations(template, length - 1)
                .SelectMany(t => template.Where(e => !t.Contains(e)),
                    (t1, t2) => t1.Concat(new int[] { t2 }));
        }

        private int CalculateMaximumGroupPower(List<Machine> template, Group group)
        {
            int maximumPower = 0;
            for (int i = 0; i < group.grouppedMachines.Count; i++)
            {
                int index = group.grouppedMachines[i];
                maximumPower += template[index].MaximumOutputPower;
            }

            return maximumPower;
        }

        private int CalculateMinimumGroupPower(List<Machine> template, Group group)
        {
            int minimumPower = 0;
            for (int i = 0; i < group.grouppedMachines.Count; i++)
            {
                int index = group.grouppedMachines[i];
                minimumPower += template[index].MinimumOutputPower;
            }

            return minimumPower;
        }
        #endregion

        #region Miscs
        private void RestoreScheduleFromBackup(List<List<Machine>> schedule, List<List<Machine>> backup)
        {
            for (int i = 0; i < backup.Count; i++)
            {
                List<Machine> machineListClone = StaticOperations.CloneMachineList(backup[i]);
                schedule[i] = machineListClone;
            }
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
        #endregion
    }
}
