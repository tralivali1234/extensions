﻿using Signum.Engine.Basics;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Linq;
using Signum.Engine.Maps;
using Signum.Entities;
using Signum.Entities.Authorization;
using Signum.Entities.Basics;
using Signum.Entities.DynamicQuery;
using Signum.Utilities;
using Signum.Utilities.DataStructures;
using Signum.Utilities.ExpressionTrees;
using Signum.Utilities.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Signum.Engine.Authorization
{
    public static partial class TypeAuthLogic
    {
        static readonly Variable<bool> queryFilterDisabled = Statics.ThreadVariable<bool>("queryFilterDisabled");
        public static IDisposable DisableQueryFilter()
        {
            if (queryFilterDisabled.Value) return null;
            queryFilterDisabled.Value = true;
            return new Disposable(() => queryFilterDisabled.Value = false);
        }

        public static bool InSave
        {
            get { return inSave.Value; }
        }

        static readonly Variable<bool> inSave = Statics.ThreadVariable<bool>("inSave");
        static IDisposable OnInSave()
        {
            if (inSave.Value) return null;
            inSave.Value = true;
            return new Disposable(() => inSave.Value = false);
        }

        const string CreatedKey = "Created";
        const string ModifiedKey = "Modified";

        static void Schema_Saving_Instance(Entity ident)
        {
            if (ident.IsNew)
            {
                var created = (List<Entity>)Transaction.UserData.GetOrCreate(CreatedKey, () => new List<Entity>());
                if (created.Contains(ident))
                    return;

                created.Add(ident);
            }
            else
            {
                if (IsCreatedOrModified(Transaction.TopParentUserData(), ident) ||
                   IsCreatedOrModified(Transaction.UserData, ident))
                    return;

                var modified = (List<Entity>)Transaction.UserData.GetOrCreate(ModifiedKey, () => new List<Entity>());

                modified.Add(ident);
            }

            Transaction.PreRealCommit -= Transaction_PreRealCommit;
            Transaction.PreRealCommit += Transaction_PreRealCommit;
        }

        private static bool IsCreatedOrModified(Dictionary<string, object> dictionary, Entity ident)
        {
            var modified = (List<Entity>)dictionary.TryGetC(ModifiedKey);
            if (modified != null && modified.Contains(ident))
                return true;

            var created = (List<Entity>)dictionary.TryGetC(CreatedKey);
            if (created != null && created.Contains(ident))
                return true;

            return false;
        }

        public static void RemovePreRealCommitChecking(Entity entity)
        {
            var created = (List<Entity>)Transaction.UserData.TryGetC(CreatedKey);
            if (created != null && created.Contains(entity))
                created.Remove(entity);

            var modified = (List<Entity>)Transaction.UserData.TryGetC(ModifiedKey);
            if (modified != null && modified.Contains(entity))
                modified.Remove(entity);
        }

        static void Transaction_PreRealCommit(Dictionary<string, object> dic)
        {
            using (OnInSave())
            {
                var modified = (List<Entity>)dic.TryGetC(ModifiedKey);

                if (modified.HasItems())
                {
                    var groups = modified.GroupBy(e => e.GetType(), e => e.Id);

                    //Assert before
                    using (Transaction tr = Transaction.ForceNew())
                    {
                        foreach (var gr in groups)
                            miAssertAllowed.GetInvoker(gr.Key)(gr.ToArray(), TypeAllowedBasic.Modify);

                        tr.Commit();
                    }

                    //Assert after
                    foreach (var gr in groups)
                    {
                        miAssertAllowed.GetInvoker(gr.Key)(gr.ToArray(), TypeAllowedBasic.Modify);
                    }
                }

                var created = (List<Entity>)Transaction.UserData.TryGetC(CreatedKey);

                if (created.HasItems())
                {
                    var groups = created.GroupBy(e => e.GetType(), e => e.Id);

                    //Assert after
                    foreach (var gr in groups)
                        miAssertAllowed.GetInvoker(gr.Key)(gr.ToArray(), TypeAllowedBasic.Create);
                }
            }
        }


        static GenericInvoker<Action<PrimaryKey[], TypeAllowedBasic>> miAssertAllowed =
            new GenericInvoker<Action<PrimaryKey[], TypeAllowedBasic>>((a, tab) => AssertAllowed<Entity>(a, tab));
        static void AssertAllowed<T>(PrimaryKey[] requested, TypeAllowedBasic typeAllowed)
            where T : Entity
        {
            using (DisableQueryFilter())
            {
                var found = requested.GroupsOf(1000).SelectMany(gr => Database.Query<T>().Where(a => gr.Contains(a.Id)).Select(a => new
                {
                    a.Id,
                    Allowed = a.IsAllowedFor(typeAllowed, ExecutionMode.InUserInterface),
                })).ToArray();

                if (found.Length != requested.Length)
                    throw new EntityNotFoundException(typeof(T), requested.Except(found.Select(a => a.Id)).ToArray());

                PrimaryKey[] notFound = found.Where(a => !a.Allowed).Select(a => a.Id).ToArray();
                if (notFound.Any())
                {
                    List<DebugData> debugInfo = Database.Query<T>().Where(a => notFound.Contains(a.Id))
                        .Select(a => a.IsAllowedForDebug(typeAllowed, ExecutionMode.InUserInterface)).ToList();

                    string details = debugInfo.ToString(a => "  '{0}'  because {1}".FormatWith(a.Lite, a.Error), "\r\n");

                    throw new UnauthorizedAccessException(AuthMessage.NotAuthorizedTo0The1WithId2.NiceToString().FormatWith(
                        typeAllowed.NiceToString(),
                        notFound.Length == 1 ? typeof(T).NiceName() : typeof(T).NicePluralName(), notFound.CommaAnd()) + "\r\n" + details);
                }
            }
        }

        public static void AssertAllowed(this IEntity ident, TypeAllowedBasic allowed)
        {
            AssertAllowed(ident, allowed, ExecutionMode.InUserInterface);
        }

        public static void AssertAllowed(this IEntity ident, TypeAllowedBasic allowed, bool inUserInterface)
        {
            if (!ident.IsAllowedFor(allowed, inUserInterface))
                throw new UnauthorizedAccessException(AuthMessage.NotAuthorizedTo0The1WithId2.NiceToString().FormatWith(allowed.NiceToString().ToLower(), ident.GetType().NiceName(), ident.Id));
        }

        public static void AssertAllowed(this Lite<IEntity> lite, TypeAllowedBasic allowed)
        {
            AssertAllowed(lite, allowed, ExecutionMode.InUserInterface);
        }

        public static void AssertAllowed(this Lite<IEntity> lite, TypeAllowedBasic allowed, bool inUserInterface)
        {
            if (lite.IdOrNull == null)
                AssertAllowed(lite.EntityOrNull, allowed, inUserInterface);

            if (!lite.IsAllowedFor(allowed, inUserInterface))
                throw new UnauthorizedAccessException(AuthMessage.NotAuthorizedTo0The1WithId2.NiceToString().FormatWith(allowed.NiceToString().ToLower(), lite.EntityType.NiceName(), lite.Id));
        }

        [MethodExpander(typeof(IsAllowedForExpander))]
        public static bool IsAllowedFor(this IEntity ident, TypeAllowedBasic allowed)
        {
            return IsAllowedFor(ident, allowed, ExecutionMode.InUserInterface);
        }

        [MethodExpander(typeof(IsAllowedForExpander))]
        public static bool IsAllowedFor(this IEntity ident, TypeAllowedBasic allowed, bool inUserInterface)
        {
            return miIsAllowedForEntity.GetInvoker(ident.GetType()).Invoke(ident, allowed, inUserInterface);
        }

        static GenericInvoker<Func<IEntity, TypeAllowedBasic, bool, bool>> miIsAllowedForEntity
            = new GenericInvoker<Func<IEntity, TypeAllowedBasic, bool, bool>>((ie, tab, ec) => IsAllowedFor<Entity>((Entity)ie, tab, ec));
        [MethodExpander(typeof(IsAllowedForExpander))]
        static bool IsAllowedFor<T>(this T entity, TypeAllowedBasic allowed, bool inUserInterface)
            where T : Entity
        {
            if (!AuthLogic.IsEnabled)
                return true;

            var tac = GetAllowed(entity.GetType());

            var min = inUserInterface ? tac.MinUI() : tac.MinDB();

            if (allowed <= min)
                return true;

            var max = inUserInterface ? tac.MaxUI() : tac.MaxDB();

            if (max < allowed)
                return false;

            var inMemoryCodition = IsAllowedInMemory<T>(tac, allowed, inUserInterface);
            if (inMemoryCodition != null)
                return inMemoryCodition(entity);

            using (DisableQueryFilter())
                return entity.InDB().WhereIsAllowedFor(allowed, inUserInterface).Any();
        }

        private static Func<T, bool> IsAllowedInMemory<T>(TypeAllowedAndConditions tac, TypeAllowedBasic allowed, bool inUserInterface) where T : Entity
        {
            if (tac.Conditions.Any(c => TypeConditionLogic.GetInMemoryCondition<T>(c.TypeCondition) == null))
                return null;

            return entity =>
            {
                foreach (var cond in tac.Conditions.Reverse())
                {
                    var func = TypeConditionLogic.GetInMemoryCondition<T>(cond.TypeCondition);

                    if (func(entity))
                        return cond.Allowed.Get(inUserInterface) >= allowed;
                }

                return tac.FallbackOrNone.Get(inUserInterface) >= allowed;
            };
        }

        [MethodExpander(typeof(IsAllowedForExpander))]
        public static bool IsAllowedFor(this Lite<IEntity> lite, TypeAllowedBasic allowed)
        {
            return IsAllowedFor(lite, allowed, ExecutionMode.InUserInterface);
        }

        [MethodExpander(typeof(IsAllowedForExpander))]
        public static bool IsAllowedFor(this Lite<IEntity> lite, TypeAllowedBasic allowed, bool inUserInterface)
        {
            return miIsAllowedForLite.GetInvoker(lite.EntityType).Invoke(lite, allowed, inUserInterface);
        }

        static GenericInvoker<Func<Lite<IEntity>, TypeAllowedBasic, bool, bool>> miIsAllowedForLite =
            new GenericInvoker<Func<Lite<IEntity>, TypeAllowedBasic, bool, bool>>((l, tab, ec) => IsAllowedFor<Entity>(l, tab, ec));
        [MethodExpander(typeof(IsAllowedForExpander))]
        static bool IsAllowedFor<T>(this Lite<IEntity> lite, TypeAllowedBasic allowed, bool inUserInterface)
            where T : Entity
        {
            if (!AuthLogic.IsEnabled)
                return true;

            using (DisableQueryFilter())
                return ((Lite<T>)lite).InDB().WhereIsAllowedFor(allowed, inUserInterface).Any();
        }

        class IsAllowedForExpander : IMethodExpander
        {
            public Expression Expand(Expression instance, Expression[] arguments, MethodInfo mi)
            {
                TypeAllowedBasic allowed = (TypeAllowedBasic)ExpressionEvaluator.Eval(arguments[1]);

                bool inUserInterface = arguments.Length == 3 ? (bool)ExpressionEvaluator.Eval(arguments[2]) : ExecutionMode.InUserInterface;

                Expression exp = arguments[0].Type.IsLite() ? Expression.Property(arguments[0], "Entity") : arguments[0];

                return IsAllowedExpression(exp, allowed, inUserInterface);
            }
        }

        [MethodExpander(typeof(IsAllowedForDebugExpander))]
        public static DebugData IsAllowedForDebug(this IEntity ident, TypeAllowedBasic allowed, bool inUserInterface)
        {
            return miIsAllowedForDebugEntity.GetInvoker(ident.GetType()).Invoke((Entity)ident, allowed, inUserInterface);
        }

        [MethodExpander(typeof(IsAllowedForDebugExpander))]
        public static string CanBeModified(this IEntity ident)
        {
            var taac = TypeAuthLogic.GetAllowed(ident.GetType());

            if (taac.Conditions.IsEmpty())
                return taac.FallbackOrNone.GetDB() >= TypeAllowedBasic.Modify ? null : AuthAdminMessage.CanNotBeModified.NiceToString();

            if (ident.IsNew)
                return null;

            return IsAllowedForDebug(ident, TypeAllowedBasic.Modify, false)?.CanBeModified;
        }

        static GenericInvoker<Func<IEntity, TypeAllowedBasic, bool, DebugData>> miIsAllowedForDebugEntity =
            new GenericInvoker<Func<IEntity, TypeAllowedBasic, bool, DebugData>>((ii, tab, ec) => IsAllowedForDebug<Entity>((Entity)ii, tab, ec));
        [MethodExpander(typeof(IsAllowedForDebugExpander))]
        static DebugData IsAllowedForDebug<T>(this T entity, TypeAllowedBasic allowed, bool inUserInterface)
            where T : Entity
        {
            if (!AuthLogic.IsEnabled)
                return null;

            if (entity.IsNew)
                throw new InvalidOperationException("The entity {0} is new".FormatWith(entity));

            using (DisableQueryFilter())
                return entity.InDB().Select(e => e.IsAllowedForDebug(allowed, inUserInterface)).SingleEx();
        }

        [MethodExpander(typeof(IsAllowedForDebugExpander))]
        public static DebugData IsAllowedForDebug(this Lite<IEntity> lite, TypeAllowedBasic allowed, bool inUserInterface)
        {
            return miIsAllowedForDebugLite.GetInvoker(lite.EntityType).Invoke(lite, allowed, inUserInterface);
        }

        static GenericInvoker<Func<Lite<IEntity>, TypeAllowedBasic, bool, DebugData>> miIsAllowedForDebugLite =
            new GenericInvoker<Func<Lite<IEntity>, TypeAllowedBasic, bool, DebugData>>((l, tab, ec) => IsAllowedForDebug<Entity>(l, tab, ec));
        [MethodExpander(typeof(IsAllowedForDebugExpander))]
        static DebugData IsAllowedForDebug<T>(this Lite<IEntity> lite, TypeAllowedBasic allowed, bool inUserInterface)
             where T : Entity
        {
            if (!AuthLogic.IsEnabled)
                return null;

            using (DisableQueryFilter())
                return ((Lite<T>)lite).InDB().Select(a => a.IsAllowedForDebug(allowed, inUserInterface)).SingleEx();
        }

        class IsAllowedForDebugExpander : IMethodExpander
        {
            public Expression Expand(Expression instance, Expression[] arguments, MethodInfo mi)
            {
                TypeAllowedBasic allowed = (TypeAllowedBasic)ExpressionEvaluator.Eval(arguments[1]);

                bool inUserInterface = arguments.Length == 3 ? (bool)ExpressionEvaluator.Eval(arguments[2]) : ExecutionMode.InUserInterface;

                Expression exp = arguments[0].Type.IsLite() ? Expression.Property(arguments[0], "Entity") : arguments[0];

                return IsAllowedExpressionDebug(exp, allowed, inUserInterface);
            }
        }


        static FilterQueryResult<T> TypeAuthLogic_FilterQuery<T>()
          where T : Entity
        {
            if (queryFilterDisabled.Value)
                return null;

            if (ExecutionMode.InGlobal || !AuthLogic.IsEnabled)
                return null;

            var ui = ExecutionMode.InUserInterface;
            AssertMinimum<T>(ui);

            ParameterExpression e = Expression.Parameter(typeof(T), "e");

            Expression body = IsAllowedExpression(e, TypeAllowedBasic.Read, ui);

            if (body is ConstantExpression ce)
            {
                if (((bool)ce.Value))
                    return null;
            }

            Func<T, bool> func = IsAllowedInMemory<T>(GetAllowed(typeof(T)), TypeAllowedBasic.Read, ui);

            return new FilterQueryResult<T>(Expression.Lambda<Func<T, bool>>(body, e), func);
        }


        [MethodExpander(typeof(WhereAllowedExpander))]
        public static IQueryable<T> WhereAllowed<T>(this IQueryable<T> query)
            where T : Entity
        {
            if (ExecutionMode.InGlobal || !AuthLogic.IsEnabled)
                return query;

            var ui = ExecutionMode.InUserInterface;

            AssertMinimum<T>(ui);

            return WhereIsAllowedFor<T>(query, TypeAllowedBasic.Read, ui);
        }

        private static void AssertMinimum<T>(bool ui) where T : Entity
        {
            var allowed = GetAllowed(typeof(T));
            var max = ui ? allowed.MaxUI() : allowed.MaxDB();
            if (max < TypeAllowedBasic.Read)
                throw new UnauthorizedAccessException("Type {0} is not authorized{1}{2}".FormatWith(typeof(T).Name,
                    ui ? " in user interface" : null,
                    allowed.Conditions.Any() ? " for any condition" : null));
        }


        [MethodExpander(typeof(WhereIsAllowedForExpander))]
        public static IQueryable<T> WhereIsAllowedFor<T>(this IQueryable<T> query, TypeAllowedBasic allowed, bool inUserInterface)
            where T : Entity
        {
            ParameterExpression e = Expression.Parameter(typeof(T), "e");

            Expression body = IsAllowedExpression(e, allowed, inUserInterface);


            if (body is ConstantExpression ce)
            {
                if (((bool)ce.Value))
                    return query;
            }

            IQueryable<T> result = query.Where(Expression.Lambda<Func<T, bool>>(body, e));

            return result;
        }

        class WhereAllowedExpander : IMethodExpander
        {
            public Expression Expand(Expression instance, Expression[] arguments, MethodInfo mi)
            {
                return miCallWhereAllowed.GetInvoker(mi.GetGenericArguments()).Invoke(arguments[0]);
            }

            static GenericInvoker<Func<Expression, Expression>> miCallWhereAllowed = new GenericInvoker<Func<Expression, Expression>>(exp => CallWhereAllowed<TypeEntity>(exp));
            static Expression CallWhereAllowed<T>(Expression expression)
                where T : Entity
            {
                IQueryable<T> query = new Query<T>(DbQueryProvider.Single, expression);
                IQueryable<T> result = WhereAllowed(query);
                return result.Expression;
            }
        }

        class WhereIsAllowedForExpander : IMethodExpander
        {
            public Expression Expand(Expression instance, Expression[] arguments, MethodInfo mi)
            {
                TypeAllowedBasic allowed = (TypeAllowedBasic)ExpressionEvaluator.Eval(arguments[1]);
                bool inUserInterface = (bool)ExpressionEvaluator.Eval(arguments[2]);

                return miCallWhereIsAllowedFor.GetInvoker(mi.GetGenericArguments())(arguments[0], allowed, inUserInterface);
            }

            static GenericInvoker<Func<Expression, TypeAllowedBasic, bool, Expression>> miCallWhereIsAllowedFor =
                new GenericInvoker<Func<Expression, TypeAllowedBasic, bool, Expression>>((ex, tab, ui) => CallWhereIsAllowedFor<TypeEntity>(ex, tab, ui));
            static Expression CallWhereIsAllowedFor<T>(Expression expression, TypeAllowedBasic allowed, bool inUserInterface)
                where T : Entity
            {
                IQueryable<T> query = new Query<T>(DbQueryProvider.Single, expression);
                IQueryable<T> result = WhereIsAllowedFor(query, allowed, inUserInterface);
                return result.Expression;
            }
        }

        public static Expression IsAllowedExpression(Expression entity, TypeAllowedBasic requested, bool inUserInterface)
        {
            Type type = entity.Type;

            TypeAllowedAndConditions tac = GetAllowed(type);

            Expression baseValue = Expression.Constant(tac.FallbackOrNone.Get(inUserInterface) >= requested);

            var expression = tac.Conditions.Aggregate(baseValue, (acum, tacRule) =>
            {
                var lambda = TypeConditionLogic.GetCondition(type, tacRule.TypeCondition);

                var exp = (Expression)Expression.Invoke(lambda, entity);

                if (tacRule.Allowed.Get(inUserInterface) >= requested)
                    return Expression.Or(exp, acum);
                else
                    return Expression.And(Expression.Not(exp), acum);
            });

            return DbQueryProvider.Clean(expression, false, null);
        }


        static ConstructorInfo ciDebugData = ReflectionTools.GetConstuctorInfo(() => new DebugData(null, TypeAllowedBasic.Create, true, TypeAllowed.Create, null));
        static ConstructorInfo ciGroupDebugData = ReflectionTools.GetConstuctorInfo(() => new ConditionDebugData(null, true, TypeAllowed.Create));
        static MethodInfo miToLite = ReflectionTools.GetMethodInfo((Entity a) => a.ToLite()).GetGenericMethodDefinition();

        internal static Expression IsAllowedExpressionDebug(Expression entity, TypeAllowedBasic requested, bool inUserInterface)
        {
            Type type = entity.Type;

            TypeAllowedAndConditions tac = GetAllowed(type);

            Expression baseValue = Expression.Constant(tac.FallbackOrNone.Get(inUserInterface) >= requested);

            var list = (from line in tac.Conditions
                        select Expression.New(ciGroupDebugData, Expression.Constant(line.TypeCondition, typeof(TypeConditionSymbol)),
                        Expression.Invoke(TypeConditionLogic.GetCondition(type, line.TypeCondition), entity),
                        Expression.Constant(line.Allowed))).ToArray();

            Expression newList = Expression.ListInit(Expression.New(typeof(List<ConditionDebugData>)), list);

            Expression liteEntity = Expression.Call(null, miToLite.MakeGenericMethod(entity.Type), entity);

            return Expression.New(ciDebugData, liteEntity,
                Expression.Constant(requested),
                Expression.Constant(inUserInterface),
                Expression.Constant(tac.Fallback),
                newList);
        }

        public class DebugData
        {
            public DebugData(Lite<IEntity> lite, TypeAllowedBasic requested, bool userInterface, TypeAllowed fallback, List<ConditionDebugData> groups)
            {
                this.Lite = lite;
                this.Requested = requested;
                this.Fallback = fallback;
                this.UserInterface = userInterface;
                this.Conditions = groups;
            }

            public Lite<IEntity> Lite { get; private set; }
            public TypeAllowedBasic Requested { get; private set; }
            public TypeAllowed Fallback { get; private set; }
            public bool UserInterface { get; private set; }

            public List<ConditionDebugData> Conditions { get; private set; }

            public bool IsAllowed
            {
                get
                {
                    foreach (var item in Conditions.AsEnumerable().Reverse())
                    {
                        if (item.InGroup)
                            return Requested <= item.Allowed.Get(UserInterface);
                    }

                    return Requested <= Fallback.Get(UserInterface);
                }
            }

            public string Error
            {
                get
                {
                    foreach (var cond in Conditions.AsEnumerable().Reverse())
                    {
                        if (cond.InGroup)
                            return Requested <= cond.Allowed.Get(UserInterface) ? null :
                                "is a {0} that belongs to condition {1} that is {2} (less than {3})".FormatWith(Lite.EntityType.TypeName(), cond.TypeCondition, cond.Allowed.Get(UserInterface), Requested);
                    }

                    return Requested <= Fallback.Get(UserInterface) ? null :
                        "is a {0} but does not belong to any condition and the base value is {1} (less than {2})".FormatWith(Lite.EntityType.TypeName(), Fallback.Get(UserInterface), Requested);
                }
            }

            public string CanBeModified
            {
                get
                {
                    foreach (var cond in Conditions.AsEnumerable().Reverse())
                    {
                        if (cond.InGroup)
                            return Requested <= cond.Allowed.Get(UserInterface) ? null :
                                AuthAdminMessage.CanNotBeModifiedBecauseIsA0.NiceToString(cond.TypeCondition.NiceToString());
                    }

                    return Requested <= Fallback.Get(UserInterface) ? null :
                        AuthAdminMessage.CanNotBeModifiedBecauseIsNotA0.NiceToString(Conditions.AsEnumerable().Reverse());
                }
            }
        }

        public class ConditionDebugData
        {
            public TypeConditionSymbol TypeCondition { get; private set; }
            public bool InGroup { get; private set; }
            public TypeAllowed Allowed { get; private set; }

            internal ConditionDebugData(TypeConditionSymbol typeCondition, bool inGroup, TypeAllowed allowed)
            {
                this.TypeCondition = typeCondition;
                this.InGroup = inGroup;
                this.Allowed = allowed;
            }
        }

        public static DynamicQueryCore<T> ToDynamicDisableAutoFilter<T>(this IQueryable<T> query)
        {
            return new AutoDynamicQueryNoFilterCore<T>(query);
        }

        internal class AutoDynamicQueryNoFilterCore<T> : AutoDynamicQueryCore<T>
        {
            public AutoDynamicQueryNoFilterCore(IQueryable<T> query)
                : base(query)
            { }

            public override async Task<ResultTable> ExecuteQueryAsync(QueryRequest request, CancellationToken token)
            {
                using (TypeAuthLogic.DisableQueryFilter())
                {
                    return await base.ExecuteQueryAsync(request, token);
                }
            }

            public override async Task<Lite<Entity>> ExecuteUniqueEntityAsync(UniqueEntityRequest request, CancellationToken token)
            {
                using (TypeAuthLogic.DisableQueryFilter())
                {
                    return await base.ExecuteUniqueEntityAsync(request, token);
                }
            }

            public override async Task<object> ExecuteQueryValueAsync(QueryValueRequest request, CancellationToken token)
            {
                using (TypeAuthLogic.DisableQueryFilter())
                {
                    return await base.ExecuteQueryValueAsync(request, token);
                }
            }
        }

        public static RuleTypeEntity ToRuleType(this TypeAllowedAndConditions allowed, Lite<RoleEntity> role, TypeEntity resource)
        {
            return new RuleTypeEntity
            {
                Role = role,
                Resource = resource,
                Allowed = allowed.Fallback.Value,
                Conditions = allowed.Conditions.Select(a => new RuleTypeConditionEmbedded
                {
                    Allowed = a.Allowed,
                    Condition = a.TypeCondition
                }).ToMList()
            };
        }

        public static TypeAllowedAndConditions ToTypeAllowedAndConditions(this RuleTypeEntity rule)
        {
            return new TypeAllowedAndConditions(rule.Allowed,
                rule.Conditions.Select(c => new TypeConditionRuleEmbedded(c.Condition, c.Allowed)).ToMList());
        }

        static SqlPreCommand Schema_Synchronizing(Replacements rep)
        {
            var conds = (from rt in Database.Query<RuleTypeEntity>()
                         from c in rt.Conditions
                         select new { rt.Resource, c.Condition, rt.Role }).ToList();

            var errors = conds.GroupBy(a => new { a.Resource, a.Condition }, a => a.Role)
                .Where(gr =>
                {
                    if (gr.Key.Condition.FieldInfo == null)
                    {
                        var replacedName = rep.TryGetC(typeof(TypeConditionSymbol).Name)?.TryGetC(gr.Key.Condition.Key);
                        if (replacedName == null)
                            return false; // Other Syncronizer will do it

                        return !TypeConditionLogic.ConditionsFor(gr.Key.Resource.ToType()).Any(a => a.Key == replacedName);
                    }

                    return !TypeConditionLogic.IsDefined(gr.Key.Resource.ToType(), gr.Key.Condition);
                })
                .ToList();

            using (rep.WithReplacedDatabaseName())
                return errors.Select(a => Administrator.UnsafeDeletePreCommandMList((RuleTypeEntity rt) => rt.Conditions, Database.MListQuery((RuleTypeEntity rt) => rt.Conditions)
                    .Where(mle => mle.Element.Condition.Is(a.Key.Condition) && mle.Parent.Resource.Is(a.Key.Resource)))
                    .AddComment("TypeCondition {0} not defined for {1} (roles {2})".FormatWith(a.Key.Condition, a.Key.Resource, a.ToString(", "))))
                    .Combine(Spacing.Double);
        }
    }
}
