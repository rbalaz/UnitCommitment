using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitCommitment.Representation
{
    public class Individual
    {
        // Representation consists of 24 arrays of
        // 10 machines with values coressponding to their 
        // power generation
        public List<List<Machine>> Schedule { get; private set; }
        public double Fitness { get; set; }
        public List<Group> groups;

        public Individual(List<List<Machine>> schedule)
        {
            Schedule = schedule;
        }

        public static Individual CloneRepresentation(Individual template)
        {
            List<List<Machine>> scheduleClone = new List<List<Machine>>();

            foreach (List<Machine> machineList in template.Schedule)
            {
                List<Machine> machineListClone = new List<Machine>();
                foreach (Machine machine in machineList)
                {
                    Machine clone = Machine.Clone(machine);
                    machineListClone.Add(clone);
                }
                scheduleClone.Add(machineListClone);
            }

            Individual individualClone = new Individual(scheduleClone);
            if (template.groups != null)
            {
                List<Group> groups = new List<Group>();
                foreach (Group group in template.groups)
                {
                    List<int> machines = new List<int>();
                    foreach (int machine in group.grouppedMachines)
                    {
                        machines.Add(machine);
                    }
                    Group groupClone = new Group(machines);
                    groups.Add(groupClone);
                }
                individualClone.groups = groups;
            }

            return individualClone;
        }

        public bool SameGroup(Individual individual)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                bool groupPresent = false;
                for (int j = 0; j < individual.groups.Count; j++)
                {
                    if (groups[i].IsEqual(individual.groups[j]))
                    {
                        groupPresent = true;
                    }
                }
                if (groupPresent == false)
                    return false;
            }

            return true;
        }

        public static void PrintSchedule(List<List<Machine>> schedule)
        {
            foreach (List<Machine> machineList in schedule)
            {
                Console.WriteLine(PrintMachineList(machineList));
            }
        }

        public static string PrintMachineList(List<Machine> machineList)
        {
            StringBuilder builder = new StringBuilder();
            foreach (Machine machine in machineList)
            {
                string value = String.Format("{0:0.#}", machine.CurrentOutputPower) + " ";
                builder.Append(value.PadLeft(5));
            }

            return builder.ToString().TrimEnd(' ');
        }
    }
}
