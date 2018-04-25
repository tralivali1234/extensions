﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using Signum.Web;
using Signum.Entities.Chart;
using Signum.Utilities;
using Signum.Engine.DynamicQuery;
using Signum.Entities.DynamicQuery;
using System.Web.Script.Serialization;
using Signum.Entities;
using Signum.Engine;
using System.Web.Routing;
using System.Web.Mvc;
using Signum.Entities.Basics;
using Signum.Engine.Basics;
using Signum.Entities.UserQueries;
using Signum.Engine.Chart;
using Signum.Engine.Authorization;
using Signum.Entities.Authorization;
using Signum.Entities.Reflection;
using System.Diagnostics;
using System.Text;
using Signum.Entities.Files;
using Signum.Web.UserQueries;
using Newtonsoft.Json.Linq;
using Signum.Web.UserAssets;
using Signum.Entities.UserAssets;

namespace Signum.Web.Chart
{
    public static class ChartClient
    {
        public static string ViewPrefix = "~/Chart/Views/{0}.cshtml";

        public static JsModule Module = new JsModule("Extensions/Signum.Web.Extensions/Chart/Scripts/Chart");
        public static JsModule ModuleScript = new JsModule("Extensions/Signum.Web.Extensions/Chart/Scripts/ChartScript");

        public static string ChartRequestView = ViewPrefix.FormatWith("ChartRequestView");
        public static string ChartBuilderView = ViewPrefix.FormatWith("ChartBuilder");
        public static string ChartResultsView = ViewPrefix.FormatWith("ChartResults");
        public static string ChartResultsTableView = ViewPrefix.FormatWith("ChartResultsTable");
        public static string ChartScriptCodeView = ViewPrefix.FormatWith("ChartScriptCode");

        public static void Start()
        {
            if (!Navigator.Manager.EntitySettings.ContainsKey(typeof(FileEntity)))
                throw new InvalidOperationException("Call FileClient.Start first with FileEntity"); 

            if (Navigator.Manager.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                Navigator.RegisterArea(typeof(ChartClient));

                Navigator.AddSettings(new List<EntitySettings>
                {
                    new ModelEntitySettings<ChartRequest>(),
                    new EmbeddedEntitySettings<ChartParameterEmbedded>(),
                    new EmbeddedEntitySettings<ChartColumnEmbedded> { PartialViewName = _ => ViewPrefix.FormatWith("ChartColumn") },
                    new EmbeddedEntitySettings<ChartScriptColumnEmbedded>{ PartialViewName = _ => ViewPrefix.FormatWith("ChartScriptColumn") },
                    new EmbeddedEntitySettings<ChartScriptParameterEmbedded>{ PartialViewName = _ => ViewPrefix.FormatWith("ChartScriptParameter") },
                    new EntitySettings<ChartScriptEntity> { PartialViewName = _ => ViewPrefix.FormatWith("ChartScript") },
                });

                ButtonBarQueryHelper.RegisterGlobalButtons(ButtonBarQueryHelper_GetButtonBarForQueryName);

                RouteTable.Routes.MapRoute(null, "ChartFor/{webQueryName}",
                    new { controller = "Chart", action = "Index", webQueryName = "" });

                UserChartClient.Start();
                ChartColorClient.Start();
            }
        }

        public static EntityMapping<ChartColumnEmbedded> MappingChartColumn = new EntityMapping<ChartColumnEmbedded>(true)
            .SetProperty(ct => ct.Token, ctx =>
            {
                var tokenName = UserAssetsHelper.GetTokenString(ctx);

                if (string.IsNullOrEmpty(tokenName))
                    return null;

                var qd = DynamicQueryManager.Current.QueryDescription(
                    Finder.ResolveQueryName(ctx.Controller.ControllerContext.HttpContext.Request.Params["webQueryName"]));

                var chartToken = (ChartColumnEmbedded)ctx.Parent.UntypedValue;

                var token = QueryUtils.Parse(tokenName, qd, SubTokensOptions.CanElement | SubTokensOptions.CanAggregate /* chartToken.ParentChart.GroupResults*/);

                if (token is AggregateToken && !chartToken.ParentChart.GroupResults)
                    token = token.Parent;

                return new QueryTokenEmbedded(token);
            })
            .SetProperty(ct => ct.DisplayName, ctx =>
            {
                if (string.IsNullOrEmpty(ctx.Input))
                    return ctx.None();

                return ctx.Input;
            });

