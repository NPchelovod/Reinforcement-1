using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class FbRoot
{
    public double Height { get; set; }
    public double Width { get; set; }
    public bool AppendDateToFileName { get; set; }
    public bool AppendDateToFolderName { get; set; }
    public bool AppendTimeToFileName { get; set; }
    public bool AppendTimeToFolderName { get; set; }
    public bool ShowExportResults { get; set; }
    public bool SaveExportReports { get; set; }
    public string SaveReportsFolder { get; set; }
    public bool EngLang { get; set; }
    public bool RusLang { get; set; }
    public string UserData { get; set; }
    public string LoginTitle { get; set; }

    [JsonPropertyName("ExportDatas")]
    public List<FbJob> Jobs { get; set; } = new List<FbJob> { };
}

public class FbJob
{
    public List<FbModel> Models { get; set; } = new List<FbModel>();
    public string Name { get; set; }
    public bool IsChecked { get; set; }
    public string JobId { get; set; }
    public DateTime CreationDate { get; set; }
    public int Version { get; set; }
    public bool ToRvt { get; set; }
    public bool ToNwc { get; set; }
    public bool ToIfc { get; set; }
    public bool Report { get; set; }
    public bool Cleaning { get; set; }
    // ... прочие скалярные поля добавьте по вкусу
}

public class FbModel
{
    public bool IsExport { get; set; }
    public bool IfcUpload { get; set; }
    public bool IsExpanded { get; set; }
    public List<FbView> Views { get; set; } = new List<FbView> { };
    public string Path { get; set; }
    public string SaveRvtFile { get; set; }
    public string SaveNwcFolder { get; set; }
    public string SaveIfcFolder { get; set; }
    public string FuturelloPath { get; set; }
}

public class FbView
{
    public string ViewName { get; set; }
    public string IfcFileName { get; set; }
    public string NwcFileName { get; set; }
    public bool Export { get; set; }
    public string SaveFolder { get; set; }
}