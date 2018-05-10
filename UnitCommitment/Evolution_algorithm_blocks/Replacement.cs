using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitCommitment.Representation;
using UnitCommitment.Utilities;

namespace UnitCommitment.Evolution_algorithm_blocks
{
    public class Replacement
    {
        private List<Individual> oldGeneration;
        private List<Individual> descendants;
        private int newGenerationCount;

        public Replacement(List<Individual> oldGeneration, List<Individual> descendants,
            int newGenerationCount)
        {
            this.oldGeneration = oldGeneration;
            this.newGenerationCount = newGenerationCount;
            this.descendants = descendants;
        }

        public List<Individual> NextGeneration()
        {
            List<Individual> newGeneration = new List<Individual>();
            // Get elite 10% of old population
            List<Individual> orderedOldGeneration = oldGeneration.OrderBy(item => item.Fitness).ToList();
            orderedOldGeneration.RemoveAll(item => StaticOperations.ValidateIndividual(item) == false);
            newGeneration.AddRange(orderedOldGeneration.Take(newGenerationCount / 10));
            // Fill up remaining spots with descendants
            List<Individual> orderedDescendants = descendants.OrderBy(item => item.Fitness).ToList();
            // Remove invalid descendants
            orderedDescendants.RemoveAll(item => StaticOperations.ValidateIndividual(item) == false);
            newGeneration.AddRange(orderedDescendants.Take(newGenerationCount - newGeneration.Count));

            return newGeneration;
        }
    }
}
