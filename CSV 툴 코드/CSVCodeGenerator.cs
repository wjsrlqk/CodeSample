using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CSVCodeGenerator : EditorWindow
{
    class TableDefinition
    {
        public string ClassName;
        public string[] Headers;
        public string[] Types;
        public List<string> KeyFields = new List<string>();
        public List<string> GroupFields = new List<string>();
    }

    class EnumDefinition
    {
        public string EnumName;
        public List<string> Headers = new List<string>();
        public List<int> Values = new List<int>();
    }
    private TextAsset csvFile;
    string outputDir = "Assets/Scripts/Generated";

    [MenuItem("Tools/CSV Code Generator")]
    public static void OpenWindow()
    {
        GetWindow<CSVCodeGenerator>("CSV Code Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("📄 CSV → C# 자동 구조체/파서 생성기", EditorStyles.boldLabel);
        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV 파일", csvFile, typeof(TextAsset), false);

        if (GUILayout.Button("코드 생성") && csvFile != null)
        {
            GenerateCode(csvFile);
        }
    }

    void GenerateCode(TextAsset csv)
    {
        string[] lines = csv.text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length == 0) return;

        bool CSVEnd = false;
        int lineIndex = 0;
        string fileName = "";
        List<TableDefinition> tables = new List<TableDefinition>();
        List<EnumDefinition> enums = new List<EnumDefinition>();
        List<string> tableNames = new List<string>();

        StringBuilder sb = new StringBuilder();
        // 자동 생성 경고 주석
        sb.AppendLine("// ------------------------------------------------------------------------------");
        sb.AppendLine("// ⚠️ 이 파일은 CSVCodeGenerator 툴에 의해 자동 생성되었습니다.");
        sb.AppendLine("// ⚠️ 직접 수정하지 마십시오. CSV 원본을 수정하고 툴을 다시 실행하세요.");
        sb.AppendLine("// ------------------------------------------------------------------------------");
        sb.AppendLine();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine();

        while (CSVEnd == false)
        {
            CSVEnd = true;

            bool inTable = false;
            bool inEnum = false;
            string className = "";
            string[] headers = null;
            string[] types = null;

            string enumName = null;
            List<string> enumHeaders = new List<string>();
            List<int> enumValues = new List<int>();

            List<string> keyField = new List<string>();
            List<string> groupField = new List<string>();
            TableDefinition tableDef = null;
            EnumDefinition enumDef = null;
            while (lineIndex < lines.Length)
            {
                string line = lines[lineIndex].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                {
                    lineIndex++;
                    continue;
                }

                var cells = line.Split(',');

                if (cells[0] == "[Table]")
                {
                    if (cells.Length < 2)
                    {
                        Debug.LogError("[Table] 다음에 클래스 이름이 필요합니다.");
                        return;
                    }
                    tableDef = new TableDefinition();
                    className = cells[1].Trim();
                    tableDef.ClassName = className;
                    tableNames.Add(className);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = className;
                    }
                    inTable = true;
                    headers = null;
                    types = null;
                    lineIndex++;
                    continue;
                }
                if (line.StartsWith("[END]"))
                {
                    if(tableDef != null)
                    {
                        tableDef.KeyFields = new List<string>(keyField);
                        tableDef.GroupFields = new List<string>(groupField);
                        tableDef.Headers = headers.ToArray();
                        tableDef.Types = types.ToArray();

                        tables.Add(tableDef);
                        tableDef = null;
                    }
                    if(enumDef != null)
                    {
                        enumDef.Headers = new List<string>(enumHeaders);
                        enumDef.Values = new List<int>(enumValues);
                        enums.Add(enumDef);
                        enumDef = null;
                    }
                    CSVEnd = false;
                    lineIndex++;
                    break;
                }
                if (cells[0].ToLower() == "[enum]")
                {
                    if (cells.Length < 2)
                    {
                        Debug.LogError("[Enum] 다음에 Enum 이름이 필요합니다.");
                        return;
                    }
                    inEnum = true;
                    enumDef = new EnumDefinition();
                    enumName = cells[1].Trim();
                    enumDef.EnumName = enumName;
                    enumHeaders = new List<string>();
                    enumValues = new List<int>();
                    lineIndex++;
                    continue;

                }
                if(inEnum)
                {
                    if (string.IsNullOrWhiteSpace(cells[0]) || cells[0].StartsWith("//"))
                    {
                        lineIndex++;
                        continue;
                    }
                    enumHeaders.Add(cells[0]);
                    int output = -1;
                    if (int.TryParse(cells[1],out output))
                        enumValues.Add(output);
                    else
                        enumValues.Add(-1);

                    lineIndex++;
                    continue;
                }
                if (inTable)
                {
                    //메타 정보 처리
                    if (line.StartsWith("#Meta:"))
                    {
                        string meta = line.Substring(6);
                        var metaParts = meta.Split(';');
                        foreach (var part in metaParts)
                        {
                            var kv = part.Split('=');
                            if (kv.Length == 2)
                            {
                                //엑셀 데이터 가공을 위해 ,를 제거 후 데이터를 저장
                                if (kv[0].Trim() == "Key") keyField.Add(kv[1].Trim().Replace(",", ""));
                                if (kv[0].Trim() == "Group") groupField.Add(kv[1].Trim().Replace(",", ""));
                            }
                        }
                    }
                    //주석은 건너뜀
                    else if (string.IsNullOrWhiteSpace(cells[0]) || cells[0].StartsWith("//"))
                    {
                        lineIndex++;
                        continue;
                    }
                    else if (headers == null)
                    {
                        headers = cells;
                    }
                    else if (types == null)
                    {
                        types = cells;
                    }
                }

                lineIndex++;
            }

            if(inTable)
            {
                inTable = false;
                //GenerateTableProcess(className,headers,types,keyField,groupField,sb);
            }
            if(inEnum)
            {
                inEnum = false;
                //GenerateEnumProcess(enumName,enumHeaders,enumValues,sb);
            }

        }
        sb.AppendLine("namespace CSV\n{");
        foreach(var iter in enums)
        {
            GenerateEnumProcess(iter.EnumName, iter.Headers, iter.Values, sb);

        }
        sb.AppendLine("}");
        foreach(var iter in tables)
        {
            GenerateTableProcess(iter.ClassName,iter.Headers,iter.Types,iter.KeyFields,iter.GroupFields,sb);
        }
        //테이블 로더 생성
        sb.AppendLine($"namespace {tableNames[0]}Loader");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class TableLoader");
        sb.AppendLine("    {");
        sb.AppendLine("        public static void LoadFromCSV(string csvText)");
        sb.AppendLine("        {");
        sb.AppendLine("            int lineCount = 0;");
        foreach (var tableName in tableNames)
        {
            sb.AppendLine($"            {tableName}.Table.LoadFromCSV(csvText, ref lineCount);");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        // 저장
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
        string outputPath = Path.Combine(outputDir, fileName + ".cs");
        File.WriteAllText(outputPath, sb.ToString());

        AssetDatabase.Refresh();

        Debug.Log($"✅ 코드 생성 완료: {outputPath}");
    }
    private void GenerateTableProcess(string className, string[] headers, string[] types, List<string> keyField, List<string> groupField,StringBuilder sb)
    {
        if (string.IsNullOrEmpty(className) || headers == null || types == null)
        {
            Debug.LogError("CSV 테이블 형식이 잘못되었습니다.");
            return;
        }


        string structName = className + "Data";

        // 구조체 정의
        sb.AppendLine("[System.Serializable]");
        sb.AppendLine($"public struct {structName}");
        sb.AppendLine("{");

        for (int i = 0; i < headers.Length; i++)
        {
            if (IsValidType(headers[i]) == false ||
                IsValidType(types[i]) == false)
            {
                continue;
            }
            sb.AppendLine($"    public {types[i]} {headers[i]};");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // 클래스 정의 시작
        sb.AppendLine($"public partial class {className} : TableBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public static {className} Table {{ get; private set; }} = new {className}();");
        sb.AppendLine($"    private List<{structName}> m_DataList = new List<{structName}>();");
        //List<List<int>> keyIndexList = new List<List<int>>();
        //List<List<int>> groupIndexList = new List<List<int>>();
        List<(string, string)> keyTypeInfo = new List<(string, string)>();
        List<(string, string)> groupTypeInfo = new List<(string, string)>();
        //키, 그룹 타입 인덱싱 
        foreach (var key in keyField)
        {
            List<int> indices = new List<int>();
            //복수 조건 체크
            var splitKey = key.Split("&");
            if (splitKey.Length > 1)
            {
                string typename = "({})";
                StringBuilder headerParts = new StringBuilder();
                StringBuilder typeParts = new StringBuilder();
                foreach (var elem in splitKey)
                {
                    int keyIndex = System.Array.IndexOf(headers, elem);
                    if (keyIndex < 0)
                    {
                        Debug.LogError($"키 필드 '{elem}'가 헤더에 없습니다.");
                        continue;
                    }
                    typeParts.Append(types[keyIndex]);

                    headerParts.Append(headers[keyIndex]);

                    if (splitKey[splitKey.Length - 1] != elem)
                    {
                        headerParts.Append(",");
                        typeParts.Append(",");
                    }
                }
                typename = typename.Replace("{}", typeParts.ToString());
                keyTypeInfo.Add((typename, headerParts.ToString()));

            }
            else
            {
                int keyIndex = System.Array.IndexOf(headers, splitKey[0]);
                keyTypeInfo.Add((types[keyIndex], headers[keyIndex]));
            }
            //    foreach(var elem in splitKey)
            //    {
            //        int keyIndex = System.Array.IndexOf(headers, elem);
            //        if(keyIndex < 0)
            //        {
            //            Debug.LogError($"키 필드 '{key}'가 헤더에 없습니다.");
            //            continue;
            //        }
            //        indices.Add(keyIndex);
            //    }
            //keyIndexList.Add(indices);
        }
        foreach (var group in groupField)
        {
            List<int> indices = new List<int>();
            //복수 조건 체크
            var splitGroup = group.Split("&");

            if (splitGroup.Length > 1)
            {
                string typename = "({})";
                StringBuilder headerParts = new StringBuilder();
                StringBuilder typeParts = new StringBuilder();
                foreach (var elem in splitGroup)
                {
                    int groupIndex = System.Array.IndexOf(headers, elem);
                    if (groupIndex < 0)
                    {
                        Debug.LogError($"키 필드 '{elem}'가 헤더에 없습니다.");
                        continue;
                    }
                    typeParts.Append(types[groupIndex]);

                    headerParts.Append(headers[groupIndex]);

                    if (splitGroup[splitGroup.Length - 1] != elem)
                    {
                        headerParts.Append(",");
                        typeParts.Append(",");
                    }
                }
                typename = typename.Replace("{}", typeParts.ToString());
                groupTypeInfo.Add((typename, headerParts.ToString()));

            }
            else
            {
                int groupIndex = System.Array.IndexOf(headers, splitGroup[0]);
                groupTypeInfo.Add((types[groupIndex], headers[groupIndex]));
            }

            //foreach (var elem in splitKey)
            //{
            //    int groupIndex = System.Array.IndexOf(headers, elem);
            //    if (groupIndex < 0)
            //    {
            //        Debug.LogError($"그룹 필드 '{group}'가 헤더에 없습니다.");
            //        continue;
            //    }
            //    indices.Add(groupIndex);
            //}

            //groupIndexList.Add(indices);
        }
        //인덱싱 데이터 기반으로 컨테이너 정의

        foreach (var info in keyTypeInfo)
        {
            string typePart = info.Item1;
            string headerPart = info.Item2;
            sb.AppendLine($"    private Dictionary<{typePart}, {structName}> m_DataBy{MakeHeaderByFunctionName(headerPart)} = new Dictionary<{typePart}, {structName}>();");
        }
        foreach (var info in groupTypeInfo)
        {
            string typePart = info.Item1;
            string headerPart = info.Item2;
            sb.AppendLine($"    private Dictionary<{typePart}, List<{structName}>> m_DataBy{MakeHeaderByFunctionName(headerPart)} = new Dictionary<{typePart}, List<{structName}>?>();");
        }
        //foreach (var keyIndex in keyIndexList)
        //sb.AppendLine($"    private Dictionary<{types[keyIndex]}, {structName}> m_DataBy{headers[keyIndex]} = new Dictionary<{types[keyIndex]}, {structName}>();");

        //foreach (var groupIndex in groupIndexList)
        //    sb.AppendLine($"    private Dictionary<{types[groupIndex]}, List<{structName}>> m_DataBy{headers[groupIndex]} = new Dictionary<{types[groupIndex]}, List<{structName}>>();");

        sb.AppendLine();
        sb.AppendLine($"    public IReadOnlyList<{structName}> repo {{ get => m_DataList;}}");

        // LoadFromCSV
        sb.AppendLine("    public void LoadFromCSV(string csvText,ref int lineCount)");
        sb.AppendLine("    {");

        sb.AppendLine("        var lines = csvText.Split(new[] { \"\\r\\n\", \"\\n\" }, StringSplitOptions.None);");
        sb.AppendLine("        bool inTable = false;");
        sb.AppendLine("        int step = 0;");
        sb.AppendLine("        for (int i = lineCount; i < lines.Length; i++) {");
        sb.AppendLine("            lineCount = i;");
        sb.AppendLine("            var line = lines[i].Trim();");
        sb.AppendLine("            if (string.IsNullOrEmpty(line) || line.StartsWith(\"//\")) continue;");
        sb.AppendLine("            if (line.StartsWith(\"[Table]\")) { inTable = true; step = 0; continue; }");
        sb.AppendLine("            if (line.StartsWith(\"[END]\")) { ++lineCount; if(inTable){break;} else{ continue; } }");
        sb.AppendLine("            if (!inTable) continue;");
        sb.AppendLine("            if (line.StartsWith(\"#Meta:\")) continue;");
        sb.AppendLine("            if (step < 2) { step++; continue; }");
        sb.AppendLine("            var cells = line.Split(',');");
        sb.AppendLine("            if (cells[0].StartsWith(\"//\")) continue;");
        sb.AppendLine("            if (cells.All(string.IsNullOrEmpty)) continue;");
        sb.AppendLine($"            {structName} data = new {structName}();");

        for (int i = 0; i < headers.Length; i++)
        {
            string field = headers[i];
            string type = types[i];

            if (IsValidType(field) == false ||
                IsValidType(type) == false)
            {
                continue;
            }

            string parse = type switch {
                "int" => $"int.TryParse(cells[{i}], out var v{i}) ? v{i} : 0",
                "float" => $"float.TryParse(cells[{i}], out var v{i}) ? v{i} : 0f",
                "string" => $"string.IsNullOrEmpty(cells[{i}]) ? string.Empty : cells[{i}]",
                "bool" => $"bool.TryParse(cells[{i}], out var v{i}) ? v{i} : false",
                _ => $"cells[{i}] // Unsupported type"
            };

            sb.AppendLine($"            data.{field} = {parse};");
        }

        sb.AppendLine("            m_DataList.Add(data);");
        foreach (var info in keyTypeInfo)
        {
            string headerPart = info.Item2;
            sb.AppendLine($"            m_DataBy{MakeHeaderByFunctionName(headerPart)}[{MakeHeaderByVariables("data", headerPart)}] = data;");
        }
        foreach (var info in groupTypeInfo)
        {
            string headerPart = info.Item2;
            sb.AppendLine($"            if (!m_DataBy{MakeHeaderByFunctionName(headerPart)}.ContainsKey({MakeHeaderByVariables("data", headerPart)}))");
            sb.AppendLine($"                m_DataBy{MakeHeaderByFunctionName(headerPart)}[{MakeHeaderByVariables("data", headerPart)}] = new List<{structName}>();");
            sb.AppendLine($"            m_DataBy{MakeHeaderByFunctionName(headerPart)}[{MakeHeaderByVariables("data", headerPart)}].Add(data);");
        }

        //foreach (var keyIndex in keyIndexList)
        //sb.AppendLine($"            m_DataBy{headers[keyIndex]}[data.{headers[keyIndex]}] = data;");

        //foreach (var groupIndex in groupIndexList)
        //{
        //    sb.AppendLine($"            if (!m_DataBy{headers[groupIndex]}.ContainsKey(data.{headers[groupIndex]}))");
        //    sb.AppendLine($"                m_DataBy{headers[groupIndex]}[data.{headers[groupIndex]}] = new List<{structName}>();");
        //    sb.AppendLine($"            m_DataBy{headers[groupIndex]}[data.{headers[groupIndex]}].Add(data);");
        //}

        sb.AppendLine("        }");
        sb.AppendLine("         LoadCSVAfterProcess();");
        sb.AppendLine("    }");

        // 조회 함수
        foreach (var info in keyTypeInfo)
        {
            string typePart = info.Item1;
            string headerPart = info.Item2;
            sb.AppendLine();
            sb.AppendLine($"    public {structName} GetBy{MakeHeaderByFunctionName(headerPart)}({typePart} key) => m_DataBy{MakeHeaderByFunctionName(headerPart)}[key];");
            sb.AppendLine($"    public bool TryGetBy{MakeHeaderByFunctionName(headerPart)}({typePart} key, out {structName} data) => m_DataBy{MakeHeaderByFunctionName(headerPart)}.TryGetValue(key, out data);");
        }
        foreach (var info in groupTypeInfo)
        {
            string typePart = info.Item1;
            string headerPart = info.Item2;
            sb.AppendLine();
            sb.AppendLine($"    public IReadOnlyList<{structName}> GetBy{MakeHeaderByFunctionName(headerPart)}({typePart} group) =>");
            sb.AppendLine($"        m_DataBy{MakeHeaderByFunctionName(headerPart)}.TryGetValue(group, out var list) ? list : new List<{structName}>();");
        }


        sb.AppendLine("}");


    }
    private void GenerateEnumProcess(string enumName,List<string> enumHeaders,List<int> enumValues, StringBuilder sb)
    {
        sb.AppendLine($"    public enum {enumName}");
        sb.AppendLine("    {");
        for(int i = 0;i < enumHeaders.Count;++i)
        {
            sb.AppendLine($"        {enumHeaders[i]},");
        }
        sb.AppendLine("    }");
    }

    private string GetHeaderName(string headers, int index)
    {
        return headers.Split(",")[index];
    }
    private string MakeHeaderByFunctionName(string headers)
    {
        return headers.Replace(",","");
    }
    private string MakeHeaderByVariables(string structureName,string headers)
    {
        var splitHeaders = headers.Split(",");
        if(splitHeaders.Length == 1)
        {
            return $"{structureName}.{splitHeaders[0]}";
        }
        StringBuilder output = new StringBuilder();
        output.Append($"(");
        for(int i = 0; i < splitHeaders.Length; ++i)
        {
            output.Append($"{structureName}.{splitHeaders[i]}");
            if( i < splitHeaders.Length - 1)
            {
                output.Append(",");
            }
        }
        output.Append(")");
        return output.ToString();
    }
    private bool IsValidType(string type)
    {
        return (type.StartsWith("//") || string.IsNullOrEmpty(type) || type == " ") == false;
    }
}
