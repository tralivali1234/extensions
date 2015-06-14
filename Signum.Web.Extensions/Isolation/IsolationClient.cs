using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Signum.Entities.Basics;
using Signum.Utilities;
using System.Reflection;
using Signum.Entities;
using Signum.Web.Operations;
using Signum.Entities.Isolation;
using Signum.Engine.Isolation;
using System.Web.Mvc;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Mvc;
using Signum.Web.Maps;

namespace Signum.Web.Isolation
{
    public static class IsolationClient
    {
        public static string ViewPrefix = "~/Isolation/Views/{0}.cshtml";
        public static JsModule Module = new JsModule("Extensions/Signum.Web.Extensions/Isolation/Scripts/Isolation");
        public static JsModule ColorsModule = new JsModule("Extensions/Signum.Web.Extensions/Isolation/Scripts/IsolationColors");

        public static void Start()
        {
            if (Navigator.Manager.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                Navigator.RegisterArea(typeof(IsolationClient));

                WidgetsHelper.GetWidget += ctx => ctx.Entity is Entity && MixinDeclarations.IsDeclared(ctx.Entity.GetType(), typeof(IsolationMixin)) ?
                    IsolationWidgetHelper.CreateWidget(ctx) : null;

                Navigator.AddSetting(new EntitySettings<IsolationEntity> { PartialViewName = _ => ViewPrefix.FormatWith("Isolation") });
                 
                Constructor.ClientManager.GlobalPreConstructors += ctx =>
                    (!MixinDeclarations.IsDeclared(ctx.Type, typeof(IsolationMixin)) || IsolationEntity.Current != null) ? null :
                    Module["getIsolation"](ClientConstructorManager.ExtraJsonParams, ctx.Prefix,
                    IsolationMessage.SelectAnIsolation.NiceToString(),
                    GetIsolationChooserOptions(ctx.Type));

                //Unnecessary with the filter
                Constructor.Manager.PreConstructors += ctx =>
                    !MixinDeclarations.IsDeclared(ctx.Type, typeof(IsolationMixin)) ? null :
                    IsolationEntity.Override(GetIsolation(ctx.ActionContext)); 

                MapClient.GetColorProviders += GetMapColors;
            }
        }

        private static IEnumerable<ChooserOption> GetIsolationChooserOptions(Type type)
        {
            var isolations = IsolationLogic.Isolations.Value.Select(iso => iso.ToChooserOption());
            if (IsolationLogic.GetStrategy(type) != IsolationStrategy.Optional)
                return isolations;

            var list = isolations.ToList();
            list.Add(new ChooserOption("", "Null"));
            return list;
        }

        public static Lite<IsolationEntity> GetIsolation(HttpActionContext ctx)
        {
            var isolation = //ctx.ControllerContext..ControllerContext.HttpContext.Request["Isolation"] ??
                (string)ctx.ControllerContext.Request.Properties["Isolation"];

            if (isolation.HasText())
                return Lite.Parse<IsolationEntity>(isolation);

            return null;
        }

        static MapColorProvider[] GetMapColors()
        {
            var strategies = IsolationLogic.GetIsolationStrategies().SelectDictionary(t => Navigator.ResolveWebTypeName(t), p => p);

            return new[]
            {
                new MapColorProvider
                { 
                    Name = "isolation", 
                    NiceName = "Isolation", 
                    GetJsProvider = ColorsModule["isolationColors"](MapClient.NodesConstant),
                    AddExtra = t => 
                    {
                        var s = strategies.TryGetS(t.webTypeName);

                        if (s == null)
                            return;
                        
                        t.extra["isolation"] = s.ToString();
                    },
                    Order = 3,
                },
            };
        }
    }

    public class IsolationFilterAttribute : ActionFilterAttribute
    {
        static string Key = "isolationDisposer";

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var iso = IsolationClient.GetIsolation(actionContext); 

            IDisposable isolation = IsolationEntity.Override(iso);
            if (isolation != null)
                actionContext.ActionArguments.Add(Key, isolation);

        }

        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            IDisposable elapsed = (IDisposable)actionExecutedContext.ActionContext.ActionArguments.TryGetC(Key);
            if (elapsed != null)
            {
                elapsed.Dispose();
                actionExecutedContext.ActionContext.ActionArguments.Remove(Key);
            }
        }
    }
}