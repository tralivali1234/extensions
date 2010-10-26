﻿#region usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Signum.Test;
using System.Web.Hosting;
using Signum.Web.Extensions.Sample.Properties;
using Signum.Engine.Maps;
using Signum.Web.Operations;
using Signum.Entities.Authorization;
using System.Threading;
using Signum.Engine;
using Signum.Engine.Authorization;
using Signum.Entities;
using Signum.Web.Queries;
using Signum.Web.Reports;
using Signum.Web.Authorization;
using Signum.Web.ControlPanel;
using Signum.Entities.ControlPanel;
#endregion

namespace Signum.Web.Extensions.Sample
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
               "v",
               "View/{typeUrlName}/{id}",
               new { controller = "Signum", action = "View", typeFriendlyName = "", id = "" }
           );

            routes.MapRoute(
                "f",
                "Find/{sfQueryUrlName}",
                new { controller = "Signum", action = "Find", sfQueryUrlName = "" }
            );

            routes.MapRoute(
                "Default",                                              // Route name
                "{controller}/{action}/{id}",                           // URL with parameters
                new { controller = "Home", action = "Index", id = "" }  // Parameter defaults
            );
        }

        protected void Application_Start()
        {
            RegisterRoutes(RouteTable.Routes);
            //RouteDebug.RouteDebugger.RewriteRoutesForTesting(RouteTable.Routes);

            Signum.Test.Extensions.Starter.Start(UserConnections.Replace(Settings.Default.ConnectionString));

            ModuleResources.ResourceToUrl = (file, area) =>
            {
                if (area) return CombinerHtmlHelper.IncludeAreaJsUrl(new[] { file });
                else return CombinerHtmlHelper.CombinedJsUrl(new[] { file });
            };
            ModuleResources.RegisterBasics();

            using (AuthLogic.Disable())
            {
                Schema.Current.Initialize();
                LinkTypesAndViews();
            }

            //AuthenticationRequiredAttribute.Authenticate = null;
        }

        private void LinkTypesAndViews()
        {
            Navigator.Start(new NavigationManager());
            Constructor.Start(new ConstructorManager());
            OperationClient.Start(new OperationManager(), true);

            AuthClient.Start(true, true, true, true);
            AuthAdminClient.Start(true, true, true, true, true, true, true);

            ContextualItemsHelper.Start();
            UserQueriesClient.Start();
            ControlPanelClient.Start();

            ReportClient.Start(true, true);

            MusicClient.Start();

            Navigator.Initialize();
        }

        protected void Application_AcquireRequestState(object sender, EventArgs e)
        {
            UserDN user = HttpContext.Current.Session == null ? null : (UserDN)HttpContext.Current.Session[AuthController.SessionUserKey];

            if (user != null)
            {
                Thread.CurrentPrincipal = user;
            }
            else
            {
                using (AuthLogic.Disable())
                {
                    //Thread.CurrentPrincipal = Database.Query<UserDN>().Where(u => u.UserName == "external").Single();
                }
            }
        }

        protected void Session_Start(object sender, EventArgs e)
        {
            AuthController.LoginFromCookie();
        }
    }
}
