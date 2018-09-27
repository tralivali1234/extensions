﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities.Disconnected;
using Signum.Engine.Maps;
using Signum.Engine.Disconnected;
using Signum.Engine.DynamicQuery;
using Signum.Entities;
using Signum.Entities.Reflection;
using Signum.Utilities;
using System.IO;
using System.Data.Common;
using Signum.Engine.Linq;
using Signum.Utilities.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Linq.Expressions;
using Signum.Engine.Authorization;
using System.Threading;
using System.Reflection;
using Signum.Engine.Operations;
using Signum.Entities.Authorization;
using Signum.Entities.Basics;
using Signum.Engine.Basics;
using System.Text.RegularExpressions;

namespace Signum.Engine.Disconnected
{
    public class ExportManager
    {
        public ExportManager()
        {
            this.miUnsafeLock = this.GetType().GetMethod("UnsafeLock", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        class DownloadTable
        {
            public Type Type;
            public Table Table;
            public IDisconnectedStrategy Strategy;
        }

        public void Initialize()
        {
            downloadTables = Schema.Current.Tables.Values
                .Select(t => new DownloadTable { Type = t.Type, Table = t, Strategy = DisconnectedLogic.GetStrategy(t.Type) })
                .Where(p => p.Strategy.Download != Download.None)
                .ToList();
        }

        List<DownloadTable> downloadTables;

        Dictionary<Lite<DisconnectedExportEntity>, RunningExports> runningExports = new Dictionary<Lite<DisconnectedExportEntity>, RunningExports>();

        class RunningExports
        {
            public Task Task;
            public CancellationTokenSource CancelationSource;
        }

        public virtual Lite<DisconnectedExportEntity> BeginExportDatabase(DisconnectedMachineEntity machine)
        {
            Lite<DisconnectedExportEntity> export = new DisconnectedExportEntity
            {
                Machine = machine.ToLite(),
                Copies = downloadTables.Select(t => new DisconnectedExportTableEmbedded
                {
                    Type = t.Type.ToTypeEntity().ToLite()
                }).ToMList()
            }.Save().ToLite();

            var cancelationSource = new CancellationTokenSource();

            var user = UserHolder.Current;

            var token = cancelationSource.Token;

            var task = Task.Factory.StartNew(() =>
            {
                using (UserHolder.UserSession(user))
                {
                    OnStartExporting(machine);
                    DisconnectedMachineEntity.Current = machine.ToLite();

                    try
                    {
                        using (token.MeasureTime(l => export.InDB().UnsafeUpdate().Set(s => s.Lock, s => l).Execute()))
                        {
                            foreach (var tuple in downloadTables)
                            {
                                token.ThrowIfCancellationRequested();

                                if (tuple.Strategy.Upload == Upload.Subset)
                                    miUnsafeLock.MakeGenericMethod(tuple.Type).Invoke(this, new object[] { machine.ToLite(), tuple.Strategy, export });
                            }
                        }

                        string connectionString;
                        using (token.MeasureTime(l => export.InDB().UnsafeUpdate().Set(s => s.CreateDatabase, s => l).Execute()))
                            connectionString = CreateDatabase(machine);

                        var newDatabase = new SqlConnector(connectionString, Schema.Current, ((SqlConnector)Connector.Current).Version);


                        using (token.MeasureTime(l => export.InDB().UnsafeUpdate().Set(s => s.CreateSchema, s => l).Execute()))
                        using (Connector.Override(newDatabase))
                        using (ObjectName.OverrideOptions(new ObjectNameOptions { AvoidDatabaseName = true }))
                        {
                            Administrator.TotalGeneration();
                        }

                        using (token.MeasureTime(l => export.InDB().UnsafeUpdate().Set(s => s.DisableForeignKeys, s => l).Execute()))
                        using (Connector.Override(newDatabase))
                        using (ObjectName.OverrideOptions(new ObjectNameOptions { AvoidDatabaseName = true }))
                        {
                            foreach (var tuple in downloadTables.Where(t => !t.Type.IsEnumEntity()))
                            {
                                token.ThrowIfCancellationRequested();

                                DisableForeignKeys(tuple.Table);
                            }
                        }

                        DatabaseName newDatabaseName = new DatabaseName(null, newDatabase.DatabaseName());

                        foreach (var tuple in downloadTables)
                        {
                            token.ThrowIfCancellationRequested();
                            int ms = 0;
                            using (token.MeasureTime(l => ms = l))
                            {
                                tuple.Strategy.Exporter.Export(tuple.Table, tuple.Strategy, newDatabaseName, machine);
                            }

                            export.MListElementsLite(_ => _.Copies).Where(c => c.Element.Type.Is(tuple.Type.ToTypeEntity())).UnsafeUpdateMList()
                            .Set(mle => mle.Element.CopyTable, mle => ms)
                            .Execute();
                        }

                        using (token.MeasureTime(l => export.InDB().UnsafeUpdate().Set(s =>s.EnableForeignKeys, s=>l).Execute()))
                            foreach (var tuple in downloadTables.Where(t => !t.Type.IsEnumEntity()))
                            {
                                token.ThrowIfCancellationRequested();

                                EnableForeignKeys(tuple.Table);
                            }

                        using (token.MeasureTime(l => export.InDB().UnsafeUpdate().Set(s => s.ReseedIds, s => l).Execute()))
                        {
                            var tablesToUpload = Schema.Current.Tables.Values.Where(t => DisconnectedLogic.GetStrategy(t.Type).Upload != Upload.None)
                                .SelectMany(t => t.TablesMList().Cast<ITable>().And(t)).Where(t => t.PrimaryKey.Identity).ToList();

                            var maxIdDictionary = tablesToUpload.ToDictionary(t => t, 
                                t => DisconnectedTools.MaxIdInRange(t, machine.SeedMin, machine.SeedMax));

                            using (Connector.Override(newDatabase))
                            using (ObjectName.OverrideOptions(new ObjectNameOptions { AvoidDatabaseName = true }))
                            {
                                foreach (var table in tablesToUpload)
                                {
                                    token.ThrowIfCancellationRequested();

                                    long? max = maxIdDictionary.GetOrThrow(table);

                                    DisconnectedTools.SetNextId(table, (max + 1) ?? machine.SeedMin);
                                }
                            }
                        }

                        CopyExport(export, newDatabase);

                        machine.InDB().UnsafeUpdate().Set(s => s.State, s => DisconnectedMachineState.Disconnected).Execute(); 
                        using (SqlConnector.Override(newDatabase))
                        using (ObjectName.OverrideOptions(new ObjectNameOptions { AvoidDatabaseName = true }))
                            machine.InDB().UnsafeUpdate().Set(s => s.State, s => DisconnectedMachineState.Disconnected).Execute();

                        using (token.MeasureTime(l => export.InDB().UnsafeUpdate().Set(s => s.BackupDatabase, s => l).Execute()))
                            BackupDatabase(machine, export, newDatabase);

                        using (token.MeasureTime(l => export.InDB().UnsafeUpdate().Set(s => s.DropDatabase, s => l).Execute()))
                            DropDatabase(newDatabase);

                        token.ThrowIfCancellationRequested();

                        export.InDB().UnsafeUpdate()
                        .Set(s=>s.State, s=>DisconnectedExportState.Completed)
                        .Set(s=>s.Total, s=>s.CalculateTotal())
                        .Execute();
                    }
                    catch (Exception e)
                    {
                        var ex = e.LogException();

                        export.InDB().UnsafeUpdate()
                        .Set(s => s.Exception, s => ex.ToLite())
                        .Set(s => s.State, s => DisconnectedExportState.Error)
                        .Execute();

                        OnExportingError(machine, export, e);
                    }
                    finally
                    {
                        runningExports.Remove(export);
                        DisconnectedMachineEntity.Current = null;

                        OnEndExporting();
                    }
                }
            });


            runningExports.Add(export, new RunningExports { Task = task, CancelationSource = cancelationSource });

            return export;
        }

        private void CopyExport(Lite<DisconnectedExportEntity> export, SqlConnector newDatabase)
        {
            var clone = export.Retrieve().Clone();

            using (Connector.Override(newDatabase))
            using (ObjectName.OverrideOptions(new ObjectNameOptions { AvoidDatabaseName = true }))
            {
                clone.Save();
            }
        }


        protected virtual void OnStartExporting(DisconnectedMachineEntity machine)
        {

        }

        protected virtual void OnEndExporting()
        {

        }

        protected virtual void OnExportingError(DisconnectedMachineEntity machine, Lite<DisconnectedExportEntity> export, Exception exception)
        {
        }

        readonly MethodInfo miUnsafeLock;
        protected virtual int UnsafeLock<T>(Lite<DisconnectedMachineEntity> machine, DisconnectedStrategy<T> strategy, Lite<DisconnectedExportEntity> stats) where T : Entity, new()
        {
            using (ExecutionMode.Global())
            {
                var result = Database.Query<T>().Where(strategy.UploadSubset).Where(a => a.Mixin<DisconnectedSubsetMixin>().DisconnectedMachine != null).Select(a =>
                    "{0} locked in {1}".FormatWith(a.Id, a.Mixin<DisconnectedSubsetMixin>().DisconnectedMachine.Entity.MachineName)).ToString("\r\n");

                if (result.HasText())
                    stats.MListElementsLite(_ => _.Copies).Where(a => a.Element.Type.Is(typeof(T).ToTypeEntity())).UnsafeUpdateMList()
                        .Set(mle => mle.Element.Errors, mle => result)
                        .Execute();

                return Database.Query<T>().Where(strategy.UploadSubset).UnsafeUpdate()
                    .Set(a => a.Mixin<DisconnectedSubsetMixin>().DisconnectedMachine, a => machine)
                    .Set(a => a.Mixin<DisconnectedSubsetMixin>().LastOnlineTicks, a => a.Ticks)
                    .Execute();
            }
        }

        public virtual void AbortExport(Lite<DisconnectedExportEntity> stat)
        {
            runningExports.GetOrThrow(stat).CancelationSource.Cancel();
        }

        protected virtual void DropDatabase(Connector newDatabase)
        {
            DisconnectedTools.DropDatabase(new DatabaseName(null, newDatabase.DatabaseName()));
        }

        protected virtual string DatabaseFileName(DisconnectedMachineEntity machine)
        {
            return Path.Combine(DisconnectedLogic.DatabaseFolder, Connector.Current.DatabaseName() + "_Export_" + 
                DisconnectedTools.CleanMachineName(machine.MachineName) + ".mdf");
        }

        protected virtual string DatabaseLogFileName(DisconnectedMachineEntity machine)
        {
            return Path.Combine(DisconnectedLogic.DatabaseFolder, Connector.Current.DatabaseName() + "_Export_" + DisconnectedTools.CleanMachineName(machine.MachineName) + "_Log.ldf");
        }

        protected virtual DatabaseName DatabaseName(DisconnectedMachineEntity machine)
        {
            return new DatabaseName(null, Connector.Current.DatabaseName() + "_Export_" + DisconnectedTools.CleanMachineName(machine.MachineName));
        }

        protected virtual string CreateDatabase(DisconnectedMachineEntity machine)
        {
            DatabaseName databaseName = DatabaseName(machine);

            DisconnectedTools.DropIfExists(databaseName);

            string fileName = DatabaseFileName(machine);
            string logFileName = DatabaseLogFileName(machine);

            DisconnectedTools.CreateDatabase(databaseName, fileName, logFileName);

            return ((SqlConnector)Connector.Current).ConnectionString.Replace(Connector.Current.DatabaseName(), databaseName.Name);
        }

        protected virtual void EnableForeignKeys(Table table)
        {
            DisconnectedTools.EnableForeignKeys(table);

            foreach (var rt in table.TablesMList())
                DisconnectedTools.EnableForeignKeys(rt);
        }

        protected virtual void DisableForeignKeys(Table table)
        {
            DisconnectedTools.DisableForeignKeys(table);

            foreach (var rt in table.TablesMList())
                DisconnectedTools.DisableForeignKeys(rt);
        }

        protected virtual void BackupDatabase(DisconnectedMachineEntity machine, Lite<DisconnectedExportEntity> export, Connector newDatabase)
        {
            string backupFileName = Path.Combine(DisconnectedLogic.BackupFolder, BackupFileName(machine, export));
            FileTools.CreateParentDirectory(backupFileName);
            DisconnectedTools.BackupDatabase(new DatabaseName(null, newDatabase.DatabaseName()), backupFileName);
        }

        public virtual string BackupNetworkFileName(DisconnectedMachineEntity machine, Lite<DisconnectedExportEntity> export)
        {
            return Path.Combine(DisconnectedLogic.BackupNetworkFolder, BackupFileName(machine, export));
        }

        protected virtual string BackupFileName(DisconnectedMachineEntity machine, Lite<DisconnectedExportEntity> export)
        {
            return "{0}.{1}.Export.{2}.bak".FormatWith(Connector.Current.DatabaseName(), machine.MachineName.ToString(), export.Id);
        }

    }


    public interface ICustomExporter
    {
        void Export(Table table, IDisconnectedStrategy strategy, DatabaseName newDatabaseName, DisconnectedMachineEntity machine);
    }

    public class BasicExporter<T> : ICustomExporter where T : Entity
    {
        public virtual void Export(Table table, IDisconnectedStrategy strategy, DatabaseName newDatabaseName, DisconnectedMachineEntity machine)
        {
            this.CopyTable(table, strategy, newDatabaseName);
        }

        protected virtual void CopyTable(Table table, IDisconnectedStrategy strategy, DatabaseName newDatabaseName)
        {
            var filter = strategy.Download == Download.Subset ? GetWhere((DisconnectedStrategy<T>)strategy) : null;

            CopyTableBasic(table, newDatabaseName, filter);

            foreach (var rt in table.TablesMList())
                CopyTableBasic(rt, newDatabaseName, filter == null ? null : (SqlPreCommandSimple)filter.Clone());
        }

        protected virtual int CopyTableBasic(ITable table, DatabaseName newDatabaseName, SqlPreCommandSimple filter)
        {
            ObjectName newTableName = table.Name.OnDatabase(newDatabaseName);

            string command =
@"INSERT INTO {0} ({2})
SELECT {3}
                    from {1} as [table]".FormatWith(
                    newTableName,
                    table.Name,
                    table.Columns.Keys.ToString(a => a.SqlEscape(), ", "),
                    table.Columns.Keys.ToString(a => "[table]." + a.SqlEscape(), ", "));

            if (filter != null)
            {
                if (table is Table)
                {
                    command += "\r\nWHERE [table].Id in ({0})".FormatWith(filter.Sql);
                }
                else
                {
                    TableMList rt = (TableMList)table;
                    command +=
                        "\r\nJOIN {0} [masterTable] on [table].{1} = [masterTable].Id".FormatWith(rt.BackReference.ReferenceTable.Name, rt.BackReference.Name.SqlEscape()) +
                        "\r\nWHERE [masterTable].Id in ({0})".FormatWith(filter.Sql);
                }
            }

            string fullCommand = !table.PrimaryKey.Identity ? command :
                ("SET IDENTITY_INSERT {0} ON\r\n".FormatWith(newTableName) +
                command + "\r\n" +
                "SET IDENTITY_INSERT {0} OFF\r\n".FormatWith(newTableName));

            return Executor.ExecuteNonQuery(fullCommand, filter?.Parameters);
        }

        protected virtual SqlPreCommandSimple GetWhere(DisconnectedStrategy<T> pair)
        {
            var query = Database.Query<T>().Where(pair.DownloadSubset).Select(a => a.Id);

            return Administrator.QueryPreCommand(query);
        }
    }

    public class DeleteAndCopyExporter<T> : BasicExporter<T> where T : Entity
    {
        public override void Export(Table table, IDisconnectedStrategy strategy, DatabaseName newDatabaseName, DisconnectedMachineEntity machine)
        {
            this.DeleteTable(table, newDatabaseName);

            this.CopyTable(table, strategy, newDatabaseName);
        }

        private void DeleteTable(Table table, DatabaseName newDatabaseName)
        {
            DisconnectedTools.DeleteTable(table.Name.OnDatabase(newDatabaseName));
        }
    }
}
