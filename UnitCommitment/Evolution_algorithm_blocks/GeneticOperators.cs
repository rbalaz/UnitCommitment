using Medallion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnitCommitment.Representation;
using UnitCommitment.Utilities;

namespace UnitCommitment.Evolution_algorithm_blocks
{
    public class GeneticOperators
    {
        private List<int> maximumHourlyChange;
        private int scale;

        public GeneticOperators(int scale)
        {
            this.scale = scale;
            maximumHourlyChange = StaticOperations.GenerateMaximumHourlyChanges(scale);
        }

        public List<Individual> GenerateDescendants(Individual mother, Individual father)
        {
            Individual motherClone = Individual.CloneRepresentation(mother);
            Individual fatherClone = Individual.CloneRepresentation(father);

            Individual mutatedMother = MutateIndividual(motherClone);
            Individual mutatedFather = MutateIndividual(fatherClone);
            if (StaticOperations.ValidateIndividual(mutatedMother) == false)
            {
                mutatedMother = Individual.CloneRepresentation(mother);
            }
            
            if (StaticOperations.ValidateIndividual(mutatedFather) == false)
            {
                mutatedFather = Individual.CloneRepresentation(father);
            }

            List<Individual> descendants = CrossOverIndividuals(mutatedMother, mutatedFather);

            return descendants;
        }

        #region Operators
        private Individual MutateIndividual(Individual individual)
        {
            Individual individualClone = Individual.CloneRepresentation(individual);
            // Every hour needs to be mutated separately
            // after mutating one of the hours, values for 
            // turned on time on machines need to be revised
            // Revision is needed only if start/terminate operators are used
            Random random = new Random();
            for (int i = 0; i < individualClone.Schedule.Count; i++)
            {
                double mutationChange = random.NextDouble();
                if (mutationChange <= 0.25)
                {
                    int mutationEffect = random.Next(0, 3);

                    // Increase mutation
                    if (mutationEffect == 0)
                    {
                        individualClone.Schedule[i] = IncreaseMutation(individualClone.Schedule[i], 
                            i == 0 ? null : individualClone.Schedule[i - 1], random);
                        if (StaticOperations.ValidateIndividual(individualClone) == false)
                            individualClone.Schedule[i] = StaticOperations.CloneMachineList(individual.Schedule[i]);
                    }
                    // Decrease mutation
                    if (mutationEffect == 1)
                    {
                        individualClone.Schedule[i] = DecreaseMutation(individualClone.Schedule[i],
                            i == 0 ? null : individualClone.Schedule[i - 1], random);
                        if (StaticOperations.ValidateIndividual(individualClone) == false)
                            individualClone.Schedule[i] = StaticOperations.CloneMachineList(individual.Schedule[i]);
                    }
                    // Start mutation
                    if (mutationEffect == 2)
                    {
                        individualClone.Schedule[i] = StartMutation(individualClone.Schedule[i],
                            i == 0 ? null : individualClone.Schedule[i - 1], random);
                        if (StaticOperations.ValidateIndividual(individualClone) == false)
                            individualClone.Schedule[i] = StaticOperations.CloneMachineList(individual.Schedule[i]);

                        ReviseMachineStatus(individualClone);
                    }
                    // Terminate mutation
                    if (mutationEffect == 3)
                    {
                        individualClone.Schedule[i] = TerminateMutation(individualClone.Schedule[i],
                            i == 0 ? null : individualClone.Schedule[i - 1], random);
                        if (StaticOperations.ValidateIndividual(individualClone) == false)
                            individualClone.Schedule[i] = StaticOperations.CloneMachineList(individual.Schedule[i]);
                        
                        ReviseMachineStatus(individualClone);
                    }
                }
            }

            return individualClone;
        }

