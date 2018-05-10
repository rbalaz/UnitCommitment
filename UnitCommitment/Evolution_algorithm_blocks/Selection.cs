using System;
using System.Collections.Generic;
using System.Linq;
using UnitCommitment.Representation;

namespace UnitCommitment.Evolution_algorithm_blocks
{
    public class Selection
    {
        private int parentsCount;
        private List<Individual> currentPopulation;

        public Selection(int parentsCount, List<Individual> currentPopulation)
        {
            this.parentsCount = parentsCount;
            this.currentPopulation = currentPopulation;
        }

        public List<Individual> SelectParents(int q)
        {
            // Q - tournament
            Random random = new Random();
            List<Individual> parents = new List<Individual>(parentsCount);
            for (int i = 0; i < parentsCount; i++)
            {
                List<Individual> participants = new List<Individual>();
                for (int j = 0; j < q; j++)
                {
                    participants.Add(currentPopulation[random.Next(0, currentPopulation.Count)]);
                }

                participants = participants.OrderBy(individual => individual.Fitness).ToList();

                parents.Add(participants[0]);
            }

            return parents;
        }

        public List<Individual> SelectDiverseParents(int q, int maximumIndividualsPerSubpopulation)
        {
            // Q - tournament
            Random random = new Random();
            List<Individual> parents = new List<Individual>(parentsCount);
            for (int i = 0; i < parentsCount; i++)
            {
                List<Individual> participants = new List<Individual>();
                for (int j = 0; j < q; j++)
                {
                    participants.Add(currentPopulation[random.Next(0, currentPopulation.Count)]);
                }

                Individual winner = GetBestIndividual(participants, parents, maximumIndividualsPerSubpopulation);

                parents.Add(participants[0]);
            }

            return parents;
        }

        private Individual GetBestIndividual(List<Individual> participants, List<Individual> parents,
            int maximumIndividualsPerSubpopulation)
        {
            participants = participants.OrderBy(individual => individual.Fitness).ToList();

            List<int> groupCounts = new List<int>();
            for (int i = 0; i < participants.Count; i++)
            {
                groupCounts.Add(GetSubpopulationCount(participants[i], parents));
            }

            int minimum = groupCounts.Min();
            int minimumIndex = groupCounts.FindIndex(item => item == minimum);

            return participants[minimumIndex];
        }

        private int GetSubpopulationCount(Individual individual, List<Individual> parents)
        {
            return parents.Count(ind => ind.SameGroup(individual));
        }
    }
}
