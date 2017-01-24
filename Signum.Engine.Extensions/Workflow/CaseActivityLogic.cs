﻿using Signum.Entities.Workflow;
using Signum.Engine;
using Signum.Engine.Dynamic;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Maps;
using Signum.Engine.Operations;
using Signum.Entities;
using Signum.Entities.Authorization;
using Signum.Entities.Basics;
using Signum.Utilities;
using Signum.Utilities.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Signum.Engine.Maps.SchemaBuilder;
using Signum.Entities.Dynamic;
using Signum.Engine.Basics;

namespace Signum.Engine.Workflow
{
    public static class CaseActivityLogic
    {

        static Expression<Func<WorkflowEntity, IQueryable<CaseEntity>>> CasesExpression =
            e => Database.Query<CaseEntity>().Where(a => a.Workflow == e);

        [ExpressionField]
        public static IQueryable<CaseEntity> Cases(this WorkflowEntity e)
        {
            return CasesExpression.Evaluate(e);
        }

        static Expression<Func<WorkflowActivityEntity, IQueryable<CaseActivityEntity>>> CaseActivitiesFromWorkflowActivityExpression =
            e => Database.Query<CaseActivityEntity>().Where(a => a.WorkflowActivity == e);
        [ExpressionField]
        public static IQueryable<CaseActivityEntity> CaseActivities(this WorkflowActivityEntity e)
        {
            return CaseActivitiesFromWorkflowActivityExpression.Evaluate(e);
        }

        static Expression<Func<CaseEntity, IQueryable<CaseActivityEntity>>> CaseActivitiesFromCaseExpression =
            e => Database.Query<CaseActivityEntity>().Where(a => a.Case == e);
        [ExpressionField]
        public static IQueryable<CaseActivityEntity> CaseActivities(this CaseEntity e)
        {
            return CaseActivitiesFromCaseExpression.Evaluate(e);
        }

        static Expression<Func<CaseActivityEntity, IQueryable<CaseNotificationEntity>>> NotificationsExpression =
            e => Database.Query<CaseNotificationEntity>().Where(a => a.CaseActivity.RefersTo(e));
        [ExpressionField]
        public static IQueryable<CaseNotificationEntity> Notifications(this CaseActivityEntity e)
        {
            return NotificationsExpression.Evaluate(e);
        }

        public static void Start(SchemaBuilder sb, DynamicQueryManager dqm)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                sb.Include<CaseEntity>()
                    .WithExpressionFrom(dqm, (WorkflowEntity w) => w.Cases())
                    .WithQuery(dqm, e => new
                    {
                        Entity = e,
                        e.Id,
                        e.Description,
                    });

                sb.Include<CaseActivityEntity>()
                    .WithExpressionFrom(dqm, (WorkflowActivityEntity c) => c.CaseActivities())
                    .WithExpressionFrom(dqm, (CaseEntity c) => c.CaseActivities())
                    .WithQuery(dqm, e => new
                    {
                        Entity = e,
                        e.Id,
                        e.StartDate,
                        e.DoneDate,
                        e.DoneBy,
                        e.Previous,
                        e.Case,
                        e.WorkflowActivity,
                    });

                sb.Include<CaseNotificationEntity>()
                    .WithExpressionFrom(dqm, (CaseActivityEntity c) => c.Notifications())
                    .WithQuery(dqm, e => new
                    {
                        Entity = e,
                        e.Id,
                        e.CaseActivity.Entity.StartDate,
                        e.State,
                        e.CaseActivity,
                        e.User,
                    });


                dqm.RegisterQuery(CaseActivityQuery.Inbox, () =>
                        from qn in Database.Query<CaseNotificationEntity>()
                        where qn.User == UserEntity.Current.ToLite()
                        let ca = qn.CaseActivity.Entity
                        let sender = ca.Previous.Entity.DoneBy.Entity
                        select new
                        {
                            Entity = qn.CaseActivity,
                            ca.WorkflowActivity.Name,
                            Sender = sender.ToLite(sender.UserName + " (" + qn.Actor.ToString() + ")"),
                            ca.Case.Description,
                            ca.StartDate,
                            ca.DoneBy,
                            ca.DoneDate,
                            qn.State,
                            qn.User,
                        });

