﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using spreadsheet = DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Signum.Entities.DynamicQuery;
using System.IO;
using Signum.Utilities.DataStructures;
using Signum.Utilities;
using System.Globalization;
using Signum.Entities.Reflection;
using Signum.Engine;
using Signum.Entities;
using Signum.Engine.DynamicQuery;
using Signum.Entities.Excel;

namespace Signum.Engine.Excel
{
    public static class ExcelGenerator
    {
        public static byte[] WriteDataInExcelFile(ResultTable queryResult, byte[] template)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteAllBytes(template);
                ms.Seek(0, SeekOrigin.Begin);

                ExcelGenerator.WriteDataInExcelFile(queryResult, ms);

                return ms.ToArray();
            }
        }

        public static void WriteDataInExcelFile(ResultTable results, string fileName)
        {
            using (FileStream fs = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite))
                WriteDataInExcelFile(results, fs);
        }

        public static void WriteDataInExcelFile(ResultTable results, Stream stream)
        {
            if (results == null)
                throw new ApplicationException(ExcelMessage.ThereAreNoResultsToWrite.NiceToString());

            using (SpreadsheetDocument document = SpreadsheetDocument.Open(stream, true))
            {
                document.PackageProperties.Creator = "";
                document.PackageProperties.LastModifiedBy = "";

                WorkbookPart workbookPart = document.WorkbookPart;

                if (workbookPart.CalculationChainPart != null)
                {
                    workbookPart.Workbook.CalculationProperties.ForceFullCalculation = true;
                    workbookPart.Workbook.CalculationProperties.FullCalculationOnLoad = true;
                }

                WorksheetPart worksheetPart = document.GetWorksheetPartByName(ExcelMessage.Data.NiceToString());
                
                CellBuilder cb = PlainExcelGenerator.CellBuilder;
                
                SheetData sheetData = worksheetPart.Worksheet.Descendants<SheetData>().SingleEx();

                List<ColumnData> columnEquivalences = GetColumnsEquivalences(document, sheetData, results);

                UInt32Value headerStyleIndex = worksheetPart.Worksheet.FindCell("A1").StyleIndex;

                //Clear sheetData from the template sample data
                sheetData.InnerXml = "";

                sheetData.Append(new Sequence<Row>()
                {
                    (from columnData in columnEquivalences
                        select cb.Cell(columnData.Column.Column.DisplayName, headerStyleIndex)).ToRow(),

                    from r in results.Rows
                        select (from columnData in columnEquivalences
                                select cb.Cell(r[columnData.Column], cb.GetTemplateCell(columnData.Column.Column.Type), columnData.StyleIndex)).ToRow()
                }.Cast<OpenXmlElement>());

                var pivotTableParts = workbookPart.PivotTableCacheDefinitionParts
                    .Where(ptpart => ptpart.PivotCacheDefinition.Descendants<WorksheetSource>()
                                                                .Any(wss => wss.Sheet.Value == ExcelMessage.Data.NiceToString()));

                foreach (PivotTableCacheDefinitionPart ptpart in pivotTableParts)
                {
                    PivotCacheDefinition pcd = ptpart.PivotCacheDefinition;
                    WorksheetSource wss = pcd.Descendants<WorksheetSource>().FirstEx();
                    wss.Reference.Value = "A1:" + GetExcelColumn(columnEquivalences.Count(ce => !ce.IsNew) - 1) + (results.Rows.Count() + 1).ToString();
                    
                    pcd.RefreshOnLoad = true;
                    pcd.SaveData = false;
                    pcd.Save();
                }

                workbookPart.Workbook.Save();
                document.Close();
            }
        }

        private static List<ColumnData> GetColumnsEquivalences(this SpreadsheetDocument document, SheetData sheetData, ResultTable results)
        {
            var resultsCols = results.Columns.ToDictionary(c => c.Column.DisplayName);

            var headerCells = sheetData.Descendants<Row>().FirstEx().Descendants<Cell>().ToList();
            var templateCols = headerCells.ToDictionary(c => document.GetCellValue(c));

            var rowDataCellTemplates = sheetData.Descendants<Row>()
                .FirstEx(r => IsValidRowDataTemplate(r, headerCells))
                .Descendants<Cell>().ToList();

            var dic = templateCols.OuterJoinDictionaryCC(resultsCols, (name, cell, resultCol) =>
            {
                if (resultCol == null)
                    throw new ApplicationException(ExcelMessage.TheExcelTemplateHasAColumn0NotPresentInTheFindWindow.NiceToString().FormatWith(name));
                
                if (cell != null)
                {
                    return new ColumnData
                    {
                        IsNew = false,
                        StyleIndex = rowDataCellTemplates[headerCells.IndexOf(cell)].StyleIndex,
                        Column = resultCol,
                    };
                }
                else
                {
                    CellBuilder cb = PlainExcelGenerator.CellBuilder;
                    return new ColumnData
                    {
                        IsNew = true,
                        StyleIndex = 0, 
                        Column = resultCol,
                    };
                }
            });

            return dic.Values.ToList();
        }

        private static bool IsValidRowDataTemplate(Row row, List<Cell> headerCells)
        { 
            if (row.RowIndex <= 1) //At least greater than 1 (row 1 must be the header one)
                return false;

            var cells = row.Descendants<Cell>().ToList();

            if (cells.Count < headerCells.Count) //Must have at least as many cells as the header row to have a template cell for all data columns
                return false;

            string headerLastCellReference = headerCells[headerCells.Count - 1].CellReference;
            string dataCellReference = cells[headerCells.Count - 1].CellReference;
            
            //they must be in the same column
            //If cellReferences of HeaderCell and DataCell differ only in the number they are on the same column
            var firstDifferentCharacter = headerLastCellReference.Zip(dataCellReference).FirstEx(t => t.Item1 != t.Item2);
            return int.TryParse(firstDifferentCharacter.Item1.ToString(), out int number);
        }

        private static string GetExcelColumn(int columnNumberBase0)
        {
            string result = "";
            int numAlphabetCharacters = 26;
            int numAlphabetRounds;
            numAlphabetRounds = Math.DivRem(columnNumberBase0, numAlphabetCharacters, out int numAlphabetCharacter);

            if (numAlphabetRounds > 0)
                result = ((char)('A' + (char)(numAlphabetRounds - 1))).ToString();

            result = result + ((char)('A' + (char)numAlphabetCharacter)).ToString();
            return result;
        }

        public class ColumnData
        {
            /// <summary>
            /// Column Data
            /// </summary>
            public Signum.Entities.DynamicQuery.ResultColumn Column { get; set; }

            /// <summary>
            /// Indicates the column is not present in the template excel
            /// </summary>
            public bool IsNew { get; set; }

            /// <summary>
            /// Style index of the column in the template excel
            /// </summary>
            public UInt32Value StyleIndex { get; set; }
        }

        static double GetColumnWidth(Type type)
        { 
            type = type.UnNullify();

            if (type == typeof(DateTime))
                return 20;
            if (type == typeof(string))
                return 50;
            if (type.IsLite())
                return 50;

            return 10;
        }
    }
}
