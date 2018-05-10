using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitCommitment.Representation
{
    public class Group
    {
        public List<int> grouppedMachines;

        public Group(List<int> grouppedMachines)
        {
            this.grouppedMachines = grouppedMachines;
        }

        public bool IsEqual(Group group)
        {
            if (group.grouppedMachines.Count != grouppedMachines.Count)
                return false;

            for (int i = 0; i < grouppedMachines.Count; i++)
            {
                if (group.grouppedMachines.Contains(grouppedMachines[i]) == false)
                    return false;
            }

            return true;
        }
    }
}
