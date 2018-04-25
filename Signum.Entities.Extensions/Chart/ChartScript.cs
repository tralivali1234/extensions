﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using Signum.Utilities;
using Signum.Entities.DynamicQuery;
using Signum.Entities.Files;
using System.Xml.Linq;
using System.Collections;
using System.Text.RegularExpressions;
using Signum.Entities.Reflection;
using Signum.Utilities.Reflection;
using System.Reflection;
using Signum.Utilities.ExpressionTrees;

namespace Signum.Entities.Chart
{
    [Serializable, EntityKind(EntityKind.Main, EntityData.Master)]
    public class ChartScriptEntity : Entity
    {
        [UniqueIndex]
        [StringLengthValidator(AllowNulls = false, Min = 3, Max = 100)]
        public string Name { get; set; }

        public Lite<FileEntity> Icon { get; set; }

        [StringLengthValidator(AllowNulls = false, Min = 3, MultiLine = true)]
        public string Script { get; set; }

        public GroupByChart GroupBy { get; set; }

        [NotifyCollectionChanged, NotifyChildProperty, PreserveOrder]
        [NotNullValidator]
        public MList<ChartScriptColumnEmbedded> Columns { get; set; } = new MList<ChartScriptColumnEmbedded>();

        [NotifyCollectionChanged, NotifyChildProperty, PreserveOrder]
        [NotNullValidator, NoRepeatValidator]
        public MList<ChartScriptParameterEmbedded> Parameters { get; set; } = new MList<ChartScriptParameterEmbedded>();

        [StringLengthValidator(AllowNulls = true, Min = 3, Max = 100)]
        public string ColumnsStructure { get; set; }

        static Expression<Func<ChartScriptEntity, string>> ToStringExpression = e => e.Name;
        [ExpressionField]
        public override string ToString()
        {
            return ToStringExpression.Evaluate(this);
        }

        public string ColumnsToString()
        {
            return Columns.ToString(a => a.ColumnType.ToString(), "|");
        }

        protected override string ChildPropertyValidation(ModifiableEntity sender, System.Reflection.PropertyInfo pi)
        {
            if (sender is ChartScriptColumnEmbedded column && pi.Name == nameof(column.IsGroupKey))
            {
                if (column.IsGroupKey)
                {
                    if (!ChartUtils.Flag(ChartColumnType.Groupable, column.ColumnType))
                        return "{0} can not be true for {1}".FormatWith(pi.NiceName(), column.ColumnType.NiceToString());
                }
            }

            if (sender is ChartScriptParameterEmbedded param && pi.Name == nameof(param.ColumnIndex))
            {
                if (param.ColumnIndex == null && param.ShouldHaveColumnIndex())
                    return ValidationMessage._0IsNecessary.NiceToString(pi.NiceName());

                if (param.ColumnIndex.HasValue && !(0 <= param.ColumnIndex && param.ColumnIndex < this.Columns.Count))
                    return ValidationMessage._0HasToBeBetween1And2.NiceToString(pi.NiceName(), 0, this.Columns.Count);
            }

            return base.ChildPropertyValidation(sender, pi);
        }

        protected override string PropertyValidation(System.Reflection.PropertyInfo pi)
        {
            if (pi.Name == nameof(GroupBy))
            {
                if (GroupBy == GroupByChart.Always || GroupBy == GroupByChart.Optional)
                {
                    if (!Columns.Any(a => a.IsGroupKey))
                        return "{0} {1} requires some key columns".FormatWith(pi.NiceName(), GroupBy.NiceToString());
                }
                else
                {
                    if (Columns.Any(a => a.IsGroupKey))
                        return "{0} {1} should not have key".FormatWith(pi.NiceName(), GroupBy.NiceToString());
                }
            }

            if (pi.Name == nameof(Script))
            {
                if (!Regex.IsMatch(Script, @"function\s+DrawChart\s*\(\s*chart\s*,\s*data\s*\)", RegexOptions.Singleline))
                {
                    return "{0} should be a definition of function DrawChart(chart, data)".FormatWith(pi.NiceName());
                }
            }

            return base.PropertyValidation(pi);
        }

        protected override void PreSaving(PreSavingContext ctx)
        {
            string from = Columns.Where(a => a.IsGroupKey).ToString(c => c.ColumnType.GetCode() + (c.IsOptional ? "?" : ""), ",");
            string to = Columns.Where(a => !a.IsGroupKey).ToString(c => c.ColumnType.GetCode() + (c.IsOptional ? "?" : ""), ",");

            ColumnsStructure = "{0} -> {1}".FormatWith(from.HasText()? from:"n", to.HasText() ? to : "n");

            base.PreSaving(ctx);
        }

        protected override void PostRetrieving()
        {
            base.PostRetrieving();
        }

        public XDocument ExportXml()
        {
            var icon = Icon?.Entity;

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("ChartScript",
                    new XAttribute("GroupBy", GroupBy.ToString()),
                    new XElement("Columns",
                        Columns.Select(c => new XElement("Column",
                            new XAttribute("DisplayName", c.DisplayName),
                            new XAttribute("ColumnType", c.ColumnType.ToString()),
                            c.IsGroupKey ? new XAttribute("IsGroupKey", true) : null,
                            c.IsOptional ? new XAttribute("IsOptional", true) : null
                         ))),
                    new XElement("Parameters",
                        Parameters.Select(p => new XElement("Parameter",
                            new XAttribute("Name", p.Name),
                            new XAttribute("Type", p.Type),
                            new XAttribute("ValueDefinition", p.ValueDefinition),
                            p.ColumnIndex == null ? null : new XAttribute("ColumnIndex", p.ColumnIndex))
                    )),
                    icon == null ? null :
                    new XElement("Icon",
                        new XAttribute("FileName", icon.FileName),
                        new XCData(Convert.ToBase64String(Icon.Entity.BinaryFile))),
                    new XElement("Script", new XCData(Script))));

        }

