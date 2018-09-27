﻿using Signum.Engine.Maps;
using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Signum.Engine;
using Signum.Engine.Basics;
using Signum.Entities;
using Signum.Engine.Authorization;
using Signum.Engine.Cache;
using System.Data.SqlClient;
using Signum.Entities.Workflow;
using Signum.Engine.Operations;
using Signum.Entities.Authorization;
using Signum.Entities.Basics;
using Signum.Engine.Scheduler;

namespace Signum.Engine.Workflow
{
    public static class WorkflowScriptRunner
    {
        static Timer timer;
        internal static DateTime? nextPlannedExecution;
        static bool running = false;
        static CancellationTokenSource CancelProcess;
        static long queuedItems;
        static Guid processIdentifier;
        static AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        public static WorkflowScriptRunnerState ExecutionState()
        {
            return new WorkflowScriptRunnerState
            {
                Running = running,
                CurrentProcessIdentifier = processIdentifier,
                ScriptRunnerPeriod = WorkflowLogic.Configuration.ScriptRunnerPeriod,
                NextPlannedExecution = nextPlannedExecution,
                IsCancelationRequested = CancelProcess != null && CancelProcess.IsCancellationRequested,
                QueuedItems = queuedItems,
            };
        }

        public static void StartRunningScripts(int initialDelayMilliseconds)
        {
            if (initialDelayMilliseconds == 0)
                ExecuteProcess();
            else
            {
                using (ExecutionContext.SuppressFlow())
                    Task.Run(() =>
                    {
                        Thread.Sleep(initialDelayMilliseconds);
                        ExecuteProcess();
                    });
            }
        }

        private static void ExecuteProcess()
        {
            if (running)
                throw new InvalidOperationException("WorkflowScriptRunner process is already running");


            using (ExecutionContext.SuppressFlow())
            {
                Task.Factory.StartNew(() =>
                {
                    SystemEventLogLogic.Log("Start WorkflowScriptRunner");
                    ExceptionEntity exception = null;
                    try
                    {
                        running = true;
                        CancelProcess = new CancellationTokenSource();
                        autoResetEvent.Set();

                        timer = new Timer(ob => WakeUp("TimerNextExecution", null),
                             null,
                             Timeout.Infinite,
                             Timeout.Infinite);

                        GC.KeepAlive(timer);

                        using (UserHolder.UserSession(AuthLogic.SystemUser))
                        {
                            if (CacheLogic.WithSqlDependency)
                            {
                                SetSqlDependency();
                            }

                            while (autoResetEvent.WaitOne())
                            {
                                if (CancelProcess.IsCancellationRequested)
                                    return;

                                timer.Change(Timeout.Infinite, Timeout.Infinite);
                                nextPlannedExecution = null;

                                using (HeavyProfiler.Log("WorkflowScriptRunner", () => "Execute process"))
                                {
                                    processIdentifier = Guid.NewGuid();
                                    if (RecruitQueuedItems())
                                    {
                                        while (queuedItems > 0 || RecruitQueuedItems())
                                        {
                                            var items = Database.Query<CaseActivityEntity>().Where(m => m.DoneDate == null && 
                                                m.ScriptExecution.ProcessIdentifier == processIdentifier)
                                                .Take(WorkflowLogic.Configuration.ChunkSizeRunningScripts).ToList();
                                            queuedItems = items.Count;
                                            foreach (var caseActivity in items)
                                            {
                                                CancelProcess.Token.ThrowIfCancellationRequested();

                                                try
                                                {
                                                    using (Transaction tr = Transaction.ForceNew())
                                                    {
                                                        caseActivity.Execute(CaseActivityOperation.ScriptExecute);

                                                        tr.Commit();
                                                    }
                                                }
                                                catch
                                                { 
                                                    try
                                                    {
                                                        var ca = caseActivity.ToLite().Retrieve();
                                                        var retry = ((WorkflowActivityEntity)ca.WorkflowActivity).Script.RetryStrategy;
                                                        var nextDate = retry?.NextDate(ca.ScriptExecution.RetryCount);
                                                        if(nextDate == null)
                                                        {
                                                            ca.Execute(CaseActivityOperation.ScriptFailureJump);
                                                        }
                                                        else
                                                        {
                                                            ca.Execute(CaseActivityOperation.ScriptScheduleRetry, nextDate.Value);
                                                        }
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        e.LogException();
                                                        throw;
                                                    }
                                                }
                                                queuedItems--;
                                            }
                                            queuedItems = Database.Query<CaseActivityEntity>()
                                            .Where(m => m.ScriptExecution.ProcessIdentifier == processIdentifier && m.DoneDate == null)
                                            .Count();
                                        }
                                    }
                                    SetTimer();
                                    SetSqlDependency();
                                }
                            }
                        }
                    }
                    catch (ThreadAbortException)
                    {

                    }
                    catch (Exception e)
                    {
                        try
                        {
                            exception = e.LogException(edn =>
                            {
                                edn.ControllerName = "WorkflowScriptRunner";
                                edn.ActionName = "ExecuteProcess";
                            });
                        }
                        catch { }
                    }
                    finally
                    {
                        SystemEventLogLogic.Log("Stop WorkflowScriptRunner", exception);
                        running = false;
                    }
                }, TaskCreationOptions.LongRunning);
            }
        }

