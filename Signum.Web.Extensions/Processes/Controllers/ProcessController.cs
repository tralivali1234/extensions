﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Threading;
using Signum.Services;
using Signum.Utilities;
using Signum.Entities;
using Signum.Web;
using Signum.Engine;
using Signum.Engine.Operations;
using Signum.Engine.Basics;
using Signum.Entities.Processes;
using Signum.Engine.Processes;
using Signum.Engine.Authorization;
using Signum.Web.Operations;

namespace Signum.Web.Processes
{
    public class ProcessController : Controller
    {
        [HttpPost]
        public JsonNetResult GetProgressExecution(PrimaryKey id)
        {
            decimal progress = Database.Query<ProcessEntity>().Where(pe =>
                    pe.Id == id && pe.State == ProcessState.Executing).Select(pe => pe.Progress).SingleOrDefaultEx() ?? 1;

            return this.JsonNet(progress);
        }

        [HttpGet]
        public new ActionResult View()
        {
            ProcessLogicState state = ProcessRunnerLogic.ExecutionState();

            if (Request.IsAjaxRequest())
                return PartialView(ProcessClient.ViewPrefix.FormatWith("ProcessPanelTable"), state);
            else
                return View(ProcessClient.ViewPrefix.FormatWith("ProcessPanel"), state);
        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Start()
        {
            ProcessPermission.ViewProcessPanel.AssertAuthorized();

            ProcessRunnerLogic.StartRunningProcesses();

            Thread.Sleep(1000);

            return null;
        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Stop()
        {
            ProcessPermission.ViewProcessPanel.AssertAuthorized();

            ProcessRunnerLogic.Stop();

            Thread.Sleep(1000);

            return null;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ProcessFromMany()
        {
            var lites = this.ParseLiteKeys<Entity>();

            var operationSymbol = lites.Select(l => l.EntityType).Distinct().Select(type => this.GetOperationKeyAssert(type)).Distinct().SingleEx();

            ProcessEntity process = PackageLogic.CreatePackageOperation(lites, operationSymbol);

            return this.DefaultConstructResult(process);
        }
    }
}
