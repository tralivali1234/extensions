﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Linq;
using Signum.Engine.Maps;
using Signum.Engine.Operations;
using Signum.Entities;
using Signum.Entities.Basics;
using Signum.Entities.Isolation;
using Signum.Utilities;
using Signum.Utilities.ExpressionTrees;
using Signum.Utilities.Reflection;
using Signum.Engine.Processes;
using Signum.Entities.Processes;
using System.ServiceModel.Channels;
using System.ServiceModel;
using Signum.Engine.Scheduler;
using Signum.Entities.Scheduler;
using Signum.Engine.Authorization;
using Signum.Entities.Authorization;

namespace Signum.Engine.Isolation
{
    public enum IsolationStrategy
    {
        Isolated,
        Optional,
        None,
    }

    public static class IsolationLogic
    {
        public static bool IsStarted;

        public static ResetLazy<List<Lite<IsolationEntity>>> Isolations;

        internal static Dictionary<Type, IsolationStrategy> strategies = new Dictionary<Type, IsolationStrategy>();

        public static void Start(SchemaBuilder sb)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                sb.Include<IsolationEntity>()
                    .WithSave(IsolationOperation.Save)
                    .WithQuery(() => iso => new
                    {
                        Entity = iso,
                        iso.Id,
                        iso.Name
                    });

                sb.Schema.EntityEventsGlobal.PreSaving += EntityEventsGlobal_PreSaving;
                sb.Schema.SchemaCompleted += AssertIsolationStrategies;
                OperationLogic.SurroundOperation += OperationLogic_SurroundOperation;

                Isolations = sb.GlobalLazy(() => Database.RetrieveAllLite<IsolationEntity>(),
                    new InvalidateWith(typeof(IsolationEntity)));

                ProcessLogic.ApplySession += ProcessLogic_ApplySession;

