using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitCommitment
{
    public class Machine
    {
        // Template parameters
        // Performance parameters
        public int MinimumOutputPower { get; private set; }
        public int MaximumOutputPower { get; private set; }
        public int MinimumDowntime { get; private set; }
        public int MinimumUptime { get; private set; }

        // Fuel cost parameters
        public double A { get; private set; }
        public double B { get; private set; }
        public double C { get; private set; }

        // Start cost parameters
        public int ColdStartCost { get; private set; }
        public int HotStartCost { get; private set; }
        public int CoolingHours { get; private set; }

        // Non-template parameters
        // Speficic parameters
        public int CurrentStatus { get; set; }
        public double CurrentOutputPower { get; set; }

        public Machine(int minimumOutputPower, int maximumOutputPower, int minimumDowntime, int minimumUptime,
            double a, double b, double c, int coldStartCost, int hotStartCost, int coolingHours)
        {
            MinimumOutputPower = minimumOutputPower;
            MaximumOutputPower = maximumOutputPower;
            MinimumUptime = minimumUptime;
            MinimumDowntime = minimumDowntime;
            A = a;
            B = b;
            C = c;
            ColdStartCost = coldStartCost;
            HotStartCost = hotStartCost;
            CoolingHours = coolingHours;
        }

        public double CalculateFuelCostCoefficient()
        {
            return (A + B * MaximumOutputPower + C * Math.Pow(MaximumOutputPower, 2)) / MaximumOutputPower;
        }

        public static Machine Clone(Machine template)
        {
            Machine machine = new Machine(template.MinimumOutputPower, template.MaximumOutputPower,
                template.MinimumDowntime, template.MinimumUptime, template.A, template.B, template.C,
                template.ColdStartCost, template.HotStartCost, template.CoolingHours)
            {
                CurrentOutputPower = template.CurrentOutputPower,
                CurrentStatus = template.CurrentStatus
            };

            return machine;
        }
    }
}
