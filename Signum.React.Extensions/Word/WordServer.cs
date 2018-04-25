﻿using Signum.Entities.UserAssets;
using Signum.React.Json;
using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Basics;
using Signum.React.UserAssets;
using Signum.Entities;
using Signum.React.ApiControllers;
using Signum.Entities.DynamicQuery;
using Signum.React.Maps;
using Signum.React.Facades;
using Signum.Engine.Cache;
using Signum.Entities.Cache;
using Signum.Engine.Authorization;
using Signum.Engine.Maps;
using Signum.Entities.Templating;
using Signum.Entities.Word;
using Signum.Engine.Word;
using Signum.React.TypeHelp;

namespace Signum.React.Word
{
    public static class WordServer
    {
        public static void Start(HttpConfiguration config)
        {
            TypeHelpServer.Start(config);
            SignumControllerFactory.RegisterArea(MethodInfo.GetCurrentMethod());

            ReflectionServer.RegisterLike(typeof(TemplateTokenMessage));

            CustomizeFiltersModel();

            EntityPackTS.AddExtension += ep =>
            {
                if (ep.entity.IsNew || !WordTemplatePermission.GenerateReport.IsAuthorized())
                    return;

                var wordTemplates = WordTemplateLogic.TemplatesByEntityType.Value.TryGetC(ep.entity.GetType());
                if (wordTemplates != null)
                {
                    var applicable = wordTemplates.Where(a => a.IsApplicable(ep.entity));
                    if (applicable.HasItems())
                        ep.Extension.Add("wordTemplates", applicable.Select(a => a.ToLite()).ToList());
                }
            };

            QueryDescriptionTS.AddExtension += qd =>
            {
                object type = QueryLogic.ToQueryName(qd.queryKey);
                if (Schema.Current.IsAllowed(typeof(WordTemplateEntity), true) == null)
                {
                    var templates = WordTemplateLogic.GetApplicableWordTemplates(type, null, WordTemplateVisibleOn.Query);

                    if (templates.HasItems())
                        qd.Extension.Add("wordTemplates", templates);
                }
            };
        }

        private static void CustomizeFiltersModel()
        {
            var converters = PropertyConverter.GetPropertyConverters(typeof(QueryModel));
            converters.Remove("queryName");

            converters.Add("queryKey", new PropertyConverter()
            {
                AvoidValidate = true,
                CustomReadJsonProperty = ctx =>
                {
                    ((QueryModel)ctx.Entity).QueryName = QueryLogic.ToQueryName((string)ctx.JsonReader.Value);
                },
                CustomWriteJsonProperty = ctx =>
                {
                    var cr = (QueryModel)ctx.Entity;

                    ctx.JsonWriter.WritePropertyName(ctx.LowerCaseName);
                    ctx.JsonWriter.WriteValue(QueryLogic.GetQueryEntity(cr.QueryName).Key);
                }
            });

            converters.Add("filters", new PropertyConverter()
            {
                AvoidValidate = true,
                CustomReadJsonProperty = ctx =>
                {
                    var list = (List<FilterTS>)ctx.JsonSerializer.Deserialize(ctx.JsonReader, typeof(List<FilterTS>));

                    var cr = (QueryModel)ctx.Entity;

                    var qd = DynamicQueryManager.Current.QueryDescription(cr.QueryName);

                    cr.Filters = list.Select(l => l.ToFilter(qd, canAggregate: true)).ToList();
                },
                CustomWriteJsonProperty = ctx =>
                {
                    var cr = (QueryModel)ctx.Entity;

                    ctx.JsonWriter.WritePropertyName(ctx.LowerCaseName);
                    ctx.JsonSerializer.Serialize(ctx.JsonWriter, cr.Filters.Select(f => new FilterTS
                    {
                        token = f.Token.FullKey(),
                        operation = f.Operation,
                        value = f.Value
                    }).ToList());
                }
            });

            converters.Add("orders", new PropertyConverter()
            {
                AvoidValidate = true,
                CustomReadJsonProperty = ctx =>
                {
                    var list = (List<OrderTS>)ctx.JsonSerializer.Deserialize(ctx.JsonReader, typeof(List<OrderTS>));

                    var cr = (QueryModel)ctx.Entity;

                    var qd = DynamicQueryManager.Current.QueryDescription(cr.QueryName);

                    cr.Orders = list.Select(l => l.ToOrder(qd, canAggregate: true)).ToList();
                },
                CustomWriteJsonProperty = ctx =>
                {
                    var cr = (QueryModel)ctx.Entity;

                    ctx.JsonWriter.WritePropertyName(ctx.LowerCaseName);
                    ctx.JsonSerializer.Serialize(ctx.JsonWriter, cr.Orders.Select(f => new OrderTS
                    {
                        token = f.Token.FullKey(),
                        orderType = f.OrderType
                    }));
                }
            });

            converters.Add("pagination", new PropertyConverter()
            {
                AvoidValidate = true,
                CustomReadJsonProperty = ctx =>
                {
                    var pagination = (PaginationTS)ctx.JsonSerializer.Deserialize(ctx.JsonReader, typeof(PaginationTS));
                    var cr = (QueryModel)ctx.Entity;
                    cr.Pagination = pagination.ToPagination();
                },
                CustomWriteJsonProperty = ctx =>
                {
                    var cr = (QueryModel)ctx.Entity;

                    ctx.JsonWriter.WritePropertyName(ctx.LowerCaseName);
                    ctx.JsonSerializer.Serialize(ctx.JsonWriter, new PaginationTS(cr.Pagination));
                }
            });
        }
    }
}