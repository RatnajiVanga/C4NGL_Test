using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using O2DESNet;
using O2DESNet.Distributions;

namespace SimED.General
{
    public class Model<TLoad> : State<Model<TLoad>.Statics>
    {
        #region Statics
        public class Statics : Scenario
        {
            /******************************************************/
            /* All static properties shall be public,             */
            /* for both getter and setter.                        */
            /******************************************************/
            public List<Procedure> Procedures { get; set; }
            public List<Resource> Resources { get; set; }
        }
        #endregion

        #region Dynamics
        /**********************************************************/
        /* All dynamic properties shall have only public getter,  */
        /* where setter should remain as private.                 */
        /**********************************************************/
        /// <summary>
        /// Map the procedure and step index to the list of quantified resource 
        /// This is to be formed and fixed at init function.
        /// </summary>
        public List<Resource>[][] ResourcePool { get; private set; }
        /// <summary>
        /// Map occupied resource to the load that is occupying it
        /// </summary>
        public Dictionary<Resource, TLoad> Occupation { get; private set; } = new Dictionary<Resource, TLoad>();
        /// <summary>
        /// Map load to the resources it is occupying (by #requirements x quantity of the requirement)
        /// </summary>
        public Dictionary<TLoad, List<List<Resource>>> Occupying { get; private set; } = new Dictionary<TLoad, List<List<Resource>>>();
        /// <summary>
        /// Map occupied resource to the list of loads pending to request it
        /// </summary>
        public Dictionary<Resource, HashSet<TLoad>> PendingLoads { get; private set; } = new Dictionary<Resource, HashSet<TLoad>>();
        /// <summary>
        /// Track the procedure and step index of each load flow in the model
        /// </summary>
        public Dictionary<TLoad, Tuple<int, int>> LoadProcedureStepIndex { get; private set; } = new Dictionary<TLoad, Tuple<int, int>>();
        /// <summary>
        /// Request resources for indexed procedure and step
        /// </summary>
        /// <param name="procedureIdx">Procedure index</param>
        /// <param name="stepIdx">Step index</param>
        /// <returns>Two-dimension list for resources accoridng to # requirement x quantity for each requirement</returns>
        private List<List<Resource>> RqstResources(int procedureIdx, int stepIdx, TLoad load)
        {
            var step = Config.Procedures[procedureIdx].Steps[stepIdx];
            var resources = new List<List<Resource>>();
            var rqst = new HashSet<Resource>();
            foreach (var req in step.Requirements)
            {
                var toRqst = ResourcePool[procedureIdx][stepIdx].Where(res => req.Condition(res) && (!Occupation.ContainsKey(res) || Occupation[res].Equals(load)) && !rqst.Contains(res))
                    .Take(req.Quantity).ToList();
                if (toRqst.Count < req.Quantity) return null;
                foreach (var res in toRqst) rqst.Add(res);
                resources.Add(toRqst);
            }
            return resources;
        }
        public int TotalNLoads { get; private set; } = 0;
        public HashSet<TLoad> AllLoads { get; private set; } = new HashSet<TLoad>();
        #endregion

        #region Events
        private abstract class InternalEvent : Event<Model<TLoad>, Statics> { } /// event adapter 

