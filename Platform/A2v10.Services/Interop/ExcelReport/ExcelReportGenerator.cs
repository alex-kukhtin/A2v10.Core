// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Globalization;

using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;

using A2v10.Data.Interfaces;

namespace A2v10.Services.Interop;

class RowSetDef
{
    internal String? SheetName;
    internal String? PropertyName;
    internal IList<Row?>? Rows;
    internal IList<Row?>? RowsForClone;
    internal UInt32 FirstRow;
    internal UInt32 RowCount;
    internal UInt32 LastRow;
    internal void Clear()
    {
        Rows = null;
        RowsForClone = null;
    }
}

class SharedStringDef
{
    internal SharedStringItem Item;
    internal String? Expression;
    internal Int32 iIndex;
    Boolean bParsed = false;

    internal SharedStringDef(SharedStringItem itm, Int32 ix)
    {
        Item = itm;
        iIndex = ix;
    }

    internal Boolean Parse()
    {
        if (bParsed)
            return true;

        String str = Item.Text?.Text ?? String.Empty;
        if (str.StartsWith('{') && str.EndsWith('}'))
        {
            Expression = str[1..^1];
            bParsed = true;
            return true;
        }
        return false;
    }
}


public class ExcelReportGenerator : IDisposable
{
    IDataModel? _dataModel;
    SharedStringTable? _sharedStringTable;
    Dictionary<String, SharedStringDef>? _sharedStringMap;
    Dictionary<Int32, SharedStringDef>? _sharedStringIndexMap;

    readonly String? _templateFile;
    readonly Stream? _templateStream;
    String? _resultFile;

    Boolean _sharedStringModified = false;
    Int32 _sharedStringCount = 0;

    public ExcelReportGenerator(String templateFile)
    {
        _templateFile = templateFile;
    }

