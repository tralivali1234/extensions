﻿using Signum.Engine;
using Signum.Engine.Basics;
using Signum.Engine.Maps;
using Signum.Engine.SchemaInfoTables;
using Signum.Entities;
using Signum.Entities.Basics;
using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Signum.React.Maps
{
    public static class SchemaMap
    {
        public static Func<MapColorProvider[]> GetColorProviders;

        internal static SchemaMapInfo GetMapInfo()
        {
            var getStats = GetRuntimeStats();

            var nodes = (from t in Schema.Current.Tables.Values
                         let type = EnumEntity.Extract(t.Type) ?? t.Type
                         select new TableInfo
                         {
                             typeName = TypeLogic.GetCleanName(t.Type),
                             niceName = type.NiceName(),
                             tableName = t.Name.ToString(),
                             columns = t.Columns.Count,
                             entityData = EntityKindCache.GetEntityData(t.Type),
                             entityKind = EntityKindCache.GetEntityKind(t.Type),
                             entityBaseType = GetEntityBaseType(t.Type),
                             @namespace = type.Namespace,
                             rows = getStats.TryGetC(t.Name)?.rows,
                             total_size_kb = getStats.TryGetC(t.Name)?.total_size_kb,
                             rows_history = t.SystemVersioned?.Let(sv => getStats.TryGetC(sv.TableName)?.rows),
                             total_size_kb_history = t.SystemVersioned?.Let(sv => getStats.TryGetC(sv.TableName)?.total_size_kb),
                             mlistTables = t.TablesMList().Select(ml => new MListTableInfo
                             {
                                 niceName = ml.PropertyRoute.PropertyInfo.NiceName(),
                                 tableName = ml.Name.ToString(),
                                 rows = getStats.TryGetC(ml.Name)?.rows,
                                 total_size_kb = getStats.TryGetC(ml.Name)?.total_size_kb,
                                 history_rows = ml.SystemVersioned?.Let(sv => getStats.TryGetC(sv.TableName)?.rows),
                                 history_total_size_kb = ml.SystemVersioned?.Let(sv => getStats.TryGetC(sv.TableName)?.total_size_kb),
                                 columns = ml.Columns.Count,
                             }).ToList()
                         }).ToList();


            var providers = GetColorProviders.GetInvocationListTyped().SelectMany(f => f()).OrderBy(a => a.Order).ToList();
            
            var extraActions = providers.Select(a => a.AddExtra).NotNull().ToList();

            if (extraActions.Any())
            {
                foreach (var n in nodes)
                {
                    foreach (var action in extraActions)
                        action(n);
                }
            }

            var normalEdges = (from t in Schema.Current.Tables.Values
                               from kvp in t.DependentTables()
                               where !kvp.Value.IsCollection
                               select new RelationInfo
                               {
                                   fromTable = t.Name.ToString(),
                                   toTable = kvp.Key.Name.ToString(),
                                   lite = kvp.Value.IsLite,
                                   nullable = kvp.Value.IsNullable
                               }).ToList();

            var mlistEdges = (from t in Schema.Current.Tables.Values
                              from tm in t.TablesMList()
                              from kvp in tm.GetTables()
                              select new RelationInfo
                              {
                                  fromTable = tm.Name.ToString(),
                                  toTable = kvp.Key.Name.ToString(),
                                  lite = kvp.Value.IsLite,
                                  nullable = kvp.Value.IsNullable
                              }).ToList();

            return new SchemaMapInfo
            {
                tables = nodes,
                relations = normalEdges.Concat(mlistEdges).ToList(),
                providers = providers.Select(p => new MapColorProviderInfo { name = p.Name, niceName = p.NiceName }).ToList()
            };
        }

        static EntityBaseType GetEntityBaseType(Type type)
        {
            if (type.IsEnumEntity())
                return EntityBaseType.EnumEntity;

            if (typeof(Symbol).IsAssignableFrom(type))
                return EntityBaseType.Symbol;

            if (typeof(SemiSymbol).IsAssignableFrom(type))
                return EntityBaseType.SemiSymbol;

            if (EntityKindCache.GetEntityKind(type) == EntityKind.Part)
                return EntityBaseType.Part;

            return EntityBaseType.Entity;
        }

        static Dictionary<ObjectName, RuntimeStats> GetRuntimeStats()
        {
            Dictionary<ObjectName, RuntimeStats> result = new Dictionary<ObjectName, RuntimeStats>();
            foreach (var dbName in Schema.Current.DatabaseNames())
            {
                using (Administrator.OverrideDatabaseInSysViews(dbName))
                {
                    var dic = Database.View<SysTables>().Select(t => KVP.Create(
                        new ObjectName(new SchemaName(dbName, t.Schema().name), t.name),
                        new RuntimeStats
                        {
                            rows = ((int?)t.Indices().SingleOrDefault(a => a.type == (int)DiffIndexType.Clustered).Partition().rows) ?? 0,
                            total_size_kb = t.Indices().SelectMany(i => i.Partition().AllocationUnits()).Sum(a => a.total_pages) * 8
                        })).ToDictionary();

                    result.AddRange(dic);
                }
            }
            return result;
        }

        public class RuntimeStats
        {
            public int rows;
            public int total_size_kb;
        }

      
    }

    public class MapColorProvider
    {
        public string Name;
        public string NiceName;
        public Action<TableInfo> AddExtra;
        public decimal Order { get; set; }
    }

    public class TableInfo
    {
        public string typeName;
        public string niceName;
        public string tableName;
        public EntityKind entityKind;
        public EntityData entityData;
        public EntityBaseType entityBaseType;
        public string @namespace;
        public int columns;
        public int? rows;
        public int? total_size_kb;
        public int? rows_history;
        public int? total_size_kb_history;
        public Dictionary<string, object> extra = new Dictionary<string, object>();

        public List<MListTableInfo> mlistTables;
    }

    public enum EntityBaseType
    {
        EnumEntity,
        Symbol,
        SemiSymbol,
        Entity,
        MList,
        Part,
    }

    public class MListTableInfo
    {
        public string niceName;
        public string tableName;
        public int columns;
        public int? rows;
        public int? total_size_kb;
        public int? history_rows;
        public int? history_total_size_kb;

        public Dictionary<string, object> extra = new Dictionary<string, object>();
    }

    public class RelationInfo
    {
        public string fromTable;
        public string toTable;
        public bool nullable;
        public bool lite;
    }

    public class MapColorProviderInfo
    {
        public string name;
        public string niceName;
    }

    public class SchemaMapInfo
    {
        public List<TableInfo> tables;
        public List<RelationInfo> relations;
        public List<MapColorProviderInfo> providers;
    }
}