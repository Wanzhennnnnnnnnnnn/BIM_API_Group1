using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Data.SqlClient; // 您的 using
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
// using System.Data.SqlClient; // (重複了，移到上方)

namespace BIM_API_Automated_Carbon_Emission_Estimation_System
{
    /// SelectionWindow.xaml 的互動邏輯
    public partial class SelectionWindow : Window
    {
        private Document _doc;
        private List<ExtractedCarbonData> _extractedData = new List<ExtractedCarbonData>();
        private const string CarbonDbConnectionString =
    "Data Source=(LocalDB)\\MSSQLLocalDB;" +
    "AttachDbFilename=\"C:\\Users\\417\\Documents\\BIM_API_Group1\\BIM_API_Automated_Carbon_Emission_System\\group1DB.mdf\";" +
    "Integrated Security=True;";

        /// <param name="doc">當前的 Revit Document</param>
        public SelectionWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();
            PopulateLevelList();
        }

        /// <summary>
        /// 填充 UI 介面上的樓層列表
        /// </summary>
        private void PopulateLevelList()
        {
            try
            {
                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                LevelsListBox.ItemsSource = levels;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("錯誤", "無法載入樓層清單: " + ex.Message);
            }
        }

        /// "取消" 按鈕點擊事件
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// "開始擷取" 按鈕點擊事件
        /// ** (已修正：改為呼叫 ExtractRebarData) **
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 獲取樓層篩選器
                List<ElementId> selectedLevelIds = LevelsListBox.SelectedItems
                    .Cast<Level>()
                    .Select(l => l.Id)
                    .ToList();

                // 2. 獲取類別篩選器
                Dictionary<BuiltInCategory, bool> categoryFilters = new Dictionary<BuiltInCategory, bool>
                {
                    { BuiltInCategory.OST_Walls, CheckWalls.IsChecked == true },
                    { BuiltInCategory.OST_Floors, CheckFloors.IsChecked == true },
                    { BuiltInCategory.OST_StructuralColumns, CheckColumns.IsChecked == true },
                    { BuiltInCategory.OST_StructuralFraming, CheckFraming.IsChecked == true },
                    { BuiltInCategory.OST_Rebar, CheckRebar.IsChecked == true }
                };

                // 3. 執行擷取
                _extractedData.Clear(); // 清空舊資料
                List<ExtractedCarbonData> extractedDataTemp = new List<ExtractedCarbonData>();

                if (categoryFilters[BuiltInCategory.OST_Walls])
                    ExtractHostData(_doc, BuiltInCategory.OST_Walls, extractedDataTemp, "結構牆", selectedLevelIds);

                if (categoryFilters[BuiltInCategory.OST_Floors])
                    ExtractHostData(_doc, BuiltInCategory.OST_Floors, extractedDataTemp, "樓板", selectedLevelIds);

                if (categoryFilters[BuiltInCategory.OST_StructuralColumns])
                    ExtractComponentData(_doc, BuiltInCategory.OST_StructuralColumns, extractedDataTemp, "結構柱", selectedLevelIds);

                if (categoryFilters[BuiltInCategory.OST_StructuralFraming])
                    ExtractComponentData(_doc, BuiltInCategory.OST_StructuralFraming, extractedDataTemp, "結構梁", selectedLevelIds);

                // ** 修正 **
                // 錯誤：ExtractComponentData(_doc, BuiltInCategory.OST_Rebar, ...);
                // 正確：
                if (categoryFilters[BuiltInCategory.OST_Rebar])
                    ExtractRebarData(_doc, extractedDataTemp, selectedLevelIds); // 呼叫正確的鋼筋擷取方法

                _extractedData = extractedDataTemp;