        private List<Machine> IncreaseMutation(List<Machine> hourSchedule, List<Machine> previous, Random random)
        {
            // Increases value of a randomly chosen running machine
            List<Machine> machineListClone = StaticOperations.CloneMachineList(hourSchedule);

            // Get currently running machines
            List<int> runningMachinesIndices = GetRunningMachineIndices(hourSchedule);

            // Randomly pick one running machine and generate chagne value
            int mutationIndex = runningMachinesIndices[random.Next(0, runningMachinesIndices.Count)];
            double maximumAllowedIncrease;
            if(previous != null)
                maximumAllowedIncrease = GetMaximumMachineIncrease(hourSchedule[mutationIndex],
                    previous[mutationIndex], maximumHourlyChange[mutationIndex]);
            else
                maximumAllowedIncrease = GetMaximumMachineIncrease(hourSchedule[mutationIndex],
                    null, maximumHourlyChange[mutationIndex]);
            double change = random.NextDouble() * maximumAllowedIncrease;
            // Increase power value of selected machine
            machineListClone[mutationIndex].CurrentOutputPower += change;

            // If machine with full power was chosen, no changes can be made
            if (change == 0)
                return hourSchedule;

            // Try reducing power of other machines to compensate the change
            double load = hourSchedule.Sum(item => item.CurrentOutputPower);
            while (change > 0)
            {
                List<int> lowerableMachineIndices = GetLowerableMachineIndices(machineListClone,previous, maximumHourlyChange);
                lowerableMachineIndices.Remove(mutationIndex);
                if (lowerableMachineIndices.Count > 0)
                {
                    // Randomise indices
                    lowerableMachineIndices.Shuffle();
                    int index = lowerableMachineIndices[0];
                    double maximumAllowedDecrease;
                    if (previous != null)
                        maximumAllowedDecrease = GetMaximumMachineDecrease(machineListClone[index],
                            previous[index], maximumHourlyChange[index]);
                    else
                        maximumAllowedDecrease = GetMaximumMachineDecrease(machineListClone[index],
                            null, maximumHourlyChange[index]);

                    // If machine can fully compensate power increase, it will be used to do it                    
                    if (maximumAllowedDecrease > change)
                    {
                        machineListClone[index].CurrentOutputPower -= change;

                    }
                    // If not, its power is reduced as much as possible 
                    else
                    {
                        machineListClone[index].CurrentOutputPower -= maximumAllowedDecrease;
                    }
                    change = machineListClone.Sum(item => item.CurrentOutputPower) - load;
                }
                else
                    break;
            }

            // If change was fully compensated, mutation was finished
            if (change == 0)
                return machineListClone;
            // If change was not fully compensated, mutation failed
            else
                return hourSchedule;
        }

        private List<Machine> DecreaseMutation(List<Machine> hourSchedule, List<Machine> previous, Random random)
        {
            // Decreases value of a randomly chosen running machine
            List<Machine> machineListClone = StaticOperations.CloneMachineList(hourSchedule);

            // Get currently running machines
            List<int> runningMachinesIndices = GetRunningMachineIndices(hourSchedule);

            // Randomly pick one running machine and generate chagne value
            int mutationIndex = runningMachinesIndices[random.Next(0, runningMachinesIndices.Count)];
            double maximumAllowedDecrease;
            if (previous != null)
                maximumAllowedDecrease = GetMaximumMachineDecrease(hourSchedule[mutationIndex],
                    previous[mutationIndex], maximumHourlyChange[mutationIndex]);
            else
                maximumAllowedDecrease = GetMaximumMachineDecrease(hourSchedule[mutationIndex], 
                    null, maximumHourlyChange[mutationIndex]);
            double change = random.NextDouble() * maximumAllowedDecrease;

            // Decrease power value of selected machine
            machineListClone[mutationIndex].CurrentOutputPower -= change;

            // If machine with lowest power was chosen, no changes can be made
            if (change == 0)
                return hourSchedule;

            // Try increasing power of other machines to compensate the change
            double load = hourSchedule.Sum(item => item.CurrentOutputPower);
            while (change > 0)
            {
                List<int> incresableMachineIndices = GetIncreasableMachineIndices(machineListClone, previous, maximumHourlyChange);
                incresableMachineIndices.Remove(mutationIndex);
                if (incresableMachineIndices.Count > 0)
                {
                    // Randomise indices
                    incresableMachineIndices.Shuffle();
                    int index = incresableMachineIndices[0];
                    double maximumAllowedIncrease;
                    if (previous != null)
                        maximumAllowedIncrease = GetMaximumMachineIncrease(machineListClone[index],
                            previous[index], maximumHourlyChange[index]);
                    else
                        maximumAllowedIncrease = GetMaximumMachineIncrease(machineListClone[index],
                            null, maximumHourlyChange[index]);

                    // If machine can fully compensate power increase, it will be used to do it
                    if (maximumAllowedIncrease > change)
                    {
                        machineListClone[index].CurrentOutputPower += change;
                    }
                    // If not, its power is reduced to minimum output power
                    else
                    {
                        machineListClone[index].CurrentOutputPower += maximumAllowedIncrease;
                    }
                    change = load - machineListClone.Sum(item => item.CurrentOutputPower);
                }
                else
                    break;
            }

            // If change was fully compensated, mutation was finished
            if (change == 0)
                return machineListClone;
            // If change was not fully compensated, mutation failed
            else
                return hourSchedule;
        }