        public static EntityMapping<ChartRequest> MappingChartRequest = new EntityMapping<ChartRequest>(true)
            .RemoveProperty(cr => cr.QueryName)
            .SetProperty(cr => cr.Filters, ctx => ExtractChartFilters(ctx))
            .SetProperty(cr => cr.Orders, ctx => ExtractChartOrders(ctx))
            .SetProperty(cb => cb.Columns, new MListCorrelatedOrDefaultMapping<ChartColumnEmbedded>(MappingChartColumn))
            .SetProperty(cb => cb.Parameters, new MListDictionaryMapping<ChartParameterEmbedded, string>(p => p.Name) { OnlyIfPossible = true });

        public class MListCorrelatedOrDefaultMapping<S> : MListMapping<S>
        {
            public MListCorrelatedOrDefaultMapping()
                : base()
            {
            }

            public MListCorrelatedOrDefaultMapping(Mapping<S> elementMapping)
                : base(elementMapping)
            {
            }

            public override MList<S> GetValue(MappingContext<MList<S>> ctx)
            {
                MList<S> list = ctx.Value;
                int i = 0;

                foreach (MappingContext<S> itemCtx in GenerateItemContexts(ctx).OrderBy(mc => mc.Prefix.Substring(mc.Prefix.LastIndexOf("_") + 1).ToInt().Value))
                {
                    if (i < list.Count)
                    {
                        itemCtx.Value = list[i];
                        itemCtx.Value = ElementMapping(itemCtx);

                        ctx.AddChild(itemCtx);
                    }
                    i++;
                }

                return list;
            }
        }

        static List<Entities.DynamicQuery.Filter> ExtractChartFilters(MappingContext<List<Entities.DynamicQuery.Filter>> ctx)
        {
            var qd = DynamicQueryManager.Current.QueryDescription(
                Finder.ResolveQueryName(ctx.Controller.ControllerContext.HttpContext.Request.Params["webQueryName"]));

            ChartRequest chartRequest = (ChartRequest)ctx.Parent.UntypedValue;

            return FindOptionsModelBinder.ExtractFilterOptions(ctx.Controller.ControllerContext.HttpContext, qd, canAggregate: chartRequest.GroupResults)
                .Select(fo => fo.ToFilter()).ToList();
        }

        static List<Order> ExtractChartOrders(MappingContext<List<Order>> ctx)
        {
            var qd = DynamicQueryManager.Current.QueryDescription(
                Finder.ResolveQueryName(ctx.Controller.ControllerContext.HttpContext.Request.Params["webQueryName"]));

            ChartRequest chartRequest = (ChartRequest)ctx.Parent.UntypedValue;

            return FindOptionsModelBinder.ExtractOrderOptions(ctx.Controller.ControllerContext.HttpContext, qd, canAggregate: true/*chartRequest.GroupResults*/)
                    .Select(fo => fo.ToOrder()).ToList();
        }

        static ToolBarButton[] ButtonBarQueryHelper_GetButtonBarForQueryName(QueryButtonContext ctx)
        {
            if (ctx.Prefix.HasText())
                return null;

            return new[] { ChartQueryButton(ctx) };
        }

        public static ToolBarButton ChartQueryButton(QueryButtonContext ctx)
        {
            if (!ChartPermission.ViewCharting.IsAuthorized())
                return null;

            string chartNewText = ChartMessage.Chart.NiceToString();

            return new ToolBarButton(ctx.Prefix, "qbChartNew")
            {
                Title = chartNewText,
                Text = chartNewText,
                OnClick = Module["openChart"](ctx.Prefix,  ctx.Url.Action("Index", "Chart"))
            };
        }

