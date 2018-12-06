using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimED.General
{
    public class Procedure
    {
        public List<Step> Steps { get; set; }
    }
    public class Step
    {
        public List<Requirement> Requirements { get; set; }
        public Func<Random, TimeSpan> ServiceTime { get; set; }
    }
    public class Requirement
    {
        public Func<Resource, bool> Condition { get; set; }
        public int Quantity { get; set; }
    }
    public class Resource
    {
        public int Id { get; set; }
        public string Tag { get; set; }
    }
}