        private List<Machine> StartMutation(List<Machine> hourSchedule, List<Machine> previous, Random random)
        {
            // Starts a random machine that is allowed to be started
            // with minimum output power
            List<Machine> machineListClone = StaticOperations.CloneMachineList(hourSchedule);

            List<int> startableMachineIndices = GetStartableMachineIndices(hourSchedule);

            // If not startable machines are found, operator is not applicable
            if (startableMachineIndices.Count == 0)
                return hourSchedule;

            startableMachineIndices.Shuffle();

            // Randomly pick one of the startable machines and start if with minimum power output
            int startIndex = startableMachineIndices[0];
            machineListClone[startIndex].CurrentOutputPower = machineListClone[startIndex].MinimumOutputPower;

            double change = machineListClone[startIndex].CurrentOutputPower;

            // Get currently running machines
            List<int> runningMachinesIndices = GetRunningMachineIndices(machineListClone);
            runningMachinesIndices.Remove(startIndex);

            // Power produced by newly started machine needs to be compensated
            double load = hourSchedule.Sum(item => item.CurrentOutputPower);
            while (change > 0)
            {
                List<int> lowerableMachineIndices = GetLowerableMachineIndices(machineListClone, previous, maximumHourlyChange);
                if (lowerableMachineIndices.Count > 0)
                {
                    // Randomise indices
                    lowerableMachineIndices.Shuffle();
                    int index = lowerableMachineIndices[0];
                    double maximumAllowedDecrease;
                    if (previous != null)
                        maximumAllowedDecrease = GetMaximumMachineDecrease(machineListClone[index],
                            previous[index], maximumHourlyChange[index]);
                    else
                        maximumAllowedDecrease = GetMaximumMachineDecrease(machineListClone[index],
                            null, maximumHourlyChange[index]);

                    // If machine can fully compensate power increase, it will be used to do it                    
                    if (maximumAllowedDecrease > change)
                    {
                        machineListClone[index].CurrentOutputPower -= change;

                    }
                    // If not, its power is reduced as much as possible 
                    else
                    {
                        machineListClone[index].CurrentOutputPower -= maximumAllowedDecrease;
                    }
                    change = machineListClone.Sum(item => item.CurrentOutputPower) - load;
                }
                else
                    break;
            }

            // If change was fully compensated, mutation was finished
            if (change == 0)
                return machineListClone;
            // If change was not fully compensated, mutation failed
            else
                return hourSchedule;
        }

        private List<Machine> TerminateMutation(List<Machine> hourSchedule, List<Machine> previous, Random random)
        {
            // Terminates randomly chosen running machine
            List<Machine> machineListClone = StaticOperations.CloneMachineList(hourSchedule);

            // Get indices of currently running machines
            List<int> terminatableMachinesIndices = GetTerminatableMachineIndices(hourSchedule);
            if (terminatableMachinesIndices.Count == 0)
                return hourSchedule;

            terminatableMachinesIndices.Shuffle();
            int terminateIndex = terminatableMachinesIndices[0];
            double change = machineListClone[terminateIndex].CurrentOutputPower;
            machineListClone[terminateIndex].CurrentOutputPower = 0;

            List<int> runningMachinesIndices = GetRunningMachineIndices(machineListClone);
            // Try increasing power of other machines to compensate change
            double load = hourSchedule.Sum(item => item.CurrentOutputPower);
            while (change > 0)
            {
                List<int> incresableMachineIndices = GetIncreasableMachineIndices(machineListClone, previous, maximumHourlyChange);
                if (incresableMachineIndices.Count > 0)
                {
                    // Randomise indices
                    incresableMachineIndices.Shuffle();
                    int index = incresableMachineIndices[0];
                    double maximumAllowedIncrease;
                    if (previous != null)
                        maximumAllowedIncrease = GetMaximumMachineIncrease(machineListClone[index],
                            previous[index], maximumHourlyChange[index]);
                    else
                        maximumAllowedIncrease = GetMaximumMachineIncrease(machineListClone[index],
                            null, maximumHourlyChange[index]);

                    // If machine can fully compensate power increase, it will be used to do it
                    if (maximumAllowedIncrease > change)
                    {
                        machineListClone[index].CurrentOutputPower += change;
                    }
                    // If not, its power is reduced to minimum output power
                    else
                    {
                        machineListClone[index].CurrentOutputPower += maximumAllowedIncrease;
                    }
                    change = load - machineListClone.Sum(item => item.CurrentOutputPower);
                }
                else
                    break;
            }

            // If change was fully compensated, mutation was finished
            if (change == 0)
                return machineListClone;
            // If change was not fully compensated, mutation failed
            else
                return hourSchedule;
        }