                Validator.OverridePropertyValidator((IsolationMixin m) => m.Isolation).StaticPropertyValidation += (mi, pi) =>
                {
                    if (strategies.GetOrThrow(mi.MainEntity.GetType()) == IsolationStrategy.Isolated && mi.Isolation == null)
                        return ValidationMessage._0IsNotSet.NiceToString(pi.NiceName());

                    return null;
                };
                IsStarted = true;
            }
        }


        static IDisposable ProcessLogic_ApplySession(ProcessEntity process)
        {
            return IsolationEntity.Override(process.Data.TryIsolation());
        }

        static IDisposable SchedulerLogic_ApplySession(ITaskEntity task, ScheduledTaskEntity scheduled, IUserEntity user)
        {
            return IsolationEntity.Override(scheduled?.TryIsolation() ?? task?.TryIsolation() ?? user?.TryIsolation());
        }

        static IDisposable OperationLogic_SurroundOperation(IOperation operation, OperationLogEntity log, Entity entity, object[] args)
        {
            return IsolationEntity.Override(entity?.TryIsolation() ?? args.TryGetArgC<Lite<IsolationEntity>>());
        }

        static void EntityEventsGlobal_PreSaving(Entity ident, PreSavingContext ctx)
        {
            if (strategies.TryGet(ident.GetType(), IsolationStrategy.None) != IsolationStrategy.None && IsolationEntity.Current != null)
            {
                if (ident.Mixin<IsolationMixin>().Isolation == null)
                {
                    ident.Mixin<IsolationMixin>().Isolation = IsolationEntity.Current;
                    ctx.InvalidateGraph();
                }
                else if (!ident.Mixin<IsolationMixin>().Isolation.Is(IsolationEntity.Current))
                    throw new ApplicationException(IsolationMessage.Entity0HasIsolation1ButCurrentIsolationIs2.NiceToString(ident, ident.Mixin<IsolationMixin>().Isolation, IsolationEntity.Current));
            }
        }

        static void AssertIsolationStrategies()
        {
            var result = EnumerableExtensions.JoinStrict(
                strategies.Keys,
                Schema.Current.Tables.Keys.Where(a => !a.IsEnumEntityOrSymbol() && !typeof(SemiSymbol).IsAssignableFrom(a)),
                a => a,
                a => a,
                (a, b) => 0);

            var extra = result.Extra.OrderBy(a => a.Namespace).ThenBy(a => a.Name).ToString(t => "  IsolationLogic.Register<{0}>(IsolationStrategy.XXX);".FormatWith(t.Name), "\r\n");

            var lacking = result.Missing.GroupBy(a => a.Namespace).OrderBy(gr => gr.Key).ToString(gr => "  //{0}\r\n".FormatWith(gr.Key) +
                gr.ToString(t => "  IsolationLogic.Register<{0}>(IsolationStrategy.XXX);".FormatWith(t.Name), "\r\n"), "\r\n\r\n");

            if (extra.HasText() || lacking.HasText())
                throw new InvalidOperationException("IsolationLogic's strategies are not synchronized with the Schema.\r\n" +
                        (extra.HasText() ? ("Remove something like:\r\n" + extra + "\r\n\r\n") : null) +
                        (lacking.HasText() ? ("Add something like:\r\n" + lacking + "\r\n\r\n") : null));

            foreach (var item in strategies.Where(kvp => kvp.Value == IsolationStrategy.Isolated || kvp.Value == IsolationStrategy.Optional).Select(a => a.Key))
            {
                giRegisterFilterQuery.GetInvoker(item)();
            }

            Schema.Current.EntityEvents<IsolationEntity>().FilterQuery += () =>
            {
                if (IsolationEntity.Current == null || ExecutionMode.InGlobal)
                    return null;

                return new FilterQueryResult<IsolationEntity>(
                    a => a.ToLite().Is(IsolationEntity.Current),
                    a => a.ToLite().Is(IsolationEntity.Current));
            };
        }

        public static IsolationStrategy GetStrategy(Type type)
        {
            return strategies[type];
        }

        static readonly GenericInvoker<Action> giRegisterFilterQuery = new GenericInvoker<Action>(() => Register_FilterQuery<Entity>());
        static void Register_FilterQuery<T>() where T : Entity
        {
            Schema.Current.EntityEvents<T>().FilterQuery += () =>
            {
                if (ExecutionMode.InGlobal || IsolationEntity.Current == null)
                    return null;

                return new FilterQueryResult<T>(
                    a => a.Mixin<IsolationMixin>().Isolation.Is(IsolationEntity.Current),
                    a => a.Mixin<IsolationMixin>().Isolation.Is(IsolationEntity.Current));
            };

            Schema.Current.EntityEvents<T>().PreUnsafeInsert += (IQueryable query, LambdaExpression constructor, IQueryable<T> entityQuery) =>
            {
                if (ExecutionMode.InGlobal || IsolationEntity.Current == null)
                    return constructor;

                if (constructor.Body.Type == typeof(T))
                {
                    var newBody = Expression.Call(
                      miSetMixin.MakeGenericMethod(typeof(T), typeof(IsolationMixin), typeof(Lite<IsolationEntity>)),
                      constructor.Body,
                      Expression.Quote(isolationProperty),
                      Expression.Constant(IsolationEntity.Current));

                    return Expression.Lambda(newBody, constructor.Parameters);
                }

                return constructor; //MListTable
            };
        }

        static MethodInfo miSetMixin = ReflectionTools.GetMethodInfo((Entity a) => a.SetMixin((IsolationMixin m) => m.Isolation, null)).GetGenericMethodDefinition();
        static Expression<Func<IsolationMixin, Lite<IsolationEntity>>> isolationProperty = (IsolationMixin m) => m.Isolation;

        public static void Register<T>(IsolationStrategy strategy) where T : Entity
        {
            strategies.Add(typeof(T), strategy);

            if (strategy == IsolationStrategy.Isolated || strategy == IsolationStrategy.Optional)
                MixinDeclarations.Register(typeof(T), typeof(IsolationMixin));

            if (strategy == IsolationStrategy.Optional)
            {
                Schema.Current.Settings.FieldAttributes((T e) => e.Mixin<IsolationMixin>().Isolation).Remove<NotNullableAttribute>(); //Remove non-null 
            }
        }


        public static IDisposable IsolationFromOperationContext()
        {
            MessageHeaders headers = OperationContext.Current.IncomingMessageHeaders;

            int val = headers.FindHeader("CurrentIsolation", "http://www.signumsoftware.com/Isolation");

            if (val == -1)
                return null;

            return IsolationEntity.Override(Lite.Parse<IsolationEntity>(headers.GetHeader<string>(val)));
        }

        public static IEnumerable<T> WhereCurrentIsolationInMemory<T>(this IEnumerable<T> collection) where T : Entity
        {
            var curr = IsolationEntity.Current;

            if (curr == null || strategies[typeof(T)] == IsolationStrategy.None)
                return collection;

            return collection.Where(a => a.Isolation().Is(curr));
        }

        public static Lite<IsolationEntity> GetOnlyIsolation(List<Lite<Entity>> selectedEntities)
        {
            return selectedEntities.GroupBy(a => a.EntityType)
                .Select(gr => strategies[gr.Key] == IsolationStrategy.None ? null : giGetOnlyIsolation.GetInvoker(gr.Key)(gr))
                .NotNull()
                .Only();
        }


        static GenericInvoker<Func<IEnumerable<Lite<Entity>>, Lite<IsolationEntity>>> giGetOnlyIsolation =
            new GenericInvoker<Func<IEnumerable<Lite<Entity>>, Lite<IsolationEntity>>>(list => GetOnlyIsolation<Entity>(list));


        public static Lite<IsolationEntity> GetOnlyIsolation<T>(IEnumerable<Lite<Entity>> selectedEntities) where T : Entity
        {
            return selectedEntities.Cast<Lite<T>>().GroupsOf(100).Select(gr =>
                Database.Query<T>().Where(e => gr.Contains(e.ToLite())).Select(e => e.Isolation()).Only()
                ).NotNull().Only();
        }

        public static Dictionary<Type, IsolationStrategy> GetIsolationStrategies()
        {
            return strategies.ToDictionary();
        }

    }
}