        /**********************************************************/
        /* All internal events shall be private,                  */
        /* and inherite from InternalEvent as defined above       */
        /**********************************************************/
        private class ArriveEvent : InternalEvent
        {
            //internal TLoad Load { get; set; }
            //internal int ProcedureIndex { get; set; }
            public override void Invoke()
            {
                var load = (TLoad)(object)This.TotalNLoads++;
                var pIdx = DefaultRS.Next(Config.Procedures.Count);
                This.AllLoads.Add(load);
                This.LoadProcedureStepIndex[load] = new Tuple<int, int>(pIdx, -1);
                Execute(new AtmptMoveEvent
                {
                    Load = load,
                    ProcedureIndex = pIdx,
                    StepIndex = 0,
                });
                Schedule(new ArriveEvent(), Exponential.Sample(DefaultRS, TimeSpan.FromMinutes(10)));
            }
        }
        private class AtmptMoveEvent : InternalEvent
        {
            internal TLoad Load { get; set; }
            internal int ProcedureIndex { get; set; }
            internal int StepIndex { get; set; }
            public override void Invoke()
            {
                if (This.LoadProcedureStepIndex[Load].Item2 == StepIndex) return; /// prevent duplicated events
                var released = new List<Resource>();
                if (StepIndex == Config.Procedures[ProcedureIndex].Steps.Count)
                {
                    /// move out from the last step
                    if (StepIndex > 0) released = Release(ProcedureIndex, StepIndex - 1);
                    This.AllLoads.Remove(Load);
                }
                else
                {
                    var rqst = This.RqstResources(ProcedureIndex, StepIndex, Load);
                    if (rqst != null)
                    {
                        /// Has sufficient requested resources
                        This.LoadProcedureStepIndex[Load] = new Tuple<int, int>(ProcedureIndex, StepIndex);
                        if (StepIndex > 0) released = Release(ProcedureIndex, StepIndex - 1).Except(rqst.SelectMany(i => i)).ToList();
                        /// creat resource occupation                        
                        This.Occupying.Add(Load, rqst);
                        foreach (var res in rqst.SelectMany(i => i)) This.Occupation.Add(res, Load);
                        /// clear from pending list
                        foreach (var res in This.ResourcePool[ProcedureIndex][StepIndex]
                            .Where(res => This.PendingLoads.ContainsKey(res)))
                            This.PendingLoads[res].Remove(Load);
                        /// Schedule for next move
                        Schedule(new AtmptMoveEvent { Load = Load, ProcedureIndex = ProcedureIndex, StepIndex = StepIndex + 1 },
                            Config.Procedures[ProcedureIndex].Steps[StepIndex].ServiceTime(DefaultRS));
                    }
                    else
                    {
                        /// Has insufficient requested resources
                        foreach (var res in This.ResourcePool[ProcedureIndex][StepIndex])
                            This.PendingLoads[res].Add(Load);
                    }
                }
                /// Attempt to move for loads pending for released resources
                foreach (var load in released.SelectMany(res => This.PendingLoads[res]).Distinct())
                {
                    var t = This.LoadProcedureStepIndex[load];
                    Schedule(new AtmptMoveEvent { Load = load, ProcedureIndex = t.Item1, StepIndex = t.Item2 + 1 });
                }
            }
            private List<Resource> Release(int pIdx, int sIdx)
            {
                var released = This.Occupying[Load].SelectMany(i => i).ToList();
                This.Occupying.Remove(Load);
                foreach (var res in released) This.Occupation.Remove(res);
                return released;
            }
        }
        #endregion

        #region Input Events - Getters
        /***************************************************************/
        /* Methods returning an InternalEvent as O2DESNet.Event,       */
        /* with parameters for the objects to be passed in.            */
        /* Note that the InternalEvent shall always carry This = this. */
        /***************************************************************/
        //public Event Arrive(TLoad load, int procedureIndex) { return new ArriveEvent { This = this, Load = load, ProcedureIndex = procedureIndex }; }
        #endregion

        #region Output Events - Reference to Getters
        /***********************************************************************/
        /* List of functions that maps outgoing objects to an external event.  */
        /* Note that the mapping is specified only in external structure.      */
        /***********************************************************************/
        //public List<Func<TLoad, Event>> OnOutput { get; private set; } = new List<Func<TLoad, Event>>();
        #endregion

        public Model(Statics config, int seed, string tag = null) : base(config, seed, tag)
        {
            Name = "Model";
            Init();
            InitEvents.Add(new ArriveEvent { This = this });
        }
        private void Init()
        {
            ResourcePool = new List<Resource>[Config.Procedures.Count][];
            for (int i = 0; i < Config.Procedures.Count; i++)
            {
                var nSteps = Config.Procedures[i].Steps.Count;
                ResourcePool[i] = new List<Resource>[nSteps];
                for (int j = 0; j < nSteps; j++)
                    ResourcePool[i][j] = Config.Procedures[i].Steps[j].Requirements
                        .SelectMany(req => Config.Resources.Where(res => req.Condition(res))).ToList();
            }

            
            PendingLoads = Config.Resources.ToDictionary(res => res, res => new HashSet<TLoad>());
        }

        public override void WarmedUp(DateTime clockTime)
        {
            base.WarmedUp(clockTime);
        }

        public override void WriteToConsole(DateTime? clockTime = null)
        {
            foreach (var load in AllLoads)
            {
                Console.Write("Id: {0}\tP_Idx: {1}\tS_Idx: {2}\t", load, LoadProcedureStepIndex[load].Item1, LoadProcedureStepIndex[load].Item2);
                if (Occupying.ContainsKey(load))
                    foreach (var res in Occupying[load].SelectMany(i => i))
                        Console.Write("{0} ", res.Tag);
                Console.WriteLine();
            }
            Console.WriteLine();
            foreach (var res in Config.Resources)
            {
                Console.Write("Id: {0}\tOccupied: {1}\tPending: ", res.Id, Occupation.ContainsKey(res) ? Occupation[res].ToString() : "null");
                foreach (var load in PendingLoads[res]) Console.Write("{0} ", load);
                Console.WriteLine();
            }
        }
    }
}
