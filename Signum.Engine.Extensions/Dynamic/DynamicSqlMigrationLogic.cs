﻿using Signum.Engine;
using Signum.Engine.Basics;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Maps;
using Signum.Engine.Migrations;
using Signum.Engine.Operations;
using Signum.Entities;
using Signum.Entities.Authorization;
using Signum.Entities.Dynamic;
using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Signum.Engine.Dynamic
{
    public static class DynamicSqlMigrationLogic
    {

        static Expression<Func<DynamicRenameEntity, bool>> IsAppliedExpression =
        r => Database.Query<DynamicSqlMigrationEntity>().Any(m => m.CreationDate > r.CreationDate);

        [ExpressionField]
        public static bool IsApplied(this DynamicRenameEntity r)
        {
            return IsAppliedExpression.Evaluate(r);
        }

        public static StringBuilder CurrentLog = null;
        public static string LastLog;

        public static void Start(SchemaBuilder sb)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                sb.Include<DynamicRenameEntity>()
                      .WithQuery(() => e => new
                      {
                          Entity = e,
                          e.Id,
                          e.CreationDate,
                          e.ReplacementKey,
                          e.OldName,
                          e.NewName,
                          IsApplied = e.IsApplied(),
                      });

                sb.Include<DynamicSqlMigrationEntity>()
                    .WithQuery(() => e => new
                    {
                        Entity = e,
                        e.Id,
                        e.CreationDate,
                        e.CreatedBy,
                        e.ExecutionDate,
                        e.ExecutedBy,
                        e.Comment,
                    });

                new Graph<DynamicSqlMigrationEntity>.Construct(DynamicSqlMigrationOperation.Create)
                {
                    Construct = args => 
                    {
                        if (DynamicLogic.CodeGenError!= null)
                            throw new InvalidOperationException(DynamicSqlMigrationMessage.PreventingGenerationNewScriptBecauseOfErrorsInDynamicCodeFixErrorsAndRestartServer.NiceToString());

                        var old = Replacements.AutoReplacement;

                        var lastRenames = Database.Query<DynamicRenameEntity>()
                        .Where(a => !a.IsApplied())
                        .OrderBy(a => a.CreationDate)
                        .ToList();

                        try
                        {
                            if (Replacements.AutoReplacement == null)
                                Replacements.AutoReplacement = ctx =>
                                {
                                    var currentName =
                                    ctx.ReplacementKey.StartsWith(Replacements.KeyEnumsForTable("")) ? AutoReplacementEnums(ctx):
                                    ctx.ReplacementKey.StartsWith(PropertyRouteLogic.PropertiesFor.FormatWith("")) ? DynamicAutoReplacementsProperties(ctx, lastRenames) :
                                    ctx.ReplacementKey.StartsWith(Replacements.KeyColumnsForTable("")) ? DynamicAutoReplacementsColumns(ctx, lastRenames) :
                                    ctx.ReplacementKey == Replacements.KeyTables ? DynamicAutoReplacementsSimple(ctx, lastRenames, Replacements.KeyTables) :
                                    ctx.ReplacementKey == typeof(OperationSymbol).Name ? DynamicAutoReplacementsOperations(ctx, lastRenames) :
                                    ctx.ReplacementKey == QueryLogic.QueriesKey ? DynamicAutoReplacementsSimple(ctx, lastRenames, DynamicTypeLogic.TypeNameKey) :
                                    DynamicAutoReplacementsSimple(ctx, lastRenames, ctx.ReplacementKey);

                                    if (currentName != null)
                                        return new Replacements.Selection(ctx.OldValue, currentName);

                                    return new Replacements.Selection(ctx.OldValue, null);
                                };

                            var script = Schema.Current.SynchronizationScript(interactive: false, replaceDatabaseName: SqlMigrationRunner.DatabaseNameReplacement);

                            return new DynamicSqlMigrationEntity
                            {
                                CreationDate = TimeZoneManager.Now,
                                CreatedBy = UserEntity.Current.ToLite(),
                                Script = script?.ToString(),
                            };
                        }
                        finally
                        {
                            Replacements.AutoReplacement = old;
                        }
                    }
                }.Register();

                new Graph<DynamicSqlMigrationEntity>.Execute(DynamicSqlMigrationOperation.Save)
                {
                    CanExecute = a=> a.ExecutionDate == null ? null : DynamicSqlMigrationMessage.TheMigrationIsAlreadyExecuted.NiceToString(),
                    CanBeNew = true,
                    CanBeModified = true,
                    Execute = (e, _) => { }
                }.Register();

                new Graph<DynamicSqlMigrationEntity>.Execute(DynamicSqlMigrationOperation.Execute)
                {
                    CanExecute = a => a.ExecutionDate == null ? null : DynamicSqlMigrationMessage.TheMigrationIsAlreadyExecuted.NiceToString(),
                    
                    Execute = (e, _) => {

                        if (CurrentLog != null)
                            throw new InvalidOperationException("There is already a migration running");

                        e.ExecutionDate = TimeZoneManager.Now;
                        e.ExecutedBy = UserEntity.Current.ToLite();

                        var oldOut = Console.Out;
                        try
                        {
                            CurrentLog = new StringBuilder();
                            LastLog = null;
                            Console.SetOut(new SynchronizedStringWriter(CurrentLog));

                            string title = e.CreationDate + (e.Comment.HasText() ? " ({0})".FormatWith(e.Comment) : null);

                            using (Transaction tr = Transaction.ForceNew(System.Data.IsolationLevel.Unspecified))
                            {
                                SqlMigrationRunner.ExecuteScript(title, e.Script);
                                tr.Commit();
                            }

                        }
                        catch(MigrationException ex)
                        {
                            ex.InnerException.PreserveStackTrace();
                            throw ex.InnerException;
                        }
                        finally
                        {
                            LastLog = CurrentLog?.ToString();
                            CurrentLog = null;
                            Console.SetOut(oldOut);
                        }
                    }
                }.Register();

                new Graph<DynamicSqlMigrationEntity>.Delete(DynamicSqlMigrationOperation.Delete)
                {
                    CanDelete = a => a.ExecutionDate == null ? null : DynamicSqlMigrationMessage.TheMigrationIsAlreadyExecuted.NiceToString(),
                    Delete = (e, _) => { e.Delete(); }
                }.Register();
            }
            
        }
        
        public static void AddDynamicRename(string replacementKey, string oldName, string newName)
        {
            new DynamicRenameEntity { ReplacementKey = replacementKey, OldName = oldName, NewName = newName }.Save();
        }

        private static string AutoReplacementEnums(Replacements.AutoReplacementContext ctx)
        {
            StringDistance sd = new StringDistance();
            return ctx.NewValues.WithMin(nv => sd.LevenshteinDistance(nv, ctx.OldValue));
        }

        public static string DynamicAutoReplacementsSimple(Replacements.AutoReplacementContext ctx, List<DynamicRenameEntity> lastRenames, string replacementKey)
        {
            var list = lastRenames.Where(a => a.ReplacementKey == replacementKey).ToList();

            var currentName = ctx.OldValue;
            foreach (var r in list)
            {
                if (r.OldName == currentName)
                    currentName = r.NewName;
            }

            if (ctx.NewValues.Contains(currentName))
                return currentName;

            return null;
        }

        public static string DynamicAutoReplacementsOperations(Replacements.AutoReplacementContext ctx, List<DynamicRenameEntity> lastRenames)
        {
            var typeReplacements = lastRenames.Where(a => a.ReplacementKey == DynamicTypeLogic.TypeNameKey).ToList();

            var typeName = ctx.OldValue.TryBefore("Operation.");
            if (typeName == null)
                return null;


            foreach (var r in typeReplacements)
            {
                if (r.OldName == typeName)
                    typeName = r.NewName;
            }

            var newName = typeName + "Operation." + ctx.OldValue.After("Operation.");
            if (ctx.NewValues.Contains(newName))
                return newName;

            return null;
        }

        public static string DynamicAutoReplacementsProperties(Replacements.AutoReplacementContext ctx, List<DynamicRenameEntity> lastRenames)
        {
            var prefix = ctx.ReplacementKey.Before(":");
            var tableNames = GetAllRenames(ctx.ReplacementKey.After(":"), DynamicTypeLogic.TypeNameKey, lastRenames);

            var allKeys = tableNames.Select(tn => prefix + ":" + tn).And(DynamicTypeLogic.UnknownPropertyKey).ToList();

            var list = lastRenames.Where(a => allKeys.Contains(a.ReplacementKey)).ToList();

            var currentName = ctx.OldValue;
            foreach (var r in list)
            {
                if (currentName.Split('.').Contains(r.OldName))
                    currentName = currentName.Split('.').Select(p => p == r.OldName ? r.NewName : p).ToString(".");
            }

            if (ctx.NewValues.Contains(currentName))
                return currentName;

            return null;
        }
        public static string DynamicAutoReplacementsColumns(Replacements.AutoReplacementContext ctx, List<DynamicRenameEntity> lastRenames)
        {
            var prefix = ctx.ReplacementKey.Before(":");
            var tableNames = GetAllRenames(ctx.ReplacementKey.After(":"), Replacements.KeyTables, lastRenames);

            var allKeys = tableNames.Select(tn => prefix + ":" + tn).And(DynamicTypeLogic.UnknownColumnKey).ToList();

            var list = lastRenames.Where(a => allKeys.Contains(a.ReplacementKey)).ToList();

            var currentName = ctx.OldValue;
            foreach (var r in list)
            {
                if (currentName.Split('_').Contains(r.OldName))
                    currentName = currentName.Split('_').Select(p => p == r.OldName ? r.NewName : p).ToString("_");
            }

            if (ctx.NewValues.Contains(currentName))
                return currentName;

            return null;
        }

        private static List<string> GetAllRenames(string lastName, string replacementKey, List<DynamicRenameEntity> lastRenames)
        {
            var currentTypeName = lastName;
            var typeRenames = lastRenames.Where(a => a.ReplacementKey == replacementKey);

            List<string> allTypeNames = new List<string> { currentTypeName };
            foreach (var item in typeRenames.Reverse())
            {
                if (item.NewName == currentTypeName)
                {
                    currentTypeName = item.OldName;
                    allTypeNames.Add(currentTypeName);
                }
            }

            return allTypeNames;
        }

        public static string GetLog()
        {
            var ll = LastLog;
            var sb = CurrentLog;
            if (ll != null)
                return ll;

            if (sb != null)
            {
                lock (sb)
                    return sb.ToString();
            }

            return null;
        }

        internal class SynchronizedStringWriter : TextWriter
        {
            private StringBuilder stringBuilder;

            public SynchronizedStringWriter(StringBuilder currentLog)
            {
                this.stringBuilder = currentLog;
            }

            public override Encoding Encoding => Encoding.Unicode;

            public override void Write(char value)
            {
                lock (stringBuilder)
                    base.Write(value);
            }

            public override void WriteLine()
            {
                lock (stringBuilder)
                    base.WriteLine();
            }

            public override void Write(string value)
            {
                lock (stringBuilder)
                    base.Write(value);
            }
        }



    }
}