        private List<Individual> CrossOverIndividuals(Individual mutatedMother, Individual mutatedFather)
        {
            // Descendants are created by randomly switching a randomly generated amount of hours
            // Only corresponding hours may be switched and only when both individuals have the same machines running
            Individual fatherClone = Individual.CloneRepresentation(mutatedFather);
            Individual motherClone = Individual.CloneRepresentation(mutatedMother);

            Random random = new Random();
            double chance = random.NextDouble();
            for (int i = 0; i < fatherClone.Schedule.Count; i++)
            {
                double value = random.NextDouble();
                if (value >= chance)
                {
                    if (IsCrossOverApplicable(fatherClone.Schedule[i], motherClone.Schedule[i]))
                    {
                        List<Machine> temp = fatherClone.Schedule[i];
                        fatherClone.Schedule[i] = motherClone.Schedule[i];
                        motherClone.Schedule[i] = temp;
                    }
                }
            }

            ReviseMachineStatus(fatherClone);
            ReviseMachineStatus(motherClone);
            List<Individual> descendants = new List<Individual>();

            if (StaticOperations.ValidateIndividual(motherClone) == false)
                descendants.Add(mutatedMother);
            else
                descendants.Add(motherClone);

            if (StaticOperations.ValidateIndividual(fatherClone) == false)
                descendants.Add(mutatedFather);
            else
                descendants.Add(fatherClone);

            return descendants;
        }
        #endregion

        #region Utilities
        private void ReviseMachineStatus(Individual representation)
        {
            List<int> initialState = StaticOperations.GenerateInitialMachinesState(scale);
            
            // Calculate values of current machine states
            for (int i = 0; i < representation.Schedule.Count; i++)
            {
                List<int> referenceStates = null;
                if (i == 0)
                {
                    referenceStates = initialState;
                }
                else
                {
                    referenceStates = representation.Schedule[i - 1].Select(machine => machine.CurrentStatus).ToList();
                }
                for (int j = 0; j < representation.Schedule[i].Count; j++)
                {
                    Machine current = representation.Schedule[i][j];
                    if (current.CurrentOutputPower > 0)
                    {
                        // Machine was running in previous hour
                        if (referenceStates[j] > 0)
                            current.CurrentStatus = referenceStates[j] + 1;
                        // Machine was turned off in previous hour
                        if (referenceStates[j] < 0)
                            current.CurrentStatus = 1;
                    }
                    if (current.CurrentOutputPower == 0)
                    {
                        // Machine was running in previous hour
                        if (referenceStates[j] > 0)
                            current.CurrentStatus = -1;
                        if (referenceStates[j] < 0)
                            current.CurrentStatus = referenceStates[j] - 1;
                    }
                }
            }
        }

        private List<int> GetStartableMachineIndices(List<Machine> hourSchedule)
        {
            List<int> startableMachineIndices = new List<int>();
            for (int i = 0; i < hourSchedule.Count; i++)
            {
                Machine selected = hourSchedule[i];
                if (selected.CurrentStatus < 0)
                {
                    if (Math.Abs(selected.CurrentStatus) >= selected.MinimumDowntime)
                        startableMachineIndices.Add(i);
                }
            }

            return startableMachineIndices;
        }

        private List<int> GetRunningMachineIndices(List<Machine> hourSchedule)
        {
            // Get currently running machines
            List<int> runningMachinesIndices = new List<int>();

            for (int i = 0; i < hourSchedule.Count; i++)
            {
                if (hourSchedule[i].CurrentOutputPower > 0)
                    runningMachinesIndices.Add(i);
            }

            return runningMachinesIndices;
        }