        static bool sqlDependencyRegistered = false;
        private static void SetSqlDependency()
        {
            if(sqlDependencyRegistered)
                return;
            
            var query = Database.Query<CaseActivityEntity>().Where(m => m.ScriptExecution != null).Select(m => m.Id);
            sqlDependencyRegistered = true;
            query.ToListWithInvalidation(typeof(CaseActivityEntity), "WorkflowScriptRunner ReadyToExecute dependency", a => {
                sqlDependencyRegistered = false;
                WakeUp("WorkflowScriptRunner ReadyToExecute dependency", a);
            });
        }

        private static bool RecruitQueuedItems()
        {
            DateTime? firstDate = WorkflowLogic.Configuration.AvoidExecutingScriptsOlderThan == null ?
                null : (DateTime?)TimeZoneManager.Now.AddHours(-WorkflowLogic.Configuration.AvoidExecutingScriptsOlderThan.Value);

            queuedItems = Database.Query<CaseActivityEntity>()
                .Where(ca => ca.DoneDate == null &&
                             ((firstDate == null || firstDate < ca.ScriptExecution.NextExecution) &&
                            ca.ScriptExecution.NextExecution < TimeZoneManager.Now))
                .UnsafeUpdate()
                .Set(m => m.ScriptExecution.ProcessIdentifier, m => processIdentifier)
                .Execute();

            return queuedItems > 0;
        }

        internal static bool WakeUp(string reason, SqlNotificationEventArgs args)
        {
            using (HeavyProfiler.Log("WorkflowScriptRunner WakeUp", () => "WakeUp! " + reason + ToString(args)))
            {
                return autoResetEvent.Set();
            }
        }

        private static string ToString(SqlNotificationEventArgs args)
        {
            if (args == null)
                return null;

            return " ({0} {1} {2})".FormatWith(args.Type, args.Source, args.Info);
        }

        private static void SetTimer()
        {
            nextPlannedExecution = TimeZoneManager.Now.AddMilliseconds(WorkflowLogic.Configuration.ScriptRunnerPeriod * 1000);
            timer.Change(WorkflowLogic.Configuration.ScriptRunnerPeriod * 1000, Timeout.Infinite);
        }

        public static void Stop()
        {
            if (!running)
                throw new InvalidOperationException("WorkflowScriptRunner is not running");

            using (HeavyProfiler.Log("WorkflowScriptRunner", () => "Stopping process"))
            {
                timer.Dispose();
                CancelProcess.Cancel();
                WakeUp("Stop", null);
                nextPlannedExecution = null;
            }
        }
    }

    public class WorkflowScriptRunnerState
    {
        public int ScriptRunnerPeriod;
        public bool Running;
        public bool IsCancelationRequested;
        public DateTime? NextPlannedExecution;
        public long QueuedItems;
        public Guid CurrentProcessIdentifier;
    }
}