        public void ImportXml(XDocument doc, string name, bool force = false)
        {
            XElement script = doc.Root;

            GroupByChart groupBy = script.Attribute("GroupBy").Value.ToEnum<GroupByChart>();

            List<ChartScriptColumnEmbedded> columns = script.Element("Columns").Elements("Column").Select(c => new ChartScriptColumnEmbedded
            {
                DisplayName = c.Attribute("DisplayName").Value,
                ColumnType = c.Attribute("ColumnType").Value.ToEnum<ChartColumnType>(),
                IsGroupKey = c.Attribute("IsGroupKey").Let(a => a != null && a.Value == "true"),
                IsOptional = c.Attribute("IsOptional").Let(a => a != null && a.Value == "true"),
            }).ToList();

            if (!IsNew && !force)
                AsssertColumns(columns);

            this.Name = name;
            this.GroupBy = groupBy;

            if (this.Columns.Count == columns.Count)
            {
                this.Columns.ZipForeach(columns, (o, n) =>
                {
                    o.ColumnType = n.ColumnType;
                    o.DisplayName = n.DisplayName;
                    o.IsGroupKey = n.IsGroupKey;
                    o.IsOptional = n.IsOptional;
                });
            }
            else
            {
                this.Columns = columns.ToMList();
            }

            List<ChartScriptParameterEmbedded> parameters = script.Element("Parameters").Elements("Parameter").Select(p => new ChartScriptParameterEmbedded
            {
                Name = p.Attribute("Name").Value,
                Type = p.Attribute("Type").Value.ToEnum<ChartParameterType>(),
                ValueDefinition = p.Attribute("ValueDefinition").Value,
                ColumnIndex = p.Attribute("ColumnIndex")?.Let(c => int.Parse(c.Value)),
            }).ToList();

            if (this.Parameters.Count == parameters.Count)
            {
                this.Parameters.ZipForeach(parameters, (o, n) =>
                {
                    o.Name = n.Name;
                    o.Type = n.Type;
                    o.ValueDefinition = n.ValueDefinition;
                    o.ColumnIndex = n.ColumnIndex;
                });
            }
            else
            {
                this.Parameters = parameters.ToMList();
            }

            this.Script = script.Elements("Script").Nodes().OfType<XCData>().Single().Value;

            var newFile = script.Element("Icon")?.Let(icon => new FileEntity
            {
                FileName = icon.Attribute("FileName").Value,
                BinaryFile = Convert.FromBase64String(icon.Nodes().OfType<XCData>().Single().Value),
            });

            if (newFile == null)
            {
                Icon = null;
            }
            else
            {
                if (Icon == null || Icon.Entity.FileName != newFile.FileName || !AreEqual(Icon.Entity.BinaryFile, newFile.BinaryFile))
                    Icon = newFile.ToLiteFat();
            }
        }

        static bool AreEqual(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i])
                    return false;
            }

            return true;
        }

        private void AsssertColumns(List<ChartScriptColumnEmbedded> columns)
        {
            string errors = Columns.ZipOrDefault(columns, (o, n) =>
            {
                if (o == null)
                {
                    if (!n.IsOptional)
                        return "Adding non optional column {0}".FormatWith(n.DisplayName);
                }
                else if (n == null)
                {
                    if (o.IsOptional)
                        return "Removing non optional column {0}".FormatWith(o.DisplayName);
                }
                else if (n.ColumnType != o.ColumnType)
                {
                    return "The column type of '{0}' ({1}) does not match with '{2}' ({3})".FormatWith(
                        o.DisplayName, o.ColumnType,
                        n.DisplayName, n.ColumnType);
                }

                return null;
            }).NotNull().ToString("\r\n");

            if (errors.HasText())
                throw new FormatException("The columns doesn't match: \r\n" + errors);
        }

        public bool IsCompatibleWith(IChartBase chartBase)
        {
            if (GroupBy == GroupByChart.Always && !chartBase.GroupResults)
                return false;

            if (GroupBy == GroupByChart.Never && chartBase.GroupResults)
                return false;

            return Columns.ZipOrDefault(chartBase.Columns, (s, c) =>
            {
                if (s == null)
                    return c.Token == null;

                if (c == null || c.Token == null)
                    return s.IsOptional;

                if (!ChartUtils.IsChartColumnType(c.Token.Token, s.ColumnType))
                    return false;

                if (c.Token.Token is AggregateToken)
                    return !s.IsGroupKey;
                else
                    return s.IsGroupKey || !chartBase.GroupResults;

            }).All(a => a);
        }

        public bool HasChanges()
        {
            var graph = GraphExplorer.FromRoot(this);
            return graph.Any(a => a.Modified == ModifiedState.SelfModified);
        }
    }

    [AutoInit]
    public static class ChartScriptOperation
    {
        public static ExecuteSymbol<ChartScriptEntity> Save;
        public static ConstructSymbol<ChartScriptEntity>.From<ChartScriptEntity> Clone;
        public static DeleteSymbol<ChartScriptEntity> Delete;
    }

    public enum GroupByChart
    {
        Always,
        Optional,
        Never
    }
}
