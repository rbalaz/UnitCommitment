using System;
using System.Collections.Generic;
using System.Linq;
using UnitCommitment.Representation;
using UnitCommitment.Utilities;

namespace UnitCommitment.Evolution_algorithm_blocks
{
    public class Evaluation
    {
        private List<int> initialState;
        private int scale;

        public Evaluation(int scale)
        {
            initialState = StaticOperations.GenerateInitialMachinesState(scale);
            this.scale = scale;
        }

        public void EvaluateIndividual(Individual representation)
        {
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

            // Calculate operating cost for each hour
            double totalOperationCost = 0;
            for (int i = 0; i < representation.Schedule.Count; i++)
            {
                // Calculate fuel costs
                double currentTotalFuelCost = 0;
                foreach (Machine machine in representation.Schedule[i])
                {
                    double currentMachineFuelCost = machine.CalculateFuelCostCoefficient() * machine.CurrentOutputPower;
                    currentTotalFuelCost += currentMachineFuelCost;
                }
                // Calculate start-up costs
                List<int> referenceStates = null;
                if (i == 0)
                {
                    referenceStates = initialState;
                }
                else
                {
                    referenceStates = representation.Schedule[i - 1].Select(machine => machine.CurrentStatus).ToList();
                }
                double currentTotalStartUpCost = 0;
                for (int j = 0; j < representation.Schedule[i].Count; j++)
                {
                    Machine current = representation.Schedule[i][j];
                    if (current.CurrentStatus == 1)
                    {
                        if (Math.Abs(referenceStates[j]) >= current.MinimumDowntime &&
                            Math.Abs(referenceStates[j]) <= current.MinimumDowntime + current.CoolingHours)
                        {
                            double currentMachineStartUpCost = current.HotStartCost;
                            currentTotalStartUpCost += currentMachineStartUpCost;
                        }
                        else
                        {
                            double currentMachineStartUpCost = current.ColdStartCost;
                            currentTotalStartUpCost += currentMachineStartUpCost;
                        }
                    }
                }
                totalOperationCost += currentTotalFuelCost + currentTotalStartUpCost;
            }

            representation.Fitness = totalOperationCost;
        }
    }
}
