﻿using Signum.Entities;
using Signum.Entities.Basics;
using Signum.Entities.Workflow;
using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signum.Engine.Workflow
{
    public static class CaseFlowLogic
    {
        public static CaseFlow GetCaseFlow(CaseEntity @case)
        {
            var averages = new Dictionary<Lite<IWorkflowNodeEntity>, double?>();            
            averages.AddRange(@case.Workflow.WorkflowActivities().Select(a => KVP.Create((Lite<IWorkflowNodeEntity>)a.ToLite(), a.AverageDuration())));
            averages.AddRange(@case.Workflow.WorkflowEvents().Where(e => e.Type == WorkflowEventType.IntermediateTimer).Select(e => KVP.Create((Lite<IWorkflowNodeEntity>)e.ToLite(), e.AverageDuration())));
            
            var caseActivities = @case.CaseActivities().Select(ca => new CaseActivityStats
            {
                CaseActivity = ca.ToLite(),
                PreviousActivity = ca.Previous,
                WorkflowActivity = ca.WorkflowActivity.ToLite(),
                WorkflowActivityType = (WorkflowActivityType?)(ca.WorkflowActivity as WorkflowActivityEntity).Type,
                WorkflowEventType = (WorkflowEventType?)(ca.WorkflowActivity as WorkflowEventEntity).Type,
                SubWorkflow =(ca.WorkflowActivity as WorkflowActivityEntity).SubWorkflow.Workflow.ToLite(),
                BpmnElementId = ca.WorkflowActivity.BpmnElementId,
                Notifications = ca.Notifications().Count(),
                StartDate = ca.StartDate,
                DoneDate = ca.DoneDate,
                DoneType = ca.DoneType,
                DoneBy = ca.DoneBy,
                Duration = ca.Duration,
                AverageDuration = averages.TryGetS(ca.WorkflowActivity.ToLite()),
                EstimatedDuration = ca.WorkflowActivity is WorkflowActivityEntity ? 
                ((WorkflowActivityEntity)ca.WorkflowActivity).EstimatedDuration :
                ((WorkflowEventEntity)ca.WorkflowActivity).Timer.Duration == null ? (double?)null :
                ((WorkflowEventEntity)ca.WorkflowActivity).Timer.Duration.ToTimeSpan().TotalMinutes
                ,
            }).ToDictionary(a => a.CaseActivity);

            var gr = WorkflowLogic.GetWorkflowNodeGraph(@case.Workflow.ToLite());

            IEnumerable<CaseConnectionStats> GetSyncPaths(CaseActivityStats prev, IWorkflowNodeEntity from, IWorkflowNodeEntity to)
            {
                if (prev.DoneType == DoneType.Timeout)
                {
                    if (from is WorkflowActivityEntity wa)
                    {
                        var conns = wa.BoundaryTimers.Where(a => a.Type == WorkflowEventType.BoundaryInterruptingTimer)
                        .SelectMany(e => GetAllConnections(gr, e, to, path => path.All(a => a.Type == ConnectionType.Normal)));
                        if (conns.Any())
                            return conns.Select(c => new CaseConnectionStats().WithConnection(c).WithDone(prev));
                    }
                    else if (from is WorkflowEventEntity we)
                    {
                        var conns = GetAllConnections(gr, we, to, path => path.All(a => a.Type == ConnectionType.Normal)); ;
                        if (conns.Any())
                            return conns.Select(c => new CaseConnectionStats().WithConnection(c).WithDone(prev));
                    }
                }
                else
                {
                    var conns = GetAllConnections(gr, from, to, path => IsValidPath(prev.DoneType.Value, path));

                    if (conns.Any())
                        return conns.Select(c => new CaseConnectionStats().WithConnection(c).WithDone(prev));
                }

                return null;
            }


            var connections = caseActivities.Values
                .Where(cs => cs.PreviousActivity != null && caseActivities.ContainsKey(cs.PreviousActivity))
                .SelectMany(cs =>
                {
                    var prev = caseActivities.GetOrThrow(cs.PreviousActivity);
                    var from = gr.GetNode(prev.WorkflowActivity);
                    var to = gr.GetNode(cs.WorkflowActivity);

                    if (prev.DoneType.HasValue)
                    {
                        var res = GetSyncPaths(prev, from, to);
                        if (res != null)
                            return res;
                    }

                    if (from is WorkflowActivityEntity waFork)
                    {
                        var conns = waFork.BoundaryTimers.Where(a => a.Type == WorkflowEventType.BoundaryForkTimer).SelectMany(e => GetAllConnections(gr, e, to, path => path.All(a => a.Type == ConnectionType.Normal)));
                        if (conns.Any())
                            return conns.Select(c => new CaseConnectionStats().WithConnection(c).WithDone(prev));
                    }

                    return new[]
                    {
                        new CaseConnectionStats
                        {
                            FromBpmnElementId = from.BpmnElementId,
                            ToBpmnElementId = to.BpmnElementId,
                        }.WithDone(prev)
                    };
                }).ToList();

      

           
            var firsts = caseActivities.Values.Where(a => (a.PreviousActivity == null || !caseActivities.ContainsKey(a.PreviousActivity)));
            foreach (var f in firsts)
            {
                WorkflowEventEntity start = GetStartEvent(@case, f.CaseActivity, gr);
                if (start != null)
                    connections.AddRange(GetAllConnections(gr, start, gr.GetNode(f.WorkflowActivity), path => path.All(a => a.Type == ConnectionType.Normal))
                        .Select(c => new CaseConnectionStats().WithConnection(c).WithDone(f)));
            }

            if(@case.FinishDate != null)
            {
                var lasts = caseActivities.Values.Where(last => !caseActivities.Values.Any(a => a.PreviousActivity.Is(last.CaseActivity))).ToList();

                var ends = gr.Events.Values.Where(a => a.Type == WorkflowEventType.Finish);
                foreach (var last in lasts)
                {
                    var from = gr.GetNode(last.WorkflowActivity);
                    foreach (var end in ends)
                    {
                        var paths = GetSyncPaths(last, from, end);
                        if (paths != null)
                            connections.AddRange(paths);
                    }
                }
            }

            return new CaseFlow
            {
                Activities = caseActivities.Values.GroupToDictionary(a => a.BpmnElementId),
                Connections = connections.Where(a => a.BpmnElementId != null).GroupToDictionary(a => a.BpmnElementId),
                Jumps = connections.Where(a => a.BpmnElementId == null).ToList(),
                AllNodes = connections.Select(a => a.FromBpmnElementId).Union(connections.Select(a => a.ToBpmnElementId)).ToList()
            };
        }

        private static bool IsValidPath(DoneType doneType, Stack<WorkflowConnectionEntity> path)
        {
            switch (doneType)
            {
                case DoneType.Next: 
                case DoneType.ScriptSuccess:
                case DoneType.Recompose:
                    return path.All(a => a.Type == ConnectionType.Normal);
                case DoneType.Approve: return path.All(a => a.Type == ConnectionType.Normal || a.Type == ConnectionType.Approve);
                case DoneType.Decline: return path.All(a => a.Type == ConnectionType.Normal || a.Type == ConnectionType.Decline);
                case DoneType.Jump: return path.All(a => a == path.FirstEx() ? a.Type == ConnectionType.Jump : a.Type == ConnectionType.Normal);
                case DoneType.ScriptFailure: return path.All(a => a == path.FirstEx() ? a.Type == ConnectionType.ScriptException : a.Type == ConnectionType.Normal);
                case DoneType.Timeout:
                default:
                    throw new InvalidOperationException();
            }

        }
        

        private static WorkflowEventEntity GetStartEvent(CaseEntity @case, Lite<CaseActivityEntity> firstActivity, WorkflowNodeGraph gr)
        {
            var wet = Database.Query<OperationLogEntity>()
            .Where(l => l.Operation == CaseActivityOperation.CreateCaseFromWorkflowEventTask.Symbol && l.Target.Is(@case))
            .Select(l => new { l.Origin, l.User })
            .SingleOrDefaultEx();

            if (wet != null)
            {
                var lite = (wet.Origin as Lite<WorkflowEventTaskEntity>).InDB(a => a.Event);
                return lite == null ? null : gr.Events.GetOrThrow(lite);
            }
            
            bool register = Database.Query<OperationLogEntity>()
               .Where(l => l.Operation == CaseActivityOperation.Register.Symbol && l.Target.Is(firstActivity) && l.Exception == null)
               .Any();

            if (register)
                return gr.Events.Values.SingleEx(a => a.Type == WorkflowEventType.Start);
            
            return gr.Events.Values.Where(a => a.Type.IsStart()).Only();
        }

        private static HashSet<WorkflowConnectionEntity> GetAllConnections(WorkflowNodeGraph gr, IWorkflowNodeEntity from, IWorkflowNodeEntity to, Func<Stack<WorkflowConnectionEntity>, bool> isValidPath)
        {
            HashSet<WorkflowConnectionEntity> result = new HashSet<WorkflowConnectionEntity>(); 

            Stack<WorkflowConnectionEntity> partialPath = new Stack<WorkflowConnectionEntity>(); 
            HashSet<IWorkflowNodeEntity> visited = new HashSet<IWorkflowNodeEntity>();
            Action<IWorkflowNodeEntity> flood = null;
            flood = node =>
            {
                if (node.Is(to))
                {
                    if (isValidPath(partialPath))
                        result.AddRange(partialPath);
                    return;
                }

                if (node is WorkflowActivityEntity && !node.Is(from))
                    return;

                
                foreach (var con in gr.NextConnections(node).ToList())
                {
                    if (!visited.Contains(con.To))
                    {
                        visited.Add(con.To);
                        partialPath.Push(con);
                        flood(con.To);
                        partialPath.Pop();
                        visited.Remove(con.To);
                    }
                }
            };

            flood(from);

            return result;
        }
    }

    public class CaseActivityStats
    {
        public Lite<CaseActivityEntity> CaseActivity;
        public Lite<CaseActivityEntity> PreviousActivity;
        public Lite<IWorkflowNodeEntity> WorkflowActivity;
        public WorkflowActivityType? WorkflowActivityType;
        public WorkflowEventType? WorkflowEventType;
        public Lite<WorkflowEntity> SubWorkflow;
        public int Notifications;
        public DateTime StartDate;
        public DateTime? DoneDate;
        public DoneType? DoneType;
        public Lite<IUserEntity> DoneBy;
        public double? Duration;
        public double? AverageDuration;
        public double? EstimatedDuration;

        public string BpmnElementId { get; internal set; }
    }

    public class CaseConnectionStats
    {
        public CaseConnectionStats WithConnection(WorkflowConnectionEntity c)
        {
            this.BpmnElementId = c.BpmnElementId;
            this.Connection = c.ToLite();
            this.FromBpmnElementId = c.From.BpmnElementId;
            this.ToBpmnElementId = c.To.BpmnElementId;
            return this;
        }

        public CaseConnectionStats WithDone(CaseActivityStats activity)
        {
            this.DoneBy = activity.DoneBy;
            this.DoneDate = activity.DoneDate;
            this.DoneType = activity.DoneType;
            return this;
        }

        public Lite<WorkflowConnectionEntity> Connection;
        public DateTime? DoneDate;
        public Lite<IUserEntity> DoneBy;
        public DoneType? DoneType;

        public string BpmnElementId { get; internal set; }
        public string FromBpmnElementId { get; internal set; }
        public string ToBpmnElementId { get; internal set; }
    }

    public class CaseFlow
    {
        public Dictionary<string, List<CaseActivityStats>> Activities;
        public Dictionary<string, List<CaseConnectionStats>> Connections;
        public List<CaseConnectionStats> Jumps;
        public List<string> AllNodes;
    }
}
