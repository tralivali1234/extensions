﻿using Signum.Engine.Files;
using Signum.Engine;
using Signum.Engine.Authorization;
using Signum.Engine.Basics;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Maps;
using Signum.Engine.Operations;
using Signum.Engine.Processes;
using Signum.Entities.Files;
using Signum.Entities.Processes;
using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Signum.Entities.Printing;
using Signum.Entities;
using System.IO;
using Signum.Engine.Scheduler;
using Signum.Entities.Basics;

namespace Signum.Engine.Printing
{
    public static class PrintingLogic
    {
        public static int DeleteFilesAfter = 24 * 60; //Minutes

        public static Action<PrintLineEntity> Print;
         
        static Expression<Func<PrintPackageEntity, IQueryable<PrintLineEntity>>> LinesExpression =
            e => Database.Query<PrintLineEntity>().Where(a => a.Package.RefersTo(e));
        
        [ExpressionField]
        public static IQueryable<PrintLineEntity> Lines(this PrintPackageEntity e)
        {
            return LinesExpression.Evaluate(e);
        }

        public static FileTypeSymbol TestFileType; 

        public static void Start(SchemaBuilder sb, DynamicQueryManager dqm, FileTypeSymbol testFileType = null)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                TestFileType = testFileType;

                sb.Include<PrintLineEntity>()
                    .WithQuery(dqm, () => p => new
                    {
                        Entity = p,
                        p.CreationDate,
                        p.File,
                        p.State,
                        p.Package,
                        p.PrintedOn,
                        p.Referred,
                    });
                
                sb.Include<PrintPackageEntity>()
                    .WithQuery(dqm, () => e => new
                    {
                        Entity = e,
                        e.Id,
                        e.Name
                    });

                ProcessLogic.AssertStarted(sb);
                ProcessLogic.Register(PrintPackageProcess.PrintPackage, new PrintPackageAlgorithm());
                PermissionAuthLogic.RegisterPermissions(PrintPermission.ViewPrintPanel);
                PrintLineGraph.Register();

