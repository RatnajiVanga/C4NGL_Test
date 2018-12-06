using O2DESNet;
using O2DESNet.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimED.General
{
    class TestProgram
    {
        static void Main()
        {
            var config = new Model<int>.Statics
            {
                Procedures = new List<Procedure>
                {
                    new Procedure
                    {
                        Steps = new List<Step>
                        {
                            new Step
                            {
                                Requirements = new List<Requirement>
                                {
                                    new Requirement{ Condition = res => res.Id > 5, Quantity = 2 },
                                    new Requirement{ Condition = res => res.Id < 3, Quantity = 1 },
                                },
                                ServiceTime = rs => Exponential.Sample(rs, TimeSpan.FromMinutes(5)),
                            },
                            new Step
                            {
                                Requirements = new List<Requirement>
                                {
                                    new Requirement{ Condition = res => res.Id > 7, Quantity = 1 },
                                    new Requirement{ Condition = res => res.Id > 2 && res.Id < 5, Quantity = 1 },
                                },
                                ServiceTime = rs => Exponential.Sample(rs, TimeSpan.FromMinutes(5)),
                            },
                            //new Step
                            //{
                            //    Requirements = new List<Requirement>
                            //    {
                            //        new Requirement{ Condition = res => res.Id > 7 && res.Id < 10, Quantity = 1 },
                            //        new Requirement{ Condition = res => res.Id > 2 && res.Id < 5, Quantity = 1 },
                            //    },
                            //    ServiceTime = rs => Exponential.Sample(rs, TimeSpan.FromMinutes(5)),
                            //},
                        }
                    },
                    new Procedure
                    {
                        Steps = new List<Step>
                        {
                            new Step
                            {
                                Requirements = new List<Requirement>
                                {
                                    new Requirement{ Condition = res => res.Id > 6 && res.Id < 8, Quantity = 1 },
                                    new Requirement{ Condition = res => res.Id > -1 && res.Id < 4, Quantity = 1 },
                                },
                                ServiceTime = rs => Exponential.Sample(rs, TimeSpan.FromMinutes(5)),
                            },
                            new Step
                            {
                                Requirements = new List<Requirement>
                                {
                                    new Requirement{ Condition = res => res.Id > 3 && res.Id < 11, Quantity = 2 },
                                    new Requirement{ Condition = res => res.Id > 1 && res.Id < 6, Quantity = 1 },
                                },
                                ServiceTime = rs => Exponential.Sample(rs, TimeSpan.FromMinutes(5)),
                            },
                            new Step
                            {
                                Requirements = new List<Requirement>
                                {
                                    new Requirement{ Condition = res => res.Id > -1 && res.Id < 3, Quantity = 1 },
                                    new Requirement{ Condition = res => res.Id > 6 && res.Id < 11, Quantity = 1 },
                                },
                                ServiceTime = rs => Exponential.Sample(rs, TimeSpan.FromMinutes(5)),
                            },
                        }
                    },
                },
                Resources = Enumerable.Range(0, 10).Select(id => new Resource { Id = id, Tag = id.ToString() }).ToList(),
            };
            var state = new Model<int>(config, 0);
           // Console.WriteLine(state);
            var sim = new Simulator(state);
            while (true)
            {
                sim.Run(1);
                Console.Clear();
                Console.WriteLine(sim.ClockTime);
                sim.WriteToConsole();
                Console.ReadKey();
            }
        }
    }
}