        public static string ChartTypeImgClass(IChartBase chartBase, ChartScriptEntity current, ChartScriptEntity script)
        {
            string css = "sf-chart-img";

            if (!chartBase.Columns.Any(a => a.Token != null && a.Token.ParseException != null) && script.IsCompatibleWith(chartBase))
                css += " sf-chart-img-equiv";

            if (script.Is(current))
                css += " sf-chart-img-curr";

            return css;
        }

        public static JObject ToChartRequest(this ChartRequest request, UrlHelper url, string prefix, ChartRequestMode mode)
        {
            return new JObject
            {
                {"prefix", prefix },
                { "webQueryName", Finder.ResolveWebQueryName(request.QueryName) },
                { "orders", new JArray(request.Orders.Select(o=>new JObject { {"orderType" ,(int)o.OrderType} , {"columnName" ,o.Token.FullKey()}}))},
                { "updateChartBuilderUrl", url.Action<ChartController>(cc => cc.UpdateChartBuilder(prefix)) },
                { "fullScreenUrl", url.Action<ChartController>(cc => cc.FullScreen(prefix))},
                { "addFilterUrl", url.Action("AddFilter", "Chart") },
                { "drawUrl", url.Action<ChartController>(cc => cc.Draw(prefix)) },
                { "openUrl", url.Action<ChartController>(cc => cc.OpenSubgroup(prefix)) },
                { "mode", (int)mode }
            };
        }

        public static JObject ToChartBuilder(this UserChartEntity userChart, UrlHelper url, string prefix)
        {
            return new JObject
            {
                {"prefix", prefix },
                { "webQueryName", Finder.ResolveWebQueryName(userChart.QueryName) },
                { "updateChartBuilderUrl", url.Action<ChartController>(cc => cc.UpdateChartBuilder(prefix)) }
            };
        }

        public static string ToJS(this QueryOrderEmbedded order)
        {
            return (order.OrderType == OrderType.Descending ? "-" : "") + order.Token.Token.FullKey();
        }

        public static void SetupParameter(ValueLine vl, ChartParameterEmbedded parameter)
        {
            var scriptParameter = parameter.ScriptParameter;

            vl.LabelColumns = new BsColumn(6);
            vl.LabelText = scriptParameter.Name;

            if (scriptParameter.Type == ChartParameterType.Number ||scriptParameter.Type == ChartParameterType.String)
            {
                vl.ValueLineType = ValueLineType.TextBox;
            }
            else if (scriptParameter.Type == ChartParameterType.Enum)
            {
                vl.ValueLineType = ValueLineType.Enum;

                var token = scriptParameter.GetToken(parameter.ParentChart);

                var compatible = scriptParameter.GetEnumValues().Where(a => a.CompatibleWith(token)).ToList();
                vl.ReadOnly = compatible.Count <= 1;
                vl.EnumComboItems = compatible.Select(ev => new SelectListItem
                {
                    Value = ev.Name,
                    Text = ev.Name,
                    Selected = ((string)vl.UntypedValue) == ev.Name
                }).ToList();

                if (!vl.ValueHtmlProps.IsNullOrEmpty())
                    vl.ValueHtmlProps.Clear();
            }


            vl.ValueHtmlProps["class"] = "sf-chart-redraw-onchange";
        }

        public static QueryTokenBuilderSettings GetQueryTokenBuilderSettings(QueryDescription qd, SubTokensOptions options)
        {
            return new QueryTokenBuilderSettings(qd, options)
            {
                Decorators = new Action<QueryToken, HtmlTag>(SearchControlHelper.CanFilterDecorator),
                ControllerUrl = RouteHelper.New().Action("NewSubTokensCombo", "Chart"),
            }; 
        }
    }

    public enum ChartRequestMode
    {
        complete,
        chart,
        data
    }

}