                // 4. 顯示報告
                ShowReport(_extractedData);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("擷取錯誤", ex.Message);
            }
        }

        /// 建立一個樓層篩選器 (LogicalOrFilter)
        /// ** (已修正：牆使用 ELEM_LEVEL_PARAM) **
        private ElementFilter CreateLevelFilter(List<ElementId> levelIds)
        {
            if (levelIds == null || levelIds.Count == 0)
                return null;

            IList<ElementFilter> levelFilters = new List<ElementFilter>();

            foreach (ElementId levelId in levelIds)
            {
                // 篩選 "Level" 參數 (適用於 牆)
                var providerWall = new ParameterValueProvider(new ElementId(BuiltInParameter.WALL_BASE_CONSTRAINT));
                var ruleWall = new FilterElementIdRule(providerWall, new FilterNumericEquals(), levelId);
                levelFilters.Add(new ElementParameterFilter(ruleWall));

                // 篩選 "Level" 參數 (適用於樓板)
                var providerFloor = new ParameterValueProvider(new ElementId(BuiltInParameter.LEVEL_PARAM));
                var ruleFloor = new FilterElementIdRule(providerFloor, new FilterNumericEquals(), levelId);
                levelFilters.Add(new ElementParameterFilter(ruleFloor));

                // 篩選 "Base Constraint" / "SCHEDULE_LEVEL_PARAM" (適用於 柱)
                var providerBase = new ParameterValueProvider(new ElementId(BuiltInParameter.SCHEDULE_LEVEL_PARAM));
                var ruleBase = new FilterElementIdRule(providerBase, new FilterNumericEquals(), levelId);
                levelFilters.Add(new ElementParameterFilter(ruleBase));

                // 篩選 "Reference Level" (適用於 梁)
                var providerRef = new ParameterValueProvider(new ElementId(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM));
                var ruleRef = new FilterElementIdRule(providerRef, new FilterNumericEquals(), levelId);
                levelFilters.Add(new ElementParameterFilter(ruleRef));
            }

            return new LogicalOrFilter(levelFilters);
        }


        /// 擷取系統族群 (Host Objects) 如 牆 和 樓板 的資料
        private void ExtractHostData(Document doc, BuiltInCategory category, List<ExtractedCarbonData> dataList, string categoryName, List<ElementId> levelIds)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType();

            ElementFilter levelFilter = CreateLevelFilter(levelIds);
            if (levelFilter != null)
                collector.WherePasses(levelFilter);

            var elements = collector.ToElements();

            foreach (Element elem in elements)
            {
                if (!(elem is HostObject host)) continue;

                string typeName = host.Name;
                string materialName = "N/A";
                ElementId materialId = ElementId.InvalidElementId;

                try
                {
                    HostObjAttributes hostType = doc.GetElement(host.GetTypeId()) as HostObjAttributes;
                    CompoundStructure cs = hostType.GetCompoundStructure();
                    if (cs != null)
                    {
                        int structLayerIndex = cs.GetFirstCoreLayerIndex();
                        if (structLayerIndex > -1)
                        {
                            materialId = cs.GetMaterialId(structLayerIndex);
                        }
                    }
                }
                catch { }

                if (materialId != ElementId.InvalidElementId)
                {
                    materialName = (doc.GetElement(materialId) as Material)?.Name ?? "N/A";
                }

                double volume_ft3 = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                double volume_m3 = UnitUtils.Convert(volume_ft3, UnitTypeId.CubicFeet, UnitTypeId.CubicMeters);

                double area_ft2 = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                double area_m2 = UnitUtils.Convert(area_ft2, UnitTypeId.SquareFeet, UnitTypeId.SquareMeters);

                dataList.Add(new ExtractedCarbonData
                {
                    ElementId = elem.Id.ToString(),
                    Category = categoryName,
                    TypeName = typeName,
                    MaterialName = materialName,
                    Volume_m3 = Math.Round(volume_m3, 4),
                    Area_m2 = Math.Round(area_m2, 4),
                    Unit = "m³ & m²"
                });
            }
        }

        /// 擷取元件族群 (Component) 如 柱 和 梁 的資料
        /// ** (已修正：備用材質參數) **
        private void ExtractComponentData(Document doc, BuiltInCategory category, List<ExtractedCarbonData> dataList, string categoryName, List<ElementId> levelIds)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType();

            ElementFilter levelFilter = CreateLevelFilter(levelIds);
            if (levelFilter != null)
                collector.WherePasses(levelFilter);

            var elements = collector.ToElements();

            foreach (Element elem in elements)
            {
                if (!(elem is FamilyInstance inst)) continue;

                string typeName = inst.Symbol.Name;
                string materialName = "N/A";
                ElementId materialId = ElementId.InvalidElementId;

                try
                {
                    materialId = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.AsElementId();
                    if (materialId == null || materialId == ElementId.InvalidElementId)
                    {
                        materialId = inst.Symbol.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM)?.AsElementId();
                    }
                }
                catch { /* 忽略 */ }

                if (materialId != null && materialId != ElementId.InvalidElementId)
                {
                    materialName = (doc.GetElement(materialId) as Material)?.Name ?? "N/A";
                }

                double volume_ft3 = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                double volume_m3 = UnitUtils.Convert(volume_ft3, UnitTypeId.CubicFeet, UnitTypeId.CubicMeters);

                dataList.Add(new ExtractedCarbonData
                {
                    ElementId = elem.Id.ToString(),
                    Category = categoryName,
                    TypeName = typeName,
                    MaterialName = materialName,
                    Volume_m3 = Math.Round(volume_m3, 4),
                    Unit = "m³"
                });
            }
        }


        // --- (方法已重新加入) ---
        /// <summary>
        /// 專門擷取 鋼筋 (Rebar) 的資料
        /// </summary>
        private void ExtractRebarData(Document doc, List<ExtractedCarbonData> dataList, List<ElementId> levelIds)
        {
            // 1. 獲取所有鋼筋 (包含在群組中的)
            List<Rebar> allRebars = GetAllRebarElements(doc);

            // 2. 獲取樓層篩選器 (如果有的話)
            ElementFilter levelFilter = null;
            HashSet<ElementId> hostIdsOnLevel = null;

            if (levelIds != null && levelIds.Count > 0)
            {
                levelFilter = CreateLevelFilter(levelIds);

                List<BuiltInCategory> hostCategories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_StructuralFoundation
                };
                ElementMulticategoryFilter hostCatFilter = new ElementMulticategoryFilter(hostCategories);

                hostIdsOnLevel = new FilteredElementCollector(doc)
                    .WherePasses(hostCatFilter)
                    .WherePasses(levelFilter)
                    .ToElementIds()
                    .ToHashSet();
            }

            // 3. 遍歷所有鋼筋
            foreach (Rebar rebar in allRebars)
            {
                // 3a. 應用樓層篩選
                if (hostIdsOnLevel != null)
                {
                    try
                    {
                        ElementId hostId = rebar.GetHostId();
                        if (hostId == null || !hostIdsOnLevel.Contains(hostId))
                        {
                            continue;
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                // 3b. 擷取資料
                RebarBarType barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
                if (barType == null) continue;

                string typeName = barType.Name;
                string materialName = "N/A";

                try
                {
                    ElementId materialId = barType.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.AsElementId();
                    if (materialId != null && materialId != ElementId.InvalidElementId)
                    {
                        materialName = (doc.GetElement(materialId) as Material)?.Name ?? "N/A";
                    }
                }
                catch { /* 忽略 */ }

                double totalWeight_kg = GetRebarWeight_kg(rebar, barType, doc);

                dataList.Add(new ExtractedCarbonData
                {
                    ElementId = rebar.Id.ToString(),
                    Category = "鋼筋",
                    TypeName = typeName,
                    MaterialName = materialName,
                    Weight_kg = Math.Round(totalWeight_kg, 2),
                    Unit = "kg"
                });
            }
        }

        // --- (方法已重新加入) ---
        /// <summary>
        /// 取得鋼筋重量 (kg)，具備相容於舊版 API (RVT 2021 及更早) 的備援系統。
        /// </summary>
        private double GetRebarWeight_kg(Rebar rebar, RebarBarType barType, Document doc)
        {
            double totalWeight_lbs = 0;

            double totalLength_ft = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_TOTAL_LENGTH).AsDouble();
            if (totalLength_ft == 0) return 0;

            // --- 備援 1: 嘗試字串名稱 (相容舊樣板) ---
            Parameter wpl_Param_String = barType.LookupParameter("Bar Weight/Length");
            if (wpl_Param_String != null && wpl_Param_String.HasValue && wpl_Param_String.AsDouble() > 0)
            {
                totalWeight_lbs = totalLength_ft * wpl_Param_String.AsDouble();
            }
            else
            {
                // --- 備援 2: 手動物理計算 (Volume * Density) (使用舊版 API) ---
                try
                {
                    double diameter_ft = barType.BarNominalDiameter;
                    double area_ft2 = Math.PI * Math.Pow(diameter_ft / 2.0, 2.0);
                    double volume_ft3 = totalLength_ft * area_ft2;

                    double density_lb_per_ft3 = 0;
                    ElementId materialId = barType.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.AsElementId();
                    if (materialId != null && materialId != ElementId.InvalidElementId)
                    {
                        Material mat = doc.GetElement(materialId) as Material;
                        if (mat != null)
                        {
                            ElementId sAssetId = mat.StructuralAssetId;
                            if (sAssetId != ElementId.InvalidElementId)
                            {
                                PropertySetElement pset = doc.GetElement(sAssetId) as PropertySetElement;
                                if (pset != null)
                                {
                                    StructuralAsset sAsset = pset.GetStructuralAsset();
                                    density_lb_per_ft3 = sAsset.Density;
                                }
                            }
                        }
                    }

                    if (density_lb_per_ft3 > 0)
                    {
                        totalWeight_lbs = volume_ft3 * density_lb_per_ft3;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print($"計算鋼筋重量失敗 (ID: {rebar.Id}): {ex.Message}");
                    totalWeight_lbs = 0;
                }
            }

            if (totalWeight_lbs > 0)
            {
                return UnitUtils.Convert(totalWeight_lbs, UnitTypeId.PoundsMass, UnitTypeId.Kilograms);
            }

            return 0;
        }

        // --- (方法已重新加入) ---
        /// <summary>
        /// 獲取專案中所有的 Rebar 元素，包含在群組中的。
        /// </summary>
        private List<Rebar> GetAllRebarElements(Document doc)
        {
            List<Rebar> rebarList = new List<Rebar>();
            HashSet<ElementId> processedIds = new HashSet<ElementId>();

            foreach (Rebar rebar in new FilteredElementCollector(doc).OfClass(typeof(Rebar)).WhereElementIsNotElementType().Cast<Rebar>())
            {
                if (processedIds.Add(rebar.Id))
                {
                    rebarList.Add(rebar);
                }
            }

            var groups = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .WhereElementIsNotElementType()
                .Cast<Group>();

            foreach (Group group in groups)
            {
                GetRebarFromGroup(doc, group, rebarList, processedIds);
            }

            return rebarList;
        }

        // --- (方法已重新加入) ---
        /// <summary>
        /// 遞迴輔助方法：從群組及其巢狀群組中獲取 Rebar。
        /// </summary>
        private void GetRebarFromGroup(Document doc, Group group, List<Rebar> rebarList, HashSet<ElementId> processedIds)
        {
            foreach (ElementId memberId in group.GetMemberIds())
            {
                Element member = doc.GetElement(memberId);

                if (member is Rebar rebar)
                {
                    if (processedIds.Add(rebar.Id))
                    {
                        rebarList.Add(rebar);
                    }
                }
                else if (member is Group nestedGroup)
                {
                    GetRebarFromGroup(doc, nestedGroup, rebarList, processedIds);
                }
            }
        }


        /// <summary>
        /// 將擷取的資料格式化並顯示在 TaskDialog 中
        /// </summary>
        private void ShowReport(List<ExtractedCarbonData> dataList)
        {
            if (dataList.Count == 0)
            {
                TaskDialog.Show("資料擷取", "在指定的篩選條件下，沒有找到任何元素。", TaskDialogCommonButtons.Close);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"已成功擷取 {dataList.Count} 筆元素資料。");
            sb.AppendLine("--- (僅顯示前 50 筆) ---");
            sb.AppendLine();
            sb.AppendLine("ID\t | 類別\t | 類型名稱\t | 材質名稱\t | 數值\t | 單位");
            sb.AppendLine("--------------------------------------------------------------------------------------------------");

            foreach (var data in dataList.Take(50))
            {
                string valueStr = "";
                if (data.Unit == "kg")
                    valueStr = data.Weight_kg.ToString();
                else if (data.Unit == "m³")
                    valueStr = data.Volume_m3.ToString();
                else if (data.Unit == "m³ & m²")
                    valueStr = $"{data.Volume_m3} / {data.Area_m2}";

                sb.AppendLine($"{data.ElementId}\t | {data.Category}\t | {data.TypeName}\t | {data.MaterialName}\t | {valueStr}\t | {data.Unit}");
            }

            TaskDialog mainDialog = new TaskDialog("BIM 資料擷取報告");
            mainDialog.MainInstruction = "資料擷取完成";
            mainDialog.MainContent = sb.ToString();
            mainDialog.Show();
        }

        /// <summary>
        /// "快速計算" 按鈕點擊事件 - 執行碳排計算
        /// </summary>
        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_extractedData == null || _extractedData.Count == 0)
            {
                TaskDialog.Show("快速計算錯誤", "請先點擊「開始擷取」按鈕以載入元件資料。");
                return;
            }

            List<CalculatedCarbonData> carbonResults = new List<CalculatedCarbonData>();
            Dictionary<string, double> carbonCoefficientCache = new Dictionary<string, double>();

            try
            {
                using (SqlConnection connection = new SqlConnection(CarbonDbConnectionString))
                {
                    connection.Open();

                    foreach (var data in _extractedData)
                    {
                        if (!carbonCoefficientCache.ContainsKey(data.MaterialName))
                        {
                            string sql = "SELECT kgCO2e FROM dbo.carbon WHERE name = @material AND unit = @unit";
                            using (SqlCommand command = new SqlCommand(sql, connection))
                            {
                                command.Parameters.AddWithValue("@material", data.MaterialName);
                                string dbUnit = (data.Unit == "m³" || data.Unit == "m³ & m²") ? "m³" : data.Unit;
                                command.Parameters.AddWithValue("@unit", dbUnit);

                                object result = command.ExecuteScalar();

                                double coefficient = (result != null && result != DBNull.Value) ? Convert.ToDouble(result) : 0.0;
                                carbonCoefficientCache.Add(data.MaterialName, coefficient);
                            }
                        }

                        double finalCoefficient = carbonCoefficientCache[data.MaterialName];
                        double carbonEmission_kgco2e = 0;
                        double quantity = 0;
                        string usedUnit = "";

                        if (finalCoefficient > 0)
                        {
                            if (data.Unit == "m³" || data.Unit == "m³ & m²")
                            {
                                quantity = data.Volume_m3;
                                carbonEmission_kgco2e = quantity * finalCoefficient;
                                usedUnit = "m³";
                            }
                            else if (data.Unit == "kg")
                            {
                                quantity = data.Weight_kg;
                                carbonEmission_kgco2e = quantity * finalCoefficient;
                                usedUnit = "kg";
                            }

                            carbonResults.Add(new CalculatedCarbonData
                            {
                                ElementId = data.ElementId,
                                Category = data.Category,
                                TypeName = data.TypeName,
                                MaterialName = data.MaterialName,
                                Quantity = quantity,
                                UsedUnit = usedUnit,
                                Coefficient = finalCoefficient,
                                CarbonEmission_kgCO2e = Math.Round(carbonEmission_kgco2e, 2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("資料庫錯誤", $"碳排計算失敗，請檢查資料庫連線：{ex.Message}");
                return;
            }

            ShowCarbonReport(carbonResults);
        }

        /// <summary>
        /// 顯示碳排計算結果的彈窗報告
        /// </summary>
        private void ShowCarbonReport(List<CalculatedCarbonData> carbonResults)
        {
            if (carbonResults.Count == 0)
            {
                TaskDialog.Show("快速計算報告", "在資料庫中，沒有找到任何符合材質和單位的碳排係數。", TaskDialogCommonButtons.Close);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"已計算 {carbonResults.Count} 筆元件的碳排放。");

            double totalCarbon = carbonResults.Sum(c => c.CarbonEmission_kgCO2e);
            sb.AppendLine($"\n**總碳排放量: {Math.Round(totalCarbon, 2)} kgCO2e**");

            sb.AppendLine("\n--- (前 50 筆詳細列表) ---");
            sb.AppendLine("ID\t | 材質名稱\t | 數量\t | 單位\t | 係數\t | 碳排放 (kgCO2e)");
            sb.AppendLine("--------------------------------------------------------------------------------------------------");

            foreach (var data in carbonResults.Take(50))
            {
                sb.AppendLine($"{data.ElementId}\t | {data.MaterialName}\t | {data.Quantity}\t | {data.UsedUnit}\t | {data.Coefficient}\t | {data.CarbonEmission_kgCO2e}");
            }

            TaskDialog mainDialog = new TaskDialog("BIM 碳排放計算報告");
            mainDialog.MainInstruction = "快速計算完成";
            mainDialog.MainContent = sb.ToString();
            mainDialog.Show();
        }

    }

    /// <summary>
    /// 用於儲存擷取資料的輔助類別 (Helper Class)
    /// </summary>
    public class ExtractedCarbonData
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public string MaterialName { get; set; }
        public double Volume_m3 { get; set; } // 立方公尺
        public double Area_m2 { get; set; }   // 平方公尺
        public double Weight_kg { get; set; } // 公斤
        public string Unit { get; set; }      // 數據的主要計算單位
    }


    /// <summary>
    /// 用於儲存碳排計算結果的輔助類別
    /// </summary>
    public class CalculatedCarbonData
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public string MaterialName { get; set; }
        public double Quantity { get; set; }      // 使用的數量 (m3 或 kg)
        public string UsedUnit { get; set; }      // 使用的單位 (m³ 或 kg)
        public double Coefficient { get; set; }   // 碳排係數
        public double CarbonEmission_kgCO2e { get; set; } // 計算出的碳排放量 (kgCO2e)
    }
}