        private List<int> GetTerminatableMachineIndices(List<Machine> hourSchedule)
        {
            // Get currently running machines
            List<int> terminatableMachinesIndices = new List<int>();

            for (int i = 0; i < hourSchedule.Count; i++)
            {
                if (hourSchedule[i].CurrentOutputPower > 0 && hourSchedule[i].CurrentStatus >= hourSchedule[i].MinimumUptime)
                    terminatableMachinesIndices.Add(i);
            }

            return terminatableMachinesIndices;
        }

        private bool IsCrossOverApplicable(List<Machine> motherHour, List<Machine> fatherHour)
        {
            for (int i = 0; i < motherHour.Count; i++)
            {
                if (motherHour[i].CurrentOutputPower == 0 && fatherHour[i].CurrentOutputPower > 0)
                    return false;
                if (fatherHour[i].CurrentOutputPower == 0 && motherHour[i].CurrentOutputPower > 0)
                    return false;
            }

            return true;
        }

        private double GetMaximumMachineDecrease(Machine machine, Machine previous, double maximumHourlyChange)
        {
            double maximumPossibleChange = machine.CurrentOutputPower - machine.MinimumOutputPower;
            if (previous == null)
                return maximumPossibleChange;
            double maximumAllowedChange = maximumHourlyChange - Math.Abs(machine.CurrentOutputPower - previous.CurrentOutputPower);

            if (previous.CurrentOutputPower == 0)
                maximumAllowedChange = maximumPossibleChange;

            return maximumAllowedChange < maximumPossibleChange ? maximumAllowedChange : maximumPossibleChange;
        }

        private double GetMaximumMachineIncrease(Machine machine, Machine previous, double maximumHourlyChange)
        {
            double maximumPossibleChange = machine.MaximumOutputPower - machine.CurrentOutputPower;
            if (previous == null)
                return maximumPossibleChange;
            double maximumAllowedChange = maximumHourlyChange - Math.Abs(machine.CurrentOutputPower - previous.CurrentOutputPower);

            if (previous.CurrentOutputPower == 0)
                maximumAllowedChange = maximumPossibleChange;

            return maximumAllowedChange < maximumPossibleChange ? maximumAllowedChange : maximumPossibleChange;
        }

        private List<int> GetLowerableMachineIndices(List<Machine> template, List<Machine> previous, List<int> maximumHourChanges)
        {
            List<int> adjustableMachineIndices = new List<int>();
            if (previous == null)
            {
                for (int i = 0; i < template.Count; i++)
                {
                    if (template[i].CurrentOutputPower > template[i].MinimumOutputPower)
                        adjustableMachineIndices.Add(i);
                }
            }
            else
            {
                for (int i = 0; i < template.Count; i++)
                {
                    if (template[i].CurrentOutputPower > template[i].MinimumOutputPower && template[i].CurrentOutputPower > 0)
                    {
                        double maximumAllowedChange = GetMaximumMachineDecrease(template[i], previous[i], maximumHourChanges[i]);
                        if (maximumAllowedChange > 0.001)
                        {
                            if (previous[i].CurrentOutputPower == 0)
                                adjustableMachineIndices.Add(i);
                            else if (Math.Abs(template[i].CurrentOutputPower - previous[i].CurrentOutputPower) < maximumHourChanges[i])
                                adjustableMachineIndices.Add(i);
                        }
                    }
                }
            }
            return adjustableMachineIndices;
        }

        private List<int> GetIncreasableMachineIndices(List<Machine> template, List<Machine> previous, List<int> maximumHourChanges)
        {
            List<int> adjustableMachineIndices = new List<int>();
            if (previous == null)
            {
                for (int i = 0; i < template.Count; i++)
                {
                    if (template[i].CurrentOutputPower < template[i].MaximumOutputPower)
                        adjustableMachineIndices.Add(i);
                }
            }
            else
            {
                for (int i = 0; i < template.Count; i++)
                {
                    if (template[i].CurrentOutputPower < template[i].MaximumOutputPower && template[i].CurrentOutputPower > 0)
                    {
                        double maximumAllowedChange = GetMaximumMachineIncrease(template[i], previous[i], maximumHourChanges[i]);
                        if (maximumAllowedChange > 0.001)
                        {
                            if (previous[i].CurrentOutputPower == 0)
                                adjustableMachineIndices.Add(i);
                            else if (Math.Abs(template[i].CurrentOutputPower - previous[i].CurrentOutputPower) < maximumHourChanges[i])
                            {
                                adjustableMachineIndices.Add(i);
                            }
                        }
                    }
                }
            }
            return adjustableMachineIndices;
        }
        #endregion
    }
}