                SimpleTaskLogic.Register(PrintTask.RemoveOldFiles, (ScheduledTaskContext ctx) =>
                {
                    var lines = Database.Query<PrintLineEntity>().Where(a => a.State == PrintLineState.Printed).Where(b => b.CreationDate <= DateTime.Now.AddMinutes(-DeleteFilesAfter));
                    foreach (var line in lines)
                    {
                        try
                        {
                            using (Transaction tr = new Transaction())
                            {
                                line.File.DeleteFileOnCommit();
                                line.State = PrintLineState.PrintedAndDeleted;
                                using (OperationLogic.AllowSave<PackageLineEntity>())
                                    line.Save();

                                tr.Commit();
                            }
                        }
                        catch (Exception e)
                        {
                            e.LogException();
                        }
                    }
                    return null;
                });
            }
        }

        public class PrintPackageAlgorithm : IProcessAlgorithm
        {
            public void Execute(ExecutingProcess executingProcess)
            {
                PrintPackageEntity package = (PrintPackageEntity)executingProcess.Data;

                executingProcess.ForEachLine(package.Lines().Where(a => a.State != PrintLineState.Printed), line =>
                {
                    PrintLineGraph.Print(line);
                });
            }
        }

        public static PrintLineEntity CreateLine(Entity referred, FileTypeSymbol fileType, string fileName, byte[] content)
        {
            return CreateLine(referred, new FilePathEmbedded(fileType, fileName, content));
        }

        public static PrintLineEntity CreateLine(Entity referred, FilePathEmbedded file)
        {
            return new PrintLineEntity
            {
                Referred = referred.ToLite(),
                State = PrintLineState.ReadyToPrint,
                File = file,
            }.Save();
        }

        public static ProcessEntity CreateProcess(FileTypeSymbol fileType = null)
        {
            using (Transaction tr = new Transaction())
            {
                var query = Database.Query<PrintLineEntity>()
                        .Where(a => a.State == PrintLineState.ReadyToPrint);

                if (fileType != null)
                    query = query.Where(a => a.File.FileType == fileType);

                if (query.Count() == 0)
                    return null;

                var package = new PrintPackageEntity()
                {
                    Name = fileType?.ToString() + " (" + query.Count() + ")"
                }.Save();

                query.UnsafeUpdate()
                    .Set(a => a.Package, a => package.ToLite())
                    .Set(a => a.State, a => PrintLineState.Enqueued)
                    .Execute();

                var result =  ProcessLogic.Create(PrintPackageProcess.PrintPackage, package).Save();

                return tr.Commit(result);
            }

        }

        public static List<PrintStat> GetReadyToPrintStats()
        {
            return Database.Query<PrintLineEntity>()
                .Where(a => a.State == PrintLineState.ReadyToPrint)
                .GroupBy(a => a.File.FileType)
                .Select(gr => new PrintStat
                {
                    fileType = gr.Key,
                    count = gr.Count()
                }).ToList();            
        }

       
        public static void CancelPrinting(Entity entity, FileTypeSymbol fileType)
        {
            var list = ReadyToPrint(entity, fileType).ToList();
            list.ForEach(a =>
            {
                a.State = PrintLineState.Cancelled;
                a.File.DeleteFileOnCommit();
            });
            list.SaveList();
        }

        public static FileContent SavePrintLine(this FileContent file, Entity entity, FileTypeSymbol fileTypeForPrinting)
        {
            CancelPrinting(entity, fileTypeForPrinting);
            CreateLine(entity, fileTypeForPrinting, Path.GetFileName(file.FileName), file.Bytes);

            return file;
        }

        public static IQueryable<PrintLineEntity> ReadyToPrint(Entity entity, FileTypeSymbol fileType)
        {
            return Database.Query<PrintLineEntity>().Where(a => a.Referred.RefersTo(entity) && a.File.FileType == fileType && a.State == PrintLineState.ReadyToPrint);
        }
    }
    public class PrintStat
    {
        public FileTypeSymbol fileType;
        public int count;
    }

    public class PrintLineGraph : Graph<PrintLineEntity, PrintLineState>
    {
        public static void Register()
        {
            GetState = e => e.State;

            new Construct(PrintLineOperation.CreateTest)
            {
                ToStates = { PrintLineState.NewTest },
                Construct = (args) => new PrintLineEntity
                {
                    State = PrintLineState.NewTest,
                    TestFileType = PrintingLogic.TestFileType,
                }
            }.Register();

            new Execute(PrintLineOperation.SaveTest)
            {
                AllowsNew = true,
                Lite = false,
                FromStates = { PrintLineState.NewTest },
                ToStates = { PrintLineState.ReadyToPrint },
                Execute = (e, _) => { e.State = PrintLineState.ReadyToPrint; }
            }.Register();

            new Execute(PrintLineOperation.Print)
            {
                FromStates = {PrintLineState.ReadyToPrint},
                ToStates = {PrintLineState.Printed, PrintLineState.Error},
                Execute = (e, _) =>
                {
                    Print(e);
                }
            }.Register();

            new Execute(PrintLineOperation.Retry)
            {
                FromStates = {PrintLineState.Error, PrintLineState.Cancelled},
                ToStates = {PrintLineState.ReadyToPrint },
                Execute = (e, _) =>
                {
                    e.State = PrintLineState.ReadyToPrint;
                    e.Package = null;
                }
            }.Register();

            new Execute(PrintLineOperation.Cancel)
            {
                FromStates = { PrintLineState.ReadyToPrint, PrintLineState.Error },
                ToStates = { PrintLineState.Cancelled },
                Execute = (e, _) =>
                {
                    e.State = PrintLineState.Cancelled;
                    e.Package = null;
                    e.PrintedOn = null;
                    e.File.DeleteFileOnCommit();
                }
            }.Register();
        }

        public static void Print(PrintLineEntity line)
        {
            using (OperationLogic.AllowSave<PrintLineEntity>())
            {
                try
                {
                    PrintingLogic.Print(line);
                    
                    line.State = PrintLineState.Printed;
                    line.PrintedOn = TimeZoneManager.Now;
                    line.Save();
                }
                catch (Exception ex)
                {
                    if (Transaction.InTestTransaction) //Transaction.IsTestTransaction
                        throw;

                    var exLog = ex.LogException().ToLite();

                    try
                    {
                        using (Transaction tr = Transaction.ForceNew())
                        {
                            line.State = PrintLineState.Error;
                            line.Save();
                            tr.Commit();
                        }
                    }
                    catch { } 

                    throw;
                }
            }
        }
    }
}