                sb.Schema.WhenIncluded<DynamicTypeEntity>(() =>
                {
                    new Graph<DynamicTypeEntity>.Execute(CaseActivityOperation.FixCaseDescriptions)
                    {
                        Execute = (e, _) =>
                        {
                            var type = TypeLogic.GetType(e.TypeName);
                            giFixCaseDescriptions.GetInvoker(type)();
                        },
                    }.Register();
                });

                CaseActivityGraph.Register();
            }
        }

        static readonly GenericInvoker<Action> giFixCaseDescriptions = new GenericInvoker<Action>(() => FixCaseDescriptions<Entity>());
        public static void FixCaseDescriptions<T>() where T : Entity
        {
            Database.Query<CaseEntity>()
                          .Where(a => a.MainEntity.GetType() == typeof(T))
                          .UnsafeUpdate()
                          .Set(a => a.Description, a => ((T)a.MainEntity).ToString())
                          .Execute();
        }

        public static Dictionary<Type, WorkflowOptions> Options = new Dictionary<Type, WorkflowOptions>(); 

        public class WorkflowOptions
        {
            public Func<ICaseMainEntity> Constructor;
            public Action<ICaseMainEntity> SaveEntity;
        }
        
        public static FluentInclude<T> WithWorkflow<T>(this FluentInclude<T> fi, Func<T> constructor, Action<T> save)
            where T: Entity, ICaseMainEntity
        {
            fi.SchemaBuilder.Schema.EntityEvents<T>().Saved += (e, args)=>
            {
                if (AvoidNotifyInProgressVariable.Value == true)
                    return;

                e.NotifyInProgress();
            };

            Options[typeof(T)] = new WorkflowOptions
            {
                Constructor = constructor,
                SaveEntity = e => save((T)e)
            };

            return fi; 
        }

        static IDisposable AvoidNotifyInProgress()
        {
            var old = AvoidNotifyInProgressVariable.Value;
            AvoidNotifyInProgressVariable.Value = true;
            return new Disposable(() => AvoidNotifyInProgressVariable.Value = old);
        }
        
        static ThreadVariable<bool> AvoidNotifyInProgressVariable = Statics.ThreadVariable<bool>("avoidNotifyInProgress");

        public static int NotifyInProgress(this ICaseMainEntity mainEntity)
        {
            return Database.Query<CaseNotificationEntity>()
                .Where(n => n.CaseActivity.Entity.Case.MainEntity == mainEntity && n.CaseActivity.Entity.DoneDate == null)
                .Where(n => n.User == UserEntity.Current.ToLite() && (n.State == CaseNotificationState.New || n.State == CaseNotificationState.Opened))
                .UnsafeUpdate()
                .Set(n => n.State, n => CaseNotificationState.InProgress)
                .Execute();
        }

        public class WorkflowExecuteStepContext
        {
            public DecisionResult? DecisionResult;
            public CaseActivityEntity CaseActivity;
            public List<CaseActivityEntity> ParallelFroms = new List<CaseActivityEntity>();
            public List<WorkflowActivityEntity> To = new List<WorkflowActivityEntity>();
            public List<WorkflowConnectionEntity> Connections = new List<WorkflowConnectionEntity>();
        }

        static bool Applicable(this WorkflowConnectionEntity wc, WorkflowExecuteStepContext ctx)
        {
            if (wc.DecisonResult != null && wc.DecisonResult != ctx.DecisionResult)
                return false;

            if (wc.Condition != null)
            {
                var alg = wc.Condition.RetrieveFromCache().Eval.Algorithm;
                var result = alg.EvaluateUntyped(ctx.CaseActivity.Case.MainEntity, new WorkflowEvaluationContext
                {
                    CaseActivity = ctx.CaseActivity,
                    DecisionResult = ctx.DecisionResult,
                    Connection = wc,
                });


                return result;
            }
            
            return true;
        }

        static void WorkflowAction(ICaseMainEntity me, WorkflowEvaluationContext ctx)
        {
            WorkflowLogic.OnTransition?.Invoke(me, ctx);

            if (ctx.Connection.Action != null)
            {
                var alg = ctx.Connection.Action.RetrieveFromCache().Eval.Algorithm;
                alg.ExecuteUntyped(me, ctx);
            };
        }

        static void SaveEntity(ICaseMainEntity mainEntity)
        {
            var options = CaseActivityLogic.Options.GetOrThrow(mainEntity.GetType());
            using (AvoidNotifyInProgress())
                options.SaveEntity(mainEntity);
        }

        public static CaseActivityEntity RetrieveForViewing(Lite<CaseActivityEntity> lite)
        {
            var ca = lite.Retrieve();

            if (ca.DoneBy == null)
                ca.Notifications()
                  .Where(n => n.User == UserEntity.Current.ToLite() && n.State == CaseNotificationState.New)
                  .UnsafeUpdate()
                  .Set(a => a.State, a => CaseNotificationState.Opened)
                  .Execute();

            return ca;
        }

        static void InsertCaseActivityNotifications(CaseActivityEntity caseActivity)
        {
            var lane = caseActivity.WorkflowActivity.Lane;
            var actors = lane.Actors.ToList();
            if (lane.ActorsEval != null)
                actors.AddRange(lane.ActorsEval.Algorithm.GetActors(caseActivity.Case.MainEntity).EmptyIfNull().NotNull());

            var notifications = actors.SelectMany(a =>
            Database.Query<UserEntity>()
            .Where(u => WorkflowLogic.IsCurrentUserActor.Evaluate(a, u))
            .Select(u => new CaseNotificationEntity
            {
                CaseActivity = caseActivity.ToLite(),
                Actor = a,
                State = CaseNotificationState.New,
                User = u.ToLite()
            })).ToList();

            notifications.BulkInsert();
        }

        class CaseActivityGraph : Graph<CaseActivityEntity, CaseActivityState>
        {
            public static void Register()
            {
                GetState = ca => ca.State;
                new ConstructFrom<WorkflowEntity>(CaseActivityOperation.Create)
                {
                    ToStates = { CaseActivityState.New},
                    Construct = (w, args) =>
                    {
                        var start = w.WorkflowEvents().Single(a => a.Type == WorkflowEventType.Start);

                        var @case = new CaseEntity
                        {
                            Workflow = w,
                            Description = w.Name,
                            MainEntity = CaseActivityLogic.Options.GetOrThrow(w.MainEntityType.ToType()).Constructor(),
                        };

                        var connection = start.NextConnectionsFromCache().SingleEx();
                        WorkflowAction(@case.MainEntity, new WorkflowEvaluationContext
                        {
                            Case = @case,
                            CaseActivity = null,
                            Connection = connection,
                            DecisionResult = null
                        });
                        var next = (WorkflowActivityEntity)connection.To;
                        return new CaseActivityEntity
                        {
                            WorkflowActivity = next,
                            OriginalWorkflowActivityName = next.Name,
                            Case = @case,
                        };
                    }
                }.Register();

                new Execute(CaseActivityOperation.Register)
                {
                    FromStates = {  CaseActivityState.New },
                    ToStates = {  CaseActivityState.PendingNext, CaseActivityState.PendingDecision},
                    AllowsNew = true,
                    Lite = false,
                    Execute = (ca, _) =>
                    {
                        SaveEntity(ca.Case.MainEntity);

                        var now = TimeZoneManager.Now;
                        var c = ca.Case;
                        c.StartDate = now;
                        c.Description = ca.Case.MainEntity.ToString().Trim();
                        c.Save();

                        ca.StartDate = now;
                        ca.Save();

                        InsertCaseActivityNotifications(ca);
                    }
                }.Register();

                new Delete(CaseActivityOperation.Delete)
                {
                    FromStates = { CaseActivityState.PendingDecision, CaseActivityState.PendingNext },
                    CanDelete = ca => (ca.Case.CaseActivities().Any(a => a != ca) ? CaseActivityMessage.CaseContainsOtherActivities.NiceToString() : null),
                    Delete = (ca, _) =>
                    {
                        var c = ca.Case;
                        ca.Notifications().UnsafeDelete();
                        ca.Delete();
                        c.Delete();
                        c.MainEntity.Delete();
                    },
                }.Register();

                new Execute(CaseActivityOperation.Approve)
                {
                    FromStates = {  CaseActivityState.PendingDecision },
                    ToStates = {  CaseActivityState.Done },
                    Lite = false,
                    Execute = (ca, args) =>
                    {
                        ExecuteStep(ca, args, DecisionResult.Approve);
                    },
                }.Register();

                new Execute(CaseActivityOperation.Decline)
                {
                    FromStates = { CaseActivityState.PendingDecision },
                    ToStates = { CaseActivityState.Done },
                    Lite = false,
                    Execute = (ca, args) =>
                    {
                        ExecuteStep(ca, args, DecisionResult.Decline);
                    },
                }.Register();

                new Execute(CaseActivityOperation.Next)
                {
                    FromStates = { CaseActivityState.PendingNext },
                    ToStates = { CaseActivityState.Done },
                    Lite = false,
                    Execute = (ca, args) =>
                    {
                        ExecuteStep(ca, args, null);
                    },
                }.Register();
            }
            
            private static void ExecuteStep(CaseActivityEntity ca, object[] args, DecisionResult? decisionResult)
            {
                using (DynamicValidationLogic.EnabledRulesExplicitely(ca.WorkflowActivity.ValidationRules
                            .Where(a => decisionResult == null || (decisionResult == DecisionResult.Approve ? a.OnAccept : a.OnDecline))
                            .Select(a => a.Rule)
                            .ToHashSet()))
                {
                    SaveEntity(ca.Case.MainEntity);

                    ca.DoneBy = UserEntity.Current.ToLite();
                    ca.DoneDate = TimeZoneManager.Now;
                    ca.Case.Description = ca.Case.MainEntity.ToString().Trim();
                    ca.Save();

                    ca.Notifications()
                       .Where(n => n.User == UserEntity.Current.ToLite())
                       .UnsafeUpdate()
                       .Set(a => a.State, a => CaseNotificationState.Done)
                       .Execute();

                    var connection = ca.WorkflowActivity.NextConnectionsFromCache().SingleEx();

                    var ctx = new WorkflowExecuteStepContext
                    {
                        CaseActivity = ca,
                        DecisionResult = decisionResult,
                        Connections = { connection }
                    };

                    if (FindNext(connection.To, ctx))
                    {
                        var t = ctx.To.Only();
                        if (ctx.ParallelFroms.IsEmpty() && t != null)
                        {
                            var nca = new CaseActivityEntity
                            {
                                StartDate = ca.DoneDate.Value,
                                Previous = ca.ToLite(),
                                WorkflowActivity = t,
                                OriginalWorkflowActivityName = t.Name,
                                Case = ca.Case
                            }.Save();

                            InsertCaseActivityNotifications(nca);
                        }
                        else
                        {
                            foreach (var t2 in ctx.To)
                            {
                                var nca = new CaseActivityEntity
                                {
                                    StartDate = ca.DoneDate.Value,
                                    Previous = ca.ToLite(),
                                    WorkflowActivity = t2,
                                    OriginalWorkflowActivityName = t2.Name,
                                    Case = ca.Case
                                }.Save();

                                InsertCaseActivityNotifications(nca);
                            }
                        }

                        ctx.Connections.ForEach(wc => WorkflowAction(ca.Case.MainEntity, new WorkflowEvaluationContext
                        {
                            Case = ca.Case,
                            CaseActivity = ca,
                            Connection = wc,
                            DecisionResult = ctx.DecisionResult
                        }));

                        ca.Case.Description = ca.Case.MainEntity.ToString().Trim();
                        ca.Case.Save();
                    }
                }
            }

            private static bool FindNext(IWorkflowNodeEntity next, WorkflowExecuteStepContext ctx)
            {
                if (next is WorkflowEventEntity)
                {
                    var ne = ((WorkflowEventEntity)next);

                    if (ne.Type == WorkflowEventType.Finish)
                        return true;

                    throw new NotImplementedException($"Unexpected {nameof(WorkflowEventType)} {ne.Type}");
                }
                else if (next is WorkflowActivityEntity)
                {
                    ctx.To.Add((WorkflowActivityEntity)next);
                    return true; 
                }
                else
                {
                    var gateway = (WorkflowGatewayEntity)next;

                    switch (gateway.Type)
                    {
                        case WorkflowGatewayType.Exclusive:
                            if (gateway.Direction == WorkflowGatewayDirection.Split)
                            {
                                var connection = gateway.NextConnectionsFromCache()
                                    .GroupBy(c => c.Order)
                                    .OrderBy(gr => gr.Key)
                                    .Select(gr => gr.SingleOrDefaultEx(c => c.Applicable(ctx)))
                                    .NotNull()
                                    .FirstEx();
                                ctx.Connections.Add(connection);
                                return FindNext(connection.To, ctx);
                            }
                            else //if (gateway.Direction == WorkflowGatewayDirection.Join)
                            {
                                var connection = gateway.NextConnectionsFromCache().SingleEx();
                                ctx.Connections.Add(connection);
                                return FindNext(connection.To, ctx);
                            }
         
                        case WorkflowGatewayType.Parallel:
                        case WorkflowGatewayType.Inclusive:
                            if (gateway.Direction == WorkflowGatewayDirection.Split)
                            {
                                var applicable = gateway.NextConnections().ToList().Where(c =>
                                {
                                    var app = c.Applicable(ctx);
                                    if (!app && gateway.Type == WorkflowGatewayType.Parallel)
                                        throw new InvalidOperationException($"Conditions not allowed in {WorkflowGatewayType.Parallel} {WorkflowGatewayDirection.Split}!");
                                    return app;
                                }).ToList();

                                if (applicable.IsEmpty())
                                    throw new InvalidOperationException("No condition applied");

                                foreach (var connection in applicable)
                                {
                                    ctx.Connections.Add(connection);
                                    FindNext(connection.To, ctx);
                                }

                                return true; 
                            }
                            else //if (gateway.Direction == WorkflowGatewayDirection.Join)
                            {
                                if (!FindPrevious(0, gateway, ctx))
                                    return false;

                                var connection = gateway.NextConnectionsFromCache().SingleEx();
                                ctx.Connections.Add(connection);
                                return FindNext(connection.To, ctx);
                            }
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
            
            private static bool FindPrevious(int depth, IWorkflowNodeEntity node, WorkflowExecuteStepContext ctx)
            {
                if (node is WorkflowEventEntity)
                {
                    throw new InvalidOperationException($"Unexpected {nameof(WorkflowEventEntity)} in {nameof(FindPrevious)}");
                }
                else if (node is WorkflowActivityEntity)
                {
                    var wa = (WorkflowActivityEntity)node;
                    if (wa.Is(ctx.CaseActivity.WorkflowActivity))
                        return true;

                    var last = ctx.CaseActivity.Case.CaseActivities().Where(a => a.WorkflowActivity == wa).OrderBy(a => a.StartDate).LastOrDefault();
                    if (last != null)
                        return (last.DoneDate.HasValue);

                    var prevsConnections = node.PreviousConnectionsFromCache().Select(a => a.From).ToList();
                    return prevsConnections.All(wn => FindPrevious(depth, wn, ctx));
                }
                else if (node is WorkflowGatewayEntity)
                {
                    var g = (WorkflowGatewayEntity)node;
                    depth += (g.Direction == WorkflowGatewayDirection.Split ? -1 : 1);

                    if (depth == 0)
                        return true;

                    switch (g.Type)
                    {
                        case WorkflowGatewayType.Exclusive:
                            {
                                var prevsExclusive = g.PreviousConnectionsFromCache().Select(a => a.From).ToList();
                                return prevsExclusive.Any(wn => FindPrevious(depth, wn, ctx));
                            }
                        case WorkflowGatewayType.Parallel:
                        case WorkflowGatewayType.Inclusive:
                            {
                                var prevsParallelAndInclusive = g.PreviousConnectionsFromCache().Select(a => a.From).ToList();
                                return prevsParallelAndInclusive.All(wn => FindPrevious(depth, wn, ctx));
                            }
                        default:
                            throw new InvalidOperationException();
                    }
                }
                throw new InvalidOperationException();
            }
        }
    }
}