    public ExcelReportGenerator(Stream templateStream)
    {
        _templateStream = templateStream;

    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(Boolean disposing)
    {
        if (disposing)
        {
            var r = _resultFile;
            _resultFile = null;
            if (r != null)
                File.Delete(r);
            _templateStream?.Close();
            _templateStream?.Dispose();
        }
    }

    public String? ResultFile => _resultFile;

    private static Dictionary<String, Dictionary<String, RowSetDef>> CreateDataSetRows(Workbook workbook)
    {
        var result = new Dictionary<String, Dictionary<String, RowSetDef>>();

        var defNames = workbook?.DefinedNames?.Elements<DefinedName>();
        if (defNames == null)
            return result;

        foreach (var defName in defNames)
        {
            var df = GetDefinedName(defName);
            if (df == null)
                continue;
            if (!result.TryGetValue(df.SheetName ?? String.Empty, out var rdict))
            {
                rdict = [];
                var sheetName = df.SheetName ?? String.Empty;
                if (sheetName.StartsWith('\'') && sheetName.EndsWith('\''))
                    sheetName = sheetName[1..^1];
                result.Add(sheetName, rdict);
            }
            rdict.Add(df.PropertyName ?? String.Empty, df);
        }
        return result;
    }

    public void GenerateReport(IDataModel dataModel)
    {
        _dataModel = dataModel;
        String tempFileName = Path.GetTempFileName();
        File.Delete(tempFileName);
        if (_templateStream != null)
        {
            using var br = new FileStream(tempFileName, FileMode.Create);
            _templateStream.CopyTo(br);
        }
        else if (_templateFile != null)
            File.Copy(_templateFile, tempFileName);
        else
            throw new InteropException("Template file or template stream is required");
        SpreadsheetDocument? doc = null;
        try
        {
            // Stream is not working ????
            doc = SpreadsheetDocument.Open(tempFileName, isEditable: true)
                ?? throw new InteropException("SpreadsheetDocument is null");

            _sharedStringTable = doc?.WorkbookPart?.SharedStringTablePart?.SharedStringTable
                ?? throw new InteropException("SharedStringTable is null");

            PrepareSharedStringTable(_sharedStringTable);

            var workBook = doc.WorkbookPart.Workbook;
            var dataSetRows = CreateDataSetRows(workBook);

            _sharedStringCount = _sharedStringTable.Elements<SharedStringItem>().Count<SharedStringItem>();

            foreach (var workSheetPart in doc.WorkbookPart.WorksheetParts)
            {
                var workSheet = workSheetPart.Worksheet;
                var sheetData = workSheet.GetFirstChild<SheetData>()
                    ?? throw new InteropException("Sheet Data not found");

                var relationshipId = doc.WorkbookPart.GetIdOfPart(workSheetPart);
                var sheet = workBook?.Sheets?.Elements<Sheet>().First(s => s.Id == relationshipId)
                         ?? throw new InteropException("Sheet not found");

                var sheetName = sheet.Name;
                Boolean modified = false;
                if (sheetName != null && sheetName.Value != null) { 
                    if (dataSetRows.TryGetValue(sheetName.Value, out var ds))
                        modified = ProcessData(ds, sheetData);
                }

                foreach (var row in sheetData.Elements<Row>())
                {
                    CorrectCellAddresses(row);
                }

                workSheet.AddChild(new IgnoredErrors(
                    new IgnoredError()
                    {
                        NumberStoredAsText = true,
                        SequenceOfReferences = new ListValue<StringValue>(
                            new List<StringValue>() { new("A1:WZZ999999") })
                    }
                ));

                if (modified)
                    workSheet.Save();
            }

            if (_sharedStringModified)
                _sharedStringTable.Save();
        }
        finally
        {
            doc?.Dispose();
        }
        _resultFile = tempFileName;
    }

    void PrepareSharedStringTable(SharedStringTable table)
    {
        var sslist = table.Elements<SharedStringItem>().ToList();
        for (Int32 i = 0; i < sslist.Count; i++)
        {
            var ssitem = sslist[i];
            if (ssitem == null) 
                continue;
            String? str = ssitem.Text?.Text;
            if (str == null || !str.StartsWith('{'))
                continue;
            _sharedStringMap ??= [];
            _sharedStringIndexMap ??= [];
            var ssd = new SharedStringDef(ssitem, i);
            _sharedStringMap.Add(str, ssd);
            _sharedStringIndexMap.Add(i, ssd);
        }
    }

    static RowSetDef? GetDefinedName(DefinedName? dn)
    {
        if (dn == null)
            return null;
        String? name = dn.Name;
        if (name == null || !name.StartsWith('_') || !name.EndsWith('_'))
            return null;
        String propName = name[1..^1];
        String showRef = dn.Text;
        Int32 exclPos = showRef.IndexOf('!');
        if (exclPos == -1)
            return null;
        String shtName = showRef[..exclPos];
        String shtRef = showRef[(exclPos + 1)..];
        Int32 colonPos = shtRef.IndexOf(':');
        if (colonPos == -1)
            return null;
        string startRef = shtRef[..colonPos]; // link to the first line of the range
        String endRef = shtRef[(colonPos + 1)..];  // link to the second line of the range
        if (startRef.Length < 2)
            return null;
        if (endRef.Length < 2)
            return null;
        UInt32 startRow = 0;
        UInt32 endRow = 0;
        if (startRef[0] == '$')
        {
            if (!UInt32.TryParse(startRef[1..], out startRow))
                return null;
        }
        if (endRef[0] == '$')
        {
            if (!UInt32.TryParse(endRef[1..], out endRow))
                return null;
        }
        return new()
        {
            SheetName = shtName,
            PropertyName = propName,
            FirstRow = startRow,
            LastRow = endRow,
            RowCount = endRow - startRow + 1
        };
    }

    Boolean ProcessData(Dictionary<String, RowSetDef>? datasetRows, SheetData sheetData)
    {
        if (datasetRows == null)
            return false;
        ProcessPlainTable(datasetRows, sheetData);

        if (datasetRows != null)
        {
            return ProcessDataSets(datasetRows, sheetData);
        }
        return false;
    }

    static Boolean IsRowInRange(Dictionary<String, RowSetDef> datasetRows, UInt32 rowIndex)
    {
        if (datasetRows == null)
            return false;
        foreach (var rd in datasetRows)
        {
            var rdv = rd.Value;
            if (rowIndex >= rdv.FirstRow && rowIndex <= rdv.LastRow)
                return true;
        }
        return false;
    }

    void ProcessPlainTable(Dictionary<String, RowSetDef> datasetRows, SheetData sheetData)
    {
        if (_dataModel == null)
            return;
        var rows = sheetData.Elements<Row>();
        foreach (var row in rows)
        {
            if (IsRowInRange(datasetRows, row.RowIndex ?? 0))
                continue;
            foreach (var cell in row.Elements<Cell>())
                SetCellData(cell, _dataModel.Root);
        }
    }

    Boolean ProcessDataSets(Dictionary<String, RowSetDef> datasetRows, SheetData sheetData)
    {
        if (_dataModel == null)
            return false;
        var result = false;
        foreach (var dataSet in datasetRows)
        {
            IList<ExpandoObject> list = _dataModel.Eval<List<ExpandoObject>>(dataSet.Key)
                ?? throw new InteropException($"The data model does not have a '{dataSet.Key}' property ");
            RowSetDef def = dataSet.Value;
            if (list.Count == 0)
            {
                // no records - delete range
                for (Int32 i = 0; i < def.RowCount; i++)
                {
                    var row = sheetData.Elements<Row>().First<Row>(r => (r.RowIndex ?? 0) == def.FirstRow + i);
                    row.Remove();
                }
                result = true;
            }
            else
            {
                UInt32 count = 0;
                for (Int32 i = 0; i < list.Count; i++)
                {
                    var lr = InsertRowFromTemplate(sheetData, def, ref count);
                    result = true;
                    var listData = list[i];
                    SetRecordData(def, listData);
                }
            }
        }
        return result;
    }

    void SetRecordData(RowSetDef def, ExpandoObject data)
    {
        // just an index (for now)
        if (def.Rows == null)
            return;
        foreach (var r in def.Rows)
        {
            if (r == null)
                continue;
            foreach (var c in r.Elements<Cell>())
                SetCellData(c, data);
        }
    }

    void SetCellData(Cell cell, ExpandoObject data)
    {
        if (cell.DataType == null || cell.CellValue == null)
            return;
        if (cell.DataType != CellValues.SharedString)
            return;
        if (_sharedStringIndexMap == null)
            return;
        String addr = cell.CellValue.Text.ToString();
        // this is the line number from SharedStrings
        if (!Int32.TryParse(addr, out Int32 strIndex))
            return;
        if (!_sharedStringIndexMap.TryGetValue(strIndex, out SharedStringDef? ssd))
            return;
        if (ssd == null)
            return;
        if (ssd.Parse())
        {
            Object? dat = _dataModel?.Eval<Object>(data, ssd.Expression ?? String.Empty);
            SetCellValueData(cell, dat);
        }
    }

    void SetCellValueData(Cell cell, Object? obj)
    {
        if (obj == null)
        {
            cell.DataType = null;
            cell.CellValue = null;
            return;
        }
        switch (obj)
        {
            case String strVal:
                cell.DataType = CellValues.SharedString;
                cell.CellValue = new CellValue(NewSharedString(strVal));
                break;
            case DateTime dt:
                {
                    var cv = new CellValue
                    {
                        Text = dt.ToOADate().ToString(CultureInfo.InvariantCulture)
                    };
                    // CellValues.Date supported in Office2010 only
                    cell.DataType = CellValues.Number;
                    cell.CellValue = cv;
                }
                break;
            case TimeSpan ts:
                {
                    cell.DataType = CellValues.Date;
                    DateTime dtv = new(ts.Ticks);
                    cell.CellValue = new CellValue(dtv.ToOADate().ToString(CultureInfo.InvariantCulture));
                }
                break;
            case Double dblVal:
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(dblVal.ToString(CultureInfo.InvariantCulture));
                break;
            case Decimal decVal:
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(decVal.ToString(CultureInfo.InvariantCulture));
                break;
            case Int64 int64Val:
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(int64Val.ToString());
                break;
            case Int32 int32Val:
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(int32Val.ToString());
                break;
            case Int16 int16Val:
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(int16Val.ToString());
                break;
            case Boolean boolVal:
                cell.DataType = CellValues.Boolean;
                cell.CellValue = new CellValue(boolVal.ToString());
                break;
            case Guid guidVal:
                cell.DataType = CellValues.String;
                cell.CellValue = new CellValue(guidVal.ToString());
                break;
            default:
                throw new InvalidOperationException($"Unknown data type {obj.GetType()}");
        }
    }

    String NewSharedString(String Value)
    {
        var ssi = new SharedStringItem();
        ssi.Append(new Text(Value));
        _sharedStringTable?.Append(ssi);
        _sharedStringModified = true;
        return (_sharedStringCount++).ToString();
    }


    static Row? InsertRowFromTemplate(SheetData sheetData, RowSetDef rd, ref UInt32 count)
    {
        Row? lastRow = null;
        if (rd.Rows == null)
        {
            // find row first
            var row = sheetData.Elements<Row>().First<Row>(r => (r.RowIndex ?? 0) == rd.FirstRow);
            rd.Rows = [];
            rd.RowsForClone = [];
            for (Int32 i = 0; i < rd.RowCount; i++)
            {
                rd.Rows.Add(row);
                rd.RowsForClone.Add(row?.Clone() as Row); // and for cloning too!
                row = row?.NextSibling<Row>();
                lastRow = row;
            }
        }
        else
        {
            // The line was already there, you need to clone it and insert it below
            if (rd.RowsForClone == null || rd.Rows == null || rd.Rows.Count == 0)
                throw new InvalidProgramException("InsertRowFromTemplate");
            lastRow = rd.Rows[rd.Rows.Count - 1];
            // next row index
            var rowIndex = rd.Rows[0]?.RowIndex?.Value ?? 0;
            UInt32 nri = rowIndex + rd.RowCount;
            for (Int32 i = 0; i < rd.Rows.Count; i++)
            {
                var sr = rd.RowsForClone[i];
                Row nr = sr?.Clone() as Row 
                   ?? throw new InvalidProgramException("Invalid Row Clone");
                nr.RowIndex = nri++;
                var insertedRow = sheetData.InsertAfter<Row>(nr, lastRow);
                CorrectRowIndex(insertedRow);
                count++;
                rd.Rows[i] = nr;
                lastRow = nr; // the last one is already inserted
            }
        }
        return lastRow;
    }
    static void CorrectRowIndex(Row insertedRow)
    {
        if (insertedRow == null)
            return;
        var nextRow = insertedRow.NextSibling<Row>();
        while (nextRow != null)
        {
            nextRow.RowIndex = insertedRow.RowIndex ?? new UInt32Value();
            nextRow.RowIndex += 1;
            nextRow = nextRow.NextSibling<Row>();
        }
    }

    static void CorrectCellAddresses(Row row)
    {
        foreach (var c in row.ChildElements)
        {
            if (c is not Cell clch || clch.CellReference == null)
                continue;
            var cr = clch.CellReference.ToString() ?? String.Empty; 
            var crn = new String(cr.Where(Char.IsLetter).ToArray());
            clch.CellReference = crn + row.RowIndex?.ToString();
        }
    }
}
