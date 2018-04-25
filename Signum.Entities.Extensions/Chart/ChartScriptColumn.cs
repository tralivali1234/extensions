﻿using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Signum.Entities.Chart
{
    [Serializable]
    public class ChartScriptColumnEmbedded : EmbeddedEntity
    {
        [StringLengthValidator(AllowNulls = false, Min = 3, Max = 80)]
        public string DisplayName { get; set; }

        public bool IsOptional { get; set; }

        public ChartColumnType ColumnType { get; set; }

        public bool IsGroupKey { get; set; }

        internal ChartScriptColumnEmbedded Clone()
        {
            return new ChartScriptColumnEmbedded
            {
                DisplayName = DisplayName,
                IsGroupKey = IsGroupKey,
                ColumnType = ColumnType,
                IsOptional = IsOptional,
            };
        }
    }

    [Flags]
    public enum ChartColumnType
    {
        [Code("i")]
        Integer = 1,
        [Code("r")]
        Real = 2,
        [Code("d")]
        Date = 4,
        [Code("dt")]
        DateTime = 8,
        [Code("s")]
        String = 16, //Guid
        [Code("l")]
        Lite = 32,
        [Code("e")]
        Enum = 64, // Boolean,
        [Code("rg")]
        RealGroupable = 128,

        [Code("G")]
        Groupable = ChartColumnTypeUtils.GroupMargin | RealGroupable | Integer | Date | String | Lite | Enum,
        [Code("M")]
        Magnitude = ChartColumnTypeUtils.GroupMargin | Integer | Real | RealGroupable,
        [Code("P")]
        Positionable = ChartColumnTypeUtils.GroupMargin | Integer | Real | RealGroupable | Date | DateTime | Enum
    }


    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class CodeAttribute : Attribute
    {
        string code;
        public CodeAttribute(string code)
        {
            this.code = code;
        }

        public string Code
        {
            get { return code; }
        }
    }

    public static class ChartColumnTypeUtils
    {
        public const int GroupMargin = 0x10000000;

        static Dictionary<ChartColumnType, string> codes = EnumFieldCache.Get(typeof(ChartColumnType)).ToDictionary(
            a => (ChartColumnType)a.Key,
            a => a.Value.GetCustomAttribute<CodeAttribute>().Code);

        public static string GetCode(this ChartColumnType columnType)
        {
            return codes.GetOrThrow(columnType);
        }

        public static string GetComposedCode(this ChartColumnType columnType)
        {
            var result = columnType.GetCode();

            if (result.HasText())
                return result;

            return EnumExtensions.GetValues<ChartColumnType>()
                .Where(a => (int)a < ChartColumnTypeUtils.GroupMargin && columnType.HasFlag(a))
                .ToString(GetCode, ",");
        }

        static Dictionary<string, ChartColumnType> fromCodes = EnumFieldCache.Get(typeof(ChartColumnType)).ToDictionary(
            a => a.Value.GetCustomAttribute<CodeAttribute>().Code,
            a => (ChartColumnType)a.Key);

        public static string TryParse(string code, out ChartColumnType type)
        {
            if (fromCodes.TryGetValue(code, out type))
                return null;

            return "{0} is not a valid type code, use {1} instead".FormatWith(code, fromCodes.Keys.CommaOr());
        }

        public static string TryParseComposed(string code, out ChartColumnType type)
        {
            type = default(ChartColumnType);
            foreach (var item in code.Split(','))
            {
                string error = TryParse(item, out ChartColumnType temp);

                if (error.HasText())
                    return error;

                type |= temp;
            }
            return null;
        }
    }
